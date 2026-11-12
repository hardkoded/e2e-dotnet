// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.EngineTargets;

/// <summary>How a <see cref="ToyEngine"/>'s <c>prepare</c> and <c>finish</c> behave. Upstream's <c>withPrepare</c> option of <c>toyEngine</c>.</summary>
internal enum ToyPrepare
{
    /// <summary>Prepare leaves <c>TOY_POOL</c> and finish returns.</summary>
    Ok,

    /// <summary>Prepare throws <c>toolchain missing</c>.</summary>
    Fail,

    /// <summary>Finish ignores its token and never returns.</summary>
    HangFinish,
}

/// <summary>
/// A two-node screen: a counter value and a button that increments it. Refs are stable, as a device's are not but this toy's are. Upstream's
/// <c>toyEngine</c> (<c>tests/integration/engine-target.test.ts</c>), with its <c>prepare</c> and <c>finish</c> hooks: <see cref="Lifecycle"/> says
/// the order they and the attempt ran in, <c>init</c> being <see cref="StartAsync"/> and <c>dispose</c> the end of its session.
/// </summary>
internal sealed class ToyEngine(ToyPrepare prepare = ToyPrepare.Ok) : IPreparableEngine
{
    private readonly List<string> _lifecycle = [];

    public IReadOnlyList<string> Lifecycle
    {
        get
        {
            lock (_lifecycle)
            {
                return [.. _lifecycle];
            }
        }
    }

    public int Count { get; private set; }

    /// <summary>What the host handed prepare, finish, and the attempt's start.</summary>
    public EnginePrepareInfo? Prepared { get; private set; }

    public EngineFinishInfo? Finished { get; private set; }

    public EngineStartOptions? Started { get; private set; }

    /// <summary>Runs inside <see cref="StartAsync"/> after it logged <c>init</c>: a wait, or a throw.</summary>
    public Func<EngineStartOptions, CancellationToken, Task>? OnStart { get; init; }

    /// <summary>Runs inside prepare after it logged <c>prepare</c>: a wait that may ignore its token.</summary>
    public Func<CancellationToken, Task>? OnPrepare { get; init; }

    /// <summary>Runs inside finish after it logged <c>finish</c>: a throw.</summary>
    public Func<CancellationToken, Task>? OnFinish { get; init; }

    public string Platform => "ios";

    public string Name => "toy-device";

    public string Version => "1.0.0";

    public EngineCapabilities Capabilities => EngineCapabilities.Observation | EngineCapabilities.Actions;

    public async Task<EnginePrepareResult> PrepareAsync(EnginePrepareInfo info, CancellationToken cancellationToken)
    {
        Record("prepare");
        Prepared = info;
        info.Log("provisioning toy device");
        if (OnPrepare is { } wait)
        {
            await wait(cancellationToken).ConfigureAwait(false);
        }

        if (prepare == ToyPrepare.Fail)
        {
            throw new InvalidOperationException("toolchain missing");
        }

        // What prepare provisioned reaches the worker through the result's env; the run's own variables are never overwritten.
        return new EnginePrepareResult { Workers = 1, Env = new Dictionary<string, string> { ["TOY_POOL"] = "sim-a,sim-b", ["TOY_CACHE"] = "/overwritten" } };
    }

    public async Task FinishAsync(EngineFinishInfo info, CancellationToken cancellationToken)
    {
        Record("finish");
        Finished = info;
        if (prepare == ToyPrepare.HangFinish)
        {
            // Ignores its token on purpose: the host must not wait on it.
            await new TaskCompletionSource().Task.ConfigureAwait(false);
        }

        if (OnFinish is { } fail)
        {
            await fail(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
    {
        Record("init");
        Started = options;
        if (OnStart is { } start)
        {
            await start(options, cancellationToken).ConfigureAwait(false);
        }

        return new Session(this);
    }

    private void Record(string step)
    {
        lock (_lifecycle)
        {
            _lifecycle.Add(step);
        }
    }

    private sealed class Session(ToyEngine engine) : IEngineSession
    {
        public string Route => "/";

        public Task OpenAsync(string url, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new Observation
            {
                Route = "/",
                Roots =
                [
                    new SemanticNode { Ref = "counter", Role = "status", Name = "count", Text = engine.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                    new SemanticNode { Ref = "increment", Role = "button", Name = "Increment" },
                ],
            });

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken)
        {
            if (node.Ref != "increment" || action is not LocatorAction.Tap)
            {
                throw new EngineException("NOT_ACTIONABLE", "the toy only taps its button");
            }

            engine.Count++;
            return Task.CompletedTask;
        }

        public Task PressAsync(string key, CancellationToken cancellationToken) => throw new EngineException("UNSUPPORTED_CAPABILITY", "the toy has no keyboard");

        public ValueTask DisposeAsync()
        {
            engine.Record("dispose");
            return ValueTask.CompletedTask;
        }
    }
}
