// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using E2E.Mobile;

namespace E2E.TestMu.Mobile.Tests.Client;

public sealed class SessionAndErrorTests
{
    [Fact]
    public async Task Boot_reads_the_window_to_prove_the_session_is_alive()
    {
        var (client, hub, _) = Appium.Over();

        await client.CallAsync(DeviceMethods.DevicesBoot, """{"platform":"ios"}""");

        var call = Assert.Single(hub.Calls);
        Assert.Equal("GET window/rect", $"{call.Method} {call.Command}");
    }

    [Fact]
    public async Task Boot_on_a_session_the_hub_dropped_is_session_not_found()
    {
        var (client, hub, _) = Appium.Over();
        hub.Script("GET", "window/rect", _ => FakeAppium.Failure(HttpStatusCode.NotFound, "invalid session id", "session expired"));

        var error = await client.FailureAsync(DeviceMethods.DevicesBoot);

        Assert.Equal("SESSION_NOT_FOUND", error.Code);
        Assert.Equal("INVALID_STATE", Appium.EngineCode(error));
    }

    [Fact]
    public async Task Prepare_and_the_lists_send_nothing()
    {
        var (client, hub, _) = Appium.Over();

        await client.CallAsync(DeviceMethods.CommandPrepare, """{"action":"ios-runner"}""");
        var devices = await client.CallAsync(DeviceMethods.DevicesList, """{"platform":"ios"}""");
        var sessions = await client.CallAsync(DeviceMethods.SessionsList);

        Assert.Empty(hub.Calls);
        Assert.Empty(devices!.AsArray());
        Assert.Empty(sessions!.AsArray());
    }

    [Fact]
    public async Task Closing_the_engine_session_leaves_the_device_session_alone()
    {
        var (client, hub, _) = Appium.Over();

        await client.CallAsync(DeviceMethods.SessionsClose);

        Assert.Empty(hub.Calls);
    }

    [Theory]
    [InlineData(DeviceMethods.AppsInstall, """{"app":"com.example","appPath":"/build/App.ipa"}""")]
    [InlineData(DeviceMethods.AppsReinstall, """{"app":"com.example","appPath":"/build/App.ipa"}""")]
    [InlineData(DeviceMethods.SettingsUpdate, """{"setting":"permission","permission":"camera","state":"grant"}""")]
    [InlineData(DeviceMethods.SettingsUpdate, """{"setting":"appearance","state":"dark"}""")]
    [InlineData(DeviceMethods.SettingsUpdate, """{"setting":"faceid","state":"match"}""")]
    [InlineData(DeviceMethods.SettingsUpdate, """{"setting":"location","state":"set"}""")]
    [InlineData(DeviceMethods.SettingsUpdate, """{"setting":"clear-app-state","app":"com.example"}""")]
    [InlineData(DeviceMethods.SettingsUpdate, """{"setting":"reset-keychain","state":"clear"}""")]
    [InlineData(DeviceMethods.InteractionsHover, """{"ref":"@e1"}""")]
    [InlineData(DeviceMethods.CommandFold, """{"pose":"open"}""")]
    [InlineData(DeviceMethods.CommandAppState, "{}")]
    [InlineData(DeviceMethods.LeasesAllocate, "{}")]
    [InlineData("nonsense.method", "{}")]
    public async Task Refuses_what_it_does_not_do_as_an_unsupported_capability_without_touching_the_device(string method, string options)
    {
        var (client, hub, _) = await Appium.ReadAsync();

        var error = await client.FailureAsync(method, options);

        Assert.Equal("UNSUPPORTED_OPERATION", error.Code);
        Assert.Equal("UNSUPPORTED_CAPABILITY", Appium.EngineCode(error));
        Assert.Empty(hub.Calls);
    }

    [Fact]
    public async Task A_missing_element_is_a_stale_ref_the_engine_observes_again_for()
    {
        var (client, hub, _) = Appium.Over();
        hub.Script("POST", "alert/accept", _ => FakeAppium.Failure(HttpStatusCode.NotFound, "no such element", "gone"));

        var error = await client.FailureAsync(DeviceMethods.CommandAlert, """{"action":"accept"}""");

        Assert.Equal("COMMAND_FAILED", error.Code);
        Assert.Equal("ref_not_found", error.Details!["reason"]);
        Assert.Equal("NODE_STALE", Assert.IsType<EngineException>(DeviceErrors.StaleOr(error, "alert")).Code);
    }

    [Theory]
    [InlineData("unknown command")]
    [InlineData("unknown method")]
    [InlineData("unsupported operation")]
    public async Task A_command_the_hub_does_not_know_is_unsupported(string code)
    {
        var (client, hub, _) = Appium.Over();
        hub.Script("POST", "execute/sync", _ => FakeAppium.Failure(HttpStatusCode.BadRequest, code, "no such method"));

        var error = await client.FailureAsync(DeviceMethods.CommandHome);

        Assert.Equal("UNSUPPORTED_OPERATION", error.Code);
        Assert.Equal("UNSUPPORTED_CAPABILITY", Appium.EngineCode(error));
    }

    [Fact]
    public async Task Rejected_credentials_say_so()
    {
        var (client, hub, _) = Appium.Over();
        hub.Script("GET", "window/rect", _ => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("Unauthorized") });

        var error = await client.FailureAsync(DeviceMethods.DevicesBoot);

        Assert.Equal("COMMAND_FAILED", error.Code);
        Assert.Contains("rejected the credentials", error.Message, StringComparison.Ordinal);
        Assert.Equal("ENGINE_FAILURE", Appium.EngineCode(error));
    }

    [Fact]
    public async Task Any_other_failure_is_a_command_failure_that_keeps_the_hubs_words()
    {
        var (client, hub, _) = Appium.Over();
        hub.Script("POST", "actions", _ => FakeAppium.Failure(HttpStatusCode.InternalServerError, "unknown error", "the device is busy"));

        var error = await client.FailureAsync(DeviceMethods.InteractionsPress, """{"x":1,"y":2}""");

        Assert.Equal("COMMAND_FAILED", error.Code);
        Assert.Contains("the device is busy", error.Message, StringComparison.Ordinal);
        Assert.Equal("unknown error", error.Details!["webdriverError"]);
    }

    [Fact]
    public async Task An_unreachable_hub_is_a_command_failure()
    {
        var driver = new WebDriverClient(new HttpClient(new Down()), FakeAppium.Hub, "sess-1", null);
        var client = new AppiumDeviceClient(driver, MobilePlatform.Ios);

        var error = await client.FailureAsync(DeviceMethods.DevicesBoot);

        Assert.Equal("COMMAND_FAILED", error.Code);
    }

    private sealed class Down : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection refused");
    }
}
