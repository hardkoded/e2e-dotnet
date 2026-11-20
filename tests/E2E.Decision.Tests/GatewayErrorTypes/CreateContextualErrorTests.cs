// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.GatewayErrorTypes;

/// <summary>Upstream's <c>describe('createContextualError')</c> in <c>describe('GatewayAuthenticationError')</c> of <c>gateway-error-types.test.ts</c>.</summary>
public sealed class CreateContextualErrorTests
{
    [Fact]
    public void Should_create_error_for_invalid_API_key_only()
    {
        var error = GatewayAuthenticationException.CreateContextualError(true, false);

        Assert.Contains("Invalid API key", error.Message);
        Assert.Contains("vercel.com/d?to=", error.Message);
        Assert.Equal(401, error.StatusCode);
    }

    [Fact]
    public void Should_create_error_for_invalid_OIDC_token_only()
    {
        var error = GatewayAuthenticationException.CreateContextualError(false, true);

        Assert.Contains("Invalid OIDC token", error.Message);
        Assert.Contains("npx vercel link", error.Message);
        Assert.Equal(401, error.StatusCode);
    }

    [Fact]
    public void Should_create_error_for_no_authentication_provided()
    {
        var error = GatewayAuthenticationException.CreateContextualError(false, false);

        Assert.Contains("No authentication provided", error.Message);
        Assert.Contains("Option 1", error.Message);
        Assert.Contains("Option 2", error.Message);
        Assert.Equal(401, error.StatusCode);
    }

    [Fact]
    public void Should_prioritize_API_key_error_when_both_were_provided()
    {
        var error = GatewayAuthenticationException.CreateContextualError(true, true);

        Assert.Contains("Invalid API key", error.Message);
        Assert.Contains("vercel.com/d?to=", error.Message);
        Assert.Equal(401, error.StatusCode);
    }

    [Fact]
    public void Should_create_error_for_neither_provided_legacy_test()
    {
        var error = GatewayAuthenticationException.CreateContextualError(false, false);

        Assert.Contains("No authentication provided", error.Message);
        Assert.Contains("Option 1", error.Message);
        Assert.Contains("Option 2", error.Message);
        Assert.Equal(401, error.StatusCode);
    }
}
