// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.AsGatewayError;

/// <summary>Upstream's <c>describe('APICallError with timeout cause')</c> in <c>describe('asGatewayError')</c> of <c>as-gateway-error.test.ts</c>. Upstream's undici timeout codes are <see cref="TimeoutException"/> here, and a cancellation not caused by a timeout is a caller cancellation.</summary>
public sealed class ApiCallErrorWithTimeoutCauseTests
{
    private static ApiCallException ApiCall(
        string message,
        Exception? cause = null,
        int? status = null,
        bool? retryable = null,
        string? body = null
    ) => new(message, status, null, retryable, cause) { ResponseBody = body };

    [Fact]
    public void Partial_Should_detect_timeout_when_APICallError_has_a_TimeoutException_in_cause()
    {
        var api = ApiCall("Cannot connect to API: Request timeout", new TimeoutException("Request timeout"));

        var result = GatewayErrors.AsGatewayError(api);

        Assert.IsType<GatewayTimeoutException>(result);
        Assert.Contains("Gateway request timed out", result.Message);
    }

    [Fact]
    public void Should_not_treat_APICallError_as_timeout_if_cause_is_not_timeout_related()
    {
        var api = ApiCall(
            "Cannot connect to API: Network connection failed",
            new Exception("Network connection failed"),
            500,
            body: """{"error":{"message":"Internal error","type":"internal_error"}}"""
        );

        Assert.IsNotType<GatewayTimeoutException>(GatewayErrors.AsGatewayError(api));
    }

    [Fact]
    public void Should_preserve_retryability_for_transport_errors_after_response_headers()
    {
        var api = ApiCall("Failed to process successful response", status: 200, retryable: true);

        var result = GatewayErrors.AsGatewayError(api);

        Assert.IsType<GatewayResponseException>(result);
        Assert.True(result.IsRetryable);
        Assert.Equal(200, result.StatusCode);
        Assert.Same(api, result.InnerException);
    }
}
