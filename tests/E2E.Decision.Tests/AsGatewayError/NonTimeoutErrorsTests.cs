// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.AsGatewayError;

/// <summary>Upstream's <c>describe('non-timeout errors')</c> in <c>describe('asGatewayError')</c> of <c>as-gateway-error.test.ts</c>. Upstream's undici timeout codes are <see cref="TimeoutException"/> here, and a cancellation not caused by a timeout is a caller cancellation.</summary>
public sealed class NonTimeoutErrorsTests
{
    [Fact]
    public void Should_not_treat_network_errors_as_timeout_errors()
    {
        var result = GatewayErrors.AsGatewayError(new Exception("Network error"));

        Assert.IsNotType<GatewayTimeoutException>(result);
        Assert.IsType<GatewayResponseException>(result);
        Assert.Contains("Gateway request failed: Network error", result.Message);
    }

    [Fact]
    public void Should_not_treat_connection_errors_as_timeout_errors()
    {
        var result = GatewayErrors.AsGatewayError(new HttpRequestException("Connection refused"));

        Assert.IsNotType<GatewayTimeoutException>(result);
        Assert.IsType<GatewayResponseException>(result);
    }

    [Fact]
    public void Should_pass_through_existing_GatewayError_instances()
    {
        var existing = GatewayTimeoutException.CreateTimeoutError("existing timeout");

        Assert.Same(existing, GatewayErrors.AsGatewayError(existing));
    }

    [Fact]
    public void Should_handle_non_Error_objects()
    {
        var result = GatewayErrors.AsGatewayError(new { message = "timeout occurred" });

        Assert.IsNotType<GatewayTimeoutException>(result);
        Assert.IsType<GatewayResponseException>(result);
    }

    [Fact]
    public void Should_handle_null()
    {
        var result = GatewayErrors.AsGatewayError(null);

        Assert.IsNotType<GatewayTimeoutException>(result);
        Assert.IsType<GatewayResponseException>(result);
    }

    [Fact]
    public void Should_handle_undefined()
    {
        // C# has no undefined; it is the same null as above.
        var result = GatewayErrors.AsGatewayError(null);

        Assert.IsNotType<GatewayTimeoutException>(result);
        Assert.IsType<GatewayResponseException>(result);
    }
}
