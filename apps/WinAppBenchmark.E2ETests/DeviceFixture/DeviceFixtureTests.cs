// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using WinAppBenchmark.E2ETests.Fixtures;

namespace WinAppBenchmark.E2ETests.DeviceFixture;

/// <summary>
/// The <c>Window</c> fixture methods no scenario flow needs, each with something the app shows or the fixture
/// reads back: the Control Inventory screen prints the color scheme and the orientation, the home list is where
/// a relaunch lands, and <c>ForegroundAppAsync</c> says which app is in front.
/// </summary>
public sealed class DeviceFixtureTests : BenchmarkTest
{
    private const string AppName = "WinAppBenchmark";

    [SetUp]
    public async Task OpenTheInventory() => await OpenScenarioAsync("Control Inventory");

    // The window's shape is the orientation a desktop has: a wider window reads landscape.
    [Test]
    public async Task SetOrientation_rotates_the_window_the_app_measures()
    {
        var orientation = Screen.GetByTestId("orientation");
        await Expect.That(orientation).ToHaveTextAsync("orientation: portrait");
        try
        {
            await Window.ResizeAsync(900, 500);
            await Expect.That(orientation).ToHaveTextAsync("orientation: landscape");
        }
        finally
        {
            await Window.ResizeAsync(480, 900);
        }

        await Expect.That(orientation).ToHaveTextAsync("orientation: portrait");
    }

    [Test]
    public async Task Home_leaves_the_app_openApp_brings_it_back_where_it_was()
    {
        Assert.That((await Window.ForegroundAppAsync()).Name, Is.EqualTo(AppName));
        await Window.MinimizeAsync();
        await Window.OpenAppAsync();
        Assert.That((await Window.ForegroundAppAsync()).Name, Is.EqualTo(AppName));
        await Expect.That(Screen.GetByTestId("inventory-header")).ToBeVisibleAsync();
    }

    // The close ends the process the engine started, so openApp launches it fresh on the home list.
    [Test]
    public async Task CloseApp_then_openApp_relaunches_on_the_home_list()
    {
        await Window.CloseAppAsync();
        await Window.OpenAppAsync();
        Assert.That((await Window.ForegroundAppAsync()).Name, Is.EqualTo(AppName));
        await Expect.That(Screen.GetByTestId("Login Form")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByTestId("inventory-header")).ToBeHiddenAsync();
    }

    [Test]
    public async Task CloseApp_leaves_the_next_commands_on_the_same_window_session()
    {
        await Window.CloseAppAsync();
        await App.ClearStateAsync();
        Assert.That((await Window.ForegroundAppAsync()).Name, Is.EqualTo(AppName));
        await Expect.That(Screen.GetByTestId("Login Form")).ToBeVisibleAsync();
    }

    // The app's own Back button pops the scenario: App.BackAsync is UNSUPPORTED_CAPABILITY on a desktop app.
    [Test]
    public async Task Back_pops_the_scenario()
    {
        await Screen.GetByTestId("back").TapAsync();
        await Expect.That(Screen.GetByTestId("Control Inventory")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByTestId("inventory-header")).ToBeHiddenAsync();
    }

    // Not ported from upstream's `describe('device fixture')`:
    //  - 'setAppearance flips the color scheme the app reads' (ios only): a window has no appearance fixture; the app follows the system theme.
    //  - 'the clipboard reads back what was written' (ios only): Window has no clipboard.
    //  - 'foregroundApp sees the home screen after home()' (android only): Window has no home.
    //  - 'foregroundApp after closeApp reports the closed session as APP_NOT_OPEN': ForegroundAppAsync after CloseAppAsync reads no app.
    //  - 'setNetwork and setAirplaneMode change what the app sees', 'setLocation and clearLocation change what the app reads', and the biometrics tests: the Windows engine has no such fixture, and the Device tab omits those lines.
}
