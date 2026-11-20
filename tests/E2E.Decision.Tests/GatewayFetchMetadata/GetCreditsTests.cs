// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayFetchMetadata.GatewayFetchMetadataFixture;

namespace E2E.Decision.Tests.GatewayFetchMetadata;

/// <summary>Upstream's <c>describe('getCredits')</c> of <c>gateway-fetch-metadata.test.ts</c>.</summary>
public sealed class GetCreditsTests
{
    [Fact]
    public async Task Should_fetch_credits_from_the_correct_endpoint()
    {
        var metadata = CreateBasicMetadataFetcher(ServerWith("""{"balance":"150.50","total_used":"75.25"}"""));

        var result = await metadata.GetCreditsAsync();

        Assert.Equal(new GatewayCreditsResponse("150.50", "75.25"), result);
    }

    [Fact]
    public async Task Should_pass_headers_correctly_to_credits_endpoint()
    {
        var server = ServerWith("""{"balance":"100.00","total_used":"50.00"}""");
        var metadata = CreateBasicMetadataFetcher(server, () =>
            new Dictionary<string, string?>
            {
                ["Authorization"] = "Bearer custom-token",
                ["Custom-Header"] = "custom-value",
            }
        );

        var result = await metadata.GetCreditsAsync();

        Assert.Equal("Bearer custom-token", server.Calls[0].RequestHeaders["authorization"]);
        Assert.Equal("custom-value", server.Calls[0].RequestHeaders["custom-header"]);
        Assert.Equal(2, server.Calls[0].RequestHeaders.Count);
        Assert.Equal(new GatewayCreditsResponse("100.00", "50.00"), result);
    }

    [Fact]
    public async Task Should_not_add_team_query_parameters_without_a_team_header()
    {
        var server = ServerWith("""{"balance":"1.00","total_used":"0.00"}""");

        await CreateBasicMetadataFetcher(server).GetCreditsAsync();

        Assert.Equal("https://api.example.com/v1/credits", server.Calls[0].RequestUrl);
    }

    [Fact]
    public async Task Should_forward_a_team_ID_from_the_team_header_as_the_teamId_query_parameter()
    {
        var server = ServerWith("""{"balance":"1.00","total_used":"0.00"}""");

        await CreateBasicMetadataFetcher(server, () =>
                new Dictionary<string, string?>
                {
                    ["Authorization"] = "Bearer vca_test-token",
                    ["x-vercel-ai-gateway-team"] = "team_123",
                }
            )
            .GetCreditsAsync();

        Assert.Equal("https://api.example.com/v1/credits?teamId=team_123", server.Calls[0].RequestUrl);
        Assert.Equal("team_123", server.Calls[0].RequestHeaders["x-vercel-ai-gateway-team"]);
    }

    [Fact]
    public async Task Should_forward_a_team_slug_from_the_team_header_as_the_slug_query_parameter()
    {
        var server = ServerWith("""{"balance":"1.00","total_used":"0.00"}""");

        await CreateBasicMetadataFetcher(server, () =>
                new Dictionary<string, string?>
                {
                    ["Authorization"] = "Bearer vca_test-token",
                    ["X-Vercel-AI-Gateway-Team"] = "team-o'brien",
                }
            )
            .GetCreditsAsync();

        var url = new Uri(server.Calls[0].RequestUrl);
        Assert.Equal("/v1/credits", url.AbsolutePath);
        var query = System.Web.HttpUtility.ParseQueryString(url.Query);
        Assert.Equal("team-o'brien", query["slug"]);
        Assert.Null(query["teamId"]);
    }

    [Fact]
    public async Task Should_handle_API_errors_for_credits_endpoint()
    {
        var server = ServerWithError(401, """{"error":{"type":"authentication_error","message":"Invalid API key"}}""");

        await Assert.ThrowsAsync<GatewayAuthenticationException>(() =>
            CreateBasicMetadataFetcher(server).GetCreditsAsync()
        );
    }

    [Fact]
    public async Task Should_handle_rate_limit_errors_for_credits_endpoint()
    {
        var server = ServerWithError(
            429,
            """{"error":{"type":"rate_limit_exceeded","message":"Rate limit exceeded"}}"""
        );

        await Assert.ThrowsAsync<GatewayRateLimitException>(() => CreateBasicMetadataFetcher(server).GetCreditsAsync());
    }

    [Fact]
    public async Task Should_handle_internal_server_errors_for_credits_endpoint()
    {
        var server = ServerWithError(
            500,
            """{"error":{"type":"internal_server_error","message":"Database unavailable"}}"""
        );

        await Assert.ThrowsAsync<GatewayInternalServerException>(() =>
            CreateBasicMetadataFetcher(server).GetCreditsAsync()
        );
    }

    [Fact]
    public async Task Should_handle_malformed_credits_response()
    {
        var metadata = CreateBasicMetadataFetcher(ServerWith("""{"balance":"not-a-number","total_used":"75.25"}"""));

        var result = await metadata.GetCreditsAsync();

        Assert.Equal(new GatewayCreditsResponse("not-a-number", "75.25"), result);
    }

    // Upstream passes a custom fetch; the port's counterpart is a custom HttpClient.
    [Fact]
    public async Task Should_use_custom_fetch_function_when_provided()
    {
        var server = ServerWith("""{"balance":"200.00","total_used":"100.50"}""");
        var metadata = CreateBasicMetadataFetcher(server);

        var result = await metadata.GetCreditsAsync();

        Assert.Equal(new GatewayCreditsResponse("200.00", "100.50"), result);
        Assert.Equal("https://api.example.com/v1/credits", server.Calls[0].RequestUrl);
        Assert.Equal("GET", server.Calls[0].RequestMethod);
        Assert.Equal("Bearer test-token", server.Calls[0].RequestHeaders["authorization"]);
    }

    [Fact]
    public async Task Should_convert_API_call_errors_to_Gateway_errors()
    {
        var server = ServerWithError(403, """{"error":{"message":"Forbidden access","type":"authentication_error"}}""");

        var error = await Assert.ThrowsAsync<GatewayAuthenticationException>(() =>
            CreateBasicMetadataFetcher(server).GetCreditsAsync()
        );

        Assert.Contains("No authentication provided", error.Message, StringComparison.Ordinal);
        Assert.Equal("authentication_error", error.Type);
        Assert.Equal(403, error.StatusCode);
    }

    [Fact]
    public async Task Should_handle_malformed_JSON_error_responses()
    {
        var server = ServerWithError(500, "{ invalid json");

        var error = await Assert.ThrowsAsync<GatewayResponseException>(() =>
            CreateBasicMetadataFetcher(server).GetCreditsAsync()
        );

        Assert.Equal(500, error.StatusCode);
        Assert.Equal("response_error", error.Type);
    }

    [Fact]
    public void Should_not_double_wrap_existing_Gateway_errors()
    {
        var existingError = new GatewayAuthenticationException("Already wrapped", 401);

        var error = GatewayErrors.AsGatewayError(existingError);

        Assert.Same(existingError, error);
        Assert.Equal("Already wrapped", error.Message);
    }

    [Fact]
    public async Task Should_preserve_error_cause_chain()
    {
        var server = ServerWithError(401, """{"error":{"message":"Token expired","type":"authentication_error"}}""");

        var error = await Assert.ThrowsAsync<GatewayAuthenticationException>(() =>
            CreateBasicMetadataFetcher(server).GetCreditsAsync()
        );

        Assert.NotNull(error.InnerException);
    }

    [Fact]
    public async Task Should_handle_empty_response()
    {
        var metadata = CreateBasicMetadataFetcher(ServerWith("""{"balance":"0.00","total_used":"0.00"}"""));

        var result = await metadata.GetCreditsAsync();

        Assert.Equal(new GatewayCreditsResponse("0.00", "0.00"), result);
    }
}
