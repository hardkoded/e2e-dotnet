// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayRealtimeModelLive.GatewayRealtimeModelLiveFixture;

namespace E2E.Decision.Tests.GatewayRealtimeModelLive;

/// <summary>Upstream's <c>describe('doCreateClientSecret')</c> of <c>gateway-realtime-model-live.test.ts</c>.</summary>
public sealed class DoCreateClientSecretTests
{
    [Fact]
    public async Task Mints_a_Live_bound_secret_and_returns_the_live_model_url()
    {
        var calls = new List<GatewayClientSecretRequest>();
        var result = await CreateTestModel(
                createClientSecret: (request, _) =>
                {
                    calls.Add(request);
                    return Task.FromResult(new GatewayClientSecret("vcst_live", 1_700_000_060));
                }
            )
            .DoCreateClientSecretAsync(new RealtimeModelV4ClientSecretOptions { ExpiresAfterSeconds = 120 });

        Assert.Equal([new GatewayClientSecretRequest("openai/gpt-live-1.1", 120, "live")], calls);
        Assert.Equal(new RealtimeModelV4ClientSecretResult("vcst_live", LiveUrl, 1_700_000_060), result);
    }

    [Fact]
    public async Task Upgrades_an_http_base_url_to_ws()
    {
        var result = await CreateTestModel(baseUrl: "http://localhost:3000/v4/ai").DoCreateClientSecretAsync(null);
        Assert.Equal("ws://localhost:3000/v4/ai/live-model?ai-model-id=openai%2Fgpt-live-1.1", result.Url);
    }
}
