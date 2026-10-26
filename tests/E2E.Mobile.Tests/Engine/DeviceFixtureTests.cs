// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using static E2E.Mobile.Tests.Screens;

namespace E2E.Mobile.Tests.Engine;

public sealed class DeviceFixtureTests
{
    private static Task Tap(DeviceSession session, SemanticNode node) => session.PerformAsync(node, new LocatorAction.Tap(), CancellationToken.None);

    private static Harness Android(string? bundleId = null, bool pinned = true) =>
        Harness.Create(
            options => options with { Platform = MobilePlatform.Android },
            pinned,
            app => bundleId is null ? app : app with { BundleId = bundleId });

    // .NET-only: upstream's engine.test.ts has no `it` for device.locator. The attempt's context reaches the fixture through IEngineFixtureHost,
    // which upstream has as EngineFixtureContext handed to the engine's fixture factory.
    [Fact]
    public async Task Mints_a_locator_from_an_agent_device_selector_on_the_attempts_screen()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        var cold = Assert.Throws<EngineException>(() => session.Device.Locator("id=ABOUT"));
        Assert.Equal("INVALID_STATE", cold.Code);
        var screen = new Screen(
            session.ObserveAsync,
            session.PerformAsync,
            () => CancellationToken.None,
            () => { },
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5),
            locate: session.LocateAsync);
        ((IEngineFixtureHost)session).CreateFixtures(new EngineFixtureContext(screen));

        var about = session.Device.Locator("id=ABOUT").First();
        Assert.Equal(1, await about.CountAsync());
        Assert.Equal(0, await session.Device.Locator("id=NOPE").CountAsync());
        Assert.Equal(1, await session.Device.Locator("id=NOPE || label=\"About\"").First().CountAsync());
        await about.TapAsync();
        JsonAssert.Args("""{"ref":"@e4","settle":true,"settleQuietMs":150}""", h.Fake.LastArgs("interactions.press"));
    }

    [Fact]
    public async Task Counts_a_back_home_alert_keyboard_rotation_or_fold_as_an_action_so_a_control_that_arrives_with_it_waits_out_the_transition_budget()
    {
        var h = Harness.Create(options => options with { TransitionMilliseconds = 120 });
        var session = await OpenAttempt(h);
        session.Origin = ActionOrigin.Test;
        await Observed(session, "Back");
        var device = session.Device;
        Func<Task>[] changes =
        [
            () => device.BackAsync(),
            () => device.HomeAsync(),
            () => device.AlertAsync(AlertAction.Accept),
            () => device.DismissKeyboardAsync(),
            () => device.SetOrientationAsync(DeviceOrientation.LandscapeLeft),
            () => device.FoldAsync(FoldPose.HalfOpen),
        ];
        foreach (var change in changes)
        {
            h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SettingsSnapshot());
            await Observed(session, "Back");
            await change();

            // The next look shows a control that was not there before the change: the screen it revealed.
            h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SettingsSnapshotWith(Submit()));
            var delays = h.Delays.Count;
            await Tap(session, await Observed(session, "Submit"));
            Assert.True(h.Delays.Skip(delays).Sum(delay => delay.TotalMilliseconds) >= 100);
            JsonAssert.Args("""{"ref":"@e11"}""", h.Fake.LastArgs("interactions.press"));
        }

        // A read leaves the budget alone: the clipboard changes no screen.
        await device.ClipboardAsync();
        var read = h.Delays.Count;
        await Tap(session, await Observed(session, "Submit"));
        Assert.Equal(read, h.Delays.Count);
    }

    [Fact]
    public async Task Reads_the_harness_signal_per_call_so_teardown_after_a_body_timeout_still_drives_the_device()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        var device = session.Device;

        // The body's signal is dead...
        using var timedOut = new CancellationTokenSource();
        await timedOut.CancelAsync();
        var failure = await Assert.ThrowsAsync<EngineException>(() => device.HomeAsync(timedOut.Token));
        Assert.Equal("CANCELLED", failure.Code);

        // ...and the same fixture instance follows the harness into the afterEach budget.
        await device.HomeAsync(CancellationToken.None);
        Assert.Single(h.Fake.Methods(), "command.home");
    }

    [Fact]
    public async Task Installs_a_build_from_a_test_replacing_by_default_and_removing_first_on_reinstall()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        h.Fake.Respond(DeviceMethods.AppsInstall, _ => new JsonObject { ["app"] = "./b/App.app", ["appPath"] = "/b/App.app", ["platform"] = "ios", ["bundleId"] = "com.example.app" });
        h.Fake.Respond(DeviceMethods.AppsReinstall, args => new JsonObject { ["app"] = args?["app"]?.DeepClone(), ["appPath"] = "/b/App.app", ["platform"] = "ios" });
        var device = session.Device;
        var before = h.Fake.Calls.Count;
        Assert.Equal(new InstalledApp("com.example.app", "com.example.app"), await device.InstallAppAsync("./b/App.app"));
        Assert.Equal(new InstalledApp("Settings"), await device.InstallAppAsync("/b/App.app", new InstallAppOptions { Reinstall = true }));
        Assert.Equal(new InstalledApp("com.other"), await device.InstallAppAsync("/b/App.app", new InstallAppOptions { App = "com.other", Reinstall = true }));
        JsonAssert.Calls(
            h.Fake.Calls,
            before,
            ("apps.install", """{"platform":"ios","appPath":"/project/b/App.app"}"""),
            ("apps.reinstall", """{"platform":"ios","app":"Settings","appPath":"/b/App.app"}"""),
            ("apps.reinstall", """{"platform":"ios","app":"com.other","appPath":"/b/App.app"}"""));

        var free = Harness.Create(pinned: false);
        var freeSession = await OpenAttempt(free);
        var refused = await Assert.ThrowsAsync<EngineException>(() => freeSession.Device.InstallAppAsync("/b/App.app", new InstallAppOptions { Reinstall = true }));
        Assert.Equal("INVALID_STATE", refused.Code);
        Assert.Equal(["devices.boot"], free.Fake.Methods());
    }

    [Fact]
    public async Task Issues_one_settings_or_system_command_per_method()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        h.Fake.Respond(DeviceMethods.CommandAppState, _ => new JsonObject { ["platform"] = "ios", ["appName"] = "Settings", ["appBundleId"] = "com.apple.Preferences" });
        h.Fake.Respond(DeviceMethods.CommandClipboard, args =>
            args?["action"]?.GetValue<string>() == "read" ? new JsonObject { ["action"] = "read", ["text"] = "pasted" } : new JsonObject { ["action"] = "write", ["textLength"] = 1 });
        var device = session.Device;
        var before = h.Fake.Calls.Count;
        await device.SetPermissionAsync(DevicePermission.Camera, PermissionState.Grant);
        await device.SetLocationAsync(new Coordinates(37.3349, -122.009));
        await device.ClearLocationAsync();
        await device.SetAppearanceAsync(Appearance.Dark);
        await device.SetOrientationAsync(DeviceOrientation.LandscapeLeft);
        await device.FoldAsync(FoldPose.HalfOpen);
        await device.SetBiometricsAsync(BiometricSensor.FaceId, BiometricResult.Match);
        await device.SetBiometricsAsync(BiometricSensor.Fingerprint, BiometricResult.NonMatch);
        await device.EnrollBiometricsAsync(BiometricSensor.TouchId, true);
        await device.OpenAppAsync("Reminders", new OpenAppOptions { Relaunch = true });
        await device.CloseAppAsync();
        Assert.Equal(new ForegroundApp("Settings", "com.apple.Preferences"), await device.ForegroundAppAsync());
        await device.HomeAsync();
        await device.BackAsync();
        await device.AlertAsync(AlertAction.Accept);
        await device.DismissKeyboardAsync();
        Assert.Equal("pasted", await device.ClipboardAsync());
        await device.SetClipboardAsync("x");
        JsonAssert.Calls(
            h.Fake.Calls,
            before,
            ("settings.update", """{"setting":"permission","permission":"camera","state":"grant","platform":"ios","app":"Settings"}"""),
            ("settings.update", """{"platform":"ios","setting":"location","state":"set","latitude":37.3349,"longitude":-122.009}"""),
            ("settings.update", """{"platform":"ios","setting":"location","state":"off"}"""),
            ("settings.update", """{"platform":"ios","setting":"appearance","state":"dark"}"""),
            ("command.orientation", """{"platform":"ios","orientation":"landscape-left"}"""),
            ("command.fold", """{"platform":"ios","pose":"half-open"}"""),
            ("settings.update", """{"platform":"ios","setting":"faceid","state":"match"}"""),
            ("settings.update", """{"platform":"ios","setting":"fingerprint","state":"nonmatch"}"""),
            ("settings.update", """{"platform":"ios","setting":"touchid","state":"enroll"}"""),
            ("apps.open", """{"app":"Reminders","platform":"ios","relaunch":true}"""),

            // The close names the app the session observed, so agent-device terminates it before the session ends.
            ("apps.close", """{"platform":"ios","app":"com.apple.Preferences"}"""),
            ("command.appState", "{}"),
            ("command.home", """{"platform":"ios"}"""),
            ("command.back", """{"settle":true,"settleQuietMs":150}"""),
            ("command.alert", """{"platform":"ios","action":"accept"}"""),
            ("command.keyboard", """{"action":"dismiss"}"""),
            ("command.clipboard", """{"platform":"ios","action":"read"}"""),
            ("command.clipboard", """{"platform":"ios","action":"write","text":"x"}"""));
    }

    [Fact]
    public async Task Changes_a_permission_on_the_pinned_app_by_name_and_on_the_workers_device_with_no_open_first_whatever_the_session_is_on()
    {
        var h = Harness.Create(options => options with { Device = "iPhone 16e" });
        await h.Engine.PrepareAsync(h.PrepareInfo(1), CancellationToken.None);
        var session = await h.Boot("ios", 0);
        var device = session.Device;
        var before = h.Fake.Calls.Count;
        await device.SetPermissionAsync(DevicePermission.Microphone, PermissionState.Reset);
        await device.OpenAppAsync("com.other", new OpenAppOptions { Relaunch = true });
        await device.CloseAppAsync();
        await device.SetPermissionAsync(DevicePermission.Camera, PermissionState.Grant);
        JsonAssert.Calls(
            h.Fake.Calls,
            before,
            ("settings.update", """{"setting":"permission","permission":"microphone","state":"reset","platform":"ios","device":"iPhone 16e","app":"Settings"}"""),
            ("apps.open", """{"app":"com.other","platform":"ios","device":"iPhone 16e","relaunch":true}"""),
            ("apps.close", """{"platform":"ios","device":"iPhone 16e","app":"com.apple.Preferences"}"""),
            ("settings.update", """{"setting":"permission","permission":"camera","state":"grant","platform":"ios","device":"iPhone 16e","app":"Settings"}"""));
    }

    [Fact]
    public async Task Leaves_the_app_to_agent_devices_session_when_the_target_pins_none()
    {
        var h = Harness.Create(pinned: false);
        var session = await OpenAttempt(h);
        var before = h.Fake.Calls.Count;
        await session.Device.SetPermissionAsync(DevicePermission.Camera, PermissionState.Deny);
        JsonAssert.Calls(h.Fake.Calls, before, ("settings.update", """{"setting":"permission","permission":"camera","state":"deny","platform":"ios"}"""));
    }

    [Fact]
    public async Task Switches_Android_location_services_on_before_a_fix_since_clearLocation_leaves_them_off_and_reads_the_foreground_package()
    {
        var h = Android("com.android.settings");
        var session = await OpenAttempt(h);
        h.Fake.Respond(DeviceMethods.CommandAppState, _ => new JsonObject { ["platform"] = "android", ["package"] = "com.android.settings", ["activity"] = ".Main" });
        Assert.Equal(new ForegroundApp("com.android.settings", "com.android.settings"), await session.Device.ForegroundAppAsync());
        var before = h.Fake.Calls.Count;
        var device = session.Device;
        await device.SetLocationAsync(new Coordinates(52.2297, 21.0122));
        await device.ClearLocationAsync();
        JsonAssert.Calls(
            h.Fake.Calls,
            before,
            ("settings.update", """{"platform":"android","setting":"location","state":"on"}"""),
            ("settings.update", """{"platform":"android","setting":"location","state":"set","latitude":52.2297,"longitude":21.0122}"""),
            ("settings.update", """{"platform":"android","setting":"location","state":"off"}"""));
    }

    [Fact]
    public async Task Opens_a_link_into_the_pinned_app_or_a_named_one_and_the_location_follows_the_app_it_landed_in()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        h.Fake.Respond(DeviceMethods.AppsOpen, _ => new JsonObject { ["session"] = "s", ["appName"] = "Benchmark", ["appBundleId"] = "dev.e2e.benchmark" });
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.NodesOnly(RawNodes.SettingsNodes));
        var device = session.Device;
        var before = h.Fake.Calls.Count;
        await device.OpenLinkAsync("e2e-benchmark://orders/42?token=abc");
        await device.OpenLinkAsync("https://example.com/verify", "com.apple.mobilesafari");
        JsonAssert.Calls(
            h.Fake.Calls,
            before,
            ("apps.open", """{"platform":"ios","app":"Settings","url":"e2e-benchmark://orders/42?token=abc"}"""),
            ("apps.open", """{"platform":"ios","app":"com.apple.mobilesafari","url":"https://example.com/verify"}"""));
        Assert.Equal("dev.e2e.benchmark / General", (await session.ObserveAsync(CancellationToken.None)).Route);
    }

    [Fact]
    public async Task Lets_Android_route_a_link_without_an_app_and_needs_one_on_iOS_where_an_unbound_open_leaves_no_session()
    {
        var android = Android(pinned: false);
        var androidSession = await OpenAttempt(android);
        android.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.NodesOnly(RawNodes.SettingsNodes));
        android.Fake.Respond(DeviceMethods.AppsOpen, _ => new JsonObject { ["session"] = "s", ["appName"] = "myapp://orders/42" });
        await androidSession.Device.OpenLinkAsync("myapp://orders/42");
        JsonAssert.Args("""{"platform":"android","app":"myapp://orders/42"}""", android.Fake.LastArgs("apps.open"));
        Assert.Equal("General", (await androidSession.ObserveAsync(CancellationToken.None)).Route);
        android.Fake.Respond(DeviceMethods.AppsOpen, _ => new JsonObject { ["session"] = "s", ["appName"] = "https://example.com", ["appBundleId"] = "com.android.chrome" });
        await androidSession.Device.OpenLinkAsync("https://example.com");
        Assert.Equal("com.android.chrome / General", (await androidSession.ObserveAsync(CancellationToken.None)).Route);

        var ios = Harness.Create(pinned: false);
        var iosSession = await OpenAttempt(ios);
        foreach (var url in new[] { "https://example.com/verify", "myapp://orders/42" })
        {
            var refused = await Assert.ThrowsAsync<TestException>(() => iosSession.Device.OpenLinkAsync(url));
            Assert.Equal("INVALID_ARGUMENT", refused.Code);
        }

        Assert.Equal(["devices.boot"], ios.Fake.Methods());
    }

    [Fact]
    public async Task Refuses_forbidden_schemes_anything_but_an_absolute_URL_to_openLink_and_any_link_to_openApp_before_any_device_command_openApp_still_opens_an_app_by_id()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        var device = session.Device;
        var before = h.Fake.Calls.Count;
        foreach (var denied in new[] { "file:///etc/passwd", "data:text/html,hi", "javascript:alert(1)", "view-source:file:///etc/passwd" })
        {
            Assert.Equal("POLICY_DENIED", (await Assert.ThrowsAsync<ConfigurationException>(() => device.OpenLinkAsync(denied))).Code);
            Assert.Equal("POLICY_DENIED", (await Assert.ThrowsAsync<ConfigurationException>(() => device.OpenAppAsync(denied))).Code);
        }

        foreach (var malformed in new[] { "orders/42", "", "https://" })
        {
            Assert.Equal("INVALID_ARGUMENT", (await Assert.ThrowsAsync<TestException>(() => device.OpenLinkAsync(malformed))).Code);
        }

        foreach (var link in new[] { "https://example.com/verify", "myapp://orders/42" })
        {
            Assert.Equal("INVALID_ARGUMENT", (await Assert.ThrowsAsync<TestException>(() => device.OpenAppAsync(link, new OpenAppOptions { Relaunch = true }))).Code);
        }

        Assert.Equal(before, h.Fake.Calls.Count);
        await device.OpenAppAsync("com.apple.mobilesafari");
        JsonAssert.Calls(h.Fake.Calls, before, ("apps.open", """{"app":"com.apple.mobilesafari","platform":"ios"}"""));
    }

    [Theory]
    [InlineData("ios", "73329AAC-8EB6-4AAD-B33C-8A8E911CD2A6", """{"platform":"ios","udid":"73329AAC-8EB6-4AAD-B33C-8A8E911CD2A6"}""")]
    [InlineData("android", "emulator-5554", """{"platform":"android","serial":"emulator-5554"}""")]
    [InlineData("ios", "iPhone 17 Pro", """{"platform":"ios","device":"iPhone 17 Pro"}""")]
    [InlineData("ios", null, """{"platform":"ios"}""")]
    public async Task Keeps_device_commands_on_the_selected_platform_device_after_closeApp_ends_the_previous_session(string platformName, string? device, string selectionJson)
    {
        var platform = platformName == "android" ? MobilePlatform.Android : MobilePlatform.Ios;
        var selection = JsonNode.Parse(selectionJson)!.AsObject();
        var h = Harness.Create(options => options with { Platform = platform, Device = device }, app: app => app with { BundleId = "com.example.app" });
        await h.Engine.PrepareAsync(h.PrepareInfo(1), CancellationToken.None);
        var session = await OpenAttempt(h);
        h.Fake.Respond(DeviceMethods.CommandAppState, _ => new JsonObject { ["platform"] = platformName, ["appName"] = "Example", ["appBundleId"] = "com.example.app", ["package"] = platform == MobilePlatform.Android ? "com.example.app" : null });
        h.Fake.Respond(DeviceMethods.CommandClipboard, args =>
            args?["action"]?.GetValue<string>() == "read" ? new JsonObject { ["action"] = "read", ["text"] = "pasted" } : new JsonObject { ["action"] = "write", ["textLength"] = 1 });
        var selected = session.Device;
        await selected.HomeAsync();
        JsonAssert.Args(selectionJson, h.Fake.LastArgs("command.home"));

        // foregroundApp reads the open session, so it carries no selection.
        await selected.ForegroundAppAsync();
        JsonAssert.Args("{}", h.Fake.LastArgs("command.appState"));
        await selected.CloseAppAsync();
        Func<Task>[] commands =
        [
            () => selected.SetLocationAsync(new Coordinates(37.3349, -122.009)),
            () => selected.ClearLocationAsync(),
            () => selected.SetAppearanceAsync(Appearance.Dark),
            () => selected.SetOrientationAsync(DeviceOrientation.LandscapeLeft),
            () => selected.FoldAsync(FoldPose.HalfOpen),
            () => selected.SetBiometricsAsync(platform == MobilePlatform.Ios ? BiometricSensor.FaceId : BiometricSensor.Fingerprint, BiometricResult.Match),
            () => selected.AlertAsync(AlertAction.Accept),
            () => selected.ClipboardAsync(),
            () => selected.SetClipboardAsync("x"),
            () => selected.SetPermissionAsync(DevicePermission.Camera, PermissionState.Grant),

            // iOS refuses going offline before any command, so the radios are Android's.
            .. platform == MobilePlatform.Ios
                ? new Func<Task>[] { () => selected.EnrollBiometricsAsync(BiometricSensor.TouchId, true), () => selected.ClearKeychainAsync() }
                : [() => selected.SetNetworkAsync(NetworkState.Offline), () => selected.SetAirplaneModeAsync(true)],
        ];
        foreach (var command in commands)
        {
            // With no session open, the close itself picks the device from the selection.
            await selected.CloseAppAsync();
            AssertSelected(selection, h.Fake.LastArgs("apps.close"));
            var before = h.Fake.Calls.Count;
            await command();
            Assert.True(h.Fake.Calls.Count > before);
            foreach (var call in h.Fake.Calls.Skip(before))
            {
                AssertSelected(selection, call.Args);
            }
        }

        await selected.CloseAppAsync();
        await session.ClearStateAsync(CancellationToken.None);
        var reset = new JsonObject { ["setting"] = "clear-app-state", ["state"] = "clear", ["app"] = "com.example.app" };
        var open = new JsonObject { ["app"] = "com.example.app", ["relaunch"] = true };
        foreach (var (key, value) in selection)
        {
            reset[key] = value!.DeepClone();
            open[key] = value.DeepClone();
        }

        JsonAssert.Args(reset.ToJsonString(), h.Fake.LastArgs("settings.update"));
        JsonAssert.Args(open.ToJsonString(), h.Fake.LastArgs("apps.open"));
    }

    private static void AssertSelected(JsonObject selection, JsonNode? args)
    {
        foreach (var (key, value) in selection)
        {
            Assert.True(JsonNode.DeepEquals(value, args?[key]), $"expected {key} {value} in {args?.ToJsonString() ?? "null"}");
        }
    }

    [Fact]
    public async Task Names_the_pinned_device_on_device_commands_so_the_ones_after_closeApp_still_reach_it_when_other_devices_are_booted_and_leaves_app_commands_to_the_session_PARTIAL_no_video_recording()
    {
        const string udid = "2BBF3F07-AF66-4F95-82AB-BF442506FC89";
        var h = Harness.Create(options => options with { Device = udid });
        await h.Engine.PrepareAsync(h.PrepareInfo(1), CancellationToken.None);
        h.Fake.Respond(DeviceMethods.CommandAppState, _ => new JsonObject { ["platform"] = "ios", ["appName"] = "Settings", ["appBundleId"] = "com.apple.Preferences" });
        var session = await OpenAttempt(h);
        var device = session.Device;
        var before = h.Fake.Calls.Count;

        // closeApp ends agent-device's session, so from here on only the selection says which device is meant.
        await device.CloseAppAsync();
        await device.ClearKeychainAsync();
        await device.SetAppearanceAsync(Appearance.Dark);
        await session.ClearStateAsync(CancellationToken.None);

        // These read the app the session has open: with a selection, Android would answer for the launcher after a closeApp.
        await device.ForegroundAppAsync();
        await session.ObserveAsync(CancellationToken.None);
        await session.DisposeAsync();
        const string selection = "\"platform\":\"ios\",\"udid\":\"" + udid + "\"";
        JsonAssert.Calls(
            h.Fake.Calls,
            before,
            ("apps.close", "{" + selection + ",\"app\":\"com.apple.Preferences\"}"),
            ("settings.update", "{" + selection + ",\"setting\":\"reset-keychain\",\"state\":\"clear\"}"),
            ("settings.update", "{" + selection + ",\"setting\":\"appearance\",\"state\":\"dark\"}"),
            ("settings.update", "{" + selection + ",\"setting\":\"clear-app-state\",\"state\":\"clear\",\"app\":\"Settings\"}"),
            ("apps.open", "{" + selection + ",\"app\":\"Settings\",\"relaunch\":true}"),
            ("command.appState", "{}"),
            ("capture.snapshot", """{"interactiveOnly":false}"""),
            ("sessions.close", "null"));
    }

    [Fact]
    public async Task Presets_the_configured_permissions_and_passes_the_launch_arguments_on_every_fresh_launch_of_the_pinned_app_and_on_no_other_open()
    {
        var h = Harness.Create(
            app: app => app with
            {
                BundleId = "com.example.app",
                LaunchArguments = ["-e2e", "YES"],
                Permissions = new Dictionary<DevicePermission, PermissionState> { [DevicePermission.Camera] = PermissionState.Grant, [DevicePermission.Location] = PermissionState.Deny },
            });
        h.Fake.Respond(DeviceMethods.AppsOpen, _ => new JsonObject { ["session"] = "s", ["appName"] = "Example", ["appBundleId"] = "com.example.app" });
        var session = await h.Boot();
        var device = session.Device;
        var before = h.Fake.Calls.Count;
        await session.RestartAsync(CancellationToken.None);
        await session.RestartAsync(CancellationToken.None);
        await session.ClearStateAsync(CancellationToken.None);
        await device.OpenAppAsync("com.example.app");
        await device.OpenAppAsync("com.other", new OpenAppOptions { Relaunch = true });
        await device.CloseAppAsync();
        await session.RestartAsync(CancellationToken.None);

        static (string Method, string Args) Open(string extra) => ("apps.open", "{\"platform\":\"ios\"," + extra + "}");
        static (string Method, string Args) Permission(string name, string state) =>
            ("settings.update", $$"""{"setting":"permission","permission":"{{name}}","state":"{{state}}","platform":"ios","app":"com.example.app"}""");
        var fresh = Open("\"app\":\"com.example.app\",\"relaunch\":true,\"launchArgs\":[\"-e2e\",\"YES\"]");
        JsonAssert.Calls(
            h.Fake.Calls,
            before,

            // The permissions name the app, so they go in before the first launch with no open to bind the session.
            Permission("camera", "grant"),
            Permission("location", "deny"),
            fresh,
            Permission("camera", "grant"),
            Permission("location", "deny"),
            fresh,

            // A state clear resets the permissions with the data. They are put back before the relaunch.
            ("settings.update", """{"platform":"ios","setting":"clear-app-state","state":"clear","app":"com.example.app"}"""),
            Permission("camera", "grant"),
            Permission("location", "deny"),
            fresh,

            // A foreground-only open, and another app, take none of the engine's options.
            Open("\"app\":\"com.example.app\""),
            Open("\"app\":\"com.other\",\"relaunch\":true"),
            ("apps.close", """{"platform":"ios","app":"com.example.app"}"""),
            Permission("camera", "grant"),
            Permission("location", "deny"),
            fresh);
    }

    [Fact]
    public async Task Launches_any_app_with_its_own_arguments_and_permissions_set_on_that_app_by_name()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        var device = session.Device;
        var before = h.Fake.Calls.Count;
        await device.OpenAppAsync(
            "com.other",
            new OpenAppOptions
            {
                Relaunch = true,
                LaunchArguments = ["--reset-onboarding"],
                Permissions = new Dictionary<DevicePermission, PermissionState> { [DevicePermission.Photos] = PermissionState.Reset },
            });

        // No arguments and an empty map send nothing.
        await device.OpenAppAsync(
            "com.other",
            new OpenAppOptions { LaunchArguments = [], Permissions = new Dictionary<DevicePermission, PermissionState> { [DevicePermission.Photos] = PermissionState.Reset } });
        await device.OpenAppAsync("com.other", new OpenAppOptions { Permissions = new Dictionary<DevicePermission, PermissionState>() });
        JsonAssert.Calls(
            h.Fake.Calls,
            before,
            ("settings.update", """{"setting":"permission","permission":"photos","state":"reset","platform":"ios","app":"com.other"}"""),
            ("apps.open", """{"platform":"ios","app":"com.other","relaunch":true,"launchArgs":["--reset-onboarding"]}"""),
            ("settings.update", """{"setting":"permission","permission":"photos","state":"reset","platform":"ios","app":"com.other"}"""),
            ("apps.open", """{"platform":"ios","app":"com.other"}"""),
            ("apps.open", """{"platform":"ios","app":"com.other"}"""));
    }

    [Fact]
    public async Task Switches_Wi_Fi_and_airplane_mode_on_Android_and_on_iOS_where_simctl_only_repaints_the_status_bar_refuses_going_offline_and_restores_with_no_device_command()
    {
        var ios = Harness.Create();
        var iosSession = await OpenAttempt(ios);
        var before = ios.Fake.Calls.Count;
        var device = iosSession.Device;
        var offline = await Assert.ThrowsAsync<EngineException>(() => device.SetNetworkAsync(NetworkState.Offline));
        Assert.Equal("UNSUPPORTED_CAPABILITY", offline.Code);
        Assert.Contains("status bar", offline.Message);
        var airplane = await Assert.ThrowsAsync<EngineException>(() => device.SetAirplaneModeAsync(true));
        Assert.Equal("UNSUPPORTED_CAPABILITY", airplane.Code);
        Assert.Contains("status bar", airplane.Message);

        // The restore a cleanup hook shared with Android runs: nothing went offline, so there is nothing to send.
        await device.SetNetworkAsync(NetworkState.Online);
        await device.SetAirplaneModeAsync(false);
        Assert.Equal(before, ios.Fake.Calls.Count);

        var android = Android("com.example.app");
        var androidSession = await OpenAttempt(android);
        var count = android.Fake.Calls.Count;
        await androidSession.Device.SetNetworkAsync(NetworkState.Offline);
        await androidSession.Device.SetNetworkAsync(NetworkState.Online);
        await androidSession.Device.SetAirplaneModeAsync(true);
        await androidSession.Device.SetAirplaneModeAsync(false);
        JsonAssert.Calls(
            android.Fake.Calls,
            count,
            ("settings.update", """{"platform":"android","setting":"wifi","state":"off"}"""),
            ("settings.update", """{"platform":"android","setting":"wifi","state":"on"}"""),
            ("settings.update", """{"platform":"android","setting":"airplane","state":"on"}"""),
            ("settings.update", """{"platform":"android","setting":"airplane","state":"off"}"""));
    }

    [Fact]
    public async Task Resets_the_simulator_keychain_on_iOS_and_refuses_on_Android_before_any_device_command()
    {
        var ios = Harness.Create();
        var iosSession = await OpenAttempt(ios);
        var before = ios.Fake.Calls.Count;
        await iosSession.Device.ClearKeychainAsync();
        JsonAssert.Calls(ios.Fake.Calls, before, ("settings.update", """{"platform":"ios","setting":"reset-keychain","state":"clear"}"""));

        var android = Android("com.example.app");
        var androidSession = await OpenAttempt(android);
        var count = android.Fake.Calls.Count;
        var refused = await Assert.ThrowsAsync<EngineException>(() => androidSession.Device.ClearKeychainAsync());
        Assert.Equal("UNSUPPORTED_CAPABILITY", refused.Code);
        Assert.Equal(count, android.Fake.Calls.Count);
    }

    [Fact]
    public async Task Folds_an_iOS_simulator_or_an_Android_emulator_through_the_one_device_command_and_maps_a_hingeless_device_to_UNSUPPORTED_CAPABILITY()
    {
        var ios = Harness.Create();
        var iosSession = await OpenAttempt(ios);
        ios.Fake.Respond(DeviceMethods.CommandFold, _ => throw new AgentDeviceError("UNSUPPORTED_OPERATION", "fold requires a foldable iOS simulator such as iPhone Duo"));
        var hingeless = await Assert.ThrowsAsync<EngineException>(() => iosSession.Device.FoldAsync(FoldPose.Open));
        Assert.Equal("UNSUPPORTED_CAPABILITY", hingeless.Code);

        // agent-device 0.21.25 poses a foldable Android emulator through the emulator console, so Android sends the same command instead of being refused before it.
        var android = Android("com.example.app");
        var androidSession = await OpenAttempt(android);
        var count = android.Fake.Calls.Count;
        await androidSession.Device.FoldAsync(FoldPose.HalfOpen);
        JsonAssert.Calls(android.Fake.Calls, count, ("command.fold", """{"platform":"android","pose":"half-open"}"""));
    }
}
