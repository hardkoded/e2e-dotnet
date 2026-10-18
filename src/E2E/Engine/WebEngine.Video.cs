// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Playwright;

namespace E2E.Engine;

/// <summary>
/// The video half of the engine (upstream <c>attempt-session.ts</c> <c>startVideo</c> and <c>collectVideo</c>, and the
/// surface's <c>startVideo</c> and <c>stopVideo</c>): an attempt that records video records the provider's own recording of
/// its browser when the provider records, and the screencast of its page otherwise.
/// </summary>
public sealed partial class WebEngine
{
    /// <summary>Where and how a session records video: the attempt's artifact directory, the provider's recording if any, and the screencast options.</summary>
    private sealed record VideoSetup(string? ArtifactsDir, Func<CancellationToken, Task<LeaseRecording?>>? Record, WebScreencastOptions? Screencast);

    private VideoSetup VideoSetupOf(EngineStartOptions options, LeaseScope? lease) =>
        new(options.ArtifactsDir, lease?.Recorder(), _options.Screencast);

    private sealed partial class WebSession
    {
        private IAttemptVideo? _video;

        /// <summary>How the session records video. Null for a session started without an artifact directory.</summary>
        public VideoSetup? Video { get; init; }

        public Task StartVideoAsync(CancellationToken cancellationToken) => RunAsync("video", cancellationToken, async () =>
        {
            var setup = Video;
            var dir = setup?.ArtifactsDir ?? throw new EngineException(EngineErrorCodes.InvalidState, "video needs the attempt's artifact directory; start the session with EngineStartOptions.ArtifactsDir");
            if (_video is not null)
            {
                return;
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
            var token = linked.Token;
            try
            {
                // The page opens first, so a provider's recording of the browser starts on the attempt's own tab.
                if (_page is null || _page.IsClosed)
                {
                    await NewPageAsync(RequireContext()).ConfigureAwait(false);
                }

                var page = Page;
                if (setup.Record is { } record)
                {
                    var provided = new ProviderVideo(record, dir);
                    await provided.ArmAsync(page, token).ConfigureAwait(false);
                    if (provided.IsRecording)
                    {
                        _video = provided;
                        return;
                    }
                }

                var screencast = new VideoRecorder(dir, setup.Screencast);
                await screencast.ArmAsync(page, token).ConfigureAwait(false);
                _video = screencast;
            }
            catch (OperationCanceledException ex) when (token.IsCancellationRequested)
            {
                throw new EngineException(EngineErrorCodes.Cancelled, "video cancelled", retryable: false, ex);
            }
            catch (Exception ex) when (WebErrors.IsPlaywright(ex))
            {
                throw WebErrors.Translate(ex, "video");
            }
        });

        /// <summary>
        /// Collects this attempt's video, independently of a failed connection: a recording is stopped on its own, and a
        /// caller that gives up leaves a stop already under way to finish.
        /// </summary>
        public async Task<IReadOnlyList<VideoSegment>> StopVideoAsync(CancellationToken cancellationToken)
        {
            if (_video is not { } video)
            {
                return [];
            }

            try
            {
                return await video.StopAsync(cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                throw new EngineException(EngineErrorCodes.Cancelled, "video cancelled", retryable: false, ex);
            }
            catch (Exception ex) when (WebErrors.IsPlaywright(ex))
            {
                throw WebErrors.Translate(ex, "video");
            }
        }

        private Task VideoPageOpenedAsync(IPage page) => _video?.PageOpenedAsync(page) ?? Task.CompletedTask;

        private Task VideoPageClosingAsync() => _video?.PageClosingAsync() ?? Task.CompletedTask;

        /// <summary>Ends a recording the attempt never collected, within the cleanup timeout; nothing it recorded is reported.</summary>
        private async Task AbandonVideoAsync()
        {
            if (_video is not { } video)
            {
                return;
            }

            using var budget = new CancellationTokenSource(E2EDefaults.CleanupTimeout);
            await video.AbandonAsync(budget.Token).WaitAsync(budget.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }
}
