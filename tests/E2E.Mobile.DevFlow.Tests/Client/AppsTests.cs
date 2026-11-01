// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Mobile.DevFlow.Tests.Client;

public sealed class AppsTests
{
    private const string App = FakeAgent.PackageId;

    /// <summary>A client with a launcher over the fake tools, as a worker with a binding builds it.</summary>
    private static (DevFlowDeviceClient Client, FakeAgent Agent, FakeTools Tools) WithLauncher(MobilePlatform platform = MobilePlatform.Ios)
    {
        var agent = new FakeAgent();
        var tools = new FakeTools();
        var options = new DevFlowAgentsOptions();
        var discovery = new AgentDiscovery(agent.Http, options) { Delay = static (_, _) => Task.CompletedTask };
        var device = platform == MobilePlatform.Ios ? new SimctlLauncher(tools, FakeTools.Udid, "iPhone 17") : (IAppLauncher)new AdbLauncher(tools, FakeTools.Serial, "emulator");
        var http = new DevFlowAgentClient(agent.Http, discovery.ResolveAsync, DevFlow.Lease, FakeAgent.Agent);
        return (new DevFlowDeviceClient(http, platform, new AppControl(device, discovery, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(20))), agent, tools);
    }

    [Fact]
    public async Task Opening_the_running_app_brings_it_forward()
    {
        var (client, agent, tools) = WithLauncher();

        var result = await client.CallAsync(DeviceMethods.AppsOpen, $$"""{"app":"{{App}}","platform":"ios"}""");

        Assert.Equal([$"xcrun simctl launch {FakeTools.Udid} {App}"], tools.Commands);
        Assert.Equal(["GET /api/v1/agent/status"], agent.Calls.Select(call => call.Route));
        Json.AssertEqual($$"""{"appBundleId":"{{App}}","appName":"MobileApp"}""", result);
    }

    [Fact]
    public async Task Boot_finds_the_agent_again_when_a_relaunch_moved_it_and_tolerates_an_app_that_is_not_running()
    {
        var (client, agent, tools) = WithLauncher();
        agent.ScriptRefused("GET", "/api/v1/agent/status", port: 9223);
        agent.ScriptJson("GET", "/api/agents", new JsonArray(new JsonObject { ["id"] = "a", ["platform"] = "iOS", ["appName"] = "MobileApp", ["port"] = 10230 }), port: DevFlowAgents.DefaultBrokerPort);
        agent.ScriptJson("GET", "/api/v1/agent/status", FakeAgent.Status(), port: 10230);

        var moved = await client.CallAsync(DeviceMethods.DevicesBoot, """{"platform":"ios"}""");

        Assert.Equal(10230, agent.Calls.Last().Uri.Port);
        Assert.Equal("MobileApp", moved!["app"]!["name"]!.GetValue<string>());
        Assert.Single(agent.LeaseCalls);

        agent.ScriptRefused("GET", "/api/v1/agent/status", port: 10230);
        agent.LeaseCalls.Clear();
        var down = await client.CallAsync(DeviceMethods.DevicesBoot, """{"platform":"ios"}""");

        Assert.Null(down);
        Assert.Empty(agent.LeaseCalls);
        Assert.Empty(tools.Commands);
    }

    [Fact]
    public async Task Opening_an_app_that_is_not_running_launches_it_and_waits_for_its_agent()
    {
        var (client, agent, tools) = WithLauncher();
        var launched = false;
        tools.Script("xcrun simctl launch", _ => { launched = true; return FakeTools.Ok(); });
        agent.Script("GET", "/api/v1/agent/status", _ => launched ? FakeAgent.Json(FakeAgent.Status()) : throw new HttpRequestException("Connection refused"));

        var result = await client.CallAsync(DeviceMethods.AppsOpen, $$"""{"app":"{{App}}"}""");

        Assert.Equal([$"xcrun simctl launch {FakeTools.Udid} {App}"], tools.Commands);
        Assert.True(agent.Calls.Count(call => call.Route == "GET /api/v1/agent/status") >= 2, "the probe before the launch and the wait after it both read the status");
        Json.AssertEqual($$"""{"appBundleId":"{{App}}","appName":"MobileApp"}""", result);
    }

    [Fact]
    public async Task A_relaunch_terminates_launches_with_the_arguments_and_finds_the_new_agent_through_the_broker()
    {
        var (client, agent, tools) = WithLauncher();
        await client.CallAsync(DeviceMethods.CaptureSnapshot, "{}");
        var relaunched = false;
        tools.Script("xcrun simctl launch", _ => { relaunched = true; return FakeTools.Ok(); });
        // The new process registers under another port: the old one refuses, the broker lists the new one.
        agent.ScriptRefused("GET", "/api/v1/agent/status", port: 9223);
        agent.Script("GET", "/api/agents", _ => relaunched
            ? FakeAgent.Json(new JsonArray(new JsonObject { ["id"] = "a", ["platform"] = "iOS", ["appName"] = "MobileApp", ["port"] = 10230 }))
            : FakeAgent.Json(new JsonArray()), port: DevFlowAgents.DefaultBrokerPort);
        agent.ScriptJson("GET", "/api/v1/agent/status", FakeAgent.Status(), port: 10230);

        await client.CallAsync(DeviceMethods.AppsOpen, $$"""{"app":"{{App}}","relaunch":true,"launchArgs":["-reset","1"]}""");
        var stale = await client.FailureAsync(DeviceMethods.InteractionsPress, """{"ref":"@btn_greet"}""");
        await client.CallAsync(DeviceMethods.CommandBack, "{}");

        Assert.Equal([$"xcrun simctl terminate {FakeTools.Udid} {App}", $"xcrun simctl launch {FakeTools.Udid} {App} -reset 1"], tools.Commands);
        Assert.Equal(DevFlowErrors.StaleRef, stale.Details!["reason"]);
        Assert.Equal(10230, agent.Calls.Last().Uri.Port);
        Assert.Equal("POST /api/v1/ui/actions/back", agent.Calls.Last().Route);
    }

    [Fact]
    public async Task A_broker_that_still_lists_the_ended_process_beside_the_new_one_is_waited_out()
    {
        var (client, agent, tools) = WithLauncher();
        var listings = 0;
        var old = new JsonObject { ["id"] = "a", ["platform"] = "iOS", ["appName"] = "MobileApp", ["port"] = 9223 };
        var fresh = new JsonObject { ["id"] = "b", ["platform"] = "iOS", ["appName"] = "MobileApp", ["port"] = 10230 };
        agent.Script("GET", "/api/agents", _ => FakeAgent.Json(++listings < 3 ? new JsonArray(old.DeepClone(), fresh.DeepClone()) : new JsonArray(fresh.DeepClone())), port: DevFlowAgents.DefaultBrokerPort);
        agent.ScriptJson("GET", "/api/v1/agent/status", FakeAgent.Status(), port: 10230);

        await client.CallAsync(DeviceMethods.AppsOpen, $$"""{"app":"{{App}}","relaunch":true}""");

        Assert.Equal(2, tools.Commands.Count);
        Assert.Equal(10230, agent.Calls.Last().Uri.Port);
        Assert.True(listings >= 3);
    }

    [Fact]
    public async Task On_Android_the_agents_port_is_forwarded_once_the_new_process_is_found()
    {
        var (client, agent, tools) = WithLauncher(MobilePlatform.Android);
        agent.ScriptJson("GET", "/api/v1/agent/status", FakeAgent.Status("android"));
        agent.ScriptJson("GET", "/api/agents", new JsonArray(new JsonObject { ["id"] = "a", ["platform"] = "Android", ["appName"] = "MobileApp", ["port"] = 10224 }), port: DevFlowAgents.DefaultBrokerPort);
        agent.ScriptJson("GET", "/api/v1/agent/status", FakeAgent.Status("android"), port: 10224);

        await client.CallAsync(DeviceMethods.AppsOpen, $$"""{"app":"{{App}}","relaunch":true,"platform":"android"}""");
        await client.CallAsync(DeviceMethods.SessionsClose);

        Assert.Equal(
            [
                $"adb -s {FakeTools.Serial} shell am force-stop {App}",
                $"adb -s {FakeTools.Serial} shell monkey -p {App} -c android.intent.category.LAUNCHER 1",
                $"adb -s {FakeTools.Serial} forward tcp:10224 tcp:10224",
                $"adb -s {FakeTools.Serial} forward --remove tcp:10224",
            ],
            tools.Commands);
    }

    [Fact]
    public async Task Launch_arguments_need_a_fresh_launch()
    {
        var (client, _, tools) = WithLauncher();

        var error = await client.FailureAsync(DeviceMethods.AppsOpen, $$"""{"app":"{{App}}","launchArgs":["-reset"]}""");

        Assert.Equal("UNSUPPORTED_OPERATION", error.Code);
        Assert.Contains("Relaunch", error.Message, StringComparison.Ordinal);
        Assert.Empty(tools.Commands);
    }

    [Fact]
    public async Task Closing_the_app_terminates_it_and_forgets_the_screen()
    {
        var (client, agent, tools) = WithLauncher();
        await client.CallAsync(DeviceMethods.CaptureSnapshot, "{}");

        await client.CallAsync(DeviceMethods.AppsClose, $$"""{"app":"{{App}}"}""");
        var stale = await client.FailureAsync(DeviceMethods.InteractionsPress, """{"ref":"@btn_greet"}""");

        Assert.Equal([$"xcrun simctl terminate {FakeTools.Udid} {App}"], tools.Commands);
        Assert.Equal(DevFlowErrors.StaleRef, stale.Details!["reason"]);
        Assert.DoesNotContain(agent.Calls, call => call.Route.EndsWith("/tap", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Installs_and_reinstalls_a_build_and_names_the_app_the_way_the_platform_does()
    {
        var (ios, _, iosTools) = WithLauncher();
        var (android, _, androidTools) = WithLauncher(MobilePlatform.Android);

        var bundle = await ios.CallAsync(DeviceMethods.AppsInstall, $$"""{"app":"{{App}}","appPath":"/build/MobileApp.app"}""");
        var package = await android.CallAsync(DeviceMethods.AppsReinstall, $$"""{"app":"{{App}}","appPath":"/build/app.apk"}""");
        var anonymous = await ios.CallAsync(DeviceMethods.AppsInstall, """{"appPath":"/build/Other.app"}""");

        Json.AssertEqual($$"""{"bundleId":"{{App}}"}""", bundle);
        Json.AssertEqual($$"""{"package":"{{App}}"}""", package);
        Json.AssertEqual("{}", anonymous);
        Assert.Equal([$"xcrun simctl install {FakeTools.Udid} /build/MobileApp.app", $"xcrun simctl install {FakeTools.Udid} /build/Other.app"], iosTools.Commands);
        Assert.Equal([$"adb -s {FakeTools.Serial} uninstall {App}", $"adb -s {FakeTools.Serial} install -r /build/app.apk"], androidTools.Commands);
    }

    [Fact]
    public async Task Home_goes_through_the_launcher()
    {
        var (android, _, tools) = WithLauncher(MobilePlatform.Android);
        var (ios, _, _) = WithLauncher();

        await android.CallAsync(DeviceMethods.CommandHome, "{}");
        var error = await ios.FailureAsync(DeviceMethods.CommandHome, "{}");

        Assert.Equal([$"adb -s {FakeTools.Serial} shell input keyevent KEYCODE_HOME"], tools.Commands);
        Assert.Equal("UNSUPPORTED_OPERATION", error.Code);
    }

    [Fact]
    public async Task Without_a_launcher_the_device_verbs_point_at_the_launch_option()
    {
        var (client, agent) = DevFlow.Over();

        var error = await client.FailureAsync(DeviceMethods.AppsClose, $$"""{"app":"{{App}}"}""");

        Assert.Equal("UNSUPPORTED_OPERATION", error.Code);
        Assert.Contains("DevFlowAgentsOptions.Launch", error.Message, StringComparison.Ordinal);
        Assert.Empty(agent.Calls);
    }
}
