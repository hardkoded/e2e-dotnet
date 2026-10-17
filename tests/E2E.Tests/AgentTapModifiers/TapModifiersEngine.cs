// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentTapModifiers;

/// <summary>An engine over another that declares <see cref="EngineCapabilities.TapModifiers"/> and keeps every tap and double tap it is sent.</summary>
internal sealed class TapModifiersEngine(IEngine inner) : IEngine
{
    /// <summary>The taps and double taps the engine received, in order.</summary>
    public List<LocatorAction> Clicks { get; } = [];

    public string Platform => inner.Platform;

    public string Version => inner.Version;

    public EngineCapabilities Capabilities => inner.Capabilities | EngineCapabilities.TapModifiers;

    public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) =>
        new Session(this, await inner.StartAsync(options, cancellationToken).ConfigureAwait(false));

    private sealed class Session(TapModifiersEngine engine, IEngineSession session) : IEngineSession
    {
        public string Route => session.Route;

        public Task OpenAsync(string url, CancellationToken cancellationToken) => session.OpenAsync(url, cancellationToken);

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken) => session.ObserveAsync(cancellationToken);

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken)
        {
            if (action is LocatorAction.Tap or LocatorAction.DoubleTap)
            {
                engine.Clicks.Add(action);
            }

            return session.PerformAsync(node, action, cancellationToken);
        }

        public Task PressAsync(string key, CancellationToken cancellationToken) => session.PressAsync(key, cancellationToken);

        public Task SwipeAsync(ScrollDirection direction, CancellationToken cancellationToken) => session.SwipeAsync(direction, cancellationToken);

        public Task BackAsync(CancellationToken cancellationToken) => session.BackAsync(cancellationToken);

        public Task RestartAsync(CancellationToken cancellationToken) => session.RestartAsync(cancellationToken);

        public Task ClearStateAsync(CancellationToken cancellationToken) => session.ClearStateAsync(cancellationToken);

        public ValueTask DisposeAsync() => session.DisposeAsync();
    }
}
