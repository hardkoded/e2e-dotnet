// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using Microsoft.Playwright;

namespace E2E.Engine;

/// <summary>
/// The trace half of the engine (upstream <c>surface.ts</c>: <c>startAttempt</c> builds the <c>TraceFeed</c>, and <c>acted</c> and
/// <c>navigating</c> feed it (the port tells the environment where an action navigates)): what an attempt tells its trace beside the app log.
/// </summary>
public sealed partial class WebEngine
{
    private sealed partial class WebSession
    {
        // What the attempt tells its trace beside the app log; null for a session started without a harness to tell.
        private TraceFeed? _trace;

        /// <summary>Opens the attempt's trace feed over the sinks the harness handed <paramref name="options"/>.</summary>
        internal WebSession TraceTo(EngineStartOptions options)
        {
            _trace = new TraceFeed(options.Screen, options.Environment, OpenPage, CaptureScreenAsync, _seams.Clock);
            return this;
        }

        /// <summary>
        /// The attempt's open page, or null, for a reader beside the test (the trace): unlike <see cref="Page"/>, it leaves an error
        /// a handler latched for the test's next operation to throw.
        /// </summary>
        private IPage? OpenPage() => _page is { IsClosed: false } page ? page : null;

        /// <summary>
        /// Captures the page as an observation without publishing it: the refs, frames, and twins the test and the model act on
        /// stay as the last real observation left them.
        /// </summary>
        private async Task<Observation> CaptureScreenAsync(OperationBudget budget)
        {
            var page = OpenPage() ?? throw new EngineException(EngineErrorCodes.InvalidState, "no app page is open");
            var walk = new FrameWalk();
            var roots = await CollectAsync(page.MainFrame, walk, budget).ConfigureAwait(false);
            return new Observation { Route = Routes.LocationOf(page.Url), Roots = roots.Select(node => ToNode(node, _testIdAttribute)).ToList(), Truncated = walk.Truncated, ScrollPosition = walk.Scroll };
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
