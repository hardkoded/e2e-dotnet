// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E;

/// <summary>
/// What a run does before the first engine session (upstream's <c>run/provision.ts</c>): start the app command the target declares. The port has
/// no engine <c>prepare</c> hooks to run beside it, and one target, so there is nothing to deduplicate across targets.
/// </summary>
internal static class Provision
{
    /// <summary>
    /// Starts the app command the target declares, when it declares one. Nothing more is acquired once <paramref name="cancellationToken"/> is
    /// cancelled, which throws; what was started by a failed start is stopped before the failure is thrown.
    /// </summary>
    public static async Task<IAppProcesses> StartDeclaredProcessesAsync(
        string targetName,
        ResolvedApp app,
        string projectRoot,
        Func<ManagedProcessHooks> hooks,
        DebugTrace debug,
        CancellationToken cancellationToken)
    {
        if (app.Command is null)
        {
            return NoProcesses.Instance;
        }

        var label = "target \"" + targetName + "\" command";
        var process = new ManagedProcess(label, app.Command, projectRoot, new Readiness(app.ReadyUrl), hooks());
        try
        {
            await debug.Time("app.start(" + label + ")", () => process.StartAsync(cancellationToken)).ConfigureAwait(false);
        }
        catch
        {
            // A start that failed or was cancelled stops its own process; this only makes certain of it.
            await process.StopAsync().ConfigureAwait(false);
            throw;
        }

        return new StartedProcess(process);
    }

    private sealed class NoProcesses : IAppProcesses
    {
        public static NoProcesses Instance { get; } = new();

        public Task StopAsync(Action<Exception> onFailure) => Task.CompletedTask;

        public bool Running => true;
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
