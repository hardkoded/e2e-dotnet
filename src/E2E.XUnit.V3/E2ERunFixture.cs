// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace E2E.XUnit.V3;

/// <summary>
/// A .NET-only run-level hook (upstream's runner stops the app commands and calls the reporters when the run ends; xUnit has no such owner). Register it as an assembly
/// fixture, and the app commands that the targets declare stop when the last test of the assembly is done, not when the test process exits, and the reporters run (see <c>E2E.Reports.Reporter</c>):
/// <code>
/// [assembly: AssemblyFixture(typeof(E2E.XUnit.V3.E2ERunFixture))]
/// </code>
/// </summary>
public sealed class E2ERunFixture : IAsyncDisposable
{
    /// <summary>
    /// Runs the reporters that the test assembly registers with <c>[assembly: E2EReporter(typeof(...))]</c> once <c>report.json</c> is final, then
    /// stops the app commands that the tests of this process started. The test assembly is the entry assembly: an xUnit v3 test project is an executable.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (Assembly.GetEntryAssembly() is { } testAssembly)
            {
                await Reporters.RunFinishedAsync(testAssembly).ConfigureAwait(false);
            }
            else
            {
                Console.Error.WriteLine("e2e: the test assembly is unknown (no entry assembly), so no reporter ran");
            }
        }
        finally
        {
            await RunApps.StopAllAsync().ConfigureAwait(false);
        }
    }
}
