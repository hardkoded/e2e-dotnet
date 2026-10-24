// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WinAppBenchmark.E2ETests.Fixtures;

namespace WinAppBenchmark.E2ETests.Smoke;

public sealed class SmokeTests : BenchmarkTest
{
    // The home list is the app's first screen; each row's test id is the scenario name and its label joins
    // the name and the description.
    [Test]
    [Category("smoke")]
    public async Task Home_lists_the_scenarios_and_opens_one()
    {
        await App.OpenAsync();
        await Expect.That(Screen.GetByTestId("Login Form")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByTestId("Modal Flow")).ToBeVisibleAsync();

        await OpenScenarioAsync("Login Form");
        await Expect.That(Screen.GetByTestId("login-button")).ToBeVisibleAsync();

        // A desktop app has no history the engine can see (App.BackAsync is UNSUPPORTED_CAPABILITY): the app's own Back button.
        await Screen.GetByTestId("back").TapAsync();
        await Expect.That(Screen.GetByTestId("Login Form")).ToBeVisibleAsync();
    }
}
