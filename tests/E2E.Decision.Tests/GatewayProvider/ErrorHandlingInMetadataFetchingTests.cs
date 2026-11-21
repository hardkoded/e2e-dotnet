// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayProvider.GatewayProviderFixture;

namespace E2E.Decision.Tests.GatewayProvider;

/// <summary>Upstream's <c>describe('Error handling in metadata fetching')</c> of <c>gateway-provider.test.ts</c> (the metadata parts and the model creation tests).</summary>
[Collection("GatewayProviderEnvironment")]
public sealed class ErrorHandlingInMetadataFetchingTests
{
    private static readonly Dictionary<string, string?> NoEnvironment = [];

    [Fact]
    public Task Should_convert_metadata_fetch_errors_to_Gateway_errors() =>
        WithEnvironmentAsync(NoEnvironment, async () =>
        {
            var server = new TestServer();
            server.RespondError(
                "https://api.example.com/*",
                500,
                """{"error":{"message":"Database connection failed","type":"internal_server_error"}}"""
            );
            var provider = ProviderOn(server, baseUrl: "https://api.example.com", apiKey: "test-key");

            var error = await Assert.ThrowsAsync<GatewayInternalServerException>(() =>
                provider.GetAvailableModelsAsync()
            );

            Assert.Equal("GatewayInternalServerError", error.Name);
            Assert.Equal("Database connection failed", error.Message);
            Assert.Equal(500, error.StatusCode);
        });

    [Fact]
    public Task Should_not_double_wrap_Gateway_errors_from_metadata_fetch() =>
        WithEnvironmentAsync(NoEnvironment, async () =>
        {
            // Upstream's mocked GatewayFetchMetadata throws the error; the port's counterpart is a client that does.
            var originalError = new GatewayAuthenticationException("Invalid token", 401);
            using var handler = new ThrowingHandler(originalError);
            var provider = new E2E.Decision.Sdk.Gateway.GatewayProvider(
                new GatewayProviderSettings
                {
                    BaseUrl = "https://api.example.com",
                    ApiKey = "test-key",
                    HttpClient = new HttpClient(handler),
                }
            );

            var error = await Assert.ThrowsAsync<GatewayAuthenticationException>(() =>
                provider.GetAvailableModelsAsync()
            );

            Assert.Same(originalError, error); // Same instance
            Assert.Equal("Invalid token", error.Message);
        });

    // Upstream mocks GatewayLanguageModel and checks its constructor call; the port reads the model's config.
    private static void AssertCreatedWith(E2E.Decision.Sdk.Gateway.GatewayLanguageModel model, string modelId)
    {
        Assert.NotNull(model);
        Assert.Equal(modelId, model.ModelId);
        Assert.Equal("gateway", model.Config.Provider);
        Assert.Equal("https://api.example.com", model.Config.BaseUrl);
        Assert.NotNull(model.Config.Headers);
        Assert.Null(model.Config.HttpClient); // fetch: undefined
        Assert.NotNull(model.Config.O11yHeaders);
    }

    [Fact]
    public Task Should_handle_model_specification_errors() => WithEnvironmentAsync(NoEnvironment, () =>
        {
            // Upstream mocks a successful metadata fetch with a model; creating the model does not fetch it.
            var provider = new E2E.Decision.Sdk.Gateway.GatewayProvider(
                new GatewayProviderSettings { BaseUrl = "https://api.example.com", ApiKey = "test-key" }
            );

            // Create a language model that should work
            using var model = provider.LanguageModel("test-model");

            // Verify the model was created with the correct parameters
            AssertCreatedWith(model, "test-model");
            return Task.CompletedTask;
        });

    [Fact]
    public Task Should_create_language_model_for_any_modelId() => WithEnvironmentAsync(NoEnvironment, () =>
        {
            // Upstream mocks a successful metadata fetch with different models; creating the model does not fetch them.
            var provider = new E2E.Decision.Sdk.Gateway.GatewayProvider(
                new GatewayProviderSettings { BaseUrl = "https://api.example.com", ApiKey = "test-key" }
            );

            // Create a language model for any model ID
            using var model = provider.LanguageModel("any-model-id");

            // The model should be created successfully
            AssertCreatedWith(model, "any-model-id");
            return Task.CompletedTask;
        });

    [Fact]
    public Task Should_handle_non_existent_model_requests() => WithEnvironmentAsync(NoEnvironment, () =>
        {
            var provider = new E2E.Decision.Sdk.Gateway.GatewayProvider(
                new GatewayProviderSettings { BaseUrl = "https://api.example.com", ApiKey = "test-key" }
            );

            // Create a language model for a non-existent model
            using var model = provider.LanguageModel("non-existent-model");

            // The model should be created successfully (validation happens at API call time)
            AssertCreatedWith(model, "non-existent-model");
            return Task.CompletedTask;
        });

    private sealed class ThrowingHandler(Exception error) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => throw error;
    }
}
