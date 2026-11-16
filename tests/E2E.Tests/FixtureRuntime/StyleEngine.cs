// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.FixtureRuntime;

/// <summary>
/// The fake engine of upstream's style-read tests (<c>defineEngine({ name: 'fake', observe, readStyle })</c>): one observation of the nodes
/// <see cref="Nodes"/> lists, and a <see cref="ReadStyle"/> that, when set, declares the computed-style capability.
/// </summary>
internal sealed class StyleEngine : IEngine
{
    /// <summary>The nodes the next observation lists, by its generation (1 for the first).</summary>
    public Func<int, IReadOnlyList<SemanticNode>> Nodes { get; init; } = _ => [new SemanticNode { Ref = "message", Role = "status", Text = "Error" }];

    /// <summary>Upstream's <c>readStyle</c>; null for an engine without the capability.</summary>
    public Func<SemanticNode, IReadOnlyList<string>, OperationContext, Task<IReadOnlyDictionary<string, string>>>? ReadStyle { get; init; }

    public string Platform => "style";

    public string Version => "1";

    public EngineCapabilities Capabilities => EngineCapabilities.Observation | (ReadStyle is null ? EngineCapabilities.None : EngineCapabilities.ComputedStyle);

    public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) => Task.FromResult<IEngineSession>(new Session(this));

    private sealed class Session(StyleEngine engine) : IEngineSession
    {
        private int _generation;

        public string Route => "/";

        public Task OpenAsync(string url, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new Observation { Route = "/", Roots = engine.Nodes(Interlocked.Increment(ref _generation)) });

        public Task<IReadOnlyDictionary<string, string>> ReadStyleAsync(SemanticNode node, IReadOnlyList<string> properties, OperationContext context, CancellationToken cancellationToken) =>
            engine.ReadStyle is { } read
                ? read(node, properties, context)
                : Task.FromException<IReadOnlyDictionary<string, string>>(new EngineException("UNSUPPORTED_CAPABILITY", "computed styles is not supported by this engine."));

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task PressAsync(string key, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SwipeAsync(ScrollDirection direction, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task BackAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task RestartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ClearStateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
