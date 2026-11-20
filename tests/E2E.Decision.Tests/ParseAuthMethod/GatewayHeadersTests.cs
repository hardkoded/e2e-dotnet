// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.ParseAuthMethod;

/// <summary>Upstream's <c>describe('gateway headers')</c> of <c>parse-auth-method.test.ts</c>.</summary>
public sealed class GatewayHeadersTests
{
    [Fact]
    public void Should_export_the_correct_auth_method_header_name() =>
        Assert.Equal("ai-gateway-auth-method", GatewayHeaders.GatewayAuthMethodHeader);

    [Fact]
    public void Should_export_the_correct_Vercel_AI_Gateway_team_header_name() =>
        Assert.Equal("x-vercel-ai-gateway-team", GatewayHeaders.VercelAiGatewayTeamHeader);
}
