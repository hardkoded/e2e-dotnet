// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Acp.Tests.Executor;

/// <summary>
/// The document engine with a hook on its observations, for a test that ends a step while the executor reads the screen: the C# shape of
/// upstream's <c>ctx.observe</c> mock that aborts the step.
/// </summary>
internal sealed class HookedEngine(IEngine inner) : IEngine
{
    private int _observations;
    private Task? _hold;

    /// <summary>Runs on the observation that follows <see cref="After"/> others, before it answers.</summary>
    public Action? OnObserve { get; set; }

    public int After { get; set; }

    /// <summary>When set, the observations that follow it wait for it before they answer: upstream's <c>ctx.observe</c> mock that holds the screen.</summary>
    public Task? Hold
    {
        get => Volatile.Read(ref _hold);
        set => Volatile.Write(ref _hold, value);
    }

    /// <summary>How many observations the engine took.</summary>
    public int Observations => Volatile.Read(ref _observations);

    public string Platform => inner.Platform;

    public string Version => inner.Version;

    public EngineCapabilities Capabilities => inner.Capabilities;

    public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) =>
        new Session(this, await inner.StartAsync(options, cancellationToken).ConfigureAwait(false));

    private sealed class Session(HookedEngine engine, IEngineSession inner) : IEngineSession
    {
        public string Route => inner.Route;

        public IReadOnlyCollection<string> ActionKinds => inner.ActionKinds;

        public IReadOnlyCollection<string> PointerActionKinds => inner.PointerActionKinds;

        public bool CanLocate => inner.CanLocate;

        public Task OpenAsync(string url, CancellationToken cancellationToken) => inner.OpenAsync(url, cancellationToken);

        public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref engine._observations) > engine.After && engine.OnObserve is { } hook)
            {
                engine.OnObserve = null;
                hook();
            }

            if (engine.Hold is { } hold)
            {
                await hold.ConfigureAwait(false);
            }

            return await inner.ObserveAsync(cancellationToken).ConfigureAwait(false);
        }

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) => inner.PerformAsync(node, action, cancellationToken);

        public Task PressAsync(string key, CancellationToken cancellationToken) => inner.PressAsync(key, cancellationToken);

        public Task TypeTextAsync(string text, bool replace, CancellationToken cancellationToken) => inner.TypeTextAsync(text, replace, cancellationToken);

        public Task<IReadOnlyList<SemanticNode>> LocateAsync(LocatorExpression expression, TimeSpan timeout, CancellationToken cancellationToken) =>
            inner.LocateAsync(expression, timeout, cancellationToken);

        public Task SwipeAsync(ScrollDirection direction, CancellationToken cancellationToken) => inner.SwipeAsync(direction, cancellationToken);

        public Task BackAsync(CancellationToken cancellationToken) => inner.BackAsync(cancellationToken);

        public Task RestartAsync(CancellationToken cancellationToken) => inner.RestartAsync(cancellationToken);

        public Task ClearStateAsync(CancellationToken cancellationToken) => inner.ClearStateAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
