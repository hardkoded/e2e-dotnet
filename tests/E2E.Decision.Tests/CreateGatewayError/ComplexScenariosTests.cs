// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.CreateGatewayError;

/// <summary>Upstream's <c>describe('Complex scenarios')</c> of <c>create-gateway-error.test.ts</c>.</summary>
public sealed class ComplexScenariosTests
{
    [Fact]
    public void Should_handle_model_not_found_with_missing_param_field()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse("""{"error":{"message":"Model not found","type":"model_not_found"}}"""),
            404
        );

        Assert.IsType<GatewayModelNotFoundException>(error);
        Assert.Null(((GatewayModelNotFoundException)error).ModelId);
    }

    [Fact]
    public void Should_handle_response_with_extra_fields()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse(
                """{"error":{"message":"Test error","type":"authentication_error","code":"AUTH_FAILED","param":null,"extraField":"should be ignored"},"metadata":"should be ignored"}"""
            ),
            401
        );

        Assert.IsType<GatewayAuthenticationException>(error);
        Assert.Contains("No authentication provided", error.Message);
    }

    [Fact]
    public void Should_preserve_error_properties_correctly()
    {
        var originalCause = new InvalidCastException("Type error");

        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse("""{"error":{"message":"Rate limit hit","type":"rate_limit_exceeded"}}"""),
            429,
            "Fallback message",
            originalCause
        );

        Assert.IsType<GatewayRateLimitException>(error);
        Assert.Equal("Rate limit hit", error.Message); // Uses response message, not default
        Assert.Equal(429, error.StatusCode);
        Assert.Same(originalCause, error.InnerException);
        Assert.Equal("GatewayRateLimitError", error.Name);
        Assert.Equal("rate_limit_exceeded", error.Type);
    }
}
