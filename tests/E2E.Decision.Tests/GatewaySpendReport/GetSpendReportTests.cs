// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Specialized;
using System.Web;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewaySpendReport.GatewaySpendReportFixture;

namespace E2E.Decision.Tests.GatewaySpendReport;

/// <summary>Upstream's <c>describe('getSpendReport')</c> of <c>gateway-spend-report.test.ts</c>.</summary>
public sealed class GetSpendReportTests
{
    private static GatewaySpendReportParams March(
        string endDate = "2026-03-25",
        string? groupBy = null,
        IReadOnlyList<string>? tags = null
    ) =>
        new()
        {
            StartDate = "2026-03-01",
            EndDate = endDate,
            GroupBy = groupBy,
            Tags = tags,
        };

    private static NameValueCollection QueryOf(TestServer server) =>
        HttpUtility.ParseQueryString(new Uri(server.Calls[0].RequestUrl).Query);

    [Fact]
    public async Task Should_fetch_from_the_correct_endpoint_with_required_params()
    {
        var server = ServerWith();

        await CreateSpendReport(server).GetSpendReportAsync(March());

        Assert.Equal("GET", server.Calls[0].RequestMethod);
        var url = new Uri(server.Calls[0].RequestUrl);
        Assert.Equal("/v1/report", url.AbsolutePath);
        Assert.Equal("2026-03-01", QueryOf(server)["start_date"]);
        Assert.Equal("2026-03-25", QueryOf(server)["end_date"]);
    }

    [Fact]
    public async Task Should_serialize_all_optional_query_params()
    {
        var server = ServerWith();

        await CreateSpendReport(server)
            .GetSpendReportAsync(
                new GatewaySpendReportParams
                {
                    StartDate = "2026-03-01",
                    EndDate = "2026-03-25",
                    GroupBy = "model",
                    DatePart = "hour",
                    UserId = "user_123",
                    Model = "anthropic/claude-sonnet-4.5",
                    Provider = "anthropic",
                    CredentialType = "byok",
                    Tags = ["production", "api"],
                }
            );

        var query = QueryOf(server);
        Assert.Equal("model", query["group_by"]);
        Assert.Equal("hour", query["date_part"]);
        Assert.Equal("user_123", query["user_id"]);
        Assert.Equal("anthropic/claude-sonnet-4.5", query["model"]);
        Assert.Equal("anthropic", query["provider"]);
        Assert.Equal("byok", query["credential_type"]);
        Assert.Equal("production,api", query["tags"]);
    }

    [Fact]
    public async Task Should_not_include_optional_params_when_not_provided()
    {
        var server = ServerWith();

        await CreateSpendReport(server).GetSpendReportAsync(March());

        var keys = QueryOf(server).AllKeys;
        foreach (
            var name in new[] { "group_by", "date_part", "user_id", "model", "provider", "credential_type", "tags" }
        )
        {
            Assert.DoesNotContain(name, keys);
        }
    }

    [Fact]
    public async Task Should_not_include_tags_when_array_is_empty()
    {
        var server = ServerWith();

        await CreateSpendReport(server).GetSpendReportAsync(March(tags: []));

        Assert.DoesNotContain("tags", QueryOf(server).AllKeys);
    }

    [Fact]
    public async Task Should_transform_snake_case_response_fields_to_camelCase()
    {
        var server = ServerWith(
            """{"results":[{"day":"2026-03-01","total_cost":12.5,"market_cost":11.0,"input_tokens":50000,"output_tokens":10000,"cached_input_tokens":5000,"cache_creation_input_tokens":2000,"reasoning_tokens":1000,"request_count":42}]}"""
        );

        var result = await CreateSpendReport(server).GetSpendReportAsync(March("2026-03-01"));

        Assert.Equal(
            new GatewaySpendReportRow
            {
                Day = "2026-03-01",
                TotalCost = 12.5,
                MarketCost = 11.0,
                InputTokens = 50000,
                OutputTokens = 10000,
                CachedInputTokens = 5000,
                CacheCreationInputTokens = 2000,
                ReasoningTokens = 1000,
                RequestCount = 42,
            },
            result.Results[0]
        );
    }

    [Fact]
    public async Task Should_transform_credential_type_to_credentialType_in_response()
    {
        var server = ServerWith("""{"results":[{"credential_type":"byok","total_cost":5.0}]}""");

        var result = await CreateSpendReport(server).GetSpendReportAsync(March(groupBy: "credential_type"));

        Assert.Equal(new GatewaySpendReportRow { CredentialType = "byok", TotalCost = 5.0 }, result.Results[0]);
        Assert.Null(typeof(GatewaySpendReportRow).GetProperty("credential_type"));
    }

    [Fact]
    public async Task Should_handle_groupBy_model_response()
    {
        var server = ServerWith(
            """{"results":[{"model":"anthropic/claude-sonnet-4.5","total_cost":10.0,"request_count":100},{"model":"openai/gpt-4o","total_cost":8.0,"request_count":50}]}"""
        );

        var result = await CreateSpendReport(server).GetSpendReportAsync(March(groupBy: "model"));

        Assert.Equal(2, result.Results.Count);
        Assert.Equal("anthropic/claude-sonnet-4.5", result.Results[0].Model);
        Assert.Equal("openai/gpt-4o", result.Results[1].Model);
    }

    [Fact]
    public async Task Should_handle_empty_results()
    {
        var server = ServerWith("""{"results":[]}""");

        var result = await CreateSpendReport(server).GetSpendReportAsync(March());

        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task Should_omit_optional_metric_fields_when_not_present()
    {
        var server = ServerWith("""{"results":[{"day":"2026-03-01","total_cost":1.5}]}""");

        var result = await CreateSpendReport(server).GetSpendReportAsync(March("2026-03-01"));

        Assert.Equal(new GatewaySpendReportRow { Day = "2026-03-01", TotalCost = 1.5 }, result.Results[0]);
        Assert.Null(result.Results[0].MarketCost);
        Assert.Null(result.Results[0].InputTokens);
    }

    [Fact]
    public async Task Should_pass_headers_correctly()
    {
        var server = ServerWith();
        var report = CreateSpendReport(server, () =>
            new Dictionary<string, string?>
            {
                ["Authorization"] = "Bearer custom-token",
                ["Custom-Header"] = "custom-value",
            }
        );

        await report.GetSpendReportAsync(March());

        Assert.Equal("Bearer custom-token", server.Calls[0].RequestHeaders["authorization"]);
        Assert.Equal("custom-value", server.Calls[0].RequestHeaders["custom-header"]);
        Assert.Equal(2, server.Calls[0].RequestHeaders.Count);
    }

    [Fact]
    public async Task Should_handle_401_authentication_errors()
    {
        var server = ServerWithError(401, """{"error":{"message":"Unauthorized","type":"authentication_error"}}""");

        var error = await Assert.ThrowsAsync<GatewayAuthenticationException>(() =>
            CreateSpendReport(server).GetSpendReportAsync(March())
        );

        Assert.Equal(401, error.StatusCode);
    }

    [Fact]
    public async Task Should_handle_429_rate_limit_errors()
    {
        var server = ServerWithError(
            429,
            """{"error":{"message":"Rate limit exceeded","type":"rate_limit_exceeded"}}"""
        );

        await Assert.ThrowsAsync<GatewayRateLimitException>(() =>
            CreateSpendReport(server).GetSpendReportAsync(March())
        );
    }

    [Fact]
    public async Task Should_handle_500_internal_server_errors()
    {
        var server = ServerWithError(
            500,
            """{"error":{"message":"Internal server error","type":"internal_server_error"}}"""
        );

        await Assert.ThrowsAsync<GatewayInternalServerException>(() =>
            CreateSpendReport(server).GetSpendReportAsync(March())
        );
    }

    [Fact]
    public async Task Should_handle_malformed_JSON_error_responses()
    {
        var server = ServerWithError(500, "{ invalid json");

        var error = await Assert.ThrowsAsync<GatewayResponseException>(() =>
            CreateSpendReport(server).GetSpendReportAsync(March())
        );

        Assert.Equal(500, error.StatusCode);
    }

    // Upstream passes a custom fetch; the port's counterpart is a custom HttpClient.
    [Fact]
    public async Task Should_use_custom_fetch_function_when_provided()
    {
        var server = ServerWith("""{"results":[{"day":"2026-03-01","total_cost":5.0}]}""");

        var result = await CreateSpendReport(server).GetSpendReportAsync(March("2026-03-01"));

        Assert.NotEmpty(server.Calls);
        Assert.Equal(5.0, result.Results[0].TotalCost);
    }
}
