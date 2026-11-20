// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.GatewayErrorTypes;

/// <summary>Upstream's <c>describe('GatewayInvalidRequestError')</c> of <c>gateway-error-types.test.ts</c>.</summary>
public sealed class GatewayInvalidRequestErrorTests
{
    [Fact]
    public void Should_create_error_with_default_values()
    {
        var error = new GatewayInvalidRequestException();

        Assert.Equal("GatewayInvalidRequestError", error.Name);
        Assert.Equal("invalid_request_error", error.Type);
        Assert.Equal("Invalid request", error.Message);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public void Should_create_error_with_custom_values()
    {
        var error = new GatewayInvalidRequestException("Missing required field", 422);

        Assert.Equal("Missing required field", error.Message);
        Assert.Equal(422, error.StatusCode);
    }

    [Fact]
    public void Should_be_identifiable_via_instance_check()
    {
        var error = new GatewayInvalidRequestException();

        Assert.IsType<GatewayInvalidRequestException>(error);
        Assert.IsNotType<GatewayAuthenticationException>(error);
        Assert.IsAssignableFrom<GatewayException>(error);
    }
}
