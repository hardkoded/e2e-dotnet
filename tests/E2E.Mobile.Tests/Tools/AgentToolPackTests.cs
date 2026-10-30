// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Mobile.Tests.Tools;

public sealed class AgentToolPackTests
{
    private static Harness Android() => Harness.Create(options => options with { Platform = MobilePlatform.Android, Device = "emulator-5554" });

    private static Task<string> Run(IReadOnlyDictionary<string, AgentTool> tools, string name, string input) =>
        tools[name].ExecuteAsync((JsonObject)JsonNode.Parse(input)!, CancellationToken.None);

    [Fact]
    public void Scopes_every_tool_to_the_platforms_of_its_engines()
    {
        var ios = Harness.Create().Engine;
        var android = Android().Engine;
        var tools = MobileTools.Create(ios);
        Assert.Equal(["alert", "open_app", "swipe"], tools.Keys.Order());
        Assert.All(tools.Values, tool => Assert.Equal(["ios"], tool.Platforms));
        Assert.All(MobileTools.Create(ios, android).Values, tool => Assert.Equal(["ios", "android"], tool.Platforms));
    }

    [Fact]
    public async Task Dispatches_to_the_engine_whose_attempt_is_running_and_refuses_outside_an_attempt()
    {
        var iosHarness = Harness.Create();
        var androidHarness = Android();
        iosHarness.Fake.Respond(DeviceMethods.AppsOpen, _ => new JsonObject { ["appName"] = "Reminders" });
        androidHarness.Fake.Respond(DeviceMethods.AppsOpen, _ => new JsonObject { ["appName"] = "Clock" });
        var tools = MobileTools.Create(iosHarness.Engine, androidHarness.Engine);

        var idle = await Assert.ThrowsAsync<EngineException>(() => Run(tools, "alert", """{"action":"dismiss"}"""));
        Assert.Equal("INVALID_STATE", idle.Code);

        var androidSession = await androidHarness.Boot("android");
        Assert.Equal("Opened Clock.", await Run(tools, "open_app", """{"app":"Clock","relaunch":true}"""));
        Assert.Matches("Swiped", await Run(tools, "swipe", """{"from":{"x":300,"y":200},"to":{"x":20,"y":200}}"""));
        Assert.Equal("Alert dismissed.", await Run(tools, "alert", """{"action":"dismiss"}"""));
        Assert.Equal(["devices.boot", "apps.open", "interactions.swipe", "command.alert"], androidHarness.Fake.Methods());
        JsonAssert.Args("""{"app":"Clock","platform":"android","serial":"emulator-5554","relaunch":true}""", androidHarness.Fake.LastArgs("apps.open"));

        // The alert carries the worker's device, as it can run with no app open.
        JsonAssert.Args("""{"platform":"android","serial":"emulator-5554","action":"dismiss"}""", androidHarness.Fake.LastArgs("command.alert"));
        Assert.Empty(iosHarness.Fake.Methods());
        await androidSession.CloseAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

        await iosHarness.Boot("ios");
        Assert.Equal("Opened Reminders.", await Run(tools, "open_app", """{"app":"Reminders"}"""));
        JsonAssert.Args("""{"app":"Reminders","platform":"ios"}""", iosHarness.Fake.LastArgs("apps.open"));
    }

    [Fact]
    public async Task Refuses_a_link_the_model_hands_to_open_app_before_the_device_sees_it()
    {
        var h = Harness.Create();
        h.Fake.Respond(DeviceMethods.AppsOpen, _ => new JsonObject { ["appName"] = "Settings" });
        await h.Boot("ios");
        var tools = MobileTools.Create(h.Engine);
        var before = h.Fake.Calls.Count;
        foreach (var denied in new[] { "file:///etc/passwd", "data:text/html,hi", "javascript:alert(1)", "view-source:file:///etc/passwd" })
        {
            var failure = await Assert.ThrowsAsync<ConfigurationException>(() => Run(tools, "open_app", $$"""{"app":"{{denied}}"}"""));
            Assert.Equal("POLICY_DENIED", failure.Code);
        }

        foreach (var link in new[] { "https://example.com/verify", "myapp://orders/42" })
        {
            var failure = await Assert.ThrowsAsync<TestException>(() => Run(tools, "open_app", $$"""{"app":"{{link}}"}"""));
            Assert.Equal("INVALID_ARGUMENT", failure.Code);
            Assert.Contains("device.openLink", failure.Message, StringComparison.Ordinal);
        }

        Assert.Equal(before, h.Fake.Calls.Count);
        Assert.Contains("not a URL", tools["open_app"].Description, StringComparison.Ordinal);
        Assert.Equal("Opened com.apple.Preferences.", await Run(tools, "open_app", """{"app":"com.apple.Preferences"}"""));
        JsonAssert.Args("""{"app":"com.apple.Preferences","platform":"ios"}""", h.Fake.LastArgs("apps.open"));
    }

    private static async Task<(Harness Harness, Func<string, Task<string>> Open)> AttemptTools(Func<MobileApp, MobileApp>? app = null, bool pinned = true)
    {
        var h = Harness.Create(pinned: pinned, app: app);
        await h.Boot("ios");
        var tools = MobileTools.Create(h.Engine);
        return (h, input => Run(tools, "open_app", input));
    }

    [Fact]
    public async Task Returns_to_the_app_under_test_when_open_app_names_no_app()
    {
        var (h, open) = await AttemptTools(app => app with { BundleId = "dev.e2e.benchmark" });
        h.Fake.Respond(DeviceMethods.AppsOpen, _ => new JsonObject { ["appBundleId"] = "dev.e2e.benchmark" });
        Assert.Equal("Opened the app under test (dev.e2e.benchmark).", await open("{}"));
        JsonAssert.Args("""{"app":"dev.e2e.benchmark","platform":"ios"}""", h.Fake.LastArgs("apps.open"));
        Assert.Equal("Opened the app under test (dev.e2e.benchmark).", await open("""{"relaunch":true}"""));
        JsonAssert.Args("""{"app":"dev.e2e.benchmark","platform":"ios","relaunch":true}""", h.Fake.LastArgs("apps.open"));
    }

    [Fact]
    public async Task Asks_for_an_app_when_the_target_pins_none()
    {
        var (h, open) = await AttemptTools(pinned: false);
        var before = h.Fake.Calls.Count;
        var failure = await Assert.ThrowsAsync<EngineException>(() => open("{}"));
        Assert.Equal("INVALID_STATE", failure.Code);
        Assert.Contains("needs the app", failure.Message, StringComparison.Ordinal);
        Assert.Equal(before, h.Fake.Calls.Count);
    }

    [Fact]
    public async Task Points_a_name_no_installed_app_matches_at_the_app_under_test_without_CLI_advice()
    {
        var (h, open) = await AttemptTools(app => app with { BundleId = "dev.e2e.benchmark" });
        h.Fake.Respond(
            DeviceMethods.AppsOpen,
            _ => throw new AgentDeviceError(
                "APP_NOT_INSTALLED",
                "No app found matching \"e2eBenchmark\"",
                new Dictionary<string, object?> { ["hint"] = "Run apps to discover the exact installed package or bundle id, or install the app before open." }));
        var failure = await Assert.ThrowsAsync<TestException>(() => open("""{"app":"e2eBenchmark"}"""));
        Assert.Equal("INVALID_ARGUMENT", failure.Code);
        Assert.Contains("Omit app to return to the app under test", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Run apps", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_the_device_error_when_the_app_under_test_is_not_installed()
    {
        var (h, open) = await AttemptTools(app => app with { BundleId = "dev.e2e.benchmark" });
        h.Fake.Respond(
            DeviceMethods.AppsOpen,
            _ => throw new AgentDeviceError("APP_NOT_INSTALLED", "No app found matching \"dev.e2e.benchmark\""));
        var failure = await Assert.ThrowsAsync<EngineException>(() => open("{}"));
        Assert.Contains("No app found matching \"dev.e2e.benchmark\"", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Omit app", failure.Message, StringComparison.Ordinal);
    }
}
