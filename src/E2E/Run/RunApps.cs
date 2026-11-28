// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using E2E.Internal;
using E2E.Reports;

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
    private static readonly ConcurrentDictionary<string, (TargetConfig Target, ResolvedApp App)> Resolved = new(
        StringComparer.Ordinal
    );

    // The holds of the tests of this process, released at the end of the run.
    private static readonly ConcurrentDictionary<string, IAppProcesses> RunHolds = new(StringComparer.Ordinal);

    // The services of the run, shared like the app commands by every holder on one config, and the values they yielded.
    private static readonly SharedAppProcesses ServicePool = new();
    private static readonly ConcurrentDictionary<string, ServiceValues> ServiceValuesByKey = new(
        StringComparer.Ordinal
    );
    private static readonly ConcurrentDictionary<string, IAppProcesses> ServiceRunHolds = new(StringComparer.Ordinal);

    /// <summary>
    /// The target as the run sees it: its app command started and held for the run, if it declares one, and its URL on the port the app is
    /// served on. A target that declares no command is returned as it is.
    /// </summary>
    public static async Task<TargetConfig> StartAsync(
        E2EConfig config,
        TargetConfig target,
        Action<string>? notice,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(target);
        var services = await StartServicesAsync(config, notice, cancellationToken).ConfigureAwait(false);
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

            var (resolved, processes) = await AcquireLockedAsync(
                    key,
                    config,
                    target,
                    services,
                    notice,
                    cancellationToken
                )
                .ConfigureAwait(false);
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
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(target);
        var (services, servicesHold) = await AcquireServicesAsync(config, notice, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            if (target.App.Command is null)
            {
                return (target, servicesHold);
            }

            var key = KeyOf(config, target);
            var gate = Gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var (resolved, processes) = await AcquireLockedAsync(
                        key,
                        config,
                        target,
                        services,
                        notice,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                return (resolved, new ServicesThenApp(processes, servicesHold));
            }
            finally
            {
                gate.Release();
            }
        }
        catch
        {
            // Services go down after the apps that depended on them, and nothing depends on them now.
            await servicesHold.StopAsync(_ => { }).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Starts the config's services, in key order, and holds them for the run (upstream's <c>startServices</c> in <c>runner.ts</c>, before the app
    /// commands start). A run without services gets <see cref="ServiceValues.Empty"/>. The services stop in <see cref="StopAllAsync"/>, after the apps.
    /// </summary>
    public static async Task<ServiceValues> StartServicesAsync(
        E2EConfig config,
        Action<string>? notice,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.ServiceEntries.Count == 0)
        {
            return ServiceValues.Empty;
        }

        var key = ServicesKey(config);
        var gate = Gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (ServiceRunHolds.ContainsKey(key) && ServiceValuesByKey.TryGetValue(key, out var running))
            {
                return running;
            }

            var (values, hold) = await AcquireServicesAsync(config, notice, cancellationToken).ConfigureAwait(false);
            ServiceRunHolds[key] = hold;
            return values;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>The values of the config's services while someone holds them, for a session opened under that hold; empty otherwise.</summary>
    public static ServiceValues ServicesOf(E2EConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return ServiceValuesByKey.TryGetValue(ServicesKey(config), out var values) ? values : ServiceValues.Empty;
    }

    // One hold on the config's services, started if nobody holds them. The services start with the first hold and stop with the last.
    private static async Task<(ServiceValues Values, IAppProcesses Hold)> AcquireServicesAsync(
        E2EConfig config,
        Action<string>? notice,
        CancellationToken cancellationToken
    )
    {
        if (config.ServiceEntries.Count == 0)
        {
            return (
                ServiceValues.Empty,
                await Provision
                    .StartDeclaredProcessesAsync(
                        [],
                        config.ProjectRoot,
                        () => new ManagedProcessHooks(),
                        DebugTrace.Off,
                        cancellationToken
                    )
                    .ConfigureAwait(false)
            );
        }

        var key = ServicesKey(config);
        var say = notice ?? (message => Console.Error.WriteLine("e2e: " + message));
        var hold = await ServicePool
            .AcquireAsync(
                key,
                async scope =>
                {
                    var hooks = new ServiceHooks
                    {
                        Starting = name => RunEvents.Emit(new SetupEvent(ServiceStep(name), SetupState.Started)),
                        Ready = (name, duration) =>
                            RunEvents.Emit(
                                new SetupEvent(ServiceStep(name), SetupState.Finished, (long)duration.TotalMilliseconds)
                            ),
                        StopFailed = failure => say("cleanup after a failed start: " + failure.Message),
                    };
                    var started = await RunServices
                        .StartAsync(config.ServiceEntries, scope, CancellationToken.None, hooks)
                        .ConfigureAwait(false);
                    if (scope.IsCancellationRequested)
                    {
                        await started.StopAsync(_ => { }).ConfigureAwait(false);
                        scope.ThrowIfCancellationRequested();
                    }

                    ServiceValuesByKey[key] = started.Values;
                    return new SharedServices(started, key);
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        return (ServiceValuesByKey[key], hold);
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
                await held.StopAsync(failure =>
                        Console.Error.WriteLine("e2e: stopping the app command failed: " + failure.Message)
                    )
                    .ConfigureAwait(false);
            }
        }

        // Services go down after the apps that depended on them.
        foreach (var key in ServiceRunHolds.Keys.ToArray())
        {
            if (ServiceRunHolds.TryRemove(key, out var held))
            {
                await held.StopAsync(failure =>
                        Console.Error.WriteLine("e2e: stopping a service failed: " + failure.Message)
                    )
                    .ConfigureAwait(false);
            }
        }
    }

    private static ServiceSetupStep ServiceStep(string name) => new("service \"" + name + "\"");

    private static string ServicesKey(E2EConfig config) => (config.ConfigPath ?? config.ProjectRoot) + "\0services";

    // The app command's setup step, named with the URL it answers on (upstream's `appStep` in `runner.ts`).
    private static AppSetupStep AppStep(string label, string? url) =>
        new(label, url is null ? null : Urls.PrintableUrl(url));

    private static string KeyOf(E2EConfig config, TargetConfig target) =>
        (config.ConfigPath ?? config.ProjectRoot) + "\0" + target.Name;

    private static async Task<(TargetConfig Target, IAppProcesses Processes)> AcquireLockedAsync(
        string key,
        E2EConfig config,
        TargetConfig target,
        ServiceValues services,
        Action<string>? notice,
        CancellationToken cancellationToken
    )
    {
        if (!Resolved.TryGetValue(key, out var resolved))
        {
            var app = AppPorts.AllocateTargetPorts(target.Name, target.App, config.ProjectRoot);
            resolved = (target.WithApp(target.App.WithUrl(app.Base?.Href)), app);
        }

        // An env function runs after the services start and before the app command does; its variables replace the function in the command.
        var env = await CommandEnvs
            .ResolveAsync(config, target.Name, resolved.App, services, cancellationToken)
            .ConfigureAwait(false);
        var declared = env is null ? resolved.App : resolved.App with { Command = resolved.App.Command!.WithEnv(env) };

        // The run's events (upstream's runner emits the same `notice` and `setup` facts); a sink that throws is quarantined, so it cannot fail the start of the app.
        var say = notice ?? (message => Console.Error.WriteLine("e2e: " + message));
        var hooks = new ManagedProcessHooks
        {
            Ci = config.Ci,
            Notice = message =>
            {
                say(message);
                RunEvents.Emit(new NoticeEvent("app", message));
            },
            Starting = (label, url) => RunEvents.Emit(new SetupEvent(AppStep(label, url), SetupState.Started)),
            Ready = (label, url, duration, reused) =>
                RunEvents.Emit(
                    new SetupEvent(
                        AppStep(label, url),
                        SetupState.Finished,
                        (long)duration.TotalMilliseconds,
                        reused ? SetupOutcome.Reused : null
                    )
                ),
        };
        var processes = await Provision
            .StartDeclaredProcessesAsync(
                [(target.Name, declared)],
                config.ProjectRoot,
                () => hooks,
                DebugTrace.Off,
                cancellationToken,
                Pool
            )
            .ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested)
        {
            // Provision returns what it held when the token ends, where a caller that remembers the result needs the cancel.
            await processes.StopAsync(_ => { }).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }

        Resolved[key] = resolved;
        return (resolved.Target, processes);
    }

    // One started set of services, as the pool shares it; its values leave the table when the last hold stops it.
    private sealed class SharedServices(RunServices services, string key) : IAppProcesses
    {
        public bool Running => services.Running;

        public async Task StopAsync(Action<Exception> onFailure)
        {
            ServiceValuesByKey.TryRemove(key, out _);
            await services.StopAsync(onFailure).ConfigureAwait(false);
        }
    }

    // An app hold and the services hold under it, released in that order (upstream stops services after the apps that depended on them).
    private sealed class ServicesThenApp(IAppProcesses app, IAppProcesses services) : IAppProcesses
    {
        public bool Running => app.Running;

        public async Task StopAsync(Action<Exception> onFailure)
        {
            try
            {
                await app.StopAsync(onFailure).ConfigureAwait(false);
            }
            finally
            {
                await services.StopAsync(onFailure).ConfigureAwait(false);
            }
        }
    }
}
