// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Playwright;

namespace E2E.Engine;

/// <summary>
/// The attempt's video as its browser provider records it: one recording of the browser for the whole attempt, whichever
/// pages open and close in it, ended by <see cref="ProviderRecordings.StopProviderRecordingAsync"/> into the attempt's one
/// segment, a file in its <c>video</c> directory or a link. Upstream's <c>ProviderVideo</c>.
/// </summary>
internal sealed class ProviderVideo(Func<CancellationToken, Task<LeaseRecording?>> record, string artifactsDir) : IAttemptVideo
{
    /// <summary>How long a recording that started after its attempt gave up gets to stop.</summary>
    private static readonly TimeSpan LateStop = TimeSpan.FromSeconds(10);

    private LeaseRecording? _started;

    /// <summary>The stop in flight, shared: a close that retries while a timed-out stop is still running waits for it instead of stopping twice.</summary>
    private Task<IReadOnlyList<VideoSegment>>? _stopping;

    /// <summary>The recording covers the browser, so no page starts a segment of its own.</summary>
    public bool IsArmed => false;

    /// <summary>
    /// True once the provider is recording. A provider that cannot record this browser answers its start with nothing
    /// (<see cref="IBrowserProvider.RecordAsync"/> returns null), where upstream's provider leaves <c>record</c> out; the
    /// session then records the screencast.
    /// </summary>
    public bool IsRecording => _started is not null;

    /// <summary>
    /// Starts the provider's recording. One that arrives once <paramref name="cancellationToken"/> was cancelled (a start
    /// that outlived its budget) is stopped at once and never kept, and never replaces the handle of a recording started
    /// after it.
    /// </summary>
    public async Task ArmAsync(IPage page, CancellationToken cancellationToken)
    {
        var started = await record(cancellationToken).ConfigureAwait(false);
        if (!cancellationToken.IsCancellationRequested)
        {
            _started = started;
            return;
        }

        if (started is not null)
        {
            using var late = new CancellationTokenSource(LateStop);
            await ((Task)StopLeaseAsync(started, late.Token)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        throw new EngineException(EngineErrorCodes.Cancelled, "video cancelled", retryable: false);
    }

    /// <summary>Pages come and go under one recording of the browser.</summary>
    public Task PageOpenedAsync(IPage page) => Task.CompletedTask;

    /// <summary>Pages come and go under one recording of the browser.</summary>
    public Task PageClosingAsync() => Task.CompletedTask;

    /// <summary>
    /// Stops the recording as the attempt's one segment; nothing when none was started. A stop that failed keeps the
    /// recording, so the attempt's close tries once more instead of leaving it running on a browser later attempts share.
    /// </summary>
    public Task<IReadOnlyList<VideoSegment>> StopAsync(CancellationToken cancellationToken)
    {
        var started = _started;
        if (started is null)
        {
            return Task.FromResult<IReadOnlyList<VideoSegment>>([]);
        }

        if (_stopping is { } running)
        {
            return running;
        }

        var stopping = StopSharedAsync(started, cancellationToken);
        _stopping = stopping;

        // Cleared once settled, after the field holds the task, so a stop that finishes at once cannot leave a stale one.
        _ = stopping.ContinueWith(
            _ =>
            {
                if (ReferenceEquals(_stopping, stopping))
                {
                    _stopping = null;
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return stopping;
    }

    /// <summary>
    /// Stops a recording the attempt never collected, best effort: the attempt keeps nothing of it. A stop that fails keeps
    /// the handle, so the recording is never marked stopped while it may still run.
    /// </summary>
    public async Task AbandonAsync(CancellationToken cancellationToken) =>
        await ((Task)StopAsync(cancellationToken)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

    private async Task<IReadOnlyList<VideoSegment>> StopSharedAsync(LeaseRecording started, CancellationToken cancellationToken)
    {
        var segment = await StopLeaseAsync(started, cancellationToken).ConfigureAwait(false);
        if (_started == started)
        {
            _started = null;
        }

        return [segment];
    }

    /// <summary>Ends one of the provider's recordings as a segment of the attempt.</summary>
    private Task<VideoSegment> StopLeaseAsync(LeaseRecording started, CancellationToken cancellationToken) =>
        ProviderRecordings.StopProviderRecordingAsync(
            started.Recording,
            new ProviderRecordingTarget { ArtifactsDir = artifactsDir, Provider = started.Provider, LeaseId = started.LeaseId },
            cancellationToken);
}
