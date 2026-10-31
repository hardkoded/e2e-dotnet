// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Internal;
using E2E.Reports;

namespace E2E;

/// <summary>How <see cref="App.StartRecordingAsync"/> names its file (upstream's <c>RecordingOptions</c>).</summary>
public sealed record RecordingOptions
{
    /// <summary>File name stem, <c>recordings/001-&lt;name&gt;.&lt;ext&gt;</c>; defaults to <c>recording</c>.</summary>
    public string? Name { get; init; }
}

/// <summary>One video <see cref="App.StopRecordingAsync"/> returns: a file, or a link to a recording a hosted service keeps (upstream's <c>RecordedVideo</c>).</summary>
public abstract record RecordedVideo
{
    /// <summary>When it started recording, as an ISO timestamp.</summary>
    public required string StartedAt { get; init; }
}

/// <summary>A video file under the attempt's <c>recordings/</c> directory (upstream's <c>RecordedVideoFile</c>).</summary>
public sealed record RecordedVideoFile : RecordedVideo
{
    /// <summary>Absolute path of the file.</summary>
    public required string Path { get; init; }
}

/// <summary>A recording a hosted browser or device service keeps, by URL (upstream's <c>RecordedVideoLink</c>).</summary>
public sealed record RecordedVideoLink : RecordedVideo
{
    /// <summary><c>http(s)</c> URL of the recording.</summary>
    public required string Url { get; init; }

    /// <summary>Media type of what the URL serves: <c>video/mp4</c>, or <c>text/html</c> for a player page.</summary>
    public required string MediaType { get; init; }
}

/// <summary>What <see cref="SessionRecordings.StopSpanAsync"/> hands back: the artifacts it registered and the videos the test sees.</summary>
internal sealed record StoppedSpan(IReadOnlyList<string> Ids, IReadOnlyList<RecordedVideo> Videos);

/// <summary>
/// The engine's one video recorder, shared by the attempt's <c>video</c> and the spans a test records with
/// <c>app.StartRecordingAsync()</c> / <c>app.StopRecordingAsync()</c>. Both ride the engine's <c>StartVideoAsync</c> /
/// <c>StopVideoAsync</c>, so only one may hold the recorder at a time: a span refuses to start while the attempt's video
/// records, and the attempt's video never collects a span's segments. A span is moved out of the engine's <c>video/</c>
/// directory into <c>recordings/</c> as soon as it stops, so the next span or the attempt's video never overwrites it.
/// Upstream's <c>SessionRecordings</c> (<c>run/recordings.ts</c>).
/// </summary>
internal sealed partial class SessionRecordings
{
    /// <summary>A span <c>app.StartRecordingAsync</c> started: its number in the attempt and its file name stem.</summary>
    private sealed record Span(int Index, string Stem);

    private static readonly object AttemptHolder = new();

    private readonly IEngineSession _session;
    private readonly string _targetName;
    private readonly bool _recordsVideo;

    // Either AttemptHolder, a Span, or null.
    private object? _holder;
    private int _spans;

    /// <param name="session">The session whose recorder this is.</param>
    /// <param name="targetName">The target, named in the refusal of an engine that records no video.</param>
    /// <param name="recordsVideo">
    /// Whether the engine declares <see cref="EngineCapabilities.Video"/>. .NET-only: upstream tests that the session's
    /// <c>artifacts</c> has <c>startVideo</c> and <c>stopVideo</c>; the port's session always has both methods.
    /// </param>
    public SessionRecordings(IEngineSession session, string targetName, bool recordsVideo)
    {
        _session = session;
        _targetName = targetName;
        _recordsVideo = recordsVideo;
    }

    /// <summary>True while a span records, so the attempt's end can stop it.</summary>
    public bool SpanRunning => _holder is Span;

    /// <summary>
    /// Starts the attempt's video. The recorder counts as held from the call on, even when the start fails: the engine may
    /// still be recording, and the attempt's close stops it either way.
    /// </summary>
    public Task StartAttemptVideoAsync(CancellationToken cancellationToken)
    {
        _holder = AttemptHolder;
        return _session.StartVideoAsync(cancellationToken);
    }

    /// <summary>Stops the attempt's video and returns its segments.</summary>
    public Task<IReadOnlyList<VideoSegment>> StopAttemptVideoAsync(CancellationToken cancellationToken)
    {
        if (ReferenceEquals(_holder, AttemptHolder))
        {
            _holder = null;
        }

        return _session.StopVideoAsync(cancellationToken);
    }

    /// <summary>
    /// Starts a span. Refused when the engine records no video, while the attempt's video records, and while another span
    /// records. A start that fails is stopped, so the next one begins from nothing.
    /// </summary>
    public async Task StartSpanAsync(string? name, CancellationToken cancellationToken)
    {
        if (!_recordsVideo)
        {
            throw new ConfigurationException("UNSUPPORTED_CAPABILITY", $"app.startRecording: target \"{_targetName}\" has an engine that records no video");
        }

        if (ReferenceEquals(_holder, AttemptHolder))
        {
            throw new ConfigurationException(
                "UNSUPPORTED_CAPABILITY",
                "app.startRecording cannot record while the test's video records, since the engine has one recorder; set video: 'off' on the test: test('...', { video: 'off' }, ...)");
        }

        if (_holder is not null)
        {
            throw new TestException("INVALID_ARGUMENT", "app.startRecording: a recording is already running; await app.stopRecording() before starting another");
        }

        var index = _spans + 1;
        var span = new Span(index, index.ToString("000", CultureInfo.InvariantCulture) + "-" + FileStem(name));
        _holder = span;
        try
        {
            await _session.StartVideoAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            _holder = null;

            // The engine may still have come up, so it is stopped within the cleanup budget: whatever that does, the start's own failure is the one to tell.
            using var budget = new CancellationTokenSource(E2EDefaults.CleanupTimeout);
            await ((Task)_session.StopVideoAsync(budget.Token)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            throw;
        }

        _spans = index;
    }

    /// <summary>
    /// Stops the running span, moves its files into <c>recordings/</c>, and registers each file or link as a video artifact;
    /// returns their artifact ids and what the test sees, absolute paths and links in play order.
    /// </summary>
    public async Task<StoppedSpan> StopSpanAsync(IArtifactSink sink, CancellationToken cancellationToken)
    {
        if (_holder is not Span span)
        {
            throw new TestException("INVALID_ARGUMENT", "app.stopRecording: no recording is running; call app.startRecording() first");
        }

        _holder = null;
        var segments = await _session.StopVideoAsync(cancellationToken).ConfigureAwait(false);
        var ids = new List<string>();
        var videos = new List<RecordedVideo>();
        var directory = Path.GetFullPath(sink.Dir);
        for (var position = 0; position < segments.Count; position++)
        {
            var segment = segments[position];
            if (segment is VideoLink link)
            {
                var url = ProviderRecordings.HostedVideoUrl(link.Url)
                    ?? throw new EngineException(EngineErrorCodes.EngineFailure, "the engine returned a video link that is not an http(s) URL: " + System.Text.Json.JsonSerializer.Serialize(link.Url), retryable: false);
                var startedAt = ParseStartedAt(link.StartedAt) ?? DateTimeOffset.UtcNow;
                ids.Add(sink.Link(url, new ArtifactLink(link.MediaType, startedAt)));
                videos.Add(new RecordedVideoLink { Url = url, MediaType = link.MediaType, StartedAt = link.StartedAt });
                continue;
            }

            var file = (VideoFile)segment;
            var part = position == 0 ? "" : "-part" + (position + 1).ToString(CultureInfo.InvariantCulture);
            var relative = "recordings/" + span.Stem + part + Path.GetExtension(file.Path);
            var absolute = Path.GetFullPath(Path.Combine(directory, relative));

            // .NET-only hardening: an engine's path is its own word, so only a file inside the attempt's directory is ours to move.
            var source = Path.GetFullPath(Path.Combine(directory, file.Path));
            if (!source.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new EngineException(EngineErrorCodes.EngineFailure, $"the engine returned a video outside the attempt's directory: {System.Text.Json.JsonSerializer.Serialize(file.Path)}", retryable: false);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.Move(source, absolute, overwrite: true);
            ids.Add(sink.Register(ArtifactKind.Video, relative, new ArtifactRegistration { StartedAt = ParseStartedAt(file.StartedAt) }));
            videos.Add(new RecordedVideoFile { Path = absolute, StartedAt = file.StartedAt });
        }

        return videos.Count == 0
            ? throw new EngineException(EngineErrorCodes.EngineFailure, $"recording {span.Index.ToString(CultureInfo.InvariantCulture)} stopped, but the engine wrote no video for it", retryable: false)
            : new StoppedSpan(ids, videos);
    }

    private static DateTimeOffset? ParseStartedAt(string startedAt) =>
        DateTimeOffset.TryParse(startedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : null;

    /// <summary>A recording name as a file name stem: letters, digits, <c>-</c>, and <c>_</c>, at most 64 characters.</summary>
    private static string FileStem(string? name)
    {
        var stem = UnsafeStemCharacters().Replace(name ?? "recording", "_");
        stem = stem.Length > 64 ? stem[..64] : stem;
        return stem.Length == 0 ? "recording" : stem;
    }

    [GeneratedRegex("[^A-Za-z0-9_-]")]
    private static partial Regex UnsafeStemCharacters();
}

/// <summary>The recorder state of the sessions (upstream's <c>sessionRecordings</c>, <c>run/recordings.ts</c>).</summary>
internal static class Recordings
{
    private static readonly ConditionalWeakTable<IEngineSession, SessionRecordings> BySession = [];

    /// <summary>
    /// The recorder state of one session, created on first use and shared by everyone who borrows the session.
    /// <paramref name="recordsVideo"/> is only read when the state is created.
    /// </summary>
    public static SessionRecordings SessionRecordings(IEngineSession session, string targetName, bool recordsVideo) =>
        BySession.GetValue(session, key => new SessionRecordings(key, targetName, recordsVideo));
}
