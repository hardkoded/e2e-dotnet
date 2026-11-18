// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using E2E.Engine;

namespace E2E.Tests.Agent;

/// <summary>
/// An engine whose screen never holds still: every observation differs from the one before, the way a progress counter or a live
/// timestamp does (upstream's <c>/ticking</c> testbed page). The address carries a clock that ticks every 200 ms once the page is opened.
/// </summary>
internal sealed class TickingEngine(IEngine inner) : IEngine
{
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(200);

    public string Platform => inner.Platform;

    public string Version => inner.Version;

    public EngineCapabilities Capabilities => inner.Capabilities;

    public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
    {
        return new Session(await inner.StartAsync(options, cancellationToken).ConfigureAwait(false));
    }

    private sealed class Session(IEngineSession inner) : IEngineSession
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        public string Route => inner.Route;

        public Task OpenAsync(string url, CancellationToken cancellationToken)
        {
            _clock.Restart();
            return inner.OpenAsync(url, cancellationToken);
        }

        public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            var seen = await inner.ObserveAsync(cancellationToken).ConfigureAwait(false);
            var ticks = (long)(_clock.Elapsed / Tick);
            return new Observation
            {
                Route = seen.Route,
                Location = "https://app.test/ticking?clock=" + ticks,
                Roots = seen.Roots,
                Truncated = seen.Truncated,
                KeyboardVisible = seen.KeyboardVisible,
                ScrollPosition = seen.ScrollPosition,
                Viewport = seen.Viewport,
                Pixels = seen.Pixels,
                TreeUnavailable = seen.TreeUnavailable,
                SecureNodeCount = seen.SecureNodeCount,
            };
        }

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) =>
            inner.PerformAsync(node, action, cancellationToken);

        public Task PressAsync(string key, CancellationToken cancellationToken) =>
            inner.PressAsync(key, cancellationToken);

        public Task<EngineScreenshot> ScreenshotAsync(CancellationToken cancellationToken) =>
            inner.ScreenshotAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
