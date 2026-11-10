// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.Agent;

/// <summary>
/// An engine whose screen keeps its tree and moves only its address, as a page does when it rewrites its URL after it renders.
/// The screen reads <paramref name="before"/> until <see cref="Moved"/> is set, then <paramref name="after"/> when one is given.
/// A null <paramref name="before"/> is an engine that reports no location at all.
/// </summary>
internal sealed class LocationEngine(IEngine inner, string? before, string? after = null) : IEngine
{
    private volatile bool _moved;

    /// <summary>Whether the address has moved on to <c>after</c>. Set by the judge's callback, read by the engine's observations.</summary>
    public bool Moved
    {
        get => _moved;
        set => _moved = value;
    }

    /// <summary>The address the screen is at now, or null when the engine reports none.</summary>
    public string? Location => Moved && after is not null ? after : before;

    public string Platform => inner.Platform;

    public string Version => inner.Version;

    public EngineCapabilities Capabilities => inner.Capabilities;

    public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
    {
        return new Session(this, await inner.StartAsync(options, cancellationToken).ConfigureAwait(false));
    }

    private sealed class Session(LocationEngine engine, IEngineSession inner) : IEngineSession
    {
        public string Route => engine.Location is null ? inner.Route : E2E.Internal.Routes.LocationOf(engine.Location);

        public Task OpenAsync(string url, CancellationToken cancellationToken) => inner.OpenAsync(url, cancellationToken);

        public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            var seen = await inner.ObserveAsync(cancellationToken).ConfigureAwait(false);
            return new Observation
            {
                Route = Route,
                Location = engine.Location,
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

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) => inner.PerformAsync(node, action, cancellationToken);

        public Task PressAsync(string key, CancellationToken cancellationToken) => inner.PressAsync(key, cancellationToken);

        public Task<EngineScreenshot> ScreenshotAsync(CancellationToken cancellationToken) => inner.ScreenshotAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
