// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using ReflectionAssembly = System.Reflection.Assembly;
using TUnit.Core;

namespace E2E.TUnit;

/// <summary>
/// A .NET-only run-level hook (upstream's runner stops the app commands and calls the reporters when the run ends; TUnit has no such owner). Derive a
/// public, non-abstract class with a public parameterless constructor from it in the test project, and the app commands that the targets declare stop
/// when the last test of the run is done, not when the test process exits, and the reporters run (see <c>E2E.Reports.Reporter</c>):
/// <code>
/// public sealed class AppCommands : E2ERunFixture;
/// </code>
/// A project that declares no such class runs none of this: TUnit runs a hook of a library for every project that references it, so the hook
/// looks for the classes derived from this one in the test assembly, and does nothing when there are none. A run with several of them runs each.
/// </summary>
public abstract class E2ERunFixture
{
    private static readonly Lock Gate = new();
    private static List<E2ERunFixture> _registered = [];

    /// <summary>
    /// The test filters, in order. They run only when <see cref="ChangedSince"/> or <c>E2E_CHANGED_SINCE</c> names a base ref; each judges
    /// the tests and the files changed since that ref, and a test it leaves out is skipped with its reason. Empty by default.
    /// </summary>
    protected virtual IReadOnlyList<ITestFilter> Filters => [];

    /// <summary>The base ref to measure the change from (upstream's <c>--changed-since</c>). Unset, the <c>E2E_CHANGED_SINCE</c> environment variable; with neither, no filter runs.</summary>
    protected virtual string? ChangedSince => null;

    /// <summary>
    /// Whether to run only the tests the previous run did not pass (upstream's <c>--last-failed</c>). Unset, the <c>E2E_LAST_FAILED</c> environment variable
    /// (<c>1</c> or <c>true</c>). The previous run's <c>report.json</c> is read from the output directory before this run replaces it; none there is
    /// <c>NO_LAST_RUN</c>. The tests it passed skip themselves with the reason <c>did not fail in the last run</c>, the filters do not run, and
    /// <see cref="E2E.Reports.FinishedRun.LastRun"/> hands the previous report to the reporters.
    /// </summary>
    protected virtual bool LastFailed => false;

    /// <summary>The config the filters read: the discovered <c>e2e.config.json</c>, or the defaults when there is none.</summary>
    protected virtual E2EConfig Config => E2EConfig.Discover();

    private HostRun? _run;

    /// <summary>Creates the run fixtures the test assembly declares and starts each, before the first test.</summary>
    [Before(HookType.TestSession)]
    public static async Task SelectTestsAsync(CancellationToken cancellationToken)
    {
        var fixtures = DeclaredIn(ReflectionAssembly.GetEntryAssembly());
        lock (Gate)
        {
            _registered = fixtures;
        }

        foreach (var fixture in fixtures)
        {
            await fixture.InitializeAsync(ReflectionAssembly.GetEntryAssembly()!, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Finishes each run fixture of the run, after the last test.</summary>
    [After(HookType.TestSession)]
    public static async Task StopAppCommandsAsync()
    {
        List<E2ERunFixture> fixtures;
        lock (Gate)
        {
            fixtures = _registered;
            _registered = [];
        }

        // Every fixture finishes, so one that throws does not leave the next one's app commands running.
        List<Exception>? failures = null;
        foreach (var fixture in fixtures)
        {
            try
            {
                await fixture.DisposeAsync().ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Reported after every fixture has finished.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                (failures ??= []).Add(ex);
            }
        }

        if (failures is not null)
        {
            throw failures.Count == 1 ? failures[0] : new AggregateException(failures);
        }
    }

    /// <summary>The run fixtures <paramref name="assembly"/> declares: its public, concrete classes derived from this one, with a public parameterless constructor.</summary>
    internal static List<E2ERunFixture> DeclaredIn(ReflectionAssembly? assembly)
    {
        if (assembly is null)
        {
            return [];
        }

        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = [.. ex.Types.OfType<Type>()];
        }

        return
        [
            .. types
                .Where(type => type is { IsAbstract: false, IsClass: true, IsVisible: true } && typeof(E2ERunFixture).IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes) is not null)
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .Select(type => (E2ERunFixture)Activator.CreateInstance(type)!),
        ];
    }

    /// <summary>
    /// Builds the reporters of <see cref="Config"/> and starts the run's events, then finds the tests of <paramref name="testAssembly"/> by reflection and runs the filters over them
    /// once, before the first test. The tests the filters left out skip themselves at their start. A run with no base ref runs every test.
    /// </summary>
    internal async Task InitializeAsync(ReflectionAssembly testAssembly, CancellationToken token)
    {
        _run = HostRun.Begin(() => Config);

        // A session the host opens on a named test is never filtered out (upstream's open-session path applies no changedSince), and is no run.
        if (SessionHandover.IsActive)
        {
            return;
        }

        E2EConfig config;
        try
        {
            config = Config;
        }
        catch (Exception) when (HostTestFilters.ResolveChangedSince(ChangedSince) is null && !HostTestFilters.ResolveLastFailed(LastFailed))
        {
            // A config that does not load is said once, by the run's start; the tests report it themselves. Only a changed-since or last-failed selection needs it here.
            return;
        }

        try
        {
            HostRun.Started(config);
            var discovered = HostRun.Collect(() => RunSelection.Discover(testAssembly));
            IReadOnlyDictionary<string, string> excluded = new Dictionary<string, string>(StringComparer.Ordinal);
            var changedSince = HostTestFilters.ResolveChangedSince(ChangedSince);
            var lastFailed = HostTestFilters.ResolveLastFailed(LastFailed);
            if (changedSince is not null || lastFailed)
            {
                var selection = await HostTestFilters.ExcludeAsync(config, Filters, changedSince, discovered, Console.Error.WriteLine, token, lastFailed).ConfigureAwait(false);
                RunSelection.Add(selection.Excluded);
                HostRun.RerunDir = selection.RerunDir;
                excluded = selection.Excluded;
                await HostTestFilters.WriteReportAsync(config, selection, token).ConfigureAwait(false);
            }

            HostRun.Planned(config.Target.Name, discovered, excluded);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            HostRun.Failed(config, ex);
            throw;
        }
    }

    /// <summary>
    /// Finishes the engines the run prepared, ends the run's events and runs the reporters that <see cref="Config"/> lists in <c>reporters</c> once <c>report.json</c> is final, then
    /// stops the app commands that the tests of this process started, and deletes the sessions that setup tests saved (see <c>E2ESetupAttribute</c>).
    /// </summary>
    internal async Task DisposeAsync()
    {
        try
        {
            await HostRun.FinishPreparedAsync().ConfigureAwait(false);
            if (_run is { } run)
            {
                await run.FinishAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _run?.Dispose();
            try
            {
                await RunApps.StopAllAsync().ConfigureAwait(false);
            }
            finally
            {
                // The run's saved sessions go with the run.
                SessionRun.Cleanup();
            }
        }
    }
}
