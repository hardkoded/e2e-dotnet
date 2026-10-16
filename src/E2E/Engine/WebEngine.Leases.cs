// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;

namespace E2E.Engine;

/// <summary>
/// The lease half of the engine (upstream <c>surface.ts</c> <c>prepare</c>, <c>finish</c>, and <c>init</c>): the hooks a
/// runner calls around a run so a <see cref="WebEngineOptions.Browser"/> provider's browsers are leased once, handed to the
/// workers, and released on every exit path. The port has no runner, so an engine started without them leases for the one
/// attempt it starts, as upstream's standalone run does.
/// </summary>
public sealed partial class WebEngine : IAsyncDisposable
{
    private readonly LeasedBrowsers? _leases;

    /// <summary>
    /// Leases one browser per worker slot the run will use from the provider, once per run and target, before any worker
    /// starts. The result's environment is what the workers read in <see cref="Init"/>. An engine with no provider has
    /// nothing to prepare.
    /// </summary>
    public Task<EnginePrepareResult> PrepareAsync(EnginePrepareInfo info, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(info);
        return _leases is null ? Task.FromResult(new EnginePrepareResult()) : _leases.PrepareAsync(info, cancellationToken);
    }

    /// <summary>
    /// Releases the browsers <see cref="PrepareAsync"/> leased, once per run and target, then has the provider sweep up the
    /// leases a worker that died left open. Call it after every worker ended, on every exit path, with a cleanup budget in
    /// <paramref name="cancellationToken"/>.
    /// </summary>
    public Task FinishAsync(EngineFinishInfo info, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(info);
        return _leases is null ? Task.CompletedTask : _leases.FinishAsync(info, cancellationToken);
    }

    /// <summary>
    /// Starts the worker of one slot: records the run it serves and, in <see cref="BrowserProviderScope.Worker"/> scope, the
    /// lease its slot was handed. Every <see cref="StartAsync"/> after it rides that lease, or a replacement once its browser
    /// dropped. It can be called again on a disposed engine.
    /// </summary>
    public void Init(EngineInitInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        _leases?.Init(info);
    }

    /// <summary>Worker shutdown: releases the replacement lease the worker acquired for itself, within the cleanup timeout. What prepare leased is the runner's to release.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_leases is null)
        {
            return;
        }

        using var budget = new CancellationTokenSource(E2EDefaults.CleanupTimeout);
        await _leases.DisposeAsync(budget.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Opens the lease an attempt rides. An engine that <see cref="Init"/> started a worker for uses the worker's; an engine
    /// started on its own prepares one slot of a run of its own first, and gives everything back when the attempt ends.
    /// </summary>
    private async Task<LeaseScope> OpenLeaseAsync(EngineStartOptions options, CancellationToken cancellationToken)
    {
        var leases = _leases!;
        var attemptId = options.AttemptId ?? Guid.NewGuid().ToString("n");
        EngineFinishInfo? standalone = null;
        if (!leases.IsInitialized)
        {
            leases = new LeasedBrowsers(_options.Browser!);
            standalone = await PrepareAloneAsync(leases, options, cancellationToken).ConfigureAwait(false);
        }

        var scope = new LeaseScope(leases, standalone);
        try
        {
            if (leases.Scope == BrowserProviderScope.Attempt)
            {
                var lease = await leases.StartAttemptAsync(attemptId, cancellationToken).ConfigureAwait(false);
                return scope.Persistent(lease);
            }

            return scope.Shared();
        }
        catch
        {
            await scope.CloseAsync(connected: false).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            throw;
        }
    }

    /// <summary>Prepares and starts the one worker of a run that has only this attempt; returns what finishing that run needs.</summary>
    private static async Task<EngineFinishInfo> PrepareAloneAsync(LeasedBrowsers leases, EngineStartOptions options, CancellationToken cancellationToken)
    {
        const string target = "web";
        var runId = Guid.NewGuid().ToString("n");
        var env = options.Env ?? ProcessEnvironment();
        void Log(string line)
        {
            // A run without a reporter has nowhere to put a progress line.
        }

        var finish = new EngineFinishInfo { RunId = runId, TargetName = target, Env = env, Log = Log };
        try
        {
            var prepared = await leases.PrepareAsync(new EnginePrepareInfo { RunId = runId, TargetName = target, Slots = 1, Env = env, Log = Log }, cancellationToken).ConfigureAwait(false);
            var workerEnv = new Dictionary<string, string?>(env);
            foreach (var (name, value) in prepared.Env)
            {
                workerEnv[name] = value;
            }

            leases.Init(new EngineInitInfo { RunId = runId, TargetName = target, WorkerSlot = 0, Env = workerEnv, Log = Log });
        }
        catch
        {
            // What prepare leased before it failed is still owed back.
            using var budget = new CancellationTokenSource(E2EDefaults.CleanupTimeout);
            await leases.FinishAsync(finish, budget.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            throw;
        }

        return finish;
    }

    private static Dictionary<string, string?> ProcessEnvironment()
    {
        var env = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            env[(string)entry.Key] = entry.Value as string;
        }

        return env;
    }

    /// <summary>
    /// The browser lease one attempt rides, and how it is given back: the connection it attaches through, and the close
    /// that follows the session's own. Closing is idempotent, so a failed start and the session it half-built both may.
    /// </summary>
    private sealed class LeaseScope(LeasedBrowsers leases, EngineFinishInfo? standalone)
    {
        private int _closed;
        private bool _attached;

        public WebConnectOptions? Connect { get; private set; }

        /// <summary>A leased browser is fresh by the provider's contract, so no earlier attempt's context is checked against it.</summary>
        public HashSet<string> UsedContexts { get; } = new(StringComparer.Ordinal);

        /// <summary>Worker scope: the worker's shared browser, attached to through the endpoint of the lease it holds.</summary>
        public LeaseScope Shared()
        {
            Connect = new WebConnectOptions
            {
                CdpEndpoint = async cancellationToken =>
                {
                    var endpoint = await leases.EndpointAsync(cancellationToken).ConfigureAwait(false);
                    _attached = true;
                    return endpoint;
                },
            };
            return this;
        }

        /// <summary>Attempt scope: the browser leased for this attempt, reconnected through the lease's own endpoint.</summary>
        public LeaseScope Persistent(BrowserLease lease)
        {
            Connect = new WebConnectOptions
            {
                CdpEndpoint = _ => Task.FromResult(lease.CdpEndpoint),
                ReconnectEndpoint = _ => Task.FromResult(lease.ReconnectEndpoint ?? lease.CdpEndpoint),
            };
            return this;
        }

        /// <summary>
        /// Gives the attempt's lease back, after its connection closed. <paramref name="connected"/> is whether the browser
        /// still held at that point: one the attempt attached to and found dropped is replaced for the worker's next attempt. A run of its own ends with
        /// the attempt: its worker is disposed and its lease released. Every step runs whatever the earlier ones did, and
        /// the first failure is reported.
        /// </summary>
        public async Task CloseAsync(bool connected)
        {
            if (Interlocked.Exchange(ref _closed, 1) == 1)
            {
                return;
            }

            using var budget = new CancellationTokenSource(E2EDefaults.CleanupTimeout);
            if (_attached)
            {
                leases.ConnectionEnded(connected);
            }

            Exception? failure = null;
            var steps = new List<Func<Task>> { () => leases.EndAttemptAsync(budget.Token) };
            if (standalone is not null)
            {
                steps.Add(() => leases.DisposeAsync(budget.Token));
                steps.Add(() => leases.FinishAsync(standalone, budget.Token));
            }

            foreach (var step in steps)
            {
                try
                {
                    await step().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    failure ??= ex;
                }
            }

            if (failure is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }
    }
}
