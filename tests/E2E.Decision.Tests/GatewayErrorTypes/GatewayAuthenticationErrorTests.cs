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
}
