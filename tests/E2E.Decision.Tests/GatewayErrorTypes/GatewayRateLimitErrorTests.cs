// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.GatewayErrorTypes;

/// <summary>Upstream's <c>describe('GatewayRateLimitError')</c> of <c>gateway-error-types.test.ts</c>.</summary>
public sealed class GatewayRateLimitErrorTests
{
    [Fact]
    public void Should_create_error_with_default_values()
    {
        var error = new GatewayRateLimitException();

        Assert.Equal("GatewayRateLimitError", error.Name);
        Assert.Equal("rate_limit_exceeded", error.Type);
        Assert.Equal("Rate limit exceeded", error.Message);
        Assert.Equal(429, error.StatusCode);
    }

    [Fact]
    public void Should_be_identifiable_via_instance_check()
    {
        var error = new GatewayRateLimitException();

        Assert.IsType<GatewayRateLimitException>(error);
        Assert.IsAssignableFrom<GatewayException>(error);
    }
}
