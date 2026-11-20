// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.CreateGatewayError;

/// <summary>Upstream's <c>describe('Error response edge cases')</c> of <c>create-gateway-error.test.ts</c>.</summary>
public sealed class ErrorResponseEdgeCasesTests
{
    [Fact]
    public void Should_preserve_empty_string_messages_from_Gateway()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse("""{"error":{"message":"","type":"authentication_error"}}"""),
            401,
            "Custom default message"
        );

        Assert.Contains("No authentication provided", error.Message); // Uses contextual message
    }

    [Fact]
    public void Should_use_defaultMessage_when_response_message_is_null()
    {
        var response = GatewayJson.Parse("""{"error":{"message":null,"type":"authentication_error"}}""");

        var error = GatewayErrors.CreateGatewayErrorFromResponse(response, 401, "Custom default message");

        // When the response doesn't pass schema validation, it creates a response error
        var responseError = Assert.IsType<GatewayResponseException>(error);
        Assert.Equal("Invalid error response format: Custom default message", error.Message);

        // Verify debugging information is included
        Assert.Equal(response.GetRawText(), responseError.Response);
        Assert.NotNull(responseError.ValidationError);
    }

    [Fact]
    public void Should_handle_error_type_as_null()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse("""{"error":{"message":"Some error","type":null}}"""),
            500
        );

        Assert.IsType<GatewayInternalServerException>(error);
    }

    [Fact]
    public void Should_include_cause_in_the_created_error()
    {
        var originalCause = new Exception("Original network error");

        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse("""{"error":{"message":"Gateway timeout","type":"internal_server_error"}}"""),
            504,
            cause: originalCause
        );

        Assert.Same(originalCause, error.InnerException);
    }
}
