// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Sdk.Gateway.GatewayRealtimeAuth;

namespace E2E.Decision.Tests.GatewayRealtimeAuth;

/// <summary>Upstream's <c>describe('getGatewayRealtimeProtocols')</c> of <c>gateway-realtime-auth.test.ts</c>.</summary>
public sealed class GetGatewayRealtimeProtocolsTests
{
    [Fact]
    public void Offers_the_marker_subprotocol_and_the_prefixed_auth_token() =>
        Assert.Equal(
            [GatewayRealtimeSubprotocol, GatewayAuthSubprotocolPrefix + "vck_test-token"],
            GetGatewayRealtimeProtocols("vck_test-token")
        );

    [Fact]
    public void Offers_encoded_team_scoping_when_provided()
    {
        var protocols = GetGatewayRealtimeProtocols("vck_test-token", "team/with/slashes");

        Assert.Equal(3, protocols.Count);
        var teamProtocol = protocols[2];
        Assert.StartsWith(GatewayTeamSubprotocolPrefix, teamProtocol, StringComparison.Ordinal);
        Assert.Matches(@"^[A-Za-z0-9_-]+\z", teamProtocol[GatewayTeamSubprotocolPrefix.Length..]);
        Assert.Equal("team/with/slashes", GetGatewayRealtimeTeamIdOrSlug(string.Join(", ", protocols)));
    }
}
