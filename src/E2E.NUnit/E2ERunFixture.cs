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

    /// <summary>The config the filters read: the discovered <c>e2e.config.json</c>, or the defaults when there is none.</summary>
    protected virtual E2EConfig Config => E2EConfig.Discover();

    /// <summary>
    /// Finds the tests of the assembly by reflection and runs the filters over them once, before the first test. The tests the filters left
    /// out skip themselves at their start. A run with no base ref runs every test.
    /// </summary>
    [OneTimeSetUp]
    public async Task SelectTestsAsync()
    {
        RunSelection.Set(new Dictionary<string, string>(StringComparer.Ordinal));
        if (HostTestFilters.ResolveChangedSince(ChangedSince) is not { } changedSince)
        {
            return;
        }

        var excluded = await HostTestFilters.ExcludeAsync(
            Config,
            Filters,
            changedSince,
            RunSelection.Discover(GetType().Assembly),
            line => global::NUnit.Framework.TestContext.Progress.WriteLine(line),
            global::NUnit.Framework.TestContext.CurrentContext.CancellationToken).ConfigureAwait(false);
        RunSelection.Set(excluded);
    }

    /// <summary>
    /// Runs the reporters that the assembly registers with <c>[assembly: E2EReporter(typeof(...))]</c> once <c>report.json</c> is final, then stops
    /// the app commands that the tests of this process started.
    /// </summary>
    [OneTimeTearDown]
    public async Task StopAppCommandsAsync()
    {
        try
        {
            await Reporters.RunFinishedAsync(GetType().Assembly).ConfigureAwait(false);
        }
        finally
        {
            await RunApps.StopAllAsync().ConfigureAwait(false);
        }
    }
}
