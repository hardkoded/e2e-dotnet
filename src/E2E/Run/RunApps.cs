// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using E2E.Internal;

namespace E2E;

/// <summary>
/// The app commands one process has started (a .NET-only holder: a .NET test framework has no run-level start and finish hooks of its own, where
/// upstream's runner owns them). Every command goes through <see cref="Provision.StartDeclaredProcessesAsync"/> and one
/// <see cref="SharedAppProcesses"/> for the process, so a test, <c>e2e explore</c>, and an MCP session that declare the same command share one
/// instance, and it stops when the last of them releases it.
/// <para>
/// A test holds its command for the run: <see cref="StartAsync"/> acquires it once per target and keeps the hold until <see cref="StopAllAsync"/>
/// ends the run (the <c>E2E.NUnit</c> and <c>E2E.XUnit.V3</c> run fixtures call it), or the process ends, where
/// <see cref="ManagedProcess.KillManagedProcessGroups"/> takes it down. <see cref="AcquireAsync"/> hands the hold to a caller that releases it
/// itself. A start that failed is not remembered, so the next caller tries again.
/// </para>
/// </summary>
internal static class RunApps
{
    private static readonly SharedAppProcesses Pool = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

    // The target as the run sees it, resolved once per target so every caller reads the one port a free port was assigned.
    private static readonly ConcurrentDictionary<string, (TargetConfig Target, ResolvedApp App)> Resolved = new(StringComparer.Ordinal);

    // The holds of the tests of this process, released at the end of the run.
    private static readonly ConcurrentDictionary<string, IAppProcesses> RunHolds = new(StringComparer.Ordinal);

    /// <summary>
    /// The target as the run sees it: its app command started and held for the run, if it declares one, and its URL on the port the app is
    /// served on. A target that declares no command is returned as it is.
    /// </summary>
    public static async Task<TargetConfig> StartAsync(E2EConfig config, TargetConfig target, Action<string>? notice, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(target);
        if (target.App.Command is null)
        {
            return target;
        }

        var key = KeyOf(config, target);
        var gate = Gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (RunHolds.TryGetValue(key, out var held))
            {
                if (held.Running && Resolved.TryGetValue(key, out var running))
                {
                    return running.Target;
                }

                // The app ended since (it crashed, or was stopped): start it again, on a new port when it asked for a free one.
                RunHolds.TryRemove(key, out _);
                Resolved.TryRemove(key, out _);
                await held.StopAsync(_ => { }).ConfigureAwait(false);
            }

            var (resolved, processes) = await AcquireLockedAsync(key, config, target, notice, cancellationToken).ConfigureAwait(false);
            RunHolds[key] = processes;
            return resolved;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Like <see cref="StartAsync"/>, but the hold is the caller's: it releases it with <see cref="IAppProcesses.StopAsync"/> when it is done
    /// (<c>e2e explore</c>, an MCP session), and the command stops when the last hold on it is released.
    /// </summary>
    public static async Task<(TargetConfig Target, IAppProcesses Processes)> AcquireAsync(
        E2EConfig config,
        TargetConfig target,
        Action<string>? notice,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(target);
        if (target.App.Command is null)
        {
            return (target, await Provision.StartDeclaredProcessesAsync([], config.ProjectRoot, () => new ManagedProcessHooks(), DebugTrace.Off, cancellationToken).ConfigureAwait(false));
        }

        var key = KeyOf(config, target);
        var gate = Gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await AcquireLockedAsync(key, config, target, notice, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Releases every hold <see cref="StartAsync"/> took, so the commands stop at the end of the run instead of when the process exits. A command
    /// something else still holds keeps running until that is released.
    /// </summary>
    public static async Task StopAllAsync()
    {
        foreach (var key in RunHolds.Keys.ToArray())
        {
            if (RunHolds.TryRemove(key, out var held))
            {
                Resolved.TryRemove(key, out _);
                await held.StopAsync(failure => Console.Error.WriteLine("e2e: stopping the app command failed: " + failure.Message)).ConfigureAwait(false);
            }
        }
    }

    private static string KeyOf(E2EConfig config, TargetConfig target) => (config.ConfigPath ?? config.ProjectRoot) + "\0" + target.Name;

    private static async Task<(TargetConfig Target, IAppProcesses Processes)> AcquireLockedAsync(
        string key,
        E2EConfig config,
        TargetConfig target,
        Action<string>? notice,
        CancellationToken cancellationToken)
    {
        if (!Resolved.TryGetValue(key, out var resolved))
        {
            var app = AppPorts.AllocateTargetPorts(target.Name, target.App, config.ProjectRoot);
            resolved = (target.WithApp(target.App.WithUrl(app.Base?.Href)), app);
        }

        var hooks = new ManagedProcessHooks
        {
            Ci = config.Ci,
            Notice = notice ?? (message => Console.Error.WriteLine("e2e: " + message)),
        };
        var processes = await Provision.StartDeclaredProcessesAsync([(target.Name, resolved.App)], config.ProjectRoot, () => hooks, DebugTrace.Off, cancellationToken, Pool).ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested)
        {
            // Provision returns what it held when the token ends, where a caller that remembers the result needs the cancel.
            await processes.StopAsync(_ => { }).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }

        Resolved[key] = resolved;
        return (resolved.Target, processes);
    }
}
