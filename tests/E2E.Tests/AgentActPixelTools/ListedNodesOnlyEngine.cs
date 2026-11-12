// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentActPixelTools;

/// <summary>
/// The engine of upstream's "node-taps-only" and "node-drags-only" fixtures: a fixed screen of listed nodes, one declared action
/// kind, and no pointer, so a point can only reach a node by its id. What the engine performed is read from <see cref="Performed"/>.
/// </summary>
internal sealed class ListedNodesOnlyEngine(string actionKind, SemanticNode[] roots) : IEngine
{
    public List<string> Performed { get; } = [];

    public string Platform => "fixture";

    public string Version => "1";

    public EngineCapabilities Capabilities => EngineCapabilities.None;

    public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) =>
        Task.FromResult<IEngineSession>(new Session(this, actionKind, roots));

    private sealed class Session(ListedNodesOnlyEngine engine, string actionKind, SemanticNode[] roots) : IEngineSession
    {
        public string Route => "/";

        public IReadOnlyCollection<string> ActionKinds { get; } = [actionKind];

        public Task OpenAsync(string url, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new Observation { Route = Route, Roots = roots, Viewport = new TraceViewport(1280, 720) });

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken)
        {
            engine.Performed.Add(action is LocatorAction.DragTo drag ? $"{node.Ref} onto {drag.Target.Ref}" : node.Ref);
            return Task.CompletedTask;
        }

        public Task PressAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SwipeAsync(ScrollDirection direction, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task BackAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RestartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ClearStateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
