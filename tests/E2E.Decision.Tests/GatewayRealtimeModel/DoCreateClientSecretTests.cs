// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayRealtimeModel.GatewayRealtimeModelFixture;

namespace E2E.Decision.Tests.GatewayRealtimeModel;

/// <summary>Upstream's <c>describe('doCreateClientSecret')</c> of <c>gateway-realtime-model.test.ts</c>.</summary>
public sealed class DoCreateClientSecretTests
{
    [Fact]
    public async Task Returns_the_minted_client_secret_and_a_wss_realtime_url_with_the_ai_model_id_query()
    {
        var result = await CreateTestModel().DoCreateClientSecretAsync(null);
        Assert.Equal("vcst_test-secret", result.Token);
        Assert.Equal(RealtimeUrl, result.Url);
    }

    [Fact]
    public async Task Upgrades_an_http_base_url_to_ws_local_development()
    {
        var result = await CreateTestModel("openai/gpt-realtime", "http://localhost:3000/v4/ai")
            .DoCreateClientSecretAsync(null);
        Assert.Equal("ws://localhost:3000/v4/ai/realtime-model?ai-model-id=openai%2Fgpt-realtime", result.Url);
    }

    [Fact]
    public async Task Passes_the_model_id_through_verbatim_the_gateway_owns_qualification()
    {
        var result = await CreateTestModel("gpt-realtime-2").DoCreateClientSecretAsync(null);
        // No client-side openai/ prefixing: mirrors the non-realtime routes.
        Assert.Equal("wss://ai-gateway.vercel.sh/v4/ai/realtime-model?ai-model-id=gpt-realtime-2", result.Url);
    }

    [Fact]
    public async Task Forwards_modelId_and_expiresAfterSeconds_to_the_mint_hook_and_surfaces_expiresAt()
    {
        var calls = new List<GatewayClientSecretRequest>();
        var model = CreateTestModel("openai/gpt-realtime-2", createClientSecret: (request, _) =>
        {
            calls.Add(request);
            return Task.FromResult(new GatewayClientSecret("vcst_minted", 1_700_000_060));
        });

        var result = await model.DoCreateClientSecretAsync(
            new RealtimeModelV4ClientSecretOptions { ExpiresAfterSeconds = 120 }
        );

        Assert.Equal([new GatewayClientSecretRequest("openai/gpt-realtime-2", 120)], calls);
        Assert.Equal("vcst_minted", result.Token);
        Assert.Equal(1_700_000_060, result.ExpiresAt);
    }

    [Fact]
    public async Task Omits_expiresAt_when_the_mint_hook_does_not_return_one()
    {
        var result = await CreateTestModel().DoCreateClientSecretAsync(null);
        Assert.Null(result.ExpiresAt);
    }
}
