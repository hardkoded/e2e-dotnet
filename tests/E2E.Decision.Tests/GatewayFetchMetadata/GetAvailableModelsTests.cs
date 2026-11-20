// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayFetchMetadata.GatewayFetchMetadataFixture;

namespace E2E.Decision.Tests.GatewayFetchMetadata;

/// <summary>Upstream's <c>describe('getAvailableModels')</c> of <c>gateway-fetch-metadata.test.ts</c>.</summary>
public sealed class GetAvailableModelsTests
{
    private static string Model(string id, string modelType) =>
        $$"""{"id":"{{id}}","name":"{{id}}","specification":{"specificationVersion":"v4","provider":"test-provider","modelId":"{{id}}"},"modelType":"{{modelType}}"}""";

    [Fact]
    public async Task Should_fetch_available_models_from_the_correct_endpoint()
    {
        var server = ServerWithModels(MockModelEntry);
        var metadata = CreateBasicMetadataFetcher(server);

        var result = await metadata.GetAvailableModelsAsync();

        Assert.Equal("GET", server.Calls[0].RequestMethod);
        Assert.Equal("https://api.example.com/config", server.Calls[0].RequestUrl);
        var model = Assert.Single(result.Models);
        Assert.Equal("model-1", model.Id);
        Assert.Equal("Model One", model.Name);
        Assert.Equal("A test model", model.Description);
        Assert.Equal(new GatewayModelPricing { Input = "0.000001", Output = "0.000002" }, model.Pricing);
        Assert.Equal(new GatewayLanguageModelSpecification("v4", "test-provider", "model-1"), model.Specification);
        Assert.Null(model.ModelType);
    }

    [Fact]
    public async Task Should_handle_models_with_pricing_information()
    {
        var metadata = CreateBasicMetadataFetcher(ServerWithModels(MockModelEntry));

        var result = await metadata.GetAvailableModelsAsync();

        Assert.Equal("model-1", result.Models[0].Id);
        Assert.Equal(new GatewayModelPricing { Input = "0.000001", Output = "0.000002" }, result.Models[0].Pricing);
    }

    [Fact]
    public async Task Should_map_cache_pricing_fields_to_SDK_names_when_present()
    {
        var entry = JsonNode.Parse(MockModelEntry)!;
        entry["pricing"] = JsonNode.Parse(
            """{"input":"0.000003","output":"0.000015","input_cache_read":"0.0000003","input_cache_write":"0.00000375"}"""
        );
        var metadata = CreateBasicMetadataFetcher(ServerWithModels(entry.ToJsonString()));

        var result = await metadata.GetAvailableModelsAsync();

        Assert.Equal(
            new GatewayModelPricing
            {
                Input = "0.000003",
                Output = "0.000015",
                CachedInputTokens = "0.0000003",
                CacheCreationInputTokens = "0.00000375",
            },
            result.Models[0].Pricing
        );
        // The Gateway's input_cache_read and input_cache_write names have no property on the pricing.
        Assert.DoesNotContain(typeof(GatewayModelPricing).GetProperties(), property =>
            property.Name is "input_cache_read" or "input_cache_write"
        );
    }

    [Fact]
    public async Task Should_handle_models_without_pricing_information()
    {
        var metadata = CreateBasicMetadataFetcher(ServerWithModels(MockModelEntryWithoutPricing));

        var result = await metadata.GetAvailableModelsAsync();

        Assert.Equal("model-2", result.Models[0].Id);
        Assert.Null(result.Models[0].Pricing);
    }

    [Fact]
    public async Task Should_handle_mixed_models_with_and_without_pricing()
    {
        var metadata = CreateBasicMetadataFetcher(ServerWithModels(MockModelEntry, MockModelEntryWithoutPricing));

        var result = await metadata.GetAvailableModelsAsync();

        Assert.Equal(2, result.Models.Count);
        Assert.Equal(new GatewayModelPricing { Input = "0.000001", Output = "0.000002" }, result.Models[0].Pricing);
        Assert.Null(result.Models[1].Pricing);
    }

    [Fact]
    public async Task Should_handle_models_with_description()
    {
        var entry = JsonNode.Parse(MockModelEntry)!;
        entry["description"] = "A powerful language model";
        var metadata = CreateBasicMetadataFetcher(ServerWithModels(entry.ToJsonString()));

        var result = await metadata.GetAvailableModelsAsync();

        Assert.Equal("A powerful language model", result.Models[0].Description);
    }

    [Fact]
    public async Task Should_accept_top_level_modelType_when_present()
    {
        var entry = JsonNode.Parse(MockModelEntry)!;
        entry["modelType"] = "language";
        var metadata = CreateBasicMetadataFetcher(ServerWithModels(entry.ToJsonString()));

        var result = await metadata.GetAvailableModelsAsync();

        Assert.Equal("language", result.Models[0].ModelType);
    }

    [Fact]
    public async Task Should_filter_out_models_with_unknown_modelType_values()
    {
        var metadata = CreateBasicMetadataFetcher(ServerWithModels(Model("model-unknown-type", "some-future-type")));

        var result = await metadata.GetAvailableModelsAsync();

        Assert.Empty(result.Models);
    }

    [Fact]
    public async Task Should_preserve_all_known_modelType_values()
    {
        string[] knownTypes =
        [
            "embedding",
            "decision",
            "evaluation",
            "extraction",
            "image",
            "language",
            "realtime",
            "reranking",
            "speech",
            "transcription",
            "video",
        ];
        var metadata = CreateBasicMetadataFetcher(
            ServerWithModels([.. knownTypes.Select(type => Model("model-" + type, type))])
        );

        var result = await metadata.GetAvailableModelsAsync();

        Assert.Equal(knownTypes.Length, result.Models.Count);
        for (var i = 0; i < knownTypes.Length; i++)
        {
            Assert.Equal(knownTypes[i], result.Models[i].ModelType);
        }
    }

    [Fact]
    public async Task Should_keep_known_models_and_filter_unknown_from_mixed_response()
    {
        var entry = JsonNode.Parse(MockModelEntry)!;
        entry["modelType"] = "language";
        var metadata = CreateBasicMetadataFetcher(
            ServerWithModels(entry.ToJsonString(), Model("model-future", "hologram"))
        );

        var result = await metadata.GetAvailableModelsAsync();

        Assert.Single(result.Models);
        Assert.Equal("language", result.Models[0].ModelType);
    }

    [Fact]
    public async Task Should_pass_headers_correctly()
    {
        var server = ServerWithModels(MockModelEntry);
        var metadata = CreateBasicMetadataFetcher(server, () =>
            new Dictionary<string, string?>
            {
                ["Authorization"] = "Bearer custom-token",
                ["Custom-Header"] = "custom-value",
            }
        );

        await metadata.GetAvailableModelsAsync();

        Assert.Equal("Bearer custom-token", server.Calls[0].RequestHeaders["authorization"]);
        Assert.Equal("custom-value", server.Calls[0].RequestHeaders["custom-header"]);
        Assert.Equal(2, server.Calls[0].RequestHeaders.Count);
    }

    [Fact]
    public async Task Should_handle_API_errors()
    {
        var server = ServerWithError(401, """{"error":{"message":"Unauthorized","type":"authentication_error"}}""");
        var metadata = CreateBasicMetadataFetcher(server);

        var error = await Assert.ThrowsAsync<GatewayAuthenticationException>(() => metadata.GetAvailableModelsAsync());

        Assert.Contains("No authentication provided", error.Message, StringComparison.Ordinal);
        Assert.Equal("authentication_error", error.Type);
        Assert.Equal(401, error.StatusCode);
    }

    [Fact]
    public async Task Should_convert_API_call_errors_to_Gateway_errors()
    {
        var server = ServerWithError(403, """{"error":{"message":"Forbidden access","type":"authentication_error"}}""");
        var metadata = CreateBasicMetadataFetcher(server);

        var error = await Assert.ThrowsAsync<GatewayAuthenticationException>(() => metadata.GetAvailableModelsAsync());

        Assert.Contains("No authentication provided", error.Message, StringComparison.Ordinal);
        Assert.Equal("authentication_error", error.Type);
        Assert.Equal(403, error.StatusCode);
    }

    [Fact]
    public async Task Should_handle_malformed_JSON_error_responses()
    {
        var metadata = CreateBasicMetadataFetcher(ServerWithError(500, "{ invalid json"));

        var error = await Assert.ThrowsAsync<GatewayResponseException>(() => metadata.GetAvailableModelsAsync());

        Assert.Equal(500, error.StatusCode);
        Assert.Equal("response_error", error.Type);
    }

    [Fact]
    public async Task Should_handle_malformed_response_data()
    {
        var metadata = CreateBasicMetadataFetcher(ServerWith("""{"invalid":"response"}"""));

        await Assert.ThrowsAnyAsync<Exception>(() => metadata.GetAvailableModelsAsync());
    }

    [Fact]
    public async Task Should_reject_models_with_invalid_pricing_format()
    {
        var metadata = CreateBasicMetadataFetcher(
            ServerWithModels(
                """{"id":"model-1","name":"Model One","pricing":{"input":123,"output":"0.000002"},"specification":{"specificationVersion":"v4","provider":"test-provider","modelId":"model-1"}}"""
            )
        );

        await Assert.ThrowsAnyAsync<Exception>(() => metadata.GetAvailableModelsAsync());
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
    public async Task Should_handle_various_server_error_types()
    {
        var server = ServerWithError(
            429,
            """{"error":{"message":"Rate limit exceeded","type":"rate_limit_exceeded"}}"""
        );
        var metadata = CreateBasicMetadataFetcher(server);

        var error = await Assert.ThrowsAsync<GatewayRateLimitException>(() => metadata.GetAvailableModelsAsync());

        Assert.Equal("Rate limit exceeded", error.Message);
        Assert.Equal("rate_limit_exceeded", error.Type);
        Assert.Equal(429, error.StatusCode);
    }

    [Fact]
    public async Task Should_handle_internal_server_errors()
    {
        var server = ServerWithError(
            500,
            """{"error":{"message":"Database connection failed","type":"internal_server_error"}}"""
        );
        var metadata = CreateBasicMetadataFetcher(server);

        var error = await Assert.ThrowsAsync<GatewayInternalServerException>(() => metadata.GetAvailableModelsAsync());

        Assert.Equal("Database connection failed", error.Message);
        Assert.Equal("internal_server_error", error.Type);
        Assert.Equal(500, error.StatusCode);
    }

    [Fact]
    public async Task Should_preserve_error_cause_chain()
    {
        var server = ServerWithError(401, """{"error":{"message":"Token expired","type":"authentication_error"}}""");
        var metadata = CreateBasicMetadataFetcher(server);

        var error = await Assert.ThrowsAsync<GatewayAuthenticationException>(() => metadata.GetAvailableModelsAsync());

        Assert.NotNull(error.InnerException);
    }

    // Upstream passes a custom fetch; the port's counterpart is a custom HttpClient.
    [Fact]
    public async Task Should_use_custom_fetch_function_when_provided()
    {
        var server = ServerWithModels(
            """{"id":"custom-model-1","name":"Custom Model One","description":"Custom model description","pricing":{"input":"0.000005","output":"0.000010"},"specification":{"specificationVersion":"v4","provider":"custom-provider","modelId":"custom-model-1"}}"""
        );
        var metadata = CreateBasicMetadataFetcher(server);

        var result = await metadata.GetAvailableModelsAsync();

        Assert.NotEmpty(server.Calls);
        var model = Assert.Single(result.Models);
        Assert.Equal("custom-model-1", model.Id);
        Assert.Equal("Custom Model One", model.Name);
        Assert.Equal("Custom model description", model.Description);
        Assert.Equal(new GatewayModelPricing { Input = "0.000005", Output = "0.000010" }, model.Pricing);
        Assert.Equal(
            new GatewayLanguageModelSpecification("v4", "custom-provider", "custom-model-1"),
            model.Specification
        );
    }

    [Fact]
    public async Task Should_handle_empty_response()
    {
        var metadata = CreateBasicMetadataFetcher(ServerWith("""{"models":[]}"""));

        var result = await metadata.GetAvailableModelsAsync();

        Assert.Empty(result.Models);
    }
}
