// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E;

/// <summary>
/// The video of one attempt, from the runner's side (upstream <c>execute.ts</c> <c>launchSession</c>, <c>closeSession</c>,
/// <c>stopVideo</c>, and <c>registerVideos</c>): starts the video the attempt's mode asks for once the engine is up, and
/// after the verdict collects it, keeps it or deletes it by the mode, and checks what the engine handed back. A video a
/// default mode asked for fails quietly; one somebody set fails the launch or the cleanup.
/// </summary>
internal sealed class ExecuteVideo
{
    private readonly VideoRecording _recording;
    private readonly SessionRecordings _recordings;
    private readonly string _artifactsDir;

    private ExecuteVideo(VideoRecording recording, SessionRecordings recordings, string artifactsDir)
    {
        _recording = recording;
        _recordings = recordings;
        _artifactsDir = artifactsDir;
    }

    /// <summary>
    /// The video this attempt records, or null for none. A mode a target or a test set is required: an engine that cannot
    /// record it fails here, before the engine starts, with <c>UNSUPPORTED_ARTIFACT</c>. A run-wide or default mode is
    /// skipped where the engine cannot record. Upstream <c>validateEngine</c>, for the one attempt a session is. The port has
    /// no retries to look ahead to, so an attempt that does not record under a retry mode is never refused.
    /// </summary>
    public static VideoRecording? Plan(E2ESessionOptions options)
    {
        var video = RecordingModes.AttemptVideo(options.Video, options.Attempt - 1);
        if (video is null)
        {
            return null;
        }

        if ((options.Engine.Capabilities & EngineCapabilities.Video) != 0)
        {
            return video;
        }

        if (options.Video.Source is RecordingSource.Target or RecordingSource.Test)
        {
            var target = string.IsNullOrWhiteSpace(options.TargetName) ? options.Engine.Platform : options.TargetName;
            var setBy = options.Video.Source == RecordingSource.Test ? $"test \"{options.TestTitle}\" sets" : "the target sets";
            throw new ConfigurationException(
                "UNSUPPORTED_ARTIFACT",
                $"target \"{target}\" (engine {options.Engine.Platform}) cannot record video, and {setBy} video: '{RecordingModes.Name(options.Video.Mode)}'");
        }

        return null;
    }

    /// <summary>
    /// Starts the video within the launch timeout. A required video's failure fails the start; a default one's is dropped.
    /// A cancel of <paramref name="cancellationToken"/> is never dropped.
    /// </summary>
    public static async Task<ExecuteVideo?> StartAsync(VideoRecording? recording, SessionRecordings recordings, string artifactsDir, TimeSpan launchTimeout, CancellationToken cancellationToken)
    {
        if (recording is null)
        {
            return null;
        }

        using var launch = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (launchTimeout > TimeSpan.Zero && launchTimeout != Timeout.InfiniteTimeSpan)
        {
            launch.CancelAfter(launchTimeout);
        }

        try
        {
            await recordings.StartAttemptVideoAsync(launch.Token).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // A default video is best effort: whatever stops it, the attempt goes on without one.
        catch (Exception) when (recording.Policy == VideoPolicy.BestEffort && !cancellationToken.IsCancellationRequested)
#pragma warning restore CA1031
        {
            return null;
        }
        catch (Exception ex) when ((ex is OperationCanceledException || ex is EngineException { Code: EngineErrorCodes.Cancelled }) && launch.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new EngineException("ENVIRONMENT_UNAVAILABLE", "The video did not start within the launch timeout (" + launchTimeout.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ms).", ex);
        }

        return new ExecuteVideo(recording, recordings, artifactsDir);
    }

    /// <summary>How a final attempt ended, from the error the framework reports for it.</summary>
    public static AttemptStatus StatusOf(Exception? error) => error switch
    {
        null => AttemptStatus.Passed,
        SkipException => AttemptStatus.Skipped,
        OperationCanceledException => AttemptStatus.Interrupted,
        E2EException { Code: "TEST_TIMEOUT" } => AttemptStatus.TimedOut,
        _ => AttemptStatus.Failed,
    };

    /// <summary>The segments the attempt keeps, set by <see cref="StopAsync"/> even when it then throws.</summary>
    public IReadOnlyList<VideoSegment> Kept { get; private set; } = [];

    /// <summary>
    /// Finalizes the video within the cleanup timeout and sets <see cref="Kept"/> to the segments the attempt keeps. A video
    /// the mode does not keep for this verdict is deleted. A required video that cannot be finalized throws, since it is a
    /// cleanup failure, and so does a link that is not an http(s) URL, once every other recording is kept; a default video
    /// that cannot be finalized is dropped.
    /// </summary>
    public async Task StopAsync(AttemptStatus status, TimeSpan cleanupTimeout, IArtifactSink? sink = null)
    {
        using var cleanup = new CancellationTokenSource();
        if (cleanupTimeout > TimeSpan.Zero && cleanupTimeout != Timeout.InfiniteTimeSpan)
        {
            cleanup.CancelAfter(cleanupTimeout);
        }

        try
        {
            var segments = await _recordings.StopAttemptVideoAsync(cleanup.Token).ConfigureAwait(false);

            // Recorded so a failure could be looked at; a pass has nothing to show.
            if (!RecordingModes.Keeps(_recording.Keep, status))
            {
                foreach (var file in segments.OfType<VideoFile>())
                {
                    // An engine's path is its own word: only a file inside the attempt's directory is ours to delete.
                    var full = Path.GetFullPath(Path.Combine(_artifactsDir, file.Path));
                    if (full.StartsWith(Path.GetFullPath(_artifactsDir) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    {
                        File.Delete(full);
                    }
                }

                return;
            }

            Register(segments, sink);
        }
#pragma warning disable CA1031 // A default video is best effort: whatever stops it, the attempt reports none.
        catch (Exception) when (_recording.Policy == VideoPolicy.BestEffort)
#pragma warning restore CA1031
        {
        }
    }

    /// <summary>
    /// Keeps an attempt's recordings, in order: a file by its path, a link by its URL. An engine's own link is held to what a
    /// provider's is, an http(s) URL; one that is not fails the cleanup once every other recording is kept, so it costs no
    /// other video. Upstream <c>registerVideos</c>.
    /// </summary>
    // An engine's path is its own word: only a file inside the attempt's directory is ours to register.
    private bool InsideAttemptDir(string path) =>
        Path.GetFullPath(Path.Combine(_artifactsDir, path)).StartsWith(Path.GetFullPath(_artifactsDir) + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private void Register(IReadOnlyList<VideoSegment> segments, IArtifactSink? sink)
    {
        var rejected = segments.OfType<VideoLink>().Where(link => ProviderRecordings.HostedVideoUrl(link.Url) is null).ToList();
        Kept = [.. segments.Where(segment => segment is not VideoLink link || !rejected.Contains(link))];
        foreach (var segment in Kept)
        {
            var startedAt = DateTimeOffset.TryParse(segment.StartedAt, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : (DateTimeOffset?)null;
            switch (segment)
            {
                case VideoFile file when sink is not null && InsideAttemptDir(file.Path):
                    sink.Register(E2E.Reports.ArtifactKind.Video, file.Path.Replace(Path.DirectorySeparatorChar, '/'), new ArtifactRegistration { StartedAt = startedAt });
                    break;
                case VideoLink link:
                    sink?.Link(link.Url, new ArtifactLink(link.MediaType, startedAt ?? DateTimeOffset.UtcNow));
                    break;
            }
        }

        if (rejected.Count > 0)
        {
            throw new EngineException(EngineErrorCodes.EngineFailure, "the engine returned a video link that is not an http(s) URL: " + string.Join(", ", rejected.Select(link => $"\"{link.Url}\"")), retryable: false);
        }
    }
}
