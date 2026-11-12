// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E;

/// <summary>
/// What a run does before the first engine session (upstream's <c>run/provision.ts</c>): start the app commands the targets declare. The port has
/// no engine <c>prepare</c> hooks to run beside it.
/// </summary>
internal static class Provision
{
    /// <summary>
    /// Starts every distinct app command the targets declare, in target order, deduplicated across targets (two browsers on one dev server
    /// share one process; upstream's <c>startDeclaredProcesses</c>). <paramref name="pool"/> decides whether each is started for this call or
    /// shared with others that declare it (<see cref="ProcessPools.Unshared"/> when it is null). Nothing more is acquired once
    /// <paramref name="cancellationToken"/> is cancelled; what was acquired is returned for release either way. A start that failed stops the
    /// ones already acquired and throws its own failure.
    /// </summary>
    public static async Task<IAppProcesses> StartDeclaredProcessesAsync(
        IEnumerable<(string Name, ResolvedApp App)> targets,
        string projectRoot,
        Func<ManagedProcessHooks> hooks,
        DebugTrace debug,
        CancellationToken cancellationToken,
        IProcessPool? pool = null)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(hooks);
        pool ??= ProcessPools.Unshared;
        var processes = new HeldProcesses();

        // A cleanup failure after a failed start is a notice at most: the startup failure is the one reported.
        void OnCleanupFailure(Exception failure) => hooks().Notice?.Invoke("cleanup after a failed start: " + failure.Message);
        try
        {
            foreach (var (label, command, readyUrl, key) in DeclaredProcesses.DeclaredCommands(targets))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return processes;
                }

                async Task<IAppProcesses> Start(CancellationToken scope)
                {
                    var process = new ManagedProcess(label, command, projectRoot, new Readiness(readyUrl), hooks());
                    var started = new StartedProcess(process);
                    try
                    {
                        await debug.Time("app.start(" + label + ")", () => process.StartAsync(scope)).ConfigureAwait(false);
                    }
                    catch
                    {
                        await started.StopAsync(OnCleanupFailure).ConfigureAwait(false);
                        throw;
                    }

                    return started;
                }

                processes.Add(await pool.AcquireAsync(projectRoot + "\0command:" + key, Start, cancellationToken).ConfigureAwait(false));
            }

            return processes;
        }
        catch
        {
            // A process that failed to start must not leave the earlier ones running.
            await processes.StopAsync(OnCleanupFailure).ConfigureAwait(false);
            throw;
        }
    }

    // Everything one call started, released in reverse.
    private sealed class HeldProcesses : IAppProcesses
    {
        private readonly List<IAppProcesses> _held = [];

        public bool Running => _held.TrueForAll(unit => unit.Running);

        public void Add(IAppProcesses unit) => _held.Add(unit);

        public async Task StopAsync(Action<Exception> onFailure)
        {
            var units = _held.ToArray();
            _held.Clear();
            for (var index = units.Length - 1; index >= 0; index--)
            {
                await units[index].StopAsync(onFailure).ConfigureAwait(false);
            }
        }
    }

    private sealed class StartedProcess(ManagedProcess process) : IAppProcesses
    {
        public bool Running => process.Running;

        public async Task StopAsync(Action<Exception> onFailure)
        {
            try
            {
                await process.StopAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                onFailure(ex);
            }
        }
    }
}
