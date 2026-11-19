// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.GatewayErrorTypes;

/// <summary>Upstream's <c>describe('GatewayAuthenticationError')</c> of <c>gateway-error-types.test.ts</c>.</summary>
public sealed class GatewayAuthenticationErrorTests
{
    [Fact]
    public void Should_create_error_with_default_values()
    {
        var error = new GatewayAuthenticationException();

        Assert.IsAssignableFrom<Exception>(error);
        Assert.IsAssignableFrom<GatewayException>(error);
        Assert.Equal("GatewayAuthenticationError", error.Name);
        Assert.Equal("authentication_error", error.Type);
        Assert.Equal("Authentication failed", error.Message);
        Assert.Equal(401, error.StatusCode);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public void Should_create_error_with_custom_values()
    {
        var cause = new Exception("Original error");
        var error = new GatewayAuthenticationException("Custom auth failed", 403, cause);

        Assert.Equal("Custom auth failed", error.Message);
        Assert.Equal(403, error.StatusCode);
        Assert.Same(cause, error.InnerException);
    }

    [Fact]
    public void Should_be_identifiable_via_instance_check()
    {
        var error = new GatewayAuthenticationException();
        var otherError = new Exception("Not a gateway error");

        Assert.IsType<GatewayAuthenticationException>(error);
        Assert.IsNotType<GatewayAuthenticationException>(otherError);
        Assert.IsAssignableFrom<GatewayException>(error);
    }

    // createContextualError
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
