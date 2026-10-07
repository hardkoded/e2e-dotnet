// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Net;
using System.Web;
using E2E.OAuth;
using E2E.Tests.Fetch;

namespace E2E.Tests.Xai;

public sealed class SpaceXaiLoginTests
{
    /// <summary>Polls without waiting out the interval, as upstream does on fake timers.</summary>
    public SpaceXaiLoginTests()
    {
        DeviceFlow.Delay = (_, _) => Task.CompletedTask;
    }

    [Fact]
    public async Task Runs_the_RFC_8628_device_flow_against_auth_x_ai_and_reads_expiry_from_the_JWT()
    {
        var exp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 900;
        var token = FakeJwt.Of("{\"exp\":" + exp.ToString(CultureInfo.InvariantCulture) + "}");
        var polls = 0;
        var issuer = new FakeApi(request =>
        {
            var form = HttpUtility.ParseQueryString(request.Body);
            if (request.Uri.AbsolutePath == "/oauth2/device/code")
            {
                Assert.Equal("b1a00492-073a-47ea-816f-4c329264a828", form["client_id"]);
                Assert.Contains("grok-cli:access", form["scope"], StringComparison.Ordinal);
                Assert.Equal("my-tool", form["referrer"]);
                return (HttpStatusCode.OK, """{ "device_code": "dc", "user_code": "ABCD-EFGH", "verification_uri": "https://accounts.x.ai/oauth2/device", "verification_uri_complete": "https://accounts.x.ai/oauth2/device?user_code=ABCD-EFGH", "expires_in": 1800, "interval": 0.001 }""");
            }

            Assert.Equal("urn:ietf:params:oauth:grant-type:device_code", form["grant_type"]);
            polls++;

            // SpaceXAI answers pending with 400 and an error field.
            return polls switch
            {
                1 => (HttpStatusCode.BadRequest, """{ "error": "authorization_pending", "error_description": "User has not yet authorized" }"""),
                2 => (HttpStatusCode.BadRequest, """{ "error": "slow_down" }"""),
                _ => (HttpStatusCode.OK, "{ \"access_token\": \"" + token + "\", \"refresh_token\": \"rt-1\", \"expires_in\": 900 }"),
            };
        });
        var provider = new XaiProvider(new HttpClient(issuer), referrer: "my-tool", issuer: "https://auth.test");
        OAuthAuthInfo? shown = null;

        var credentials = await provider.LoginAsync(
            new OAuthLoginCallbacks { OnAuth = info => shown = info, OnPrompt = (_, _) => Task.FromResult("") },
            new OAuthLoginOptions(),
            CancellationToken.None);

        Assert.Equal(("https://accounts.x.ai/oauth2/device?user_code=ABCD-EFGH", "ABCD-EFGH"), (shown!.Url, shown.UserCode));
        Assert.Equal((token, "rt-1", exp * 1000), (credentials.Access, credentials.Refresh, credentials.Expires));
    }
}
