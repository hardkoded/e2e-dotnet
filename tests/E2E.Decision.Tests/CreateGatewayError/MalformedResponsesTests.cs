// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.CreateGatewayError;

/// <summary>Upstream's <c>describe('Malformed responses')</c> of <c>create-gateway-error.test.ts</c>.</summary>
public sealed class MalformedResponsesTests
{
    [Fact]
    public void Should_create_GatewayResponseError_for_completely_invalid_response()
    {
        var response = GatewayJson.Parse("""{"invalidField":"value"}""");

        var error = GatewayErrors.CreateGatewayErrorFromResponse(response, 500);

        Assert.IsType<GatewayResponseException>(error);
        Assert.Equal("Invalid error response format: Gateway request failed", error.Message);
        Assert.Equal(500, error.StatusCode);

        // Verify debugging information is included
        var responseError = (GatewayResponseException)error;
        Assert.Equal(response.GetRawText(), responseError.Response);
        Assert.NotNull(responseError.ValidationError);
    }

    [Fact]
    public void Should_create_GatewayResponseError_for_missing_error_field()
    {
        var response = GatewayJson.Parse("""{"data":"some data"}""");

        var error = GatewayErrors.CreateGatewayErrorFromResponse(response, 500, "Custom error message");

        Assert.IsType<GatewayResponseException>(error);
        Assert.Equal("Invalid error response format: Custom error message", error.Message);

        // Verify debugging information is included
        var responseError = (GatewayResponseException)error;
        Assert.Equal(response.GetRawText(), responseError.Response);
        Assert.NotNull(responseError.ValidationError);
    }

    [Fact]
    public void Should_create_GatewayResponseError_for_null_response()
    {
        var response = GatewayJson.Parse("""null""");

        var error = GatewayErrors.CreateGatewayErrorFromResponse(response, 500);

        Assert.IsType<GatewayResponseException>(error);
        Assert.Equal("Invalid error response format: Gateway request failed", error.Message);

        // Verify debugging information is included
        var responseError = (GatewayResponseException)error;
        Assert.Null(responseError.Response);
        Assert.NotNull(responseError.ValidationError);
    }

    [Fact]
    public void Should_create_GatewayResponseError_for_string_response()
    {
        var response = GatewayJson.Parse("\"Error string\"");

        var error = GatewayErrors.CreateGatewayErrorFromResponse(response, 500);

        Assert.IsType<GatewayResponseException>(error);
        Assert.Equal("Invalid error response format: Gateway request failed", error.Message);

        // Verify debugging information is included
        var responseError = (GatewayResponseException)error;
        Assert.Equal("Error string", responseError.Response);
        Assert.NotNull(responseError.ValidationError);
    }

    [Fact]
    public void Should_create_GatewayResponseError_for_array_response()
    {
        var response = GatewayJson.Parse("""["error","array"]""");

        var error = GatewayErrors.CreateGatewayErrorFromResponse(response, 500);

        Assert.IsType<GatewayResponseException>(error);
        Assert.Equal("Invalid error response format: Gateway request failed", error.Message);

        // Verify debugging information is included
        var responseError = (GatewayResponseException)error;
        Assert.Equal(response.GetRawText(), responseError.Response);
        Assert.NotNull(responseError.ValidationError);
    }
}
