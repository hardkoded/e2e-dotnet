// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using E2E.Mobile;
using E2E.NUnit;
using MobileBenchmark.E2ETests.Fixtures;

namespace MobileBenchmark.E2ETests.DeviceFixture;

/// <summary>
/// Upstream's <c>test.describe('device fixture')</c> of <c>device-fixture.e2e.ts</c>: the <c>device</c> methods no scenario
/// flow needs, each with something the app shows or the fixture reads back. The Control Inventory screen prints the color
/// scheme and the orientation, the home list is where a relaunch lands, and <c>ForegroundAppAsync</c> says which app is in front.
/// </summary>
public sealed class DeviceFixtureTests : BenchmarkTest
{
    /// <summary>An app id no device has.</summary>
    private const string MissingId = "dev.e2e.benchmark.missing";

    [SetUp]
    public async Task OpenTheInventory() => await OpenScenarioAsync("Control Inventory");

    // Only the iOS build follows the system appearance (UIUserInterfaceStyle); the Android build stays light.
    [Test]
    public async Task SetAppearance_flips_the_color_scheme_the_app_reads()
    {
        Assume.That(Platform, Is.EqualTo(MobilePlatform.Ios), "only the iOS build follows the system appearance");
        var scheme = Screen.GetByTestId("color-scheme");
        await Expect.That(scheme).ToHaveTextAsync("scheme: light");
        try
        {
            await Device.SetAppearanceAsync(Appearance.Dark);
            await Expect.That(scheme).ToHaveTextAsync("scheme: dark");
        }
        finally
        {
            await Device.SetAppearanceAsync(Appearance.Light);
        }

        await Expect.That(scheme).ToHaveTextAsync("scheme: light");
    }

    [Test]
    public async Task SetOrientation_rotates_the_window_and_viewport_dimensions()
    {
        var orientation = Screen.GetByTestId("orientation");
        await Expect.That(orientation).ToHaveTextAsync("orientation: portrait");
        var portrait = await Device.ViewportAsync();
        Assert.That(double.IsFinite(portrait.Width), Is.True);
        Assert.That(double.IsFinite(portrait.Height), Is.True);
        Assert.That(portrait.Width, Is.GreaterThan(0));
        Assert.That(portrait.Height, Is.GreaterThan(portrait.Width));
        try
        {
            await Device.SetOrientationAsync(DeviceOrientation.LandscapeLeft);
            await Expect.That(orientation).ToHaveTextAsync("orientation: landscape");
            Assert.That(await Device.ViewportAsync(), Is.EqualTo(new ViewportSize(portrait.Height, portrait.Width)));
        }
        finally
        {
            await Device.SetOrientationAsync(DeviceOrientation.Portrait);
        }

        await Expect.That(orientation).ToHaveTextAsync("orientation: portrait");
        Assert.That(await Device.ViewportAsync(), Is.EqualTo(portrait));
    }

    // Android 14 and later ship no shell command for the clipboard service, so agent-device refuses both calls on the
    // emulator as unsupported. The text is new each run: a write that does nothing reads back the last run's text, not an error.
    [Test]
    public async Task The_clipboard_reads_back_what_was_written()
    {
        Assume.That(Platform, Is.EqualTo(MobilePlatform.Ios), "Android 14 and later have no clipboard shell command");
        var text = $"e2e clipboard {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        await Device.SetClipboardAsync(text);
        Assert.That(await Device.ClipboardAsync(), Is.EqualTo(text));
    }

    [Test]
    public async Task Home_leaves_the_app_openApp_brings_it_back_where_it_was()
    {
        Assert.That((await Device.ForegroundAppAsync()).BundleId, Is.EqualTo(AppId));
        await Device.HomeAsync();
        await Device.OpenAppAsync(AppId);
        Assert.That((await Device.ForegroundAppAsync()).BundleId, Is.EqualTo(AppId));
        await Expect.That(Screen.GetByTestId("inventory-header")).ToBeVisibleAsync();
    }

    // agent-device's iOS appstate answers from the session and refuses a device selector without one, so on iOS
    // HomeAsync leaves ForegroundAppAsync naming the pinned app; Android reads the device's foreground activity.
    [Test]
    public async Task ForegroundApp_sees_the_home_screen_after_home()
    {
        Assume.That(Platform, Is.EqualTo(MobilePlatform.Android), "iOS answers from the session");
        await Device.HomeAsync();
        Assert.That((await Device.ForegroundAppAsync()).BundleId, Is.Not.EqualTo(AppId));
    }

    // The close names the app, so agent-device terminates it before ending the session; a bare close would leave it
    // running and openApp would resume it here.
    [Test]
    public async Task CloseApp_then_openApp_relaunches_on_the_home_list()
    {
        await Device.CloseAppAsync();
        await Device.OpenAppAsync(AppId);
        Assert.That((await Device.ForegroundAppAsync()).BundleId, Is.EqualTo(AppId));
        await Expect.That(Screen.GetByTestId("Login Form")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByTestId("inventory-header")).ToBeHiddenAsync();
    }

    // CloseApp ends agent-device's session, so what follows reaches this worker's device only by naming it. CI boots two
    // iOS simulators, where a command that named none failed with "2 devices match this request equally".
    [Test]
    public async Task CloseApp_leaves_the_next_commands_on_the_same_device()
    {
        await Device.CloseAppAsync();
        if (Platform == MobilePlatform.Ios)
        {
            await Device.ClearKeychainAsync();
        }

        await App.ClearStateAsync();
        Assert.That((await Device.ForegroundAppAsync()).BundleId, Is.EqualTo(AppId));
        await Expect.That(Screen.GetByTestId("Login Form")).ToBeVisibleAsync();
    }

    // The reference promises APP_NOT_OPEN for a lost session.
    [Test]
    public async Task ForegroundApp_after_closeApp_reports_the_closed_session_as_APP_NOT_OPEN()
    {
        await Device.CloseAppAsync();
        var lost = await Assert.ThrowsAsync<TestException>(async () => await Device.ForegroundAppAsync());
        Assert.That(lost?.Code, Is.EqualTo("APP_NOT_OPEN"));
    }

    // The app routes e2e-benchmark://<scenario-slug> to that scenario (App.OnAppLinkRequestReceived), so the link lands on
    // Login Form's own fields and Control Inventory is no longer on screen. The iOS simulator can stop a custom-scheme
    // link at an "Open in ...?" prompt, so the prompt is answered only while it shows.
    [Test]
    public async Task OpenLink_opens_the_scenario_a_deep_link_names()
    {
        await Device.OpenLinkAsync("e2e-benchmark://login-form");
        var prompt = Screen.GetByRole("button", "Open");
        var email = Screen.GetByTestId("email-input");
        await Expect
            .Poll(
                async () =>
                {
                    if (await prompt.IsVisibleAsync())
                    {
                        await prompt.TapAsync();
                    }

                    return await email.IsVisibleAsync();
                },
                new PollOptions { Timeout = TimeSpan.FromSeconds(10) }
            )
            .ToBeAsync(true);
        Assert.That((await Device.ForegroundAppAsync()).BundleId, Is.EqualTo(AppId));
        await Expect.That(Screen.GetByTestId("inventory-header")).ToBeHiddenAsync();
    }

    [Test]
    public async Task IsAppInstalled_finds_the_app_under_test_and_no_app_that_is_absent()
    {
        Assert.That(await Device.IsAppInstalledAsync(), Is.True);
        Assert.That(await Device.IsAppInstalledAsync(AppId), Is.True);
        Assert.That(await Device.IsAppInstalledAsync(MissingId), Is.False);
        Assert.That(await Device.UninstallAppAsync(MissingId), Is.False);
    }

    // Removing the app under test needs its build to put it back, which only a run that names one has (CI always does);
    // the next test opens it fresh.
    [Test]
    public async Task UninstallApp_removes_the_app_until_installApp_puts_it_back()
    {
        Assume.That(AppPath, Is.Not.Null, "no build to reinstall the app from");
        // The reinstall runs whatever fails, so the tests after this one still find the app.
        try
        {
            Assert.That(await Device.UninstallAppAsync(), Is.True);
            Assert.That(await Device.IsAppInstalledAsync(), Is.False);
        }
        finally
        {
            await Device.InstallAppAsync();
        }

        Assert.That(await Device.IsAppInstalledAsync(), Is.True);
        await Device.OpenAppAsync(AppId, new OpenAppOptions { Relaunch = true });
        Assert.That((await Device.ForegroundAppAsync()).BundleId, Is.EqualTo(AppId));
        await Expect.That(Screen.GetByTestId("Login Form")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Device_back_pops_the_scenario()
    {
        await Device.BackAsync();
        await Expect.That(Screen.GetByTestId("Control Inventory")).ToBeVisibleAsync();
        await Expect.That(Screen.GetByTestId("inventory-header")).ToBeHiddenAsync();
    }

    // The client is bound to this worker's session, so a raw call reads the screen this test is on, on the device the
    // engine booted.
    [Test]
    public async Task UnstableRaw_reaches_the_test_session_through_the_raw_driver()
    {
        var raw = Device.UnstableRaw;
        Assert.That(Platform, Is.EqualTo(raw.Platform));
        var sessions = (JsonArray)(await raw.Client.CallAsync(DeviceMethods.SessionsList, new JsonObject(), default))!;
        var own = sessions.OfType<JsonObject>().FirstOrDefault(entry => (string?)entry["name"] == raw.Session);
        Assert.That(own, Is.Not.Null);
        Assert.That((string?)own!["device"]?["id"], Is.EqualTo(raw.DeviceId));
        Assert.That((string?)own["device"]?["name"], Is.EqualTo(raw.DeviceName));
        var snapshot = await raw.Client.CallAsync(DeviceMethods.CaptureSnapshot, new JsonObject(), default);
        var nodes = (snapshot?["nodes"] as JsonArray) ?? [];
        Assert.That(nodes.OfType<JsonObject>().Any(node => (string?)node["identifier"] == "inventory-header"), Is.True);
    }

    // The Device section prints what the device tells the app: the network state, a position read on demand, and the
    // outcome of a biometric prompt.

    // Android only: the engine refuses to take an iOS device offline (UNSUPPORTED_CAPABILITY), since agent-device's iOS
    // network settings only paint the simulator's status bar and the app keeps its connection. SetNetwork switches Wi-Fi
    // alone, and the emulator keeps a cellular link, so Wi-Fi off hands the app over to cellular; only airplane mode cuts
    // both. A radio that comes back re-associates and validates before Android makes it the default network again.
    [Test]
    public async Task SetNetwork_and_setAirplaneMode_change_what_the_app_sees()
    {
        Assume.That(Platform, Is.EqualTo(MobilePlatform.Android), "the engine refuses to take an iOS device offline");
        var reconnect = TimeSpan.FromSeconds(30);
        await Screen.GetByTestId("tab-device").TapAsync();
        var network = Screen.GetByTestId("network-state");
        var type = Screen.GetByTestId("network-type");
        await Expect.That(network).ToHaveTextAsync("network: connected");
        await Expect.That(type).ToHaveTextAsync("network type: wifi", timeout: reconnect);
        try
        {
            await Device.SetNetworkAsync(NetworkState.Offline);
            await Expect.That(type).ToHaveTextAsync(new Regex("^network type: (cellular|none)$"));
            await Device.SetNetworkAsync(NetworkState.Online);
            await Expect.That(type).ToHaveTextAsync("network type: wifi", timeout: reconnect);
            await Device.SetAirplaneModeAsync(true);
            await Expect.That(network).ToHaveTextAsync("network: disconnected");
        }
        finally
        {
            await Device.SetAirplaneModeAsync(false);
            await Device.SetNetworkAsync(NetworkState.Online);
        }

        await Expect.That(type).ToHaveTextAsync("network type: wifi", timeout: reconnect);
    }

    // A read returns the provider's last fix, which lags a new one by a moment, so each read is repeated until the app
    // prints the position that was set. On Android ClearLocation switches location services off and the emulator keeps
    // that across runs; SetLocation switches them back on first, and the first fix after that takes a while to produce.
    [Test]
    public async Task SetLocation_and_clearLocation_change_what_the_app_reads()
    {
        await Screen.GetByTestId("tab-device").TapAsync();
        // Granted up front, so the read never waits on the system's permission dialog.
        await Device.SetPermissionAsync(DevicePermission.Location, PermissionState.Grant);
        var read = Screen.GetByTestId("read-location");
        var status = Screen.GetByTestId("location-status");
        // Switching device location off can make Play services put its "No location access" warning (Find My Device)
        // over the app, now and then; it hides the read button until it is closed.
        var noAccess = Screen.GetByRole("button", "Close");

        // A read in flight is left to finish: the poll only looks, and asks for another read once the last one has
        // answered. An assertion inside the poll would hold it for the assertion's own budget instead.
        async Task ReadsBackAsync(Regex expected) =>
            await Expect
                .Poll(
                    async () =>
                    {
                        if (await noAccess.IsVisibleAsync())
                        {
                            await noAccess.TapAsync();
                        }

                        var text = (await status.AllTextContentsAsync()).FirstOrDefault() ?? "";
                        if (text != "location: reading" && !expected.IsMatch(text))
                        {
                            await read.TapAsync();
                        }

                        return text;
                    },
                    new PollOptions { Timeout = TimeSpan.FromSeconds(60) }
                )
                .ToMatchAsync(Does.Match(expected.ToString()));

        try
        {
            await Device.SetLocationAsync(new Coordinates(52.2297, 21.0122));
            await ReadsBackAsync(new Regex(@"^location: 52\.2297, 21\.0122$"));
            await Device.SetLocationAsync(new Coordinates(37.7749, -122.4194));
            await ReadsBackAsync(new Regex(@"^location: 37\.7749, -122\.4194$"));
        }
        finally
        {
            await Device.ClearLocationAsync();
        }

        // Android has no provider left and errors; iOS clears the simulated fix together with the app's authorization.
        await ReadsBackAsync(new Regex("^location: (unavailable|permission denied)"));
    }

    // The emulator's fingerprint answers only once a lock screen and a fingerprint are enrolled by hand, so only the
    // simulator's Face ID runs. The status reads "authenticating" before the system sheet listens, and a match posted to
    // no sheet is dropped, so the match repeats until one lands.
    [Test]
    public async Task EnrollBiometrics_and_setBiometrics_answer_a_biometric_prompt()
    {
        Assume.That(Platform, Is.EqualTo(MobilePlatform.Ios), "only the simulator's Face ID runs");
        await Screen.GetByTestId("tab-device").TapAsync();
        var status = Screen.GetByTestId("biometrics-status");
        await Device.EnrollBiometricsAsync(BiometricSensor.FaceId, true);
        try
        {
            await Screen.GetByTestId("unlock-biometrics").TapAsync();
            await Expect.That(status).ToHaveTextAsync("biometrics: authenticating");
            await Expect
                .Poll(
                    async () =>
                    {
                        await Device.SetBiometricsAsync(BiometricSensor.FaceId, BiometricResult.Match);
                        return await status.TextContentAsync();
                    },
                    new PollOptions { Timeout = TimeSpan.FromSeconds(10) }
                )
                .ToBeAsync("biometrics: unlocked");
        }
        finally
        {
            await Device.EnrollBiometricsAsync(BiometricSensor.FaceId, false);
        }
    }

    // A volume press brings up Android's volume panel over the app, and the tree reads its sliders. The second press
    // lowers the level again (one step below where it started if it started at the top); no test reads it. The panel hides
    // itself a few seconds later, before the next test taps.
    [Test]
    public async Task PressButton_shows_the_volume_panel_on_Android()
    {
        Assume.That(Platform, Is.EqualTo(MobilePlatform.Android), "only Android has a volume panel");
        var media = Screen.GetByRole("slider", "Media");
        await Device.PressButtonAsync(HardwareButton.VolumeUp);
        await Expect.That(media).ToBeVisibleAsync();
        await Device.PressButtonAsync(HardwareButton.VolumeDown);
        await Expect.That(media).ToBeHiddenAsync(TimeSpan.FromSeconds(10));
    }

    // XCUIDevice has no volume press on a simulator, so agent-device refuses before it sends anything.
    [Test]
    public async Task PressButton_is_refused_on_an_iOS_simulator()
    {
        Assume.That(Platform, Is.EqualTo(MobilePlatform.Ios), "only an iOS simulator refuses the press");
        var refused = await Assert.ThrowsAsync<TestException>(async () =>
            await Device.PressButtonAsync(HardwareButton.VolumeUp)
        );
        Assert.That(refused?.Code, Is.EqualTo("UNSUPPORTED_CAPABILITY"));
    }
}
