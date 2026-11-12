// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentScrollPages;

/// <summary>
/// A document engine that runs a callback before every observation it answers, as upstream's fake engine's <c>observe</c> hook
/// does. The callback and the read take <c>sync</c> together, since the scene is turned by timers on other threads.
/// </summary>
internal sealed class ObservedDocumentEngine(DocumentWorld world, object sync, Action beforeObserve) : IEngine
{
    private readonly DocumentEngine _inner = new(world);

    public string Platform => _inner.Platform;

    public string Version => _inner.Version;

    public EngineCapabilities Capabilities => _inner.Capabilities;

    public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
    {
        return new Session(sync, beforeObserve, await _inner.StartAsync(options, cancellationToken).ConfigureAwait(false));
    }

    private sealed class Session(object sync, Action beforeObserve, IEngineSession inner) : IEngineSession
    {
        public string Route => inner.Route;

        public Task OpenAsync(string url, CancellationToken cancellationToken) => inner.OpenAsync(url, cancellationToken);

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            lock (sync)
            {
                beforeObserve();
                return inner.ObserveAsync(cancellationToken);
            }
        }

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) => inner.PerformAsync(node, action, cancellationToken);

        public Task PressAsync(string key, CancellationToken cancellationToken) => inner.PressAsync(key, cancellationToken);

        public Task SwipeAsync(ScrollDirection direction, CancellationToken cancellationToken) => inner.SwipeAsync(direction, cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
