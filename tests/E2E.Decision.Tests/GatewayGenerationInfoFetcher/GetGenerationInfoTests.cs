// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using System.Web;
using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.GatewayGenerationInfoFetcher;

/// <summary>Upstream's <c>describe('getGenerationInfo')</c> of <c>gateway-generation-info.test.ts</c>.</summary>
public sealed class GetGenerationInfoTests
{
    private const string Id = "gen_01ARZ3NDEKTSV4RRFFQ69G5FAV";
    private const string Wildcard = "https://api.example.com/*";

    // Upstream's mockGenerationResponse.
    private const string MockGenerationResponse = """
        {"data":{"id":"gen_01ARZ3NDEKTSV4RRFFQ69G5FAV","total_cost":0.00123,"market_cost":0.0011,"surcharge_cost":0.00013,"gateway_cost":0.00123,"upstream_inference_cost":0.0011,"usage":0.00123,"created_at":"2024-01-01T00:00:00.000Z","model":"gpt-4","is_byok":false,"provider_name":"openai","streamed":true,"finish_reason":"stop","latency":200,"generation_time":1500,"native_tokens_prompt":100,"native_tokens_completion":50,"native_tokens_reasoning":0,"native_tokens_cached":0,"native_tokens_cache_creation":0,"billable_web_search_calls":0}}
        """;

    private static TestServer ServerWith(string body = MockGenerationResponse)
    {
        var server = new TestServer();
        server.RespondJson(Wildcard, body);
        return server;
    }

    private static TestServer ServerWithData(Action<JsonObject> change)
    {
        var body = JsonNode.Parse(MockGenerationResponse)!;
        change(body["data"]!.AsObject());
        return ServerWith(body.ToJsonString());
    }

    // Upstream's createFetcher.
    private static E2E.Decision.Sdk.Gateway.GatewayGenerationInfoFetcher CreateFetcher(
        TestServer server,
        Func<IReadOnlyDictionary<string, string?>>? headers = null
    ) =>
        new(
            new GatewayConfig
            {
                BaseUrl = "https://api.example.com",
                Headers = _ =>
                    Task.FromResult(
                        headers?.Invoke() ?? new Dictionary<string, string?> { ["Authorization"] = "Bearer test-token" }
                    ),
                HttpClient = server.Client,
            }
        );

    private static GatewayGenerationInfoParams ById => new() { Id = Id };

    [Fact]
    public async Task Should_fetch_from_the_correct_endpoint_with_generation_ID()
    {
        var server = ServerWith();

        await CreateFetcher(server).GetGenerationInfoAsync(ById);

        Assert.Equal("GET", server.Calls[0].RequestMethod);
        var url = new Uri(server.Calls[0].RequestUrl);
        Assert.Equal("/v1/generation", url.AbsolutePath);
        Assert.Equal(Id, HttpUtility.ParseQueryString(url.Query)["id"]);
    }

    [Fact]
    public async Task Should_transform_snake_case_response_fields_to_camelCase()
    {
        var result = await CreateFetcher(ServerWith()).GetGenerationInfoAsync(ById);

        Assert.Equal(
            new GatewayGenerationInfo
            {
                Id = Id,
                TotalCost = 0.00123,
                MarketCost = 0.0011,
                SurchargeCost = 0.00013,
                GatewayCost = 0.00123,
                UpstreamInferenceCost = 0.0011,
                Usage = 0.00123,
                CreatedAt = "2024-01-01T00:00:00.000Z",
                Model = "gpt-4",
                IsByok = false,
                ProviderName = "openai",
                Streamed = true,
                FinishReason = "stop",
                Latency = 200,
                GenerationTime = 1500,
                PromptTokens = 100,
                CompletionTokens = 50,
                ReasoningTokens = 0,
                CachedTokens = 0,
                CacheCreationTokens = 0,
                BillableWebSearchCalls = 0,
            },
            result
        );
    }

    [Fact]
    public async Task Should_unwrap_the_data_envelope()
    {
        var result = await CreateFetcher(ServerWith()).GetGenerationInfoAsync(ById);

        // The result is the data object directly, not { data: ... }.
        Assert.Null(typeof(GatewayGenerationInfo).GetProperty("Data"));
        Assert.Equal(Id, result.Id);
    }

    [Fact]
    public async Task Should_not_have_snake_case_fields_in_result()
    {
        var result = await CreateFetcher(ServerWith()).GetGenerationInfoAsync(ById);

        Assert.NotNull(result);
        foreach (
            var name in new[]
            {
                "total_cost",
                "market_cost",
                "surcharge_cost",
                "gateway_cost",
                "is_byok",
                "provider_name",
                "created_at",
                "generation_time",
                "finish_reason",
            }
        )
        {
            Assert.Null(typeof(GatewayGenerationInfo).GetProperty(name));
        }
    }

    [Fact]
    public async Task Should_pass_headers_correctly()
    {
        var server = ServerWith();
        var fetcher = CreateFetcher(server, () =>
            new Dictionary<string, string?>
            {
                ["Authorization"] = "Bearer custom-token",
                ["Custom-Header"] = "custom-value",
            }
        );

        await fetcher.GetGenerationInfoAsync(ById);

        Assert.Equal("Bearer custom-token", server.Calls[0].RequestHeaders["authorization"]);
        Assert.Equal("custom-value", server.Calls[0].RequestHeaders["custom-header"]);
        Assert.Equal(2, server.Calls[0].RequestHeaders.Count);
    }

    [Fact]
    public async Task Should_handle_401_authentication_errors()
    {
        var server = new TestServer();
        server.RespondError(Wildcard, 401, """{"error":{"message":"Unauthorized","type":"authentication_error"}}""");

        var error = await Assert.ThrowsAsync<GatewayAuthenticationException>(() =>
            CreateFetcher(server).GetGenerationInfoAsync(ById)
        );

        Assert.Equal(401, error.StatusCode);
    }

    [Fact]
    public async Task Should_handle_500_internal_server_errors()
    {
        var server = new TestServer();
        server.RespondError(
            Wildcard,
            500,
            """{"error":{"message":"Failed to retrieve usage data","type":"internal_server_error"}}"""
        );

        await Assert.ThrowsAsync<GatewayInternalServerException>(() =>
            CreateFetcher(server).GetGenerationInfoAsync(ById)
        );
    }

    [Fact]
    public async Task Should_handle_malformed_JSON_error_responses()
    {
        var server = new TestServer();
        server.RespondError(Wildcard, 500, "{ invalid json");

        var error = await Assert.ThrowsAsync<GatewayResponseException>(() =>
            CreateFetcher(server).GetGenerationInfoAsync(ById)
        );

        Assert.Equal(500, error.StatusCode);
    }

    // Upstream passes a custom fetch; the port's counterpart is a custom HttpClient.
    [Fact]
    public async Task Should_use_custom_fetch_function_when_provided()
    {
        var server = ServerWith();

        var result = await CreateFetcher(server).GetGenerationInfoAsync(ById);

        Assert.NotEmpty(server.Calls);
        Assert.Equal(0.00123, result.TotalCost);
        Assert.Equal("gpt-4", result.Model);
    }

    [Fact]
    public async Task Should_encode_special_characters_in_generation_ID()
    {
        var server = ServerWith();

        await CreateFetcher(server).GetGenerationInfoAsync(ById);

        var url = new Uri(server.Calls[0].RequestUrl);
        Assert.Equal(Id, HttpUtility.ParseQueryString(url.Query)["id"]);
    }

    [Fact]
    public async Task Should_handle_BYOK_generation_response()
    {
        var server = ServerWithData(data =>
        {
            data["is_byok"] = true;
            data["upstream_inference_cost"] = 0.0009;
            data["provider_name"] = "anthropic";
            data["model"] = "claude-sonnet-4";
        });

        var result = await CreateFetcher(server).GetGenerationInfoAsync(ById);

        Assert.True(result.IsByok);
        Assert.Equal(0.0009, result.UpstreamInferenceCost);
        Assert.Equal("anthropic", result.ProviderName);
    }

    // it.each([undefined, null]): false is undefined (the field is absent), true is null.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_accept_an_unavailable_cost_breakdown(bool isNull)
    {
        var server = ServerWithData(data =>
        {
            foreach (var name in new[] { "market_cost", "surcharge_cost", "gateway_cost" })
            {
                if (isNull)
                {
                    data[name] = null;
                }
                else
                {
                    data.Remove(name);
                }
            }
        });

        var result = await CreateFetcher(server).GetGenerationInfoAsync(ById);

        Assert.Equal(0.00123, result.TotalCost);
        Assert.Null(result.MarketCost);
        Assert.Null(result.SurchargeCost);
        Assert.Null(result.GatewayCost);
    }

    [Fact]
    public async Task Should_preserve_zero_cost_values()
    {
        var server = ServerWithData(data =>
        {
            data["market_cost"] = 0;
            data["surcharge_cost"] = 0;
            data["gateway_cost"] = 0;
        });

        var result = await CreateFetcher(server).GetGenerationInfoAsync(ById);

        Assert.Equal(0, result.MarketCost);
        Assert.Equal(0, result.SurchargeCost);
        Assert.Equal(0, result.GatewayCost);
    }

    // it.each([undefined, null]): false is undefined (the field is absent), true is null.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_omit_unavailable_marketCost(bool isNull)
    {
        var server = ServerWithData(data =>
        {
            if (isNull)
            {
                data["market_cost"] = null;
            }
            else
            {
                data.Remove("market_cost");
            }
        });

        var result = await CreateFetcher(server).GetGenerationInfoAsync(ById);

        Assert.Null(result.MarketCost);
        Assert.Equal(0.00013, result.SurchargeCost);
        Assert.Equal(0.00123, result.GatewayCost);
    }
}
