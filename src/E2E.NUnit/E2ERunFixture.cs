// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;

namespace E2E.NUnit;

/// <summary>
/// A .NET-only run-level hook (upstream's runner stops the app commands when the run ends; NUnit has no such owner). Derive a
/// <c>[SetUpFixture]</c> from it, outside any namespace so it covers the whole assembly, and the app commands that the targets declare stop
/// when the last test of the assembly is done, not when the test process exits:
/// <code>
/// [SetUpFixture]
/// public sealed class AppCommands : E2ERunFixture;
/// </code>
/// </summary>
public abstract class E2ERunFixture
{
    /// <summary>Stops the app commands that the tests of this process started.</summary>
    [OneTimeTearDown]
    public async Task StopAppCommandsAsync() => await RunApps.StopAllAsync().ConfigureAwait(false);
}
