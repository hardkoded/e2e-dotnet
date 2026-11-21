// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Sdk.Gateway.GatewayRealtimeAuth;

namespace E2E.Decision.Tests.GatewayRealtimeAuth;

/// <summary>Upstream's <c>describe('getGatewayRealtimeAuthToken')</c> of <c>gateway-realtime-auth.test.ts</c>.</summary>
public sealed class GetGatewayRealtimeAuthTokenTests
{
    [Fact]
    public void Round_trips_a_token_produced_by_getGatewayRealtimeProtocols()
    {
        // The server receives the offered subprotocols as a comma-joined header.
        var header = string.Join(", ", GetGatewayRealtimeProtocols("vck_test-token"));
        Assert.Equal("vck_test-token", GetGatewayRealtimeAuthToken(header));
    }

    [Fact]
    public void Tolerates_arbitrary_whitespace_and_subprotocol_ordering()
    {
        var header = $"  {GatewayAuthSubprotocolPrefix}oidc-token  ,  {GatewayRealtimeSubprotocol}  ";
        Assert.Equal("oidc-token", GetGatewayRealtimeAuthToken(header));
    }

    [Fact]
    public void Returns_undefined_when_no_auth_subprotocol_is_present() =>
        Assert.Null(GetGatewayRealtimeAuthToken(GatewayRealtimeSubprotocol));

    [Fact]
    public void Returns_undefined_for_an_empty_whitespace_token() =>
        Assert.Null(GetGatewayRealtimeAuthToken(GatewayAuthSubprotocolPrefix + "   "));

    [Fact]
    public void Returns_undefined_for_missing_header_values()
    {
        Assert.Null(GetGatewayRealtimeAuthToken(null));
        Assert.Null(GetGatewayRealtimeAuthToken(""));
    }

    [Fact]
    public void Preserves_tokens_that_contain_dots_e_g_JWT_OIDC()
    {
        const string jwt = "header.payload.signature";
        var header = string.Join(",", GetGatewayRealtimeProtocols(jwt));
        Assert.Equal(jwt, GetGatewayRealtimeAuthToken(header));
    }
}
