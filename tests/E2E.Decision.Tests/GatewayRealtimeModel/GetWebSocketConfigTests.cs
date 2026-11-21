// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Sdk.Gateway.GatewayRealtimeAuth;
using static E2E.Decision.Tests.GatewayRealtimeModel.GatewayRealtimeModelFixture;

namespace E2E.Decision.Tests.GatewayRealtimeModel;

/// <summary>Upstream's <c>describe('getWebSocketConfig')</c> of <c>gateway-realtime-model.test.ts</c>.</summary>
public sealed class GetWebSocketConfigTests
{
    [Fact]
    public void Smuggles_the_bearer_token_through_the_subprotocol_browser_has_no_header_channel()
    {
        var config = CreateTestModel().GetWebSocketConfig("vck_test-token", RealtimeUrl);
        Assert.Equal(RealtimeUrl, config.Url);
        Assert.Equal([GatewayRealtimeSubprotocol, GatewayAuthSubprotocolPrefix + "vck_test-token"], config.Protocols);
    }

    [Fact]
    public void Smuggles_optional_team_scoping_through_the_subprotocol()
    {
        var config = CreateTestModel(
                "openai/gpt-realtime",
                "https://ai-gateway.vercel.sh/v4/ai",
                "vck_test-token",
                "team_123"
            )
            .GetWebSocketConfig("vck_test-token", RealtimeUrl);

        Assert.Equal("team_123", GetGatewayRealtimeTeamIdOrSlug(string.Join(", ", config.Protocols!)));
    }
}
