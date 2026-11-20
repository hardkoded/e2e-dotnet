// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.GatewayErrorTypes;

/// <summary>Upstream's <c>describe('GatewayResponseError')</c> of <c>gateway-error-types.test.ts</c>.</summary>
public sealed class GatewayResponseErrorTests
{
    [Fact]
    public void Should_create_error_with_default_values()
    {
        var error = new GatewayResponseException();

        Assert.Equal("GatewayResponseError", error.Name);
        Assert.Equal("response_error", error.Type);
        Assert.Equal("Invalid response from Gateway", error.Message);
        Assert.Equal(502, error.StatusCode);
        Assert.Null(error.Response);
        Assert.Null(error.ValidationError);
    }

    [Fact]
    public void Should_create_error_with_response_and_validation_error_details()
    {
        var response = """{"invalidField":"value"}""";
        var validationError = "error: Required";

        var error = new GatewayResponseException(
            "Custom parsing error",
            422,
            response,
            validationError: validationError
        );

        Assert.Equal("Custom parsing error", error.Message);
        Assert.Equal(422, error.StatusCode);
        Assert.Equal(response, error.Response);
        Assert.Equal(validationError, error.ValidationError);
    }

    [Fact]
    public void Should_be_identifiable_via_instance_check()
    {
        var error = new GatewayResponseException();

        Assert.IsType<GatewayResponseException>(error);
        Assert.IsAssignableFrom<GatewayException>(error);
    }
}
