// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using E2E.Engine;

namespace E2E.Cli.Mcp;

/// <summary>A recording that stopped: its files in order, and how long it ran (upstream's <c>FinishedRecording</c>).</summary>
internal sealed record FinishedRecording(int Index, string? Name, IReadOnlyList<string> Files, DateTimeOffset StartedAt, TimeSpan Duration);

/// <summary>
/// Video recordings of a live session (upstream's <c>mcp/recording.ts</c>). A session does not record by itself: the coding agent starts
/// one when the part worth watching begins and stops it when that part is over, as many times as it likes. Each recording is the engine's
/// video of the attempt between the two calls, moved out of the attempt directory into <c>.e2e/videos/&lt;session&gt;/</c> as soon as it
/// stops, so the next one never overwrites it and closing the session never takes it along. Recordings are not masked.
/// </summary>
internal sealed partial class SessionRecorder(IEngineSession engine, string attemptDir, string outDir, TimeSpan timeout) : IDisposable
{
    private readonly SemaphoreSlim _queue = new(1, 1);
    private ActiveRecording? _active;
    private int _count;

    /// <summary>Starts a recording; a recording already running is reported, not restarted.</summary>
    public Task<string> StartAsync(string? name, CancellationToken cancellationToken = default) =>
        SerialAsync(
            async () =>
            {
                if (_active is { } running)
                {
                    return $"{Describe(running.Index, running.Name)} is already running since {running.StartedAt:O}; stop_recording ends it.";
                }

                try
                {
                    using var bound = Bounded(cancellationToken);
                    await engine.StartVideoAsync(bound.Token).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // An engine may count a start that failed as a recording; stopping it lets the next start_recording begin from nothing.
                    using var cleanup = new CancellationTokenSource(timeout);
                    await ((Task)engine.StopVideoAsync(cleanup.Token)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                    throw;
                }

                _active = new ActiveRecording(++_count, name, DateTimeOffset.UtcNow);
                return $"{Describe(_active.Index, _active.Name)} started. Act as usual; stop_recording saves the video, and close_session saves one still running.";
            },
            cancellationToken);

    /// <summary>
    /// Stops the running recording and moves its files out of the attempt directory; null when nothing records. A stop or a move that fails
    /// leaves the recording in place, so stop_recording or close_session can try again: the engine is asked to stop only once, and a file
    /// already moved stays where it went.
    /// </summary>
    public Task<FinishedRecording?> StopAsync(CancellationToken cancellationToken = default) =>
        SerialAsync<FinishedRecording?>(
            async () =>
            {
                if (_active is not { } active)
                {
                    return null;
                }

                if (active.Segments is null)
                {
                    // Before the engine call: finalizing the file can take seconds and records nothing.
                    var at = DateTimeOffset.UtcNow;
                    using var bound = Bounded(cancellationToken);
                    active.Segments = await engine.StopVideoAsync(bound.Token).ConfigureAwait(false);
                    active.StoppedAt = at;
                }

                var files = active.Segments.Select((segment, position) => Keep(active, segment, position)).ToList();
                _active = null;
                var startedAt = active.Segments.Count > 0 && DateTimeOffset.TryParse(active.Segments[0].StartedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var first) ? first : active.StartedAt;
                var duration = active.StoppedAt - startedAt;
                return new FinishedRecording(active.Index, active.Name, files, startedAt, duration < TimeSpan.Zero ? TimeSpan.Zero : duration);
            },
            cancellationToken);

    public void Dispose() => _queue.Dispose();

    /// <summary>Renders a stopped recording for the agent (upstream's <c>describeRecording</c>).</summary>
    public static string Describe(FinishedRecording recording)
    {
        ArgumentNullException.ThrowIfNull(recording);
        var lines = new List<string> { $"{Describe(recording.Index, recording.Name)} stopped after {recording.Duration.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s." };
        if (recording.Files.Count == 0)
        {
            lines.Add("The engine wrote no video: nothing was shown while it ran.");
        }
        else if (recording.Files.Count > 1)
        {
            lines.Add($"{recording.Files.Count.ToString(CultureInfo.InvariantCulture)} files, one per page the surface showed; they play in this order:");
        }

        lines.AddRange(recording.Files.Select(file => "- " + file));
        return string.Join('\n', lines);
    }

    /// <summary>The session's <c>start_recording</c> and <c>stop_recording</c> tools (upstream's <c>recordingTools</c>).</summary>
    public IReadOnlyList<KeyValuePair<string, CatalogTool>> Tools() =>
    [
        new("start_recording", new CatalogTool
        {
            Description = "Start recording a video of the app, for a person to watch: a demo for a pull request, or evidence of a bug. Everything on screen until stop_recording is in it, unmasked. One recording at a time; start and stop as many as you need.",
            InputSchema = JsonSerializer.SerializeToElement(new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["name"] = new JsonObject { ["type"] = "string", ["pattern"] = "^[A-Za-z0-9][A-Za-z0-9_-]*$", ["maxLength"] = 64, ["description"] = "Short name for the file, e.g. \"checkout-flow\"" },
                },
                ["additionalProperties"] = false,
            }),
            Execute = async (args, token) =>
            {
                var name = args.ValueKind == JsonValueKind.Object && args.TryGetProperty("name", out var given) ? given.GetString() : null;
                if (name is not null && (name.Length > 64 || !ValidName().IsMatch(name)))
                {
                    // The argument check ignores a schema's pattern, so the name is checked here, worded as the call's own argument errors are.
                    throw new ConfigurationException("INVALID_ARGUMENT", "call start_recording: name: letters, digits, \"-\", and \"_\" only, starting with a letter or digit, up to 64; tools {tool: \"start_recording\"} shows its arguments");
                }

                return await StartAsync(name, token).ConfigureAwait(false);
            },
        }),
        new("stop_recording", new CatalogTool
        {
            Description = "Stop the running recording and save it: returns the absolute path of each video file, ready to attach to a pull request.",
            InputSchema = JsonSerializer.SerializeToElement(new { type = "object", properties = new { }, additionalProperties = false }),
            Execute = async (_, token) =>
            {
                var recording = await StopAsync(token).ConfigureAwait(false);
                return recording is null ? "Nothing is recording; start_recording starts a recording." : Describe(recording);
            },
        }),
    ];

    private static string Describe(int index, string? name) => $"Recording {index.ToString(CultureInfo.InvariantCulture)}" + (name is null ? "" : $" \"{name}\"");

    /// <summary>Runs <paramref name="body"/> after every call before it has settled.</summary>
    private async Task<T> SerialAsync<T>(Func<Task<T>> body, CancellationToken cancellationToken)
    {
        await _queue.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await body().ConfigureAwait(false);
        }
        finally
        {
            _queue.Release();
        }
    }

    private CancellationTokenSource Bounded(CancellationToken cancellationToken)
    {
        var bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bound.CancelAfter(timeout);
        return bound;
    }

    /// <summary>
    /// Moves one segment to <c>&lt;outDir&gt;/&lt;index&gt;[-&lt;name&gt;][-part&lt;n&gt;]&lt;ext&gt;</c>, unless an earlier try did, and returns where it
    /// landed; a provider's link stays where the provider keeps it.
    /// </summary>
    private string Keep(ActiveRecording recording, VideoSegment segment, int position)
    {
        if (segment is VideoLink link)
        {
            return ProviderRecordings.HostedVideoUrl(link.Url)
                ?? throw new EngineException(EngineErrorCodes.EngineFailure, "the engine returned a video link that is not an http(s) URL: " + JsonSerializer.Serialize(link.Url), retryable: false);
        }

        var file = (VideoFile)segment;
        var stem = string.Join('-', new[] { recording.Index.ToString(CultureInfo.InvariantCulture), recording.Name, position == 0 ? null : "part" + (position + 1).ToString(CultureInfo.InvariantCulture) }.Where(part => part is not null));
        var destination = Path.Combine(outDir, stem + Path.GetExtension(file.Path));

        // .NET-only hardening: an engine's path is its own word, so only a file inside the attempt's directory is ours to move.
        var directory = Path.GetFullPath(attemptDir);
        var source = Path.GetFullPath(Path.Combine(directory, file.Path));
        if (!source.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new EngineException(EngineErrorCodes.EngineFailure, "the engine returned a video outside the attempt's directory: " + JsonSerializer.Serialize(file.Path), retryable: false);
        }

        if (!File.Exists(source) && File.Exists(destination))
        {
            return destination;
        }

        Directory.CreateDirectory(outDir);
        File.Move(source, destination, overwrite: true);
        return destination;
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]*\z")]
    private static partial Regex ValidName();

    /// <summary>A recording the engine is capturing. <see cref="Segments"/> is set once the engine stopped it: what it wrote, still to be moved.</summary>
    private sealed record ActiveRecording(int Index, string? Name, DateTimeOffset StartedAt)
    {
        public IReadOnlyList<VideoSegment>? Segments { get; set; }

        public DateTimeOffset StoppedAt { get; set; }
    }
}
