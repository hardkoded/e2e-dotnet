// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using E2E.Internal;

namespace E2E;

/// <summary>
/// The app commands one test process has started (a .NET-only holder: a .NET test framework has no run-level start and finish hooks, where
/// upstream's runner owns them). The first test, <c>e2e explore</c>, or MCP session that needs a target's <c>app.command</c> starts it, and every
/// later one finds it running; nothing stops it before the process ends, where <see cref="ManagedProcess.KillManagedProcessGroups"/> takes it
/// down. A start that failed is not remembered, so the next caller tries again.
/// </summary>
internal static class RunApps
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly ConcurrentDictionary<string, (TargetConfig Target, IAppProcesses Processes)> Started = new(StringComparer.Ordinal);

    /// <summary>
    /// The target as the run sees it: its app command started, if it declares one, and its URL on the port the app is served on. A target that
    /// declares no command is returned as it is.
    /// </summary>
    public static async Task<TargetConfig> StartAsync(E2EConfig config, TargetConfig target, Action<string>? notice, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(target);
        if (target.App.Command is null)
        {
            return target;
        }

        var key = (config.ConfigPath ?? config.ProjectRoot) + "\0" + target.Name;
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Started.TryGetValue(key, out var running))
            {
                if (running.Processes.Running)
                {
                    return running.Target;
                }

                // The app ended since (it crashed, or was stopped): start it again, on a new port when it asked for a free one.
                Started.TryRemove(key, out _);
                await running.Processes.StopAsync(_ => { }).ConfigureAwait(false);
            }

            var app = AppPorts.AllocateTargetPorts(target.Name, target.App, config.ProjectRoot);
            var hooks = new ManagedProcessHooks
            {
                Ci = config.Ci,
                Notice = notice ?? (message => Console.Error.WriteLine("e2e: " + message)),
            };
            var processes = await Provision.StartDeclaredProcessesAsync(target.Name, app, config.ProjectRoot, () => hooks, new DebugTrace(false), cancellationToken).ConfigureAwait(false);
            var resolved = target.WithApp(target.App.WithUrl(app.Base?.Href));
            Started[key] = (resolved, processes);
            return resolved;
        }
        finally
        {
            Gate.Release();
        }
    }
}
