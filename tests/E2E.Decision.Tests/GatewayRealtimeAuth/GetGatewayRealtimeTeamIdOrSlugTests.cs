// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Sdk.Gateway.GatewayRealtimeAuth;

namespace E2E.Decision.Tests.GatewayRealtimeAuth;

/// <summary>Upstream's <c>describe('getGatewayRealtimeTeamIdOrSlug')</c> of <c>gateway-realtime-auth.test.ts</c>.</summary>
public sealed class GetGatewayRealtimeTeamIdOrSlugTests
{
    [Fact]
    public void Round_trips_a_team_id_produced_by_getGatewayRealtimeProtocols()
    {
        var header = string.Join(", ", GetGatewayRealtimeProtocols("vck_test-token", "team_123"));
        Assert.Equal("team_123", GetGatewayRealtimeTeamIdOrSlug(header));
    }

    [Fact]
    public void Tolerates_arbitrary_whitespace_and_subprotocol_ordering()
    {
        var teamProtocol = GetGatewayRealtimeProtocols("token", "my-team")[2];
        var header = $"  {teamProtocol}  ,  {GatewayRealtimeSubprotocol}  ";
        Assert.Equal("my-team", GetGatewayRealtimeTeamIdOrSlug(header));
    }

    [Fact]
    public void Returns_undefined_when_no_team_subprotocol_is_present() =>
        Assert.Null(GetGatewayRealtimeTeamIdOrSlug(GatewayRealtimeSubprotocol));

    [Fact]
    public void Returns_undefined_for_malformed_encoded_team_values() =>
        Assert.Null(GetGatewayRealtimeTeamIdOrSlug(GatewayTeamSubprotocolPrefix + "%%%"));
}
