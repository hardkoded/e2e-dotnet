// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.AsGatewayError;

/// <summary>Upstream's <c>describe('error properties')</c> in <c>describe('asGatewayError')</c> of <c>as-gateway-error.test.ts</c>. Upstream's undici timeout codes are <see cref="TimeoutException"/> here, and a cancellation not caused by a timeout is a caller cancellation.</summary>
public sealed class ErrorPropertiesTests
{
    [Fact]
    public void Should_preserve_the_original_error_as_cause()
    {
        var original = new TimeoutException("timeout error");

        Assert.Same(original, GatewayErrors.AsGatewayError(original).InnerException);
    }

    [Fact]
    public void Should_set_correct_status_code_for_timeout_errors() =>
        Assert.Equal(408, GatewayErrors.AsGatewayError(new TimeoutException("timeout")).StatusCode);

    [Fact]
    public void Should_have_correct_error_type() =>
        Assert.Equal("timeout_error", GatewayErrors.AsGatewayError(new TimeoutException("timeout")).Type);
}
