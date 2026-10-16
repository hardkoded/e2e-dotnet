// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using E2E.Mobile;

namespace E2E.TestMu.Mobile.Tests.Session;

public sealed class WebDriverClientTests
{
    private const string Key = "secret-access-key";

    private static readonly HubCredentials Credentials = new("someone", Key);

    private static TestMuSessionRequest Request => new() { Platform = MobilePlatform.Ios, DeviceName = "iPhone 15", App = "lt://APP1" };

    private static Task<WebDriverClient> CreateAsync(FakeAppium hub, TimeSpan? timeout = null) =>
        WebDriverClient.CreateSessionAsync(hub.Http, FakeAppium.Hub, Credentials, Request.ToCapabilities(Credentials), timeout ?? TimeSpan.FromSeconds(5), CancellationToken.None);

    [Fact]
    public async Task Creates_a_session_with_the_LT_Options_capabilities_and_basic_auth()
    {
        var hub = new FakeAppium();
        var driver = await CreateAsync(hub);

        Assert.Equal(FakeAppium.SessionId, driver.SessionId);
        var call = Assert.Single(hub.Calls);
        Assert.Equal("POST", call.Method.Method);
        Assert.Equal("/wd/hub/session", call.Path);
        Assert.Equal("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"someone:{Key}")), call.Authorization);
        Json.AssertEqual(
            """
            {"capabilities":{"alwaysMatch":{"LT:Options":{
              "w3c":true,"platformName":"ios","isRealMobile":true,"user":"someone","accessKey":"KEY","deviceName":"iPhone 15","app":"lt://APP1"}}}}
            """.Replace("KEY", Key, StringComparison.Ordinal),
            call.Body);
    }

    [Fact]
    public async Task Reads_the_session_id_of_the_older_protocol()
    {
        var hub = new FakeAppium();
        hub.Script("POST", "", _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"sessionId":"legacy-1","value":null}""") });

        Assert.Equal("legacy-1", (await CreateAsync(hub)).SessionId);
    }

    [Fact]
    public async Task A_create_without_a_session_id_fails()
    {
        var hub = new FakeAppium();
        hub.ScriptValue("POST", "", new JsonObject { ["capabilities"] = new JsonObject() });

        var failure = await Assert.ThrowsAsync<WebDriverException>(() => CreateAsync(hub));
        Assert.Equal("session not created", failure.Error);
    }

    [Fact]
    public async Task Sends_commands_to_the_session_with_the_same_credentials()
    {
        var hub = new FakeAppium();
        var driver = hub.Driver(Credentials);

        await driver.GetAsync("window/rect", CancellationToken.None);
        await driver.PostAsync("alert/accept", null, CancellationToken.None);
        await driver.ExecuteAsync("mobile: activateApp", new JsonObject { ["bundleId"] = "com.example" }, CancellationToken.None);

        Assert.Equal(["/wd/hub/session/sess-1/window/rect", "/wd/hub/session/sess-1/alert/accept", "/wd/hub/session/sess-1/execute/sync"], hub.Calls.Select(call => call.Path));
        Assert.All(hub.Calls, call => Assert.StartsWith("Basic ", call.Authorization, StringComparison.Ordinal));
        Json.AssertEqual("{}", hub.Calls[1].Body);
        Json.AssertEqual("""{"script":"mobile: activateApp","args":[{"bundleId":"com.example"}]}""", hub.Calls[2].Body);
    }

    [Fact]
    public async Task A_WebDriver_error_carries_its_code_and_status()
    {
        var hub = new FakeAppium();
        hub.Script("GET", "source", _ => FakeAppium.Failure(HttpStatusCode.NotFound, "no such element", "gone"));

        var failure = await Assert.ThrowsAsync<WebDriverException>(() => hub.Driver().GetAsync("source", CancellationToken.None));
        Assert.Equal("no such element", failure.Error);
        Assert.Equal(HttpStatusCode.NotFound, failure.Status);
        Assert.Contains("gone", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_error_page_that_is_not_JSON_still_fails_with_its_status()
    {
        var hub = new FakeAppium();
        hub.Script("GET", "source", _ => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("<html>401</html>") });

        var failure = await Assert.ThrowsAsync<WebDriverException>(() => hub.Driver().GetAsync("source", CancellationToken.None));
        Assert.Equal(HttpStatusCode.Unauthorized, failure.Status);
        Assert.Contains("401", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_access_key_is_never_in_an_error()
    {
        var hub = new FakeAppium();
        hub.Script("GET", "source", _ => FakeAppium.Failure(HttpStatusCode.InternalServerError, "unknown error", $"echoed {Key} back"));

        var failure = await Assert.ThrowsAsync<WebDriverException>(() => hub.Driver(Credentials).GetAsync("source", CancellationToken.None));
        Assert.DoesNotContain(Key, failure.Message, StringComparison.Ordinal);
        Assert.Contains("echoed ***** back", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_credentials_do_not_print_the_key()
    {
        Assert.DoesNotContain(Key, Credentials.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_hub_that_cannot_be_reached_is_an_unknown_error_without_the_key()
    {
        var driver = new WebDriverClient(new HttpClient(new Unreachable(Key)), FakeAppium.Hub, "sess-1", Credentials);

        var failure = await Assert.ThrowsAsync<WebDriverException>(() => driver.GetAsync("source", CancellationToken.None));
        Assert.Equal("unknown error", failure.Error);
        Assert.Null(failure.Status);
        Assert.DoesNotContain(Key, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_hub_that_does_not_answer_in_time_is_a_timeout()
    {
        var slow = new HttpClient(new Hanging());

        var failure = await Assert.ThrowsAsync<WebDriverException>(() =>
            WebDriverClient.CreateSessionAsync(slow, FakeAppium.Hub, Credentials, Request.ToCapabilities(Credentials), TimeSpan.FromMilliseconds(50), CancellationToken.None));
        Assert.Equal("timeout", failure.Error);
    }

    [Fact]
    public async Task A_cancelled_caller_is_a_cancellation_not_a_timeout()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new WebDriverClient(new HttpClient(new Hanging()), FakeAppium.Hub, "sess-1", null).GetAsync("source", cancelled.Token));
    }

    [Fact]
    public async Task Deletes_the_session_and_takes_a_session_that_is_gone_as_deleted()
    {
        var hub = new FakeAppium();
        await hub.Driver().DeleteSessionAsync(CancellationToken.None);
        hub.Script("DELETE", "", _ => FakeAppium.Failure(HttpStatusCode.NotFound, "invalid session id", "no such session"));
        await hub.Driver().DeleteSessionAsync(CancellationToken.None);

        Assert.Equal(["DELETE /wd/hub/session/sess-1", "DELETE /wd/hub/session/sess-1"], hub.Calls.Select(call => $"{call.Method} {call.Path}"));
    }

    [Fact]
    public async Task A_delete_that_fails_for_another_reason_is_reported()
    {
        var hub = new FakeAppium();
        hub.Script("DELETE", "", _ => FakeAppium.Failure(HttpStatusCode.InternalServerError, "unknown error", "boom"));

        await Assert.ThrowsAsync<WebDriverException>(() => hub.Driver().DeleteSessionAsync(CancellationToken.None));
    }

    private sealed class Unreachable(string text) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException($"no route to {text}");
    }

    private sealed class Hanging : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage();
        }
    }
}
