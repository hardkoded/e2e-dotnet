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
    /// Runs the reporters that the assembly registers with <c>[assembly: E2EReporter(typeof(...))]</c> once <c>report.json</c> is final, then stops
    /// the app commands that the tests of this process started, and deletes the sessions that setup tests saved (see <c>E2ESetupAttribute</c>).
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
