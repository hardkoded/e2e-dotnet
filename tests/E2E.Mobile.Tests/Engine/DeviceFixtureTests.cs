// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine;
using static E2E.Mobile.Tests.Screens;

namespace E2E.Mobile.Tests.Engine;

public sealed class DeviceFixtureTests
{
    private static Task Tap(DeviceSession session, SemanticNode node) =>
        session.PerformAsync(node, new LocatorAction.Tap(), OperationContext.Test, CancellationToken.None);

    private static Harness Android(string? bundleId = null, bool pinned = true) =>
        Harness.Create(
            options => options with { Platform = MobilePlatform.Android },
            pinned,
            app => bundleId is null ? app : app with { BundleId = bundleId }
        );

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
            locate: session.LocateAsync
        );
        ((IEngineFixtureHost)session).CreateFixtures(new EngineFixtureContext(screen));

        var about = session.Device.Locator("id=ABOUT").First();
        Assert.Equal(1, await about.CountAsync());
        Assert.Equal(0, await session.Device.Locator("id=NOPE").CountAsync());
        Assert.Equal(1, await session.Device.Locator("id=NOPE || label=\"About\"").First().CountAsync());
        await about.TapAsync();
        JsonAssert.Args("""{"ref":"@e4","settle":true,"settleQuietMs":150}""", h.Fake.LastArgs("interactions.press"));
    }

    [Fact]
    public async Task Gives_a_screen_swipe_the_remaining_transition_budget_after_an_alert_is_dismissed()
    {
        var h = Harness.Create(options => options with { TransitionMilliseconds = 120 });
        var session = await OpenAttempt(h);
        h.Clock.Advance(TimeSpan.FromMilliseconds(150));
        await RootOf(session);
        await session.Device.AlertAsync(AlertAction.Accept);
        var dismissedAt = h.Clock.GetUtcNow();
        var screen = await RootOf(session);
        h.Clock.Advance(TimeSpan.FromMilliseconds(30));

        await session.PerformAsync(
            screen,
            new LocatorAction.Swipe(ScrollDirection.Down),
            OperationContext.Test,
            CancellationToken.None
        );

        Assert.Equal(TimeSpan.FromMilliseconds(120), h.Clock.GetUtcNow() - dismissedAt);
        JsonAssert.Args(
            """{"direction":"down","postGestureStabilization":false}""",
            h.Fake.LastArgs("interactions.scroll")
        );
    }

    [Fact]
    public async Task Partial_Counts_a_back_home_hardware_button_press_lock_unlock_alert_keyboard_rotation_or_fold_as_an_action_so_a_control_that_arrives_with_it_waits_out_the_transition_budget()
    {
        var h = Harness.Create(options => options with { TransitionMilliseconds = 120 });
        var session = await OpenAttempt(h);
        await Observed(session, "Back");
        var device = session.Device;
        Func<Task>[] changes =
        [
            () => device.BackAsync(),
            () => device.HomeAsync(),
            () => device.PressButtonAsync(HardwareButton.VolumeUp),
            () => device.LockAsync(),
            () => device.UnlockAsync(),
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
        // The harness's project root, /project, is not on disk: boot it on a temp directory that holds the build.
        using var project = new TempBuilds();
        project.Create("b/App.app");
        var build = project.Full("b/App.app");
        var h = Harness.Create(projectRoot: project.Root);
        var session = await OpenAttempt(h);
        h.Fake.Respond(DeviceMethods.AppsInstall, _ => new JsonObject
        {
            ["app"] = build,
            ["appPath"] = build,
            ["platform"] = "ios",
            ["bundleId"] = "com.example.app",
        });
        h.Fake.Respond(DeviceMethods.AppsReinstall, args => new JsonObject
        {
            ["app"] = args?["app"]?.DeepClone(),
            ["appPath"] = build,
            ["platform"] = "ios",
        });
        var device = session.Device;
        var before = h.Fake.Calls.Count;
        Assert.Equal(
            new InstalledApp("com.example.app", "com.example.app"),
            await device.InstallAppAsync("./b/App.app")
        );
        Assert.Equal(
            new InstalledApp("Settings"),
            await device.InstallAppAsync(build, new InstallAppOptions { Reinstall = true })
        );
        Assert.Equal(
            new InstalledApp("com.other"),
            await device.InstallAppAsync(build, new InstallAppOptions { App = "com.other", Reinstall = true })
        );
        JsonAssert.Calls(
            h.Fake.Calls,
            before,
            ("apps.install", JsonSerializer.Serialize(new { platform = "ios", appPath = build })),
            (
                "apps.reinstall",
                JsonSerializer.Serialize(
                    new
                    {
                        platform = "ios",
                        app = "Settings",
                        appPath = build,
                    }
                )
            ),
            (
                "apps.reinstall",
                JsonSerializer.Serialize(
                    new
                    {
                        platform = "ios",
                        app = "com.other",
                        appPath = build,
                    }
                )
            )
        );

        var free = Harness.Create(pinned: false);
        var freeSession = await OpenAttempt(free);
        var refused = await Assert.ThrowsAsync<EngineException>(() =>
            freeSession.Device.InstallAppAsync(build, new InstallAppOptions { Reinstall = true })
        );
        Assert.Equal("INVALID_STATE", refused.Code);
        Assert.Equal(["devices.boot"], free.Fake.Methods());
    }

    [Fact]
    public async Task Refuses_a_build_path_with_nothing_at_it_as_INVALID_ARGUMENT_before_any_device_command()
    {
        var h = Harness.Create(app: app => app with { AppPath = "../elsewhere/Missing.app" });
        var session = await OpenAttempt(h);
        var device = session.Device;
        var before = h.Fake.Calls.Count;
        foreach (var missing in new[] { "../missing/App.app", "/missing/App.app" })
        {
            var refused = await Assert.ThrowsAsync<TestException>(() => device.InstallAppAsync(missing));
            Assert.Equal("INVALID_ARGUMENT", refused.Code);
            Assert.Equal(
                $"device.installApp({JsonSerializer.Serialize(missing)}): no build at {Path.GetFullPath(missing, Harness.ProjectRoot)}; the path resolves against the project root",
                refused.Message
            );
            var again = await Assert.ThrowsAsync<TestException>(() =>
                device.InstallAppAsync(missing, new InstallAppOptions { Reinstall = true })
            );
            Assert.Equal("INVALID_ARGUMENT", again.Code);
        }

        var empty = await Assert.ThrowsAsync<TestException>(() => device.InstallAppAsync(""));
        Assert.Equal("INVALID_ARGUMENT", empty.Code);
        Assert.Equal([], h.Fake.Calls.Skip(before).ToList());
    }

    [Fact]
    public async Task Uninstalls_and_looks_up_apps_by_id_or_listed_name_the_pinned_app_by_default()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        h.Fake.Respond(DeviceMethods.AppsUninstall, args => new JsonObject
        {
            ["app"] = args?["app"]?.DeepClone(),
            ["wasInstalled"] = args?["app"]?.GetValue<string>() != "dev.gone",
        });
        h.Fake.Respond(DeviceMethods.AppsList, _ => new JsonArray(
            "Settings (com.apple.Preferences)",
            "e2e Benchmark (dev.e2e.benchmark)",
            "dev.bare.package"
        ));
        var device = session.Device;
        var before = h.Fake.Calls.Count;
        Assert.True(await device.IsAppInstalledAsync("dev.e2e.benchmark"));
        Assert.True(await device.IsAppInstalledAsync("e2e Benchmark"));
        Assert.True(await device.IsAppInstalledAsync("dev.bare.package"));
        Assert.False(await device.IsAppInstalledAsync("dev.e2e"));
        Assert.True(await device.IsAppInstalledAsync());
        Assert.True(await device.UninstallAppAsync("dev.e2e.benchmark"));
        Assert.False(await device.UninstallAppAsync("dev.gone"));
        Assert.True(await device.UninstallAppAsync());
        JsonAssert.Calls(
            h.Fake.Calls,
            before,
            ("apps.list", """{"platform":"ios","appsFilter":"all"}"""),
            ("apps.list", """{"platform":"ios","appsFilter":"all"}"""),
            ("apps.list", """{"platform":"ios","appsFilter":"all"}"""),
            ("apps.list", """{"platform":"ios","appsFilter":"all"}"""),
            ("apps.list", """{"platform":"ios","appsFilter":"all"}"""),
            ("apps.uninstall", """{"platform":"ios","app":"dev.e2e.benchmark"}"""),
            ("apps.uninstall", """{"platform":"ios","app":"dev.gone"}"""),
            ("apps.uninstall", """{"platform":"ios","app":"Settings"}""")
        );

        var free = Harness.Create(pinned: false);
        var freeSession = await OpenAttempt(free);
        var uninstall = await Assert.ThrowsAsync<EngineException>(() => freeSession.Device.UninstallAppAsync());
        Assert.Equal("INVALID_STATE", uninstall.Code);
        var lookup = await Assert.ThrowsAsync<EngineException>(() => freeSession.Device.IsAppInstalledAsync());
        Assert.Equal("INVALID_STATE", lookup.Code);
        Assert.Equal(["devices.boot"], free.Fake.Methods());
    }

    [Fact]
    public async Task Partial_Issues_one_settings_or_system_command_per_method()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        h.Fake.Respond(DeviceMethods.CommandAppState, _ => new JsonObject
        {
            ["platform"] = "ios",
            ["appName"] = "Settings",
            ["appBundleId"] = "com.apple.Preferences",
        });
        h.Fake.Respond(DeviceMethods.CommandClipboard, args =>
            args?["action"]?.GetValue<string>() == "read"
                ? new JsonObject { ["action"] = "read", ["text"] = "pasted" }
                : new JsonObject { ["action"] = "write", ["textLength"] = 1 }
        );
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
        await device.LockAsync();
        await device.UnlockAsync();
        await device.AlertAsync(AlertAction.Accept);
        await device.DismissKeyboardAsync();
        Assert.Equal("pasted", await device.ClipboardAsync());
        await device.SetClipboardAsync("x");
        JsonAssert.Calls(
            h.Fake.Calls,
            before,
            (
                "settings.update",
                """{"setting":"permission","permission":"camera","state":"grant","platform":"ios","app":"Settings"}"""
            ),
            (
                "settings.update",
                """{"platform":"ios","setting":"location","state":"set","latitude":37.3349,"longitude":-122.009}"""
            ),
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
            ("command.lockScreen", """{"platform":"ios","action":"lock"}"""),
            ("command.lockScreen", """{"platform":"ios","action":"unlock"}"""),
            ("command.alert", """{"platform":"ios","action":"accept"}"""),
            ("command.keyboard", """{"action":"dismiss"}"""),
            ("command.clipboard", """{"platform":"ios","action":"read"}"""),
            ("command.clipboard", """{"platform":"ios","action":"write","text":"x"}""")
        );
    }

    [Fact]
    public async Task Refuses_coordinates_out_of_range_or_not_finite_as_INVALID_ARGUMENT_before_any_device_command()
    {
        var h = Harness.Create(
            options => options with { Platform = MobilePlatform.Android },
            app: app => app with { BundleId = "com.android.settings" }
        );
        var session = await OpenAttempt(h);
        var device = session.Device;
        var before = h.Fake.Calls.Count;
        (Coordinates Coordinates, string Message)[] refused =
        [
            (new Coordinates(152.5, 21.01), "latitude must be a finite number from -90 to 90, got 152.5"),
            (new Coordinates(-90.5, 21.01), "latitude must be a finite number from -90 to 90, got -90.5"),
            (new Coordinates(double.NaN, 21.01), "latitude must be a finite number from -90 to 90, got NaN"),
            (new Coordinates(52.23, 181), "longitude must be a finite number from -180 to 180, got 181"),
            (
                new Coordinates(52.23, double.PositiveInfinity),
                "longitude must be a finite number from -180 to 180, got Infinity"
            ),
        ];
        foreach (var (coordinates, message) in refused)
        {
            var failure = await Assert.ThrowsAsync<TestException>(() => device.SetLocationAsync(coordinates));
            Assert.Equal("INVALID_ARGUMENT", failure.Code);
            Assert.Equal($"device.setLocation: {message}", failure.Message);
        }

        Assert.Equal([], h.Fake.Calls.Skip(before).ToList());

        // The edges themselves are valid.
        await device.SetLocationAsync(new Coordinates(-90, 180));
        JsonAssert.Args(
            """{"platform":"android","setting":"location","state":"set","latitude":-90,"longitude":180}""",
            h.Fake.LastArgs("settings.update")
        );
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
            (
                "settings.update",
                """{"setting":"permission","permission":"microphone","state":"reset","platform":"ios","device":"iPhone 16e","app":"Settings"}"""
            ),
            ("apps.open", """{"app":"com.other","platform":"ios","device":"iPhone 16e","relaunch":true}"""),
            ("apps.close", """{"platform":"ios","device":"iPhone 16e","app":"com.apple.Preferences"}"""),
            (
                "settings.update",
                """{"setting":"permission","permission":"camera","state":"grant","platform":"ios","device":"iPhone 16e","app":"Settings"}"""
            )
        );
    }

    [Fact]
    public async Task Leaves_the_app_to_agent_devices_session_when_the_target_pins_none()
    {
        var h = Harness.Create(pinned: false);
        var session = await OpenAttempt(h);
        var before = h.Fake.Calls.Count;
        await session.Device.SetPermissionAsync(DevicePermission.Camera, PermissionState.Deny);
        JsonAssert.Calls(
            h.Fake.Calls,
            before,
            ("settings.update", """{"setting":"permission","permission":"camera","state":"deny","platform":"ios"}""")
        );
    }

    [Fact]
    public async Task Switches_Android_location_services_on_before_a_fix_since_clearLocation_leaves_them_off_and_reads_the_foreground_package()
    {
        var h = Android("com.android.settings");
        var session = await OpenAttempt(h);
        h.Fake.Respond(DeviceMethods.CommandAppState, _ => new JsonObject
        {
            ["platform"] = "android",
            ["package"] = "com.android.settings",
            ["activity"] = ".Main",
        });
        Assert.Equal(
            new ForegroundApp("com.android.settings", "com.android.settings"),
            await session.Device.ForegroundAppAsync()
        );
        var before = h.Fake.Calls.Count;
        var device = session.Device;
        await device.SetLocationAsync(new Coordinates(52.2297, 21.0122));
        await device.ClearLocationAsync();
        JsonAssert.Calls(
            h.Fake.Calls,
            before,
            ("settings.update", """{"platform":"android","setting":"location","state":"on"}"""),
            (
                "settings.update",
                """{"platform":"android","setting":"location","state":"set","latitude":52.2297,"longitude":21.0122}"""
            ),
            ("settings.update", """{"platform":"android","setting":"location","state":"off"}""")
        );
    }

    [Fact]
    public async Task Opens_a_link_into_the_pinned_app_or_a_named_one_and_the_location_follows_the_app_it_landed_in()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        h.Fake.Respond(DeviceMethods.AppsOpen, _ => new JsonObject
        {
            ["session"] = "s",
            ["appName"] = "Benchmark",
            ["appBundleId"] = "dev.e2e.benchmark",
        });
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.NodesOnly(RawNodes.SettingsNodes));
        var device = session.Device;
        var before = h.Fake.Calls.Count;
        await device.OpenLinkAsync("e2e-benchmark://orders/42?token=abc");
        await device.OpenLinkAsync("https://example.com/verify", "com.apple.mobilesafari");
        JsonAssert.Calls(
            h.Fake.Calls,
            before,
            ("apps.open", """{"platform":"ios","app":"Settings","url":"e2e-benchmark://orders/42?token=abc"}"""),
            ("apps.open", """{"platform":"ios","app":"com.apple.mobilesafari","url":"https://example.com/verify"}""")
        );
        Assert.Equal("dev.e2e.benchmark / General", (await session.ObserveAsync(CancellationToken.None)).Route);
    }

    [Fact]
    public async Task Opens_app_open_path_links_on_a_target_with_app_linkBase_in_the_pinned_app_through_session_openLink()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        h.Fake.Respond(DeviceMethods.AppsOpen, _ => new JsonObject
        {
            ["session"] = "s",
            ["appName"] = "Benchmark",
            ["appBundleId"] = "dev.e2e.benchmark",
        });
        var before = h.Fake.Calls.Count;
        await ((IEngineSession)session).OpenLinkAsync(
            "e2e-benchmark://orders/42",
            OperationContext.Test,
            CancellationToken.None
        );
        JsonAssert.Calls(
            h.Fake.Calls,
            before,
            ("apps.open", """{"platform":"ios","app":"Settings","url":"e2e-benchmark://orders/42"}""")
        );
        var refused = await Assert.ThrowsAsync<ConfigurationException>(() =>
            ((IEngineSession)session).OpenLinkAsync(
                "javascript:alert(1)",
                OperationContext.Test,
                CancellationToken.None
            )
        );
        Assert.Equal("POLICY_DENIED", refused.Code);
    }

    [Fact]
    public async Task Lets_Android_route_a_link_without_an_app_and_needs_one_on_iOS_where_an_unbound_open_leaves_no_session()
    {
        var android = Android(pinned: false);
        var androidSession = await OpenAttempt(android);
        android.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.NodesOnly(RawNodes.SettingsNodes));
        android.Fake.Respond(DeviceMethods.AppsOpen, _ => new JsonObject
        {
            ["session"] = "s",
            ["appName"] = "myapp://orders/42",
        });
        await androidSession.Device.OpenLinkAsync("myapp://orders/42");
        JsonAssert.Args("""{"platform":"android","app":"myapp://orders/42"}""", android.Fake.LastArgs("apps.open"));
        Assert.Equal("General", (await androidSession.ObserveAsync(CancellationToken.None)).Route);
        android.Fake.Respond(DeviceMethods.AppsOpen, _ => new JsonObject
        {
            ["session"] = "s",
            ["appName"] = "https://example.com",
            ["appBundleId"] = "com.android.chrome",
        });
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
        foreach (
            var denied in new[]
            {
                "file:///etc/passwd",
                "data:text/html,hi",
                "javascript:alert(1)",
                "view-source:file:///etc/passwd",
            }
        )
        {
            Assert.Equal(
                "POLICY_DENIED",
                (await Assert.ThrowsAsync<ConfigurationException>(() => device.OpenLinkAsync(denied))).Code
            );
            Assert.Equal(
                "POLICY_DENIED",
                (await Assert.ThrowsAsync<ConfigurationException>(() => device.OpenAppAsync(denied))).Code
            );
        }

        foreach (var malformed in new[] { "orders/42", "", "https://" })
        {
            Assert.Equal(
                "INVALID_ARGUMENT",
                (await Assert.ThrowsAsync<TestException>(() => device.OpenLinkAsync(malformed))).Code
            );
        }

        foreach (var link in new[] { "https://example.com/verify", "myapp://orders/42" })
        {
            Assert.Equal(
                "INVALID_ARGUMENT",
                (
                    await Assert.ThrowsAsync<TestException>(() =>
                        device.OpenAppAsync(link, new OpenAppOptions { Relaunch = true })
                    )
                ).Code
            );
        }

        Assert.Equal(before, h.Fake.Calls.Count);
        await device.OpenAppAsync("com.apple.mobilesafari");
        JsonAssert.Calls(h.Fake.Calls, before, ("apps.open", """{"app":"com.apple.mobilesafari","platform":"ios"}"""));
    }

    [Theory]
    [InlineData(
        "ios",
        "73329AAC-8EB6-4AAD-B33C-8A8E911CD2A6",
        """{"platform":"ios","udid":"73329AAC-8EB6-4AAD-B33C-8A8E911CD2A6"}"""
    )]
    [InlineData("android", "emulator-5554", """{"platform":"android","serial":"emulator-5554"}""")]
    [InlineData("ios", "iPhone 17 Pro", """{"platform":"ios","device":"iPhone 17 Pro"}""")]
    [InlineData("ios", null, """{"platform":"ios"}""")]
    public async Task Keeps_device_commands_on_the_selected_platform_device_after_closeApp_ends_the_previous_session(
        string platformName,
        string? device,
        string selectionJson
    )
    {
        var platform = platformName == "android" ? MobilePlatform.Android : MobilePlatform.Ios;
        var selection = JsonNode.Parse(selectionJson)!.AsObject();
        var h = Harness.Create(
            options => options with { Platform = platform, Device = device },
            app: app => app with { BundleId = "com.example.app" }
        );
        await h.Engine.PrepareAsync(h.PrepareInfo(1), CancellationToken.None);
        var session = await OpenAttempt(h);
        h.Fake.Respond(DeviceMethods.CommandAppState, _ => new JsonObject
        {
            ["platform"] = platformName,
            ["appName"] = "Example",
            ["appBundleId"] = "com.example.app",
            ["package"] = platform == MobilePlatform.Android ? "com.example.app" : null,
        });
        h.Fake.Respond(DeviceMethods.CommandClipboard, args =>
            args?["action"]?.GetValue<string>() == "read"
                ? new JsonObject { ["action"] = "read", ["text"] = "pasted" }
                : new JsonObject { ["action"] = "write", ["textLength"] = 1 }
        );
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
            () =>
                selected.SetBiometricsAsync(
                    platform == MobilePlatform.Ios ? BiometricSensor.FaceId : BiometricSensor.Fingerprint,
                    BiometricResult.Match
                ),
            () => selected.AlertAsync(AlertAction.Accept),
            () => selected.PressButtonAsync(HardwareButton.VolumeUp),
            () => selected.ClipboardAsync(),
            () => selected.SetClipboardAsync("x"),
            () => selected.SetPermissionAsync(DevicePermission.Camera, PermissionState.Grant),
            // iOS refuses going offline before any command, so the radios are Android's.
            .. platform == MobilePlatform.Ios
                ? new Func<Task>[]
                {
                    () => selected.EnrollBiometricsAsync(BiometricSensor.TouchId, true),
                    () => selected.ClearKeychainAsync(),
                }
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
        var reset = new JsonObject
        {
            ["setting"] = "clear-app-state",
            ["state"] = "clear",
            ["app"] = "com.example.app",
        };
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
            Assert.True(
                JsonNode.DeepEquals(value, args?[key]),
                $"expected {key} {value} in {args?.ToJsonString() ?? "null"}"
            );
        }
    }

    [Fact]
    public async Task Names_the_pinned_device_on_device_commands_so_the_ones_after_closeApp_still_reach_it_when_other_devices_are_booted_and_leaves_app_commands_to_the_session_PARTIAL_no_video_recording()
    {
        const string udid = "2BBF3F07-AF66-4F95-82AB-BF442506FC89";
        var h = Harness.Create(options => options with { Device = udid });
        await h.Engine.PrepareAsync(h.PrepareInfo(1), CancellationToken.None);
        h.Fake.Respond(DeviceMethods.CommandAppState, _ => new JsonObject
        {
            ["platform"] = "ios",
            ["appName"] = "Settings",
            ["appBundleId"] = "com.apple.Preferences",
        });
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
            (
                "settings.update",
                "{" + selection + ",\"setting\":\"clear-app-state\",\"state\":\"clear\",\"app\":\"Settings\"}"
            ),
            ("apps.open", "{" + selection + ",\"app\":\"Settings\",\"relaunch\":true}"),
            ("command.appState", "{}"),
            ("capture.snapshot", """{"interactiveOnly":false}"""),
            ("sessions.close", "null")
        );
    }

    [Fact]
    public async Task Presets_the_configured_permissions_and_passes_the_launch_arguments_on_every_fresh_launch_of_the_pinned_app_and_on_no_other_open()
    {
        var h = Harness.Create(app: app =>
            app with
            {
                BundleId = "com.example.app",
                LaunchArguments = ["-e2e", "YES"],
                Permissions = new Dictionary<DevicePermission, PermissionState>
                {
                    [DevicePermission.Camera] = PermissionState.Grant,
                    [DevicePermission.Location] = PermissionState.Deny,
                },
            }
        );
        h.Fake.Respond(DeviceMethods.AppsOpen, _ => new JsonObject
        {
            ["session"] = "s",
            ["appName"] = "Example",
            ["appBundleId"] = "com.example.app",
        });
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
            (
                "settings.update",
                $$"""{"setting":"permission","permission":"{{name}}","state":"{{state}}","platform":"ios","app":"com.example.app"}"""
            );
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
            (
                "settings.update",
                """{"platform":"ios","setting":"clear-app-state","state":"clear","app":"com.example.app"}"""
            ),
            Permission("camera", "grant"),
            Permission("location", "deny"),
            fresh,
            // A foreground-only open, and another app, take none of the engine's options.
            Open("\"app\":\"com.example.app\""),
            Open("\"app\":\"com.other\",\"relaunch\":true"),
            ("apps.close", """{"platform":"ios","app":"com.example.app"}"""),
            Permission("camera", "grant"),
            Permission("location", "deny"),
            fresh
        );
    }

    [Theory]
    [InlineData(MobilePlatform.Ios, "ios")]
    [InlineData(MobilePlatform.Android, "android")]
    public async Task Grants_every_permission_before_each_fresh_launch_with_all_in_one_agent_device_all_on_either_platform_and_for_one_openApp(
        MobilePlatform platform,
        string wire
    )
    {
        var h = Harness.Create(
            configure: options => options with { Platform = platform },
            app: app => app with { BundleId = "com.example.app", AllPermissions = true }
        );
        h.Fake.Respond(DeviceMethods.AppsOpen, _ => new JsonObject
        {
            ["session"] = "s",
            ["appName"] = "Example",
            ["appBundleId"] = "com.example.app",
        });
        var session = await h.Boot();
        var device = session.Device;
        var before = h.Fake.Calls.Count;
        await session.RestartAsync(CancellationToken.None);
        await session.ClearStateAsync(CancellationToken.None);
        await device.OpenAppAsync("com.other", new OpenAppOptions { Relaunch = true, AllPermissions = true });

        // agent-device expands `all` itself: the app's declared permissions on Android, each privacy service on iOS.
        (string Method, string Args) All(string app) =>
            (
                "settings.update",
                $$"""{"setting":"permission","permission":"all","state":"grant","platform":"{{wire}}","app":"{{app}}"}"""
            );
        var fresh = ("apps.open", $$"""{"platform":"{{wire}}","app":"com.example.app","relaunch":true}""");
        JsonAssert.Calls(
            h.Fake.Calls,
            before,
            All("com.example.app"),
            fresh,
            (
                "settings.update",
                $$"""{"platform":"{{wire}}","setting":"clear-app-state","state":"clear","app":"com.example.app"}"""
            ),
            All("com.example.app"),
            fresh,
            All("com.other"),
            ("apps.open", $$"""{"platform":"{{wire}}","app":"com.other","relaunch":true}""")
        );
    }

    [Fact]
    public async Task Launches_any_app_with_its_own_arguments_a_list_or_an_object_spelled_for_the_platform_and_permissions_set_on_that_app_by_name()
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
                Permissions = new Dictionary<DevicePermission, PermissionState>
                {
                    [DevicePermission.Photos] = PermissionState.Reset,
                },
            }
        );

        await device.OpenAppAsync(
            "com.other",
            new OpenAppOptions
            {
                Relaunch = true,
                LaunchArgumentValues = new Dictionary<string, LaunchArgumentValue?>
                {
                    ["AppleLocale"] = "fr_FR",
                    ["resetOnboarding"] = true,
                },
            }
        );

        // No arguments and an empty map send nothing.
        await device.OpenAppAsync(
            "com.other",
            new OpenAppOptions
            {
                LaunchArguments = [],
                Permissions = new Dictionary<DevicePermission, PermissionState>
                {
                    [DevicePermission.Photos] = PermissionState.Reset,
                },
            }
        );
        await device.OpenAppAsync(
            "com.other",
            new OpenAppOptions { Permissions = new Dictionary<DevicePermission, PermissionState>() }
        );
        JsonAssert.Calls(
            h.Fake.Calls,
            before,
            (
                "settings.update",
                """{"setting":"permission","permission":"photos","state":"reset","platform":"ios","app":"com.other"}"""
            ),
            (
                "apps.open",
                """{"platform":"ios","app":"com.other","relaunch":true,"launchArgs":["--reset-onboarding"]}"""
            ),
            (
                "apps.open",
                """{"platform":"ios","app":"com.other","relaunch":true,"launchArgs":["-AppleLocale","fr_FR","-resetOnboarding","YES"]}"""
            ),
            (
                "settings.update",
                """{"setting":"permission","permission":"photos","state":"reset","platform":"ios","app":"com.other"}"""
            ),
            ("apps.open", """{"platform":"ios","app":"com.other"}"""),
            ("apps.open", """{"platform":"ios","app":"com.other"}""")
        );
    }

    [Fact]
    public async Task Spells_a_launch_argument_object_for_the_platform_on_every_fresh_launch_of_the_pinned_app_and_sends_nothing_for_an_empty_object()
    {
        var ios = Harness.Create(app: app =>
            app with
            {
                BundleId = "com.example.app",
                LaunchArgumentValues = new Dictionary<string, LaunchArgumentValue?>
                {
                    ["AppleLanguages"] = new[] { "fr", "en-GB" },
                    ["AppleLocale"] = "fr_FR",
                    ["e2e"] = true,
                    ["retries"] = 3,
                },
            }
        );
        var iosSession = await OpenAttempt(ios);
        await iosSession.RestartAsync(CancellationToken.None);
        Assert.Equal(
            JsonNode
                .Parse(
                    """{"platform":"ios","app":"com.example.app","relaunch":true,"launchArgs":["-AppleLanguages","(fr, en-GB)","-AppleLocale","fr_FR","-e2e","YES","-retries","3"]}"""
                )!
                .ToJsonString(),
            ios.Fake.LastArgs("apps.open")!.ToJsonString()
        );

        var android = Harness.Create(
            options => options with { Platform = MobilePlatform.Android },
            app: app =>
                app with
                {
                    BundleId = "com.example.app",
                    LaunchArgumentValues = new Dictionary<string, LaunchArgumentValue?>
                    {
                        ["e2e"] = true,
                        ["locale"] = "fr_FR",
                        ["retries"] = 3,
                        ["tags"] = new[] { "a", "b" },
                    },
                }
        );
        var androidSession = await OpenAttempt(android);
        await androidSession.RestartAsync(CancellationToken.None);
        Assert.Equal(
            JsonNode
                .Parse(
                    """{"platform":"android","app":"com.example.app","relaunch":true,"launchArgs":["--ez","e2e","true","--es","locale","fr_FR","--ei","retries","3","--esa","tags","a,b"]}"""
                )!
                .ToJsonString(),
            android.Fake.LastArgs("apps.open")!.ToJsonString()
        );

        var empty = Harness.Create(app: app =>
            app with
            {
                BundleId = "com.example.app",
                LaunchArgumentValues = new Dictionary<string, LaunchArgumentValue?>(),
            }
        );
        var emptySession = await OpenAttempt(empty);
        await emptySession.RestartAsync(CancellationToken.None);
        Assert.Equal(
            JsonNode.Parse("""{"platform":"ios","app":"com.example.app","relaunch":true}""")!.ToJsonString(),
            empty.Fake.LastArgs("apps.open")!.ToJsonString()
        );
    }

    /// <summary>Partial: upstream's <c>{ env: null }</c> is a value the port's typed dictionary cannot hold except as the entry left out, so the wrong-shape refusal is checked with a null list item.</summary>
    [Fact]
    public async Task Partial_Refuses_openApp_launch_arguments_of_the_wrong_shape_or_ones_Android_cannot_carry_before_any_device_command()
    {
        var ios = Harness.Create();
        var iosSession = await OpenAttempt(ios);
        var before = ios.Fake.Calls.Count;
        var wrong = await Assert.ThrowsAsync<TestException>(() =>
            iosSession.Device.OpenAppAsync(
                "com.other",
                new OpenAppOptions { Relaunch = true, LaunchArguments = ["-a", null!] }
            )
        );
        Assert.Equal("INVALID_ARGUMENT", wrong.Code);
        Assert.Equal(before, ios.Fake.Calls.Count);

        var android = Android("com.example.app");
        var androidSession = await OpenAttempt(android);
        var sent = android.Fake.Calls.Count;
        var apple = await Assert.ThrowsAsync<TestException>(() =>
            androidSession.Device.OpenAppAsync(
                "com.other",
                new OpenAppOptions
                {
                    Relaunch = true,
                    LaunchArgumentValues = new Dictionary<string, LaunchArgumentValue?> { ["AppleLocale"] = "fr_FR" },
                }
            )
        );
        Assert.Equal("INVALID_ARGUMENT", apple.Code);
        Assert.Contains(
            "device.openApp({ launchArguments }).AppleLocale is an iOS launch argument",
            apple.Message,
            StringComparison.Ordinal
        );
        var comma = await Assert.ThrowsAsync<TestException>(() =>
            androidSession.Device.OpenAppAsync(
                "com.other",
                new OpenAppOptions
                {
                    Relaunch = true,
                    LaunchArgumentValues = new Dictionary<string, LaunchArgumentValue?> { ["tags"] = new[] { "a,b" } },
                }
            )
        );
        Assert.Equal("INVALID_ARGUMENT", comma.Code);
        Assert.Equal(sent, android.Fake.Calls.Count);
    }

    // Partial: upstream passes the name `notification` as a string key. DevicePermission is a closed enum, so the name is a cast number (`42`) here, which the
    // refusal quotes in the same words: `device.openApp({ permissions }) has unknown key "42"`.
    [Fact]
    public async Task Partial_Refuses_an_openApp_permission_name_it_does_not_know_before_any_device_command()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        var device = session.Device;
        var before = h.Fake.Calls.Count;
        var refused = await Assert.ThrowsAsync<TestException>(() =>
            device.OpenAppAsync(
                "com.other",
                new OpenAppOptions
                {
                    Relaunch = true,
                    Permissions = new Dictionary<DevicePermission, PermissionState>
                    {
                        [(DevicePermission)42] = PermissionState.Grant,
                    },
                }
            )
        );
        Assert.Equal("INVALID_ARGUMENT", refused.Code);
        Assert.Contains("device.openApp({ permissions }) has unknown key \"42\"", refused.Message);
        Assert.Equal(before, h.Fake.Calls.Count);
    }

    [Fact]
    public async Task Refuses_a_setPermission_name_or_state_it_does_not_know_before_any_device_command_and_still_sends_a_valid_one()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        var device = session.Device;
        var before = h.Fake.Calls.Count;
        // The non-string name: a cast number on the enum overload, as the typed API has no other way to pass one.
        var number = await Assert.ThrowsAsync<TestException>(() =>
            device.SetPermissionAsync((DevicePermission)42, PermissionState.Grant)
        );
        Assert.Equal("INVALID_ARGUMENT", number.Code);
        Assert.Matches(
            @"^device\.setPermission permission must be one of camera, .*, got type number\z",
            number.Message
        );
        var cast = await Assert.ThrowsAsync<TestException>(() =>
            device.SetPermissionAsync(DevicePermission.Camera, (PermissionState)42)
        );
        Assert.Equal("INVALID_ARGUMENT", cast.Code);
        Assert.Equal("device.setPermission.camera must be one of grant, deny, reset, got 42", cast.Message);
        var typo = await Assert.ThrowsAsync<TestException>(() => device.SetPermissionAsync("camra", "grant"));
        Assert.Equal("INVALID_ARGUMENT", typo.Code);
        Assert.Equal("device.setPermission has unknown key \"camra\"; did you mean \"camera\"?", typo.Message);
        var allow = await Assert.ThrowsAsync<TestException>(() => device.SetPermissionAsync("camera", "allow"));
        Assert.Equal("INVALID_ARGUMENT", allow.Code);
        Assert.Equal("device.setPermission.camera must be one of grant, deny, reset, got \"allow\"", allow.Message);
        var undefinedState = await Assert.ThrowsAsync<TestException>(() => device.SetPermissionAsync("camera", null));
        Assert.Equal("INVALID_ARGUMENT", undefinedState.Code);
        Assert.Equal(
            "device.setPermission.camera must be one of grant, deny, reset, got undefined",
            undefinedState.Message
        );
        var undefinedName = await Assert.ThrowsAsync<TestException>(() => device.SetPermissionAsync(null, "grant"));
        Assert.Equal(
            "device.setPermission permission must be one of camera, microphone, photos, contacts, notifications, calendar, location, reminders, motion, siri, media-library, got type undefined",
            undefinedName.Message
        );
        Assert.Equal(before, h.Fake.Calls.Count);
        await device.SetPermissionAsync("camera", "deny");
        Assert.Equal(["settings.update"], h.Fake.Methods().Skip(before));
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
            ("settings.update", """{"platform":"android","setting":"airplane","state":"off"}""")
        );
    }

    [Fact]
    public async Task Resets_the_simulator_keychain_on_iOS_and_refuses_on_Android_before_any_device_command()
    {
        var ios = Harness.Create();
        var iosSession = await OpenAttempt(ios);
        var before = ios.Fake.Calls.Count;
        await iosSession.Device.ClearKeychainAsync();
        JsonAssert.Calls(
            ios.Fake.Calls,
            before,
            ("settings.update", """{"platform":"ios","setting":"reset-keychain","state":"clear"}""")
        );

        var android = Android("com.example.app");
        var androidSession = await OpenAttempt(android);
        var count = android.Fake.Calls.Count;
        var refused = await Assert.ThrowsAsync<EngineException>(() => androidSession.Device.ClearKeychainAsync());
        Assert.Equal("UNSUPPORTED_CAPABILITY", refused.Code);
        Assert.Equal(count, android.Fake.Calls.Count);
    }

    [Theory]
    [InlineData(HardwareButton.VolumeUp, "volume-up")]
    [InlineData(HardwareButton.VolumeDown, "volume-down")]
    [InlineData(HardwareButton.Power, "power")]
    public async Task DevicePressButton_presses_that_agent_device_hardware_button_on_the_selected_device(
        HardwareButton button,
        string name
    )
    {
        var h = Harness.Create(
            options => options with { Platform = MobilePlatform.Android, Device = "emulator-5554" },
            true,
            app => app with { BundleId = "com.example.app" }
        );
        var session = await OpenAttempt(h);
        var before = h.Fake.Calls.Count;
        await session.Device.PressButtonAsync(button);
        JsonAssert.Calls(
            h.Fake.Calls,
            before,
            ("command.hardwareButton", $$"""{"platform":"android","serial":"emulator-5554","button":"{{name}}"}""")
        );
    }

    // Upstream also refuses text names such as "VolumeUp" and "mute"; a HardwareButton enum cannot hold those, so only a value outside the enum reaches this check.
    [Theory]
    [InlineData(3)]
    [InlineData(-1)]
    [InlineData(99)]
    public async Task Partial_DevicePressButton_refuses_a_name_outside_the_list_before_any_device_call(int value)
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        var before = h.Fake.Calls.Count;
        var failure = await Assert.ThrowsAsync<TestException>(() =>
            session.Device.PressButtonAsync((HardwareButton)value)
        );
        Assert.Equal("INVALID_ARGUMENT", failure.Code);
        Assert.Contains("not a hardware button; press one of volume-up, volume-down, power", failure.Message);
        Assert.Equal(before, h.Fake.Calls.Count);
    }

    [Fact]
    public async Task Maps_agent_devices_refusal_of_a_simulators_hardware_button_to_UNSUPPORTED_CAPABILITY_with_its_hint()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);

        // agent-device 0.21.42's own refusal on an iOS simulator, as an iPhone 18 Pro simulator returned it.
        const string hint =
            "hardware-button volume is supported on physical iPhone and iPad; iOS Simulators refuse because XCUIDevice exposes no volume press there.";
        h.Fake.Respond(DeviceMethods.CommandHardwareButton, _ =>
            throw new AgentDeviceError(
                "UNSUPPORTED_OPERATION",
                "hardware-button is not supported on this device",
                new Dictionary<string, object?> { ["reason"] = "unsupported-device-kind", ["hint"] = hint }
            )
        );
        var failure = await Assert.ThrowsAsync<EngineException>(() =>
            session.Device.PressButtonAsync(HardwareButton.VolumeUp)
        );
        Assert.Equal("UNSUPPORTED_CAPABILITY", failure.Code);
        Assert.Contains(hint, failure.Message);
        Assert.Contains("command.hardwareButton", h.Fake.Methods());
    }

    [Fact]
    public async Task Locks_and_unlocks_the_selected_device_through_agent_devices_one_lock_screen_command()
    {
        var android = Harness.Create(
            options => options with { Platform = MobilePlatform.Android, Device = "emulator-5554" },
            true,
            app => app with { BundleId = "com.example.app" }
        );
        var session = await OpenAttempt(android);
        android.Fake.Respond(DeviceMethods.CommandLockScreen, args => new JsonObject
        {
            ["action"] = "lock-screen",
            ["state"] = args?["action"]?.GetValue<string>() == "lock" ? "locked" : "unlocked",
            ["message"] = "done",
        });
        var before = android.Fake.Calls.Count;
        await session.Device.LockAsync();
        await session.Device.UnlockAsync();
        JsonAssert.Calls(
            android.Fake.Calls,
            before,
            ("command.lockScreen", """{"platform":"android","serial":"emulator-5554","action":"lock"}"""),
            ("command.lockScreen", """{"platform":"android","serial":"emulator-5554","action":"unlock"}""")
        );
    }

    [Fact]
    public async Task Maps_agent_devices_lock_screen_refusals_to_UNSUPPORTED_CAPABILITY_with_a_message_that_names_the_cause_and_the_way_out()
    {
        // agent-device 0.21.43's own refusals, as its daemon and the iOS runner raise them.
        (bool Android, bool Lock, string Message, string Reason, string Hint, string Cause)[] refusals =
        [
            (
                false,
                true,
                "Runtime operation lock-screen is unavailable",
                "unsupported-device-kind",
                "lock-screen is supported on iPhone and iPad simulators only.",
                "lock-screen is supported on iPhone and iPad simulators only."
            ),
            (
                true,
                true,
                "The screen lock of Pixel 9 is set to None, so there is no lock screen to show",
                "lock-screen-disabled",
                "Set a screen lock other than None in Settings > Security > Screen lock (Swipe needs no credential), then retry.",
                "set to None"
            ),
            (
                true,
                false,
                "The screen lock of Pixel 9 asks for a credential (pin), which lock-screen unlock does not enter",
                "lock-screen-secure",
                "unlock never enters a credential. Unlock the device by hand, or remove its screen lock in Settings > Security, then retry.",
                "asks for a credential (pin)"
            ),
            (
                false,
                false,
                "The simulator asks for a passcode, which lock-screen unlock does not enter",
                "lock-screen-secure",
                "Enter the passcode by hand, or turn it off in Settings > Face ID & Passcode, then retry.",
                "asks for a passcode"
            ),
        ];
        foreach (var refusal in refusals)
        {
            var h = refusal.Android ? Android("com.example.app") : Harness.Create();
            var session = await OpenAttempt(h);
            h.Fake.Respond(DeviceMethods.CommandLockScreen, _ => throw new AgentDeviceError(
                    "UNSUPPORTED_OPERATION",
                    refusal.Message,
                    new Dictionary<string, object?>
                    {
                        ["reason"] = refusal.Reason,
                        ["dispatched"] = "no",
                        ["hint"] = refusal.Hint,
                    }
                ));
            var failure = await Assert.ThrowsAsync<EngineException>(() =>
                refusal.Lock ? session.Device.LockAsync() : session.Device.UnlockAsync()
            );
            Assert.Equal("UNSUPPORTED_CAPABILITY", failure.Code);
            Assert.Contains(refusal.Cause, failure.Message);
            Assert.Contains($"Hint: {refusal.Hint}", failure.Message);
        }
    }

    [Fact]
    public async Task Reports_a_lock_state_the_device_never_confirmed_as_ENGINE_FAILURE_with_agent_devices_message()
    {
        var h = Android("com.example.app");
        var session = await OpenAttempt(h);
        h.Fake.Respond(DeviceMethods.CommandLockScreen, _ =>
            throw new AgentDeviceError(
                "COMMAND_FAILED",
                "Pixel 9 still reports the keyguard hidden",
                new Dictionary<string, object?> { ["reason"] = "lock-screen-unverified", ["dispatched"] = "unknown" }
            )
        );
        var failure = await Assert.ThrowsAsync<EngineException>(() => session.Device.LockAsync());
        Assert.Equal("ENGINE_FAILURE", failure.Code);
        Assert.Contains("still reports the keyguard hidden", failure.Message);
    }

    [Fact]
    public async Task Folds_an_iOS_simulator_or_an_Android_emulator_through_the_one_device_command_and_maps_a_hingeless_device_to_UNSUPPORTED_CAPABILITY()
    {
        var ios = Harness.Create();
        var iosSession = await OpenAttempt(ios);
        ios.Fake.Respond(DeviceMethods.CommandFold, _ =>
            throw new AgentDeviceError(
                "UNSUPPORTED_OPERATION",
                "fold requires a foldable iOS simulator such as iPhone Duo"
            )
        );
        var hingeless = await Assert.ThrowsAsync<EngineException>(() => iosSession.Device.FoldAsync(FoldPose.Open));
        Assert.Equal("UNSUPPORTED_CAPABILITY", hingeless.Code);

        // agent-device 0.21.25 poses a foldable Android emulator through the emulator console, so Android sends the same command instead of being refused before it.
        var android = Android("com.example.app");
        var androidSession = await OpenAttempt(android);
        var count = android.Fake.Calls.Count;
        await androidSession.Device.FoldAsync(FoldPose.HalfOpen);
        JsonAssert.Calls(android.Fake.Calls, count, ("command.fold", """{"platform":"android","pose":"half-open"}"""));
    }

    // Upstream reads the labels off the declared `openApp`, `openLink`, and `setClipboard` operations. The port records each fixture call as a step, so the labels are read off the steps.
    [Fact]
    public async Task Labels_openApp_and_openLink_steps_without_the_link_query_and_setClipboard_by_length_so_a_token_never_enters_the_report()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        var screen = new Screen(
            session.ObserveAsync,
            session.PerformAsync,
            () => CancellationToken.None,
            () => { },
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5),
            locate: session.LocateAsync
        )
        {
            Steps = new StepRecorder("attempt"),
        };
        ((IEngineFixtureHost)session).CreateFixtures(new EngineFixtureContext(screen));
        var device = session.Device;

        async Task<string> LabelOf(Func<Task> call, string? refusedAs = null)
        {
            // The labelled step is recorded whether or not the call reaches the device: a link is no app id, and a bare path no link.
            var error = await Record.ExceptionAsync(call);
            if (refusedAs is null)
            {
                Assert.Null(error);
            }
            else
            {
                Assert.Equal(refusedAs, Assert.IsType<TestException>(error).Code);
            }

            return screen.Steps!.All()[^1].Label;
        }

        Assert.Equal(
            "<link>",
            await LabelOf(
                () => device.OpenAppAsync("https://user:pw@app.example.com/magic?token=s3cret#frag"),
                "INVALID_ARGUMENT"
            )
        );
        Assert.Equal("com.apple.Preferences", await LabelOf(() => device.OpenAppAsync("com.apple.Preferences")));
        Assert.Equal("myapp://orders/42", await LabelOf(() => device.OpenLinkAsync("myapp://orders/42?ref=mail")));
        Assert.Equal(
            "https://app.example.com/magic",
            await LabelOf(() => device.OpenLinkAsync("https://user:pw@app.example.com/magic?token=s3cret#frag"))
        );
        Assert.Equal("<link>", await LabelOf(() => device.OpenLinkAsync("verify?token=s3cret"), "INVALID_ARGUMENT"));
        // Clipboard text is often a code or token the test copied, never a configured secret the ledger would redact.
        Assert.Equal("6 chars", await LabelOf(() => device.SetClipboardAsync("482913")));
    }
}
