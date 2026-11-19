// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.AsGatewayError;

/// <summary>
/// Upstream's <c>describe('asGatewayError')</c> of <c>as-gateway-error.test.ts</c>. Upstream's undici timeout codes are
/// <see cref="TimeoutException"/> here (HttpClient's timeout), and a cancellation not caused by a timeout is a caller cancellation.
/// </summary>
public sealed class AsGatewayErrorTests
{
    private static ApiCallException ApiCall(
        string message,
        Exception? cause = null,
        int? status = null,
        bool? retryable = null,
        string? body = null
    ) => new(message, status, null, retryable, cause) { ResponseBody = body };

    // caller cancellation
    [Fact]
    public void Partial_Should_rethrow_a_cancellation_unchanged() =>
        AssertRethrown(new OperationCanceledException("Request cancelled"));

    [Fact]
    public void Partial_Should_rethrow_a_task_cancellation_unchanged() =>
        AssertRethrown(new TaskCanceledException("Request cancelled"));

    [Fact]
    public void Partial_Should_rethrow_a_cancellation_with_a_caller_token_unchanged() =>
        AssertRethrown(new OperationCanceledException("Request cancelled", new CancellationToken(true)));

    [Fact]
    public void Partial_Should_rethrow_a_cancellation_with_an_inner_error_unchanged() =>
        AssertRethrown(new OperationCanceledException("Request cancelled", new InvalidOperationException()));

    private static void AssertRethrown(Exception error) =>
        Assert.Same(error, Assert.Throws(error.GetType(), () => GatewayErrors.AsGatewayError(error)));

    // timeout error detection
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

    // non-timeout errors
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

    // error properties
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

    // APICallError with timeout cause
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
