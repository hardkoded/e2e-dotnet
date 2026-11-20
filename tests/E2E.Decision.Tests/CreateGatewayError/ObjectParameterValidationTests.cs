// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.CreateGatewayError;

/// <summary>Upstream's <c>describe('Object parameter validation')</c> of <c>create-gateway-error.test.ts</c>.</summary>
public sealed class ObjectParameterValidationTests
{
    [Fact]
    public void Should_use_default_defaultMessage_when_not_provided()
    {
        var response = GatewayJson.Parse("""{"invalidField":"value"}""");

        var error = GatewayErrors.CreateGatewayErrorFromResponse(response, 500);

        Assert.IsType<GatewayResponseException>(error);
        Assert.Equal("Invalid error response format: Gateway request failed", error.Message);

        // Verify debugging information is included
        var responseError = (GatewayResponseException)error;
        Assert.Equal(response.GetRawText(), responseError.Response);
        Assert.NotNull(responseError.ValidationError);
    }

    [Fact]
    public void Should_handle_undefined_cause()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse("""{"error":{"message":"Test error","type":"authentication_error"}}"""),
            401
        );

        Assert.Null(error.InnerException);
    }
}
