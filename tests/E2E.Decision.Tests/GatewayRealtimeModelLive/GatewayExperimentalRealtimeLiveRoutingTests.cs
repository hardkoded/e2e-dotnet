// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayRealtimeModelLive.GatewayRealtimeModelLiveFixture;

namespace E2E.Decision.Tests.GatewayRealtimeModelLive;

/// <summary>Upstream's <c>describe('gateway.experimental_realtime Live routing')</c> of <c>gateway-realtime-model-live.test.ts</c>.</summary>
[Collection("GatewayProviderEnvironment")]
public sealed class GatewayExperimentalRealtimeLiveRoutingTests
{
    private const string MintUrl = "https://ai-gateway.vercel.sh/v1/realtime/client-secrets";

    private static GatewayRealtimeFactory Realtime(TestServer? server = null) =>
        new E2E.Decision.Sdk.Gateway.GatewayProvider(
            new GatewayProviderSettings { ApiKey = "vck_test-token", HttpClient = server?.Client }
        ).ExperimentalRealtime;

    [Theory]
    [InlineData("openai/gpt-live-1")]
    [InlineData("openai/gpt-live-1.1")]
    public void Creates_a_Live_model_for(string modelId)
    {
        var model = Assert.IsType<E2E.Decision.Sdk.Gateway.GatewayRealtimeModelLive>(Realtime().Create(modelId));
        Assert.Equal("gateway.live", model.Provider);
    }

    [Theory]
    [InlineData("openai/gpt-realtime-2")]
    [InlineData("google/gemini-3.8-live")]
    [InlineData("spacexai/grok-voice-3.0")]
    public void Keeps_the_model_on_the_realtime_route(string modelId)
    {
        var model = Assert.IsType<E2E.Decision.Sdk.Gateway.GatewayRealtimeModel>(Realtime().Create(modelId));
        Assert.Equal("gateway.realtime", model.Provider);
    }

    [Fact]
    public void Honors_an_explicit_api_option()
    {
        Assert.IsType<E2E.Decision.Sdk.Gateway.GatewayRealtimeModelLive>(
            Realtime().Create("openai/gpt-live-2", new GatewayRealtimeOptions { Api = "live" })
        );
        Assert.IsType<E2E.Decision.Sdk.Gateway.GatewayRealtimeModel>(
            Realtime().Create("openai/gpt-live-1.1", new GatewayRealtimeOptions { Api = "realtime" })
        );
    }

    [Fact]
    public void Rejects_an_unknown_api_option() =>
        Assert.Throws<InvalidArgumentException>(() =>
            Realtime().Create("openai/gpt-live-1.1", new GatewayRealtimeOptions { Api = "other" })
        );

    [Fact]
    public async Task Mints_a_Live_token_with_routeKind_live_and_returns_the_live_model_url()
    {
        var server = new TestServer();
        server.RespondJson(MintUrl, """{"token":"vcst_live","expiresAt":1700000060}""");

        var result = await Realtime(server)
            .GetTokenAsync(
                new GatewayRealtimeGetTokenOptions { Model = "openai/gpt-live-1.1", ExpiresAfterSeconds = 60 }
            );

        Assert.Equal(new RealtimeFactoryV4GetTokenResult("vcst_live", LiveUrl, 1_700_000_060), result);
        var call = Assert.Single(server.Calls);
        Assert.Equal(MintUrl, call.RequestUrl);
        Assert.True(
            JsonNode.DeepEquals(
                Json("""{"model":"openai/gpt-live-1.1","routeKind":"live","expiresIn":60}"""),
                call.RequestBodyJson
            )
        );
    }

    [Fact]
    public async Task Routes_getToken_by_the_api_option_and_leaves_realtime_mints_unchanged()
    {
        var server = new TestServer();
        server.RespondJson(MintUrl, """{"token":"vcst_x"}""");
        var realtime = Realtime(server);

        var live = await realtime.GetTokenAsync(
            new GatewayRealtimeGetTokenOptions { Model = "openai/gpt-live-2", Api = "live" }
        );
        var turnBased = await realtime.GetTokenAsync(
            new GatewayRealtimeGetTokenOptions { Model = "openai/gpt-live-1.1", Api = "realtime" }
        );

        Assert.Contains("/v4/ai/live-model?", live.Url, StringComparison.Ordinal);
        Assert.Contains("/v4/ai/realtime-model?", turnBased.Url, StringComparison.Ordinal);
        Assert.Collection(
            server.Calls,
            call =>
                Assert.True(
                    JsonNode.DeepEquals(
                        Json("""{"model":"openai/gpt-live-2","routeKind":"live"}"""),
                        call.RequestBodyJson
                    )
                ),
            call => Assert.True(JsonNode.DeepEquals(Json("""{"model":"openai/gpt-live-1.1"}"""), call.RequestBodyJson))
        );
    }
}
