// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace MobileBenchmark.E2ETests.Fixtures;

/// <summary>
/// The suite's base class, the port of upstream's <c>tests/fixtures.ts</c>: <see cref="MobileAppTest"/> plus
/// <see cref="OpenScenarioAsync"/>. <see cref="MobileAppTest"/> installs the build once per run, which is upstream's
/// <c>build</c> fixture, and launches the app fresh before each test, which is upstream's <c>app.open()</c>.
/// </summary>
public abstract class BenchmarkTest : MobileAppTest
{
    /// <summary>
    /// Opens one scenario from the home list. A row is found by its label, "&lt;name&gt;. " and the description. A scenario is
    /// up once its row has left the tree and its own header shows the route name. The tap gets one more try when the
    /// list swallowed it, after a back when the row is already gone.
    /// </summary>
    protected async Task OpenScenarioAsync(string name)
    {
        var row = Screen.GetByLabel($"{name}. ", exact: false);
        await Expect.That(row).ToBeVisibleAsync();
        var shown = Screen.GetByText(name);
        await row.TapAsync();
        try
        {
            await Expect.That(row).ToBeHiddenAsync(TimeSpan.FromSeconds(10));
            await Expect.That(shown).ToBeVisibleAsync(timeout: TimeSpan.FromSeconds(10));
        }
        catch (TestException)
        {
            if (await row.IsHiddenAsync())
            {
                await Device.BackAsync();
                await Expect.That(row).ToBeVisibleAsync();
            }

            await row.TapAsync();
            await Expect.That(row).ToBeHiddenAsync(TimeSpan.FromSeconds(10));
            await Expect.That(shown).ToBeVisibleAsync(timeout: TimeSpan.FromSeconds(10));
        }
    }
}
