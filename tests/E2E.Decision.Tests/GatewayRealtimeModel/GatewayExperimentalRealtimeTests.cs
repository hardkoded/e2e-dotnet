// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Sdk.Gateway.GatewayRealtimeAuth;
using static E2E.Decision.Tests.GatewayRealtimeModel.GatewayRealtimeModelFixture;

namespace E2E.Decision.Tests.GatewayRealtimeModel;

/// <summary>Upstream's <c>describe('gateway.experimental_realtime')</c> of <c>gateway-realtime-model.test.ts</c>.</summary>
[Collection("GatewayProviderEnvironment")]
public sealed class GatewayExperimentalRealtimeTests
{
    private const string MintUrl = "https://ai-gateway.vercel.sh/v1/realtime/client-secrets";

    [Fact]
    public void Creates_a_realtime_model_from_a_model_id()
    {
        var model = ProviderOn().ExperimentalRealtime.Create("openai/gpt-realtime");
        Assert.Equal("v4", model.SpecificationVersion);
        Assert.Equal("openai/gpt-realtime", model.ModelId);
        Assert.Equal("gateway.realtime", model.Provider);
    }

    [Fact]
    public async Task Mints_a_vcst_client_secret_via_v1_realtime_client_secrets_and_returns_it_with_the_wss_url()
    {
        var server = new TestServer();
        server.RespondJson(MintUrl, new { token = "vcst_minted", expiresAt = 1_700_000_060 });

        var result = await ProviderOn(server)
            .ExperimentalRealtime.GetTokenAsync(
                new GatewayRealtimeGetTokenOptions { Model = "openai/gpt-realtime", ExpiresAfterSeconds = 120 }
            );

        // Minted token (not the raw key), and the mint hit the v1 route on the gateway origin, not the realtime baseURL path (/v4/ai).
        Assert.Equal("vcst_minted", result.Token);
        Assert.Equal(1_700_000_060, result.ExpiresAt);
        Assert.Equal(RealtimeUrl, result.Url);
        var call = Assert.Single(server.Calls);
        Assert.Equal(MintUrl, call.RequestUrl);

        // The request body forwarded the model and the TTL. routeKind is omitted for realtime mints (the gateway default) so older
        // gateway deployments keep accepting them.
        var sentBody = Assert.IsType<JsonObject>(call.RequestBodyJson);
        Assert.Equal("openai/gpt-realtime", (string?)sentBody["model"]);
        Assert.Equal(120, (int?)sentBody["expiresIn"]);
        Assert.False(sentBody.ContainsKey("routeKind"));
        // Authenticated with the long-lived key.
        Assert.Equal("Bearer vck_test-token", call.RequestHeaders["authorization"]);
    }

    [Fact]
    public async Task Tolerates_a_null_expiresAt_from_the_mint_endpoint_and_omits_it_from_the_result()
    {
        var server = new TestServer();
        server.RespondJson(MintUrl, """{"token":"vcst_minted","expiresAt":null}""");

        var result = await ProviderOn(server)
            .ExperimentalRealtime.GetTokenAsync(new GatewayRealtimeGetTokenOptions { Model = "openai/gpt-realtime" });

        Assert.Equal("vcst_minted", result.Token);
        Assert.Null(result.ExpiresAt);
    }

    [Fact]
    public void Carries_teamIdOrSlug_through_realtime_WebSocket_protocols()
    {
        var model = ProviderOn(teamIdOrSlug: "my-team").ExperimentalRealtime.Create("openai/gpt-realtime");
        var config = model.GetWebSocketConfig("vcst_test-secret", RealtimeUrl);

        Assert.Equal("my-team", GetGatewayRealtimeTeamIdOrSlug(string.Join(", ", config!.Protocols!)));
    }

    [Fact]
    public void Allows_building_the_realtime_codec_model_in_browsers_no_minting()
    {
        // Building the model is just the event codec and WS-config helper, which the browser needs to drive the transport with a
        // server-minted token. No credential is touched here, so it must not throw.
        using var browser = IsBrowserRuntime.Simulate();
        var model = ProviderOn().ExperimentalRealtime.Create("openai/gpt-realtime");

        Assert.Equal("v4", model.SpecificationVersion);
        Assert.Equal("wss://x/y", model.GetWebSocketConfig("vcst_x", "wss://x/y")!.Url);
    }

    [Fact]
    public async Task Rejects_minting_getToken_in_browsers_the_credential_must_stay_server_side()
    {
        using var browser = IsBrowserRuntime.Simulate();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ProviderOn()
                .ExperimentalRealtime.GetTokenAsync(
                    new GatewayRealtimeGetTokenOptions { Model = "openai/gpt-realtime" }
                )
        );
        Assert.Matches("must be minted server-side", error.Message);
    }
}
