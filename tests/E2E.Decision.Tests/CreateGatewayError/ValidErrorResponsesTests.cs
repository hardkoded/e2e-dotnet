// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayJson;

namespace E2E.Decision.Tests.CreateGatewayError;

/// <summary>Upstream's <c>describe('Valid error responses')</c> of <c>create-gateway-error.test.ts</c>.</summary>
public sealed class ValidErrorResponsesTests
{
    [Fact]
    public void Should_create_GatewayAuthenticationError_for_authentication_error_type()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse("""{"error":{"message":"Invalid API key","type":"authentication_error"}}"""),
            401
        );

        Assert.IsType<GatewayAuthenticationException>(error);
        Assert.Contains("No authentication provided", error.Message);
        Assert.Equal(401, error.StatusCode);
        Assert.Equal("authentication_error", error.Type);
    }

    [Fact]
    public void Should_create_GatewayInvalidRequestError_for_invalid_request_error_type()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse("""{"error":{"message":"Missing required parameter","type":"invalid_request_error"}}"""),
            400
        );

        Assert.IsType<GatewayInvalidRequestException>(error);
        Assert.Equal("Missing required parameter", error.Message);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public void Should_create_GatewayForbiddenError_for_forbidden_type()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse("""{"error":{"message":"Request denied by a routing rule.","type":"forbidden"}}"""),
            403
        );

        Assert.IsType<GatewayForbiddenException>(error);
        Assert.Equal("Request denied by a routing rule.", error.Message);
        Assert.Equal(403, error.StatusCode);
        Assert.Equal("forbidden", error.Type);
        Assert.Null(((GatewayForbiddenException)error).RuleId);
    }

    [Fact]
    public void Exposes_the_ruleId_on_GatewayForbiddenError_when_present_in_param()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse(
                """{"error":{"message":"Request denied by a routing rule.","type":"forbidden","param":{"ruleId":"rule_abc123"}}}"""
            ),
            403
        );

        Assert.IsType<GatewayForbiddenException>(error);
        Assert.Equal("rule_abc123", ((GatewayForbiddenException)error).RuleId);
    }

    [Fact]
    public void Leaves_ruleId_undefined_when_the_forbidden_param_has_an_unexpected_shape()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse(
                """{"error":{"message":"Request denied by a routing rule.","type":"forbidden","param":"model"}}"""
            ),
            403
        );

        Assert.IsType<GatewayForbiddenException>(error);
        Assert.Null(((GatewayForbiddenException)error).RuleId);
    }

    [Fact]
    public void Should_create_GatewayRateLimitError_for_rate_limit_exceeded_type()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse(
                """{"error":{"message":"Rate limit exceeded. Try again later.","type":"rate_limit_exceeded"}}"""
            ),
            429
        );

        Assert.IsType<GatewayRateLimitException>(error);
        Assert.Equal("Rate limit exceeded. Try again later.", error.Message);
        Assert.Equal(429, error.StatusCode);
    }

    [Fact]
    public void Should_create_GatewayModelNotFoundError_for_model_not_found_type()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse(
                """{"error":{"message":"Model not available","type":"model_not_found","param":{"modelId":"gpt-ai-sdk-test"}}}"""
            ),
            404
        );

        Assert.IsType<GatewayModelNotFoundException>(error);
        Assert.Equal("Model not available", error.Message);
        Assert.Equal(404, error.StatusCode);
        Assert.Equal("gpt-ai-sdk-test", ((GatewayModelNotFoundException)error).ModelId);
    }

    [Fact]
    public void Should_create_GatewayNotFoundError_for_not_found_type()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse("""{"error":{"message":"Async job not found.","type":"not_found"}}"""),
            404
        );

        Assert.IsType<GatewayNotFoundException>(error);
        Assert.Equal("Async job not found.", error.Message);
        Assert.Equal(404, error.StatusCode);
        Assert.Equal("not_found", error.Type);
        Assert.False(error.IsRetryable);
    }

    [Fact]
    public void Should_create_GatewayModelNotFoundError_without_modelId_for_invalid_param()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse(
                """{"error":{"message":"Model not available","type":"model_not_found","param":{"invalidField":"value"}}}"""
            ),
            404
        );

        Assert.IsType<GatewayModelNotFoundException>(error);
        Assert.Null(((GatewayModelNotFoundException)error).ModelId);
    }

    [Fact]
    public void Should_create_GatewayInternalServerError_for_internal_server_error_type()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse(
                """{"error":{"message":"Internal server error occurred","type":"internal_server_error"}}"""
            ),
            500
        );

        Assert.IsType<GatewayInternalServerException>(error);
        Assert.Equal("Internal server error occurred", error.Message);
        Assert.Equal(500, error.StatusCode);
    }

    [Fact]
    public void Should_create_GatewayFailedDependencyError_for_failed_dependency_type()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse(
                """{"error":{"message":"A dependency required by the request was unavailable","type":"failed_dependency"}}"""
            ),
            424
        );

        Assert.IsType<GatewayFailedDependencyException>(error);
        Assert.Equal("failed_dependency", error.Type);
        Assert.Equal("A dependency required by the request was unavailable", error.Message);
        Assert.Equal(424, error.StatusCode);
        Assert.False(error.IsRetryable);
    }

    [Fact]
    public void Should_create_GatewayInternalServerError_for_unknown_error_type()
    {
        var error = GatewayErrors.CreateGatewayErrorFromResponse(
            GatewayJson.Parse("""{"error":{"message":"Unknown error occurred","type":"unknown_error_type"}}"""),
            500
        );

        Assert.IsType<GatewayInternalServerException>(error);
        Assert.Equal("Unknown error occurred", error.Message);
        Assert.Equal(500, error.StatusCode);
    }
}
