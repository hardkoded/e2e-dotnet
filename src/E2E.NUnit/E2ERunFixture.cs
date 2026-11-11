// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;

namespace E2E.NUnit;

/// <summary>
/// A .NET-only run-level hook (upstream's runner stops the app commands and calls the reporters when the run ends; NUnit has no such owner). Derive a
/// <c>[SetUpFixture]</c> from it, outside any namespace so it covers the whole assembly, and the app commands that the targets declare stop
/// when the last test of the assembly is done, not when the test process exits, and the reporters run (see <c>E2E.Reports.Reporter</c>):
/// <code>
/// [SetUpFixture]
/// public sealed class AppCommands : E2ERunFixture;
/// </code>
/// </summary>
public abstract class E2ERunFixture
{
    /// <summary>
    /// The test filters, in order. They run only when <see cref="ChangedSince"/> or <c>E2E_CHANGED_SINCE</c> names a base ref; each judges
    /// the tests and the files changed since that ref, and a test it leaves out is ignored with its reason. Empty by default.
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

    /// <summary>
    /// Builds the reporters of <see cref="Config"/> and starts the run's events, then finds the tests of the assembly by reflection and runs the filters over them
    /// once, before the first test. The tests the filters left out skip themselves at their start. A run with no base ref runs every test.
    /// </summary>
    [OneTimeSetUp]
    public async Task SelectTestsAsync()
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
            var discovered = HostRun.Collect(() => RunSelection.Discover(GetType().Assembly));
            IReadOnlyDictionary<string, string> excluded = new Dictionary<string, string>(StringComparer.Ordinal);
            var changedSince = HostTestFilters.ResolveChangedSince(ChangedSince);
            var lastFailed = HostTestFilters.ResolveLastFailed(LastFailed);
            if (changedSince is not null || lastFailed)
            {
                var selection = await HostTestFilters.ExcludeAsync(
                    config,
                    Filters,
                    changedSince,
                    discovered,
                    line => global::NUnit.Framework.TestContext.Progress.WriteLine(line),
                    global::NUnit.Framework.TestContext.CurrentContext.CancellationToken,
                    lastFailed).ConfigureAwait(false);
                RunSelection.Add(selection.Excluded);
                HostRun.RerunDir = selection.RerunDir;
                excluded = selection.Excluded;
                await HostTestFilters.WriteReportAsync(config, selection, global::NUnit.Framework.TestContext.CurrentContext.CancellationToken).ConfigureAwait(false);
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
    /// Finishes the engines the run prepared, ends the run's events and runs the reporters that <see cref="Config"/> lists in <c>reporters</c> once <c>report.json</c> is final, then stops
    /// the app commands that the tests of this process started, and deletes the sessions that setup tests saved (see <c>E2ESetupAttribute</c>).
    /// </summary>
    [OneTimeTearDown]
    public async Task StopAppCommandsAsync()
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
