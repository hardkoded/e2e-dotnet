// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using Xunit;

namespace E2E.XUnit.V3;

/// <summary>
/// A .NET-only run-level hook (upstream's runner stops the app commands and calls the reporters when the run ends; xUnit has no such owner). Register it as an assembly
/// fixture, and the app commands that the targets declare stop when the last test of the assembly is done, not when the test process exits, and the reporters run (see <c>E2E.Reports.Reporter</c>):
/// <code>
/// [assembly: AssemblyFixture(typeof(E2E.XUnit.V3.E2ERunFixture))]
/// </code>
/// To give the run test filters, derive a fixture, override <see cref="Filters"/>, and register that type instead.
/// </summary>
public class E2ERunFixture : IAsyncLifetime
{
    /// <summary>
    /// The test filters, in order. They run only when <see cref="ChangedSince"/> or <c>E2E_CHANGED_SINCE</c> names a base ref; each judges
    /// the tests and the files changed since that ref, and a test it leaves out is skipped with its reason. Empty by default.
    /// </summary>
    protected virtual IReadOnlyList<ITestFilter> Filters => [];

    /// <summary>The base ref to measure the change from (upstream's <c>--changed-since</c>). Unset, the <c>E2E_CHANGED_SINCE</c> environment variable; with neither, no filter runs.</summary>
    protected virtual string? ChangedSince => null;

    /// <summary>The config the filters read: the discovered <c>e2e.config.json</c>, or the defaults when there is none.</summary>
    protected virtual E2EConfig Config => E2EConfig.Discover();

    /// <summary>
    /// Finds the tests of the assembly by reflection and runs the filters over them once, before the first test. The tests the filters left
    /// out skip themselves at their start. A run with no base ref runs every test.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        // A session the host opens on a named test is never filtered out (upstream's open-session path applies no changedSince).
        if (SessionHandover.IsActive || HostTestFilters.ResolveChangedSince(ChangedSince) is not { } changedSince || TestAssemblyOf(Xunit.TestContext.Current.TestAssembly?.AssemblyPath) is not { } testAssembly)
        {
            return;
        }

        var config = Config;
        var token = Xunit.TestContext.Current.CancellationToken;
        var selection = await HostTestFilters.ExcludeAsync(config, Filters, changedSince, RunSelection.Discover(testAssembly), Console.Error.WriteLine, token).ConfigureAwait(false);
        RunSelection.Add(selection.Excluded);
        await HostTestFilters.WriteReportAsync(config, selection, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs the reporters that <see cref="Config"/> lists in <c>reporters</c> once <c>report.json</c> is final, then
    /// stops the app commands that the tests of this process started, and deletes the sessions that setup tests saved (see <c>E2ESetupAttribute</c>).
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await Reporters.RunFinishedAsync(Config).ConfigureAwait(false);
        }
        finally
        {
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

    // The assembly xUnit is running: already loaded in the usual case, so it is matched by path before anything is loaded.
    private static Assembly? TestAssemblyOf(string? path) =>
        path is null
            ? Assembly.GetEntryAssembly()
            : Array.Find(AppDomain.CurrentDomain.GetAssemblies(), assembly => !assembly.IsDynamic && string.Equals(assembly.Location, path, StringComparison.OrdinalIgnoreCase))
                ?? Assembly.LoadFrom(path);
}
