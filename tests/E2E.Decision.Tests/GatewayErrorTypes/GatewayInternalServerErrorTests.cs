// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.GatewayErrorTypes;

/// <summary>Upstream's <c>describe('GatewayInternalServerError')</c> of <c>gateway-error-types.test.ts</c>.</summary>
public sealed class GatewayInternalServerErrorTests
{
    [Fact]
    public void Should_create_error_with_default_values()
    {
        var error = new GatewayInternalServerException();

        Assert.Equal("GatewayInternalServerError", error.Name);
        Assert.Equal("internal_server_error", error.Type);
        Assert.Equal("Internal server error", error.Message);
        Assert.Equal(500, error.StatusCode);
    }

    [Fact]
    public void Should_be_identifiable_via_instance_check()
    {
        var error = new GatewayInternalServerException();

        Assert.IsType<GatewayInternalServerException>(error);
        Assert.IsAssignableFrom<GatewayException>(error);
    }
}
