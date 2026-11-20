// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayJson;

namespace E2E.Decision.Tests.CreateGatewayError;

/// <summary>Upstream's <c>describe('authentication_error with authMethod context')</c> of <c>create-gateway-error.test.ts</c>.</summary>
public sealed class AuthenticationErrorWithAuthMethodContextTests
{
    [Fact]
    public void Should_create_contextual_error_for_API_key_authentication_failure()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse("""{"error":{"type":"authentication_error","message":"Invalid API key"}}"""),
            401,
            authMethod: "api-key"
        );

        Assert.IsType<GatewayAuthenticationException>(error);
        Assert.Contains("Invalid API key", error.Message);
        Assert.Contains("vercel.com/d?to=", error.Message);
        Assert.Equal(401, error.StatusCode);
    }

    [Fact]
    public void Should_create_contextual_error_for_OIDC_authentication_failure()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse("""{"error":{"type":"authentication_error","message":"Invalid OIDC token"}}"""),
            401,
            authMethod: "oidc"
        );

        Assert.IsType<GatewayAuthenticationException>(error);
        Assert.Contains("Invalid OIDC token", error.Message);
        Assert.Contains("npx vercel link", error.Message);
        Assert.Equal(401, error.StatusCode);
    }

    [Fact]
    public void Should_create_contextual_error_without_authMethod_context()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse("""{"error":{"type":"authentication_error","message":"Authentication failed"}}"""),
            401
        );

        Assert.IsType<GatewayAuthenticationException>(error);
        Assert.Contains("No authentication provided", error.Message);
        Assert.Equal(401, error.StatusCode);
    }
}
