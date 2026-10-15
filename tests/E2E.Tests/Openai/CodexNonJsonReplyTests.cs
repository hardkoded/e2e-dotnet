// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using E2E.OAuth;
using E2E.Tests.Fetch;

namespace E2E.Tests.Openai;

/// <summary>No upstream <c>it</c>: a proxy that answers 200 with an HTML page must give an <see cref="OAuthException"/>, not a raw <see cref="System.Text.Json.JsonException"/>.</summary>
[Collection(InstantDeviceFlow.Name)]
public sealed class CodexNonJsonReplyTests
{
    private const string Issuer = "https://auth.test";

    [Theory]
    [InlineData("/api/accounts/deviceauth/usercode")]
    [InlineData("/api/accounts/deviceauth/token")]
    public async Task Device_login_fails_with_FLOW_FAILED_when_a_200_reply_is_not_JSON(string brokenPath)
    {
        var issuer = new FakeApi(request => request.Uri.AbsolutePath == brokenPath
            ? (HttpStatusCode.OK, "<html>captive portal</html>")
            : (HttpStatusCode.OK, """{ "device_auth_id": "dev-1", "user_code": "ABCD", "interval": "0.001" }"""));
        var provider = new CodexProvider(new HttpClient(issuer), issuer: Issuer);

        var error = await Assert.ThrowsAsync<OAuthException>(() => provider.LoginAsync(
            new OAuthLoginCallbacks { OnAuth = _ => { }, OnPrompt = (_, _) => Task.FromResult("") },
            new OAuthLoginOptions { Device = true },
            CancellationToken.None));

        Assert.Equal(OAuthException.FlowFailed, error.Code);
    }
}
