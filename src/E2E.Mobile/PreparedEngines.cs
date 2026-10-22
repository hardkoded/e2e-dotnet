// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using E2E.Engine;

namespace E2E.Mobile;

/// <summary>
/// Upstream's <c>PreparedEngines</c> (<c>run/provision.ts</c>), for the one engine and the one slot of an attempt: the prepare and the finish around a hosted device when the engine was started on its own, with no runner to call prepare and finish around
/// it, as <c>e2e explore</c> and <c>e2e mcp</c> start it. Upstream's <c>openStandaloneAttempt</c> (<c>run/standalone.ts</c>)
/// prepares the engine for one slot of a run of its own and finishes that run when the attempt closes; this is the same pair around
/// <see cref="MobileEngine.PrepareAsync"/> and <see cref="MobileEngine.FinishAsync"/>. A lease bills until it is released, so the release
/// runs once, whichever of the session's close, a failed start, and the end of the process gets there first. There is no upstream
/// standalone host in the port, so the engine starts the pair itself and the session holds this object (.NET-only: the release runs once and also at process exit).
/// </summary>
internal sealed class PreparedEngines
{
    /// <summary>How much longer than the cleanup budget a release is waited for, when the provider ignores the token that ends the budget.</summary>
    private static readonly TimeSpan ReleaseGrace = TimeSpan.FromSeconds(2);

    private readonly MobileEngine _engine;
    private readonly MobileFinishInfo _finish;
    private readonly EventHandler _exit;
    private readonly Action _onReleased;
    private readonly object _gate = new();
    private Task? _releasing;

    private PreparedEngines(MobileEngine engine, MobileFinishInfo finish, IReadOnlyDictionary<string, string> workerEnv, Action onReleased)
    {
        _onReleased = onReleased;
        _engine = engine;
        _finish = finish;
        WorkerEnv = workerEnv;
        _exit = (_, _) => ReleaseAtExit();
    }

    /// <summary>The environment prepare left for the one worker: where it reads the leased device from.</summary>
    public IReadOnlyDictionary<string, string> WorkerEnv { get; }

    /// <summary>
    /// Leases one device for the engine's target. A prepare that fails or is cancelled gives back what it leased before it throws
    /// its own failure, so a lease that was granted and then rejected is not left billing.
    /// </summary>
    public static async Task<PreparedEngines> PrepareAsync(MobileEngine engine, string projectRoot, IReadOnlyDictionary<string, string?> env, Action onReleased, CancellationToken cancellationToken)
    {
        var options = engine.Options;
        var runId = Guid.NewGuid().ToString("n");
        var finish = new MobileFinishInfo { RunId = runId, TargetName = options.TargetName, Env = env, Log = Ignore, CleanupTimeout = engine.CleanupTimeout };
        Task<MobilePrepareResult>? preparing = null;
        try
        {
            preparing = engine.PrepareAsync(
                new MobilePrepareInfo
                {
                    RunId = runId,
                    TargetName = options.TargetName,
                    ProjectRoot = projectRoot,
                    Slots = 1,
                    Env = env,
                    App = options.App,
                    Log = Ignore,
                },
                cancellationToken);

            // A caller who cancels does not wait for a provider that ignores the token; what that provider grants later is released where it lands.
            var prepared = await preparing.WaitAsync(cancellationToken).ConfigureAwait(false);
            var lease = new PreparedEngines(engine, finish, prepared.Env, onReleased);
            AppDomain.CurrentDomain.ProcessExit += lease._exit;
            return lease;
        }
        catch (Exception cause)
        {
            // A prepare nobody awaits any more must not leave an unobserved fault behind.
            _ = preparing?.ContinueWith(static task => task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

            // What prepare leased before it failed is still owed back; the failure of prepare is the one to report.
            await GiveBackAsync(engine, finish).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

            // The pool reports a lease that was cancelled as a failed lease; the caller who cancelled asked for cancellation.
            if (cancellationToken.IsCancellationRequested && cause is not E2EException { Code: EngineErrorCodes.Cancelled })
            {
                throw new EngineException(EngineErrorCodes.Cancelled, "leasing the hosted device was cancelled", cause);
            }

            throw;
        }
    }

    /// <summary>The process environment, which is where a provider reads its token from when no one passes another.</summary>
    public static IReadOnlyDictionary<string, string?> ProcessEnvironment()
    {
        var env = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            env[(string)entry.Key] = entry.Value as string;
        }

        return env;
    }

    /// <summary>
    /// Gives the device back, once: a second call waits for the first. A release that fails or outlives the cleanup budget throws, because
    /// the device may still be billing and its owner must hear of it.
    /// </summary>
    public Task FinishAsync()
    {
        lock (_gate)
        {
            if (_releasing is null)
            {
                AppDomain.CurrentDomain.ProcessExit -= _exit;
                _releasing = ReleaseOnceAsync();
            }

            return _releasing;
        }
    }

    private async Task ReleaseOnceAsync()
    {
        try
        {
            await GiveBackAsync(_engine, _finish).ConfigureAwait(false);
        }
        finally
        {
            _onReleased();
        }
    }

    private static async Task GiveBackAsync(MobileEngine engine, MobileFinishInfo finish)
    {
        var budget = finish.CleanupTimeout;
        using var cancel = new CancellationTokenSource(budget);
        try
        {
            // A provider that ignores its token must not keep the close waiting: the bound is the budget, not the provider's goodwill.
            await engine.FinishAsync(finish, cancel.Token).WaitAsync(budget + ReleaseGrace).ConfigureAwait(false);
        }
        catch (Exception cause) when (cancel.IsCancellationRequested || cause is TimeoutException)
        {
            throw new EngineException(
                EngineErrorCodes.EngineFailure,
                $"releasing the hosted device did not finish within {budget.TotalSeconds:0} s; the provider may still bill it until its own time limit",
                retryable: false,
                cause);
        }
    }

    private static void Ignore(string line)
    {
        // The run has no reporter: a progress line has nowhere to go, and a lease id or session URL in it would only be one more thing to redact.
    }

    /// <summary>The release for a process that ends while the lease is held (an unhandled exit), which no close will reach. It waits, because nothing runs after it.</summary>
    internal void ReleaseAtExit()
    {
        try
        {
            FinishAsync().Wait(_finish.CleanupTimeout + ReleaseGrace);
        }
        catch (Exception)
        {
            // The process is going away; there is no log left to report to, and the provider's own time limit ends the lease.
        }
    }
}
