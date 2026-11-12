// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>
/// Who starts the app processes an attempt declares (upstream's <c>ProcessPool</c> in <c>run/process-pool.ts</c>). <see cref="ProcessPools.Unshared"/>
/// starts each one for the attempt alone, as a run does; <see cref="SharedAppProcesses"/> hands every attempt that declares the same process one
/// instance of it, so live sessions on one dev server share it. Either way the attempt starts and stops through
/// <see cref="Provision.StartDeclaredProcessesAsync"/>: the pool only decides whether a process is started or reused.
/// </summary>
internal interface IProcessPool
{
    /// <summary>
    /// One hold on the process <paramref name="key"/> names, released by the returned <see cref="IAppProcesses.StopAsync"/>. <paramref name="start"/>
    /// starts a fresh instance under the token it is given: the attempt's <paramref name="cancellationToken"/>, or for a shared process one that is
    /// cancelled once no attempt waits on the start any more.
    /// </summary>
    Task<IAppProcesses> AcquireAsync(string key, Func<CancellationToken, Task<IAppProcesses>> start, CancellationToken cancellationToken);
}

/// <summary>The pools a run can start its processes through (upstream's <c>UNSHARED</c>).</summary>
internal static class ProcessPools
{
    /// <summary>Every attempt starts its own processes, under its own token.</summary>
    public static IProcessPool Unshared { get; } = new UnsharedPool();

    private sealed class UnsharedPool : IProcessPool
    {
        public Task<IAppProcesses> AcquireAsync(string key, Func<CancellationToken, Task<IAppProcesses>> start, CancellationToken cancellationToken) =>
            start(cancellationToken);
    }
}

/// <summary>
/// One instance per process across attempts: the first attempt that declares one starts it, and it stops when the last attempt holding it
/// releases it, so closing one live session never takes the dev server out from under another. A shared start outlives the attempt that began it
/// while another still waits on it, and is cancelled once every attempt waiting on it gave up (a cancelled request, a server shutting down), so a
/// hung start never leaves what it started behind; the next attempt starts it afresh. An attempt that arrives while a process stops starts a fresh
/// one once the stop is done.
/// </summary>
internal sealed class SharedAppProcesses : IProcessPool
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, SharedEntry> _entries = new(StringComparer.Ordinal);

    // Stops still running, and starts abandoned, by key.
    private readonly Dictionary<string, Task> _draining = new(StringComparer.Ordinal);

    public async Task<IAppProcesses> AcquireAsync(string key, Func<CancellationToken, Task<IAppProcesses>> start, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(start);
        SharedEntry shared;
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var existing))
            {
                // A stop that failed has still ended its process: the fresh start goes ahead.
                var drained = _draining.GetValueOrDefault(key) ?? Task.CompletedTask;
                var abandon = new CancellationTokenSource();
                existing = new SharedEntry(abandon) { Started = StartAfterAsync(drained, start, abandon.Token) };
                var created = existing;
                _ = created.Started.ContinueWith(
                    task =>
                    {
                        lock (_gate)
                        {
                            created.Settled = true;
                            if (task.Status != TaskStatus.RanToCompletion)
                            {
                                Forget(key, created);
                            }
                        }

                        // Settled, so no attempt giving up can cancel it any more.
                        created.Abandon.Dispose();
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                _entries[key] = existing;
            }

            shared = existing;
            shared.Users++;
            shared.Waiting++;
        }

        var waiting = true;
        void GiveUp()
        {
            var abandonStart = false;
            lock (_gate)
            {
                if (!waiting)
                {
                    return;
                }

                waiting = false;
                shared.Waiting--;
                if (shared.Waiting > 0 || shared.Settled)
                {
                    return;
                }

                abandonStart = true;
                Forget(key, shared);

                // A start that completes despite the cancel has nobody left to stop it: it stops here.
                _draining[key] = StopAbandonedAsync(shared.Started);
            }

            if (abandonStart)
            {
                try
                {
                    shared.Abandon.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // The start settled in the meantime: it has nothing left to cancel.
                }
            }
        }

        IAppProcesses processes;
        using (cancellationToken.Register(GiveUp))
        {
            try
            {
                // An attempt that gives up stops waiting at once, whether or not others still wait on the start.
                processes = await shared.Started.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // The wait ends inline when the token is cancelled, possibly before the registration above ran: giving up is idempotent.
                if (cancellationToken.IsCancellationRequested)
                {
                    GiveUp();
                }

                lock (_gate)
                {
                    shared.Users--;
                }

                throw;
            }
            finally
            {
                lock (_gate)
                {
                    if (waiting)
                    {
                        waiting = false;
                        shared.Waiting--;
                    }
                }
            }
        }

        return new Hold(this, key, shared, processes);
    }

    private static async Task<IAppProcesses> StartAfterAsync(Task drained, Func<CancellationToken, Task<IAppProcesses>> start, CancellationToken cancellationToken)
    {
        try
        {
            await drained.ConfigureAwait(false);
        }
        catch
        {
            // The earlier stop reported its failure to its own caller.
        }

        return await start(cancellationToken).ConfigureAwait(false);
    }

    private static async Task StopAbandonedAsync(Task<IAppProcesses> started)
    {
        try
        {
            var processes = await started.ConfigureAwait(false);
            await processes.StopAsync(_ => { }).ConfigureAwait(false);
        }
        catch
        {
            // The start failed or was cancelled: it stopped what it spawned.
        }
    }

    // Drops entry from the pool, unless a fresh start already took its key. Callers hold the gate.
    private void Forget(string key, SharedEntry entry)
    {
        if (_entries.TryGetValue(key, out var current) && ReferenceEquals(current, entry))
        {
            _entries.Remove(key);
        }
    }

    private sealed class SharedEntry(CancellationTokenSource abandon)
    {
        public int Users { get; set; }

        // Set once the start settled: from then on an attempt giving up abandons nothing.
        public bool Settled { get; set; }

        // Attempts still waiting on the start whose own token has not been cancelled.
        public int Waiting { get; set; }

        // Cancels the start once no attempt waits on it any more.
        public CancellationTokenSource Abandon { get; } = abandon;

        public Task<IAppProcesses> Started { get; set; } = null!;
    }

    private sealed class Hold(SharedAppProcesses pool, string key, SharedEntry shared, IAppProcesses processes) : IAppProcesses
    {
        private int _released;

        public bool Running => processes.Running;

        public async Task StopAsync(Action<Exception> onFailure)
        {
            // One hold releases once, however often it is stopped.
            if (Interlocked.Exchange(ref _released, 1) != 0)
            {
                return;
            }

            TaskCompletionSource? done = null;
            lock (pool._gate)
            {
                shared.Users--;
                if (shared.Users > 0)
                {
                    return;
                }

                pool.Forget(key, shared);
                done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                pool._draining[key] = done.Task;
            }

            try
            {
                await processes.StopAsync(onFailure).ConfigureAwait(false);
            }
            finally
            {
                lock (pool._gate)
                {
                    if (pool._draining.TryGetValue(key, out var current) && ReferenceEquals(current, done.Task))
                    {
                        pool._draining.Remove(key);
                    }
                }

                done.TrySetResult();
            }
        }
    }
}
