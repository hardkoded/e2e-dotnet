// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using Microsoft.Playwright;

namespace E2E.Engine;

/// <summary>
/// The trace half of the engine (upstream <c>surface.ts</c>: <c>startAttempt</c> builds the <c>TraceFeed</c> and installs the app log,
/// and <c>acted</c> and <c>navigating</c> feed them): what an attempt tells its trace. The app log is <see cref="AppLog"/>.
/// </summary>
public sealed partial class WebEngine
{
    private sealed partial class WebSession
    {
        // What the attempt tells its trace beside the app log; null for a session started without a harness to tell.
        private TraceFeed? _trace;

        // The harness's sinks for what the app did on its own. A persistent session installs its app log before it knows them.
        private volatile Action<AppLogEntry>? _appLog;
        private volatile Action<string>? _navigation;

        // Navigations the engine itself is making: the app log leaves them to the step that asked for them.
        private int _ownNavigations;

        /// <summary>Opens the attempt's trace feed over the sinks the harness handed <paramref name="options"/>.</summary>
        internal WebSession TraceTo(EngineStartOptions options)
        {
            _trace = new TraceFeed(options.Screen, options.Environment, OpenPage, CaptureScreenAsync, _seams.Clock);
            _appLog = options.AppLog;
            _navigation = options.Navigation;
            return this;
        }

        /// <summary>
        /// The attempt's open page, or null, for a reader beside the test (the trace): unlike <see cref="Page"/>, it leaves an error
        /// a handler latched for the test's next operation to throw.
        /// </summary>
        private IPage? OpenPage() => _page is { IsClosed: false } page ? page : null;

        /// <summary>
        /// Captures the page as an observation without publishing it: the refs, frames, and twins the test and the model act on
        /// stay as the last real observation left them, in the page too.
        /// </summary>
        private async Task<Observation> CaptureScreenAsync(OperationBudget budget)
        {
            var page = OpenPage() ?? throw new EngineException(EngineErrorCodes.InvalidState, "no app page is open");
            var walk = new FrameWalk { Publish = false };
            var roots = await CollectAsync(page.MainFrame, walk, budget).ConfigureAwait(false);
            return new Observation { Route = Routes.LocationOf(page.Url), Roots = roots.Select(node => ToNode(node, _testIdAttribute)).ToList(), Truncated = walk.Truncated, ScrollPosition = walk.Scroll };
        }

        /// <summary>Runs one navigation the engine itself makes, so the app log leaves it to the step that asked for it. Tells the trace what the attempt runs on once it is done.</summary>
        private async Task NavigatingAsync(Func<Task> work)
        {
            Interlocked.Increment(ref _ownNavigations);
            try
            {
                await work().ConfigureAwait(false);
                _trace?.TellEnvironment();
            }
            finally
            {
                Interlocked.Decrement(ref _ownNavigations);
            }
        }

        /// <summary>Runs one action, then hands the trace the screen it left. An action that failed leaves none.</summary>
        private async Task ActedAsync(CancellationToken cancellationToken, Func<Task> work)
        {
            await work().ConfigureAwait(false);
            if (_trace is { } trace)
            {
                await trace.ScreenAfterActionAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
