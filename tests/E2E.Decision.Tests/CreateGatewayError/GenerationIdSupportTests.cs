// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayJson;

namespace E2E.Decision.Tests.CreateGatewayError;

/// <summary>Upstream's <c>describe('generationId support')</c> of <c>create-gateway-error.test.ts</c>.</summary>
public sealed class GenerationIdSupportTests
{
    [Fact]
    public void Should_include_generationId_in_error_when_present_in_response()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse(
                """{"error":{"message":"Internal server error","type":"internal_server_error"},"generationId":"gen_01ABC123XYZ"}"""
            ),
            500
        );

        Assert.IsType<GatewayInternalServerException>(error);
        Assert.Equal("gen_01ABC123XYZ", error.GenerationId);
    }

    [Fact]
    public void Should_include_generationId_in_authentication_error()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse(
                """{"error":{"message":"Invalid API key","type":"authentication_error"},"generationId":"gen_01AUTH456"}"""
            ),
            401,
            authMethod: "api-key"
        );

        Assert.IsType<GatewayAuthenticationException>(error);
        Assert.Equal("gen_01AUTH456", error.GenerationId);
    }

    [Fact]
    public void Should_include_generationId_in_rate_limit_error()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse(
                """{"error":{"message":"Rate limit exceeded","type":"rate_limit_exceeded"},"generationId":"gen_01RATE789"}"""
            ),
            429
        );

        Assert.IsType<GatewayRateLimitException>(error);
        Assert.Equal("gen_01RATE789", error.GenerationId);
    }

    [Fact]
    public void Should_include_generationId_in_model_not_found_error()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse(
                """{"error":{"message":"Model not found","type":"model_not_found","param":{"modelId":"gpt-5"}},"generationId":"gen_01MODEL000"}"""
            ),
            404
        );

        Assert.IsType<GatewayModelNotFoundException>(error);
        Assert.Equal("gen_01MODEL000", error.GenerationId);
    }

    [Fact]
    public void Should_have_undefined_generationId_when_not_present_in_response()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse("""{"error":{"message":"Some error","type":"internal_server_error"}}"""),
            500
        );

        Assert.Null(error.GenerationId);
    }

    [Fact]
    public void Should_extract_generationId_from_malformed_response_when_possible()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse("""{"invalidField":"value","generationId":"gen_01MALFORMED"}"""),
            500
        );

        Assert.IsType<GatewayResponseException>(error);
        Assert.Equal("gen_01MALFORMED", error.GenerationId);
    }
}
