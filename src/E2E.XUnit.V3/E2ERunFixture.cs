// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.XUnit.V3;

/// <summary>
/// A .NET-only run-level hook (upstream's runner stops the app commands when the run ends; xUnit has no such owner). Register it as an assembly
/// fixture, and the app commands that the targets declare stop when the last test of the assembly is done, not when the test process exits:
/// <code>
/// [assembly: AssemblyFixture(typeof(E2E.XUnit.V3.E2ERunFixture))]
/// </code>
/// </summary>
public sealed class E2ERunFixture : IAsyncDisposable
{
    /// <summary>Stops the app commands that the tests of this process started.</summary>
    public async ValueTask DisposeAsync() => await RunApps.StopAllAsync().ConfigureAwait(false);
}
