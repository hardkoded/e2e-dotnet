// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.AsGatewayError;

/// <summary>Upstream's <c>describe('timeout error detection')</c> in <c>describe('asGatewayError')</c> of <c>as-gateway-error.test.ts</c>. Upstream's undici timeout codes are <see cref="TimeoutException"/> here, and a cancellation not caused by a timeout is a caller cancellation.</summary>
public sealed class TimeoutErrorDetectionTests
{
    [Fact]
    public void Partial_Should_detect_a_TimeoutException()
    {
        var result = GatewayErrors.AsGatewayError(new TimeoutException("Request timeout"));

        Assert.IsType<GatewayTimeoutException>(result);
        Assert.Contains("Request timeout", result.Message);
    }

    [Fact]
    public void Partial_Should_detect_a_second_TimeoutException() =>
        Assert.IsType<GatewayTimeoutException>(GatewayErrors.AsGatewayError(new TimeoutException("Body timeout")));

    [Fact]
    public void Partial_Should_detect_a_cancellation_caused_by_a_timeout() =>
        Assert.IsType<GatewayTimeoutException>(
            GatewayErrors.AsGatewayError(
                new TaskCanceledException("Connect timeout", new TimeoutException("Connect timeout"))
            )
        );
}
