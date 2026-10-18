// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;

namespace E2E.Mobile.DevFlow.Tests.Client;

public sealed class SessionAndErrorTests
{
    [Fact]
    public async Task Boot_reads_the_status_and_claims_the_lease()
    {
        var (client, agent) = DevFlow.Over(platform: null);

        await client.CallAsync(DeviceMethods.DevicesBoot, """{"platform":"ios"}""");

        Assert.Equal(["GET /api/v1/agent/status"], agent.Calls.Select(call => call.Route));
        Json.AssertEqual("""{"action":"claim","leaseId":"lease-1","holderKind":"e2e","label":"e2e mobileapp slot 1 of 1"}""", Assert.Single(agent.LeaseCalls).Body);
    }

    [Fact]
    public async Task Boot_fails_the_session_when_another_host_holds_the_lease()
    {
        var (client, agent) = DevFlow.Over();
        agent.ScriptLeaseHeldBy("inspector", "VS Code");

        var error = await client.FailureAsync(DeviceMethods.DevicesBoot, """{"platform":"ios"}""");

        Assert.Equal("COMMAND_FAILED", error.Code);
        Assert.Contains("inspector (VS Code)", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_client_without_a_lease_identity_claims_nothing()
    {
        var agent = new FakeAgent();
        var client = new DevFlowDeviceClient(new DevFlowAgentClient(agent.Http, FakeAgent.Agent, lease: null), MobilePlatform.Ios);

        await client.CallAsync(DeviceMethods.DevicesBoot, """{"platform":"ios"}""");
        await client.CallAsync(DeviceMethods.CommandBack, "{}");

        Assert.Empty(agent.LeaseCalls);
        Assert.Null(agent.Calls[1].Header("X-DevFlow-Lease"));
    }

    [Fact]
    public async Task Boot_refuses_an_agent_of_another_platform()
    {
        var (client, _) = DevFlow.Over(platform: null);

        var error = await client.FailureAsync(DeviceMethods.DevicesBoot, """{"platform":"android"}""");

        Assert.Equal("COMMAND_FAILED", error.Code);
        Assert.Contains("runs a ios app", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Boot_with_no_agent_listening_is_session_not_found()
    {
        var (client, agent) = DevFlow.Over();
        agent.ScriptRefused("GET", "/api/v1/agent/status");

        var error = await client.FailureAsync(DeviceMethods.DevicesBoot, """{"platform":"ios"}""");

        Assert.Equal("SESSION_NOT_FOUND", error.Code);
        Assert.Equal("INVALID_STATE", DevFlow.EngineCode(error));
        Assert.Contains("is the app running", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Prepare_the_lists_and_the_session_close_send_nothing()
    {
        var (client, agent) = DevFlow.Over();

        await client.CallAsync(DeviceMethods.CommandPrepare, """{"action":"ios-runner"}""");
        var devices = await client.CallAsync(DeviceMethods.DevicesList, """{"platform":"ios"}""");
        var sessions = await client.CallAsync(DeviceMethods.SessionsList);
        await client.CallAsync(DeviceMethods.SessionsClose);

        Assert.Empty(agent.Calls);
        Assert.Empty(devices!.AsArray());
        Assert.Empty(sessions!.AsArray());
    }

    [Fact]
    public async Task Opening_the_app_the_agent_runs_in_answers_its_identity()
    {
        var (client, agent) = DevFlow.Over();

        var result = await client.CallAsync(DeviceMethods.AppsOpen, $$"""{"app":"{{FakeAgent.PackageId}}","platform":"ios"}""");

        Assert.Equal(["GET /api/v1/agent/status"], agent.Calls.Select(call => call.Route));
        Json.AssertEqual($$"""{"appBundleId":"{{FakeAgent.PackageId}}","appName":"MobileApp"}""", result);
    }

    [Fact]
    public async Task Opening_another_app_than_the_one_running_fails()
    {
        var (client, _) = DevFlow.Over();

        var error = await client.FailureAsync(DeviceMethods.AppsOpen, """{"app":"com.example.other"}""");

        Assert.Equal("COMMAND_FAILED", error.Code);
        Assert.Contains("not com.example.other", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData($$"""{"app":"{{FakeAgent.PackageId}}","relaunch":true}""")]
    [InlineData($$"""{"app":"{{FakeAgent.PackageId}}","launchArgs":["-reset"]}""")]
    [InlineData("""{"url":"myapp://orders/42"}""")]
    [InlineData("""{"app":"https://example.com/"}""")]
    public async Task A_relaunch_launch_arguments_and_links_are_not_supported_yet(string options)
    {
        var (client, agent) = DevFlow.Over();

        var error = await client.FailureAsync(DeviceMethods.AppsOpen, options);

        Assert.Equal("UNSUPPORTED_OPERATION", error.Code);
        Assert.Empty(agent.Calls);
    }

    [Fact]
    public async Task The_foreground_app_is_the_one_the_agent_runs_in()
    {
        var (client, _) = DevFlow.Over();

        var state = await client.CallAsync(DeviceMethods.CommandAppState, "{}");

        Json.AssertEqual($$"""{"appBundleId":"{{FakeAgent.PackageId}}","appName":"MobileApp"}""", state);
    }

    [Fact]
    public async Task The_appearance_sets_the_apps_theme()
    {
        var (client, agent) = DevFlow.Over();

        await client.CallAsync(DeviceMethods.SettingsUpdate, """{"setting":"appearance","state":"dark","platform":"ios"}""");

        var call = Assert.Single(agent.Calls);
        Assert.Equal("PUT /api/v1/device/app/theme", call.Route);
        Json.AssertEqual("""{"theme":"dark"}""", call.Body);
    }

    [Theory]
    [InlineData(DeviceMethods.AppsInstall, """{"app":"com.example","appPath":"/build/App.app"}""")]
    [InlineData(DeviceMethods.AppsReinstall, """{"app":"com.example","appPath":"/build/App.app"}""")]
    [InlineData(DeviceMethods.AppsClose, """{"app":"com.example"}""")]
    [InlineData(DeviceMethods.SettingsUpdate, """{"setting":"permission","permission":"camera","state":"grant"}""")]
    [InlineData(DeviceMethods.SettingsUpdate, """{"setting":"wifi","state":"off"}""")]
    [InlineData(DeviceMethods.SettingsUpdate, """{"setting":"location","state":"set"}""")]
    [InlineData(DeviceMethods.SettingsUpdate, """{"setting":"clear-app-state","app":"com.example"}""")]
    [InlineData(DeviceMethods.CommandAlert, """{"action":"accept"}""")]
    [InlineData(DeviceMethods.CommandHome, "{}")]
    [InlineData(DeviceMethods.CommandOrientation, """{"orientation":"landscape-left"}""")]
    [InlineData(DeviceMethods.CommandFold, """{"pose":"open"}""")]
    [InlineData(DeviceMethods.CommandClipboard, """{"action":"read"}""")]
    [InlineData(DeviceMethods.LeasesAllocate, "{}")]
    [InlineData("nonsense.method", "{}")]
    public async Task Refuses_what_it_does_not_do_as_an_unsupported_capability_without_touching_the_agent(string method, string options)
    {
        var (client, agent) = await DevFlow.ReadAsync();

        var error = await client.FailureAsync(method, options);

        Assert.Equal("UNSUPPORTED_OPERATION", error.Code);
        Assert.Equal("UNSUPPORTED_CAPABILITY", DevFlow.EngineCode(error));
        Assert.Empty(agent.Calls);
    }

    [Fact]
    public async Task An_agent_that_does_not_answer_in_time_fails_the_command()
    {
        var (client, agent) = DevFlow.Over();
        agent.Script("GET", "/api/v1/agent/status", _ => throw new TaskCanceledException("timed out"));

        var error = await client.FailureAsync(DeviceMethods.DevicesBoot, """{"platform":"ios"}""");

        Assert.Equal("COMMAND_FAILED", error.Code);
        Assert.Equal("timeout", error.Details!["devflowError"]);
    }

    [Fact]
    public async Task A_route_this_agent_does_not_serve_is_an_unsupported_capability()
    {
        var (client, agent) = DevFlow.Over();
        agent.ScriptFailure("PUT", "/api/v1/device/app/theme", HttpStatusCode.NotFound, "Not found");

        var error = await client.FailureAsync(DeviceMethods.SettingsUpdate, """{"setting":"appearance","state":"light"}""");

        Assert.Equal("UNSUPPORTED_OPERATION", error.Code);
    }
}
