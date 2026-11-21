// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;
using Vercel.Oidc;
using static E2E.Decision.Tests.GatewayProvider.GatewayProviderFixture;
using Env = E2E.Decision.Sdk.Gateway.VercelEnvironment;

namespace E2E.Decision.Tests.GatewayProvider;

/// <summary>Upstream's <c>describe('createGateway')</c> of <c>gateway-provider.test.ts</c> (the language model factory).</summary>
[Collection("GatewayProviderEnvironment")]
public sealed class CreateGatewayTests
{
    private static readonly Dictionary<string, string?> NoEnvironment = [];

    [Fact]
    public Task Should_create_a_provider_owned_batch_service() => WithEnvironmentAsync(NoEnvironment, async () =>
        {
            var server = new TestServer();
            server.RespondJson(BaseUrl + "/batch/status", """{"batchId":"job_1","status":"pending"}""");
            var provider = new E2E.Decision.Sdk.Gateway.GatewayProvider(
                new GatewayProviderSettings
                {
                    BaseUrl = BaseUrl,
                    ApiKey = "test-api-key",
                    HttpClient = server.Client,
                }
            );

            // Upstream mocks GatewayBatch and reads its constructor config; the port has no module mocks, so the test reads the batch's provider
            // and the request it makes with that config.
            var batch = provider.ExperimentalBatch();
            await batch.DoGetBatchStatusAsync(new E2E.Decision.Sdk.BatchV4OperationOptions { BatchId = "job_1" });

            Assert.Equal("gateway.batch", batch.Provider);
            var call = Assert.Single(server.Calls);
            Assert.Equal(BaseUrl + "/batch/status", call.RequestUrl);
            Assert.Equal("Bearer test-api-key", call.RequestHeaders["authorization"]);
            Assert.Equal("api-key", call.RequestHeaders["ai-gateway-auth-method"]);
        });

    [Fact]
    public Task Should_create_provider_with_correct_configuration() => WithEnvironmentAsync(NoEnvironment, async () =>
        {
            var options = new GatewayProviderSettings
            {
                BaseUrl = BaseUrl,
                ApiKey = "test-api-key",
                Headers = new Dictionary<string, string> { ["Custom-Header"] = "value" },
            };
            using (var model = new E2E.Decision.Sdk.Gateway.GatewayProvider(options).LanguageModel("test-model"))
            {
                Assert.Equal("test-model", model.ModelId);
                Assert.Equal("gateway", model.Config.Provider);
                Assert.Equal(BaseUrl, model.Config.BaseUrl);
                Assert.NotNull(model.Config.Headers);
                Assert.Null(model.Config.HttpClient); // fetch: undefined
            }

            var headers = await ResolveHeadersAsync(
                new GatewayProviderSettings
                {
                    BaseUrl = BaseUrl,
                    ApiKey = "test-api-key",
                    Headers = new Dictionary<string, string> { ["Custom-Header"] = "value" },
                }
            );

            Assert.Equal("Bearer test-api-key", headers["authorization"]);
            Assert.Equal("value", headers["custom-header"]);
            Assert.False(string.IsNullOrEmpty(headers["ai-gateway-protocol-version"]));
            Assert.Equal("api-key", headers["ai-gateway-auth-method"]);
            Assert.StartsWith("e2e-dotnet/", headers["user-agent"], StringComparison.Ordinal);
        });

    [Fact]
    public Task Should_use_OIDC_token_when_no_API_key_is_provided() =>
        WithEnvironmentAsync(new Dictionary<string, string?> { ["VERCEL_OIDC_TOKEN"] = "mock-oidc-token" }, async () =>
        {
            var headers = await ResolveHeadersAsync(
                new GatewayProviderSettings
                {
                    BaseUrl = BaseUrl,
                    Headers = new Dictionary<string, string> { ["Custom-Header"] = "value" },
                }
            );

            Assert.Equal("Bearer mock-oidc-token", headers["authorization"]);
            Assert.Equal("value", headers["custom-header"]);
            Assert.False(string.IsNullOrEmpty(headers["ai-gateway-protocol-version"]));
            Assert.Equal("oidc", headers["ai-gateway-auth-method"]);
            Assert.StartsWith("e2e-dotnet/", headers["user-agent"], StringComparison.Ordinal);
        });

    [Fact]
    public Task Should_use_apiKey_and_teamIdOrSlug_for_language_model_requests() =>
        WithEnvironmentAsync(new Dictionary<string, string?> { ["VERCEL_OIDC_TOKEN"] = "must-not-be-used" }, async () =>
        {
            var headers = await ResolveHeadersAsync(
                new GatewayProviderSettings
                {
                    ApiKey = "vca_test-token",
                    TeamIdOrSlug = "vercel",
                    Headers = new Dictionary<string, string> { ["Custom-Header"] = "value" },
                }
            );

            Assert.Equal("Bearer vca_test-token", headers["authorization"]);
            Assert.Equal("value", headers["custom-header"]);
            Assert.Equal("vercel", headers["x-vercel-ai-gateway-team"]);
            Assert.Equal("api-key", headers["ai-gateway-auth-method"]);
        });

    [Fact]
    public Task Should_pass_teamIdOrSlug_with_URL_special_characters_as_a_header_value() =>
        WithEnvironmentAsync(NoEnvironment, async () =>
        {
            var headers = await ResolveHeadersAsync(
                new GatewayProviderSettings { ApiKey = "vca_test-token", TeamIdOrSlug = "team-o'brien" }
            );

            Assert.Equal("team-o'brien", headers["x-vercel-ai-gateway-team"]);
        });

    [Fact]
    public Task Should_omit_team_header_for_apiKey_requests_without_teamIdOrSlug() =>
        WithEnvironmentAsync(NoEnvironment, async () =>
        {
            var headers = await ResolveHeadersAsync(new GatewayProviderSettings { ApiKey = "vca_test-token" });

            Assert.Equal("Bearer vca_test-token", headers["authorization"]);
            Assert.DoesNotContain("x-vercel-ai-gateway-team", headers.Keys);
        });

    [Fact]
    public Task Should_pass_teamIdOrSlug_with_OIDC_requests() =>
        WithEnvironmentAsync(new Dictionary<string, string?> { ["VERCEL_OIDC_TOKEN"] = "mock-oidc-token" }, async () =>
        {
            var headers = await ResolveHeadersAsync(new GatewayProviderSettings { TeamIdOrSlug = "vercel" });

            Assert.Equal("Bearer mock-oidc-token", headers["authorization"]);
            Assert.Equal("vercel", headers["x-vercel-ai-gateway-team"]);
            Assert.Equal("oidc", headers["ai-gateway-auth-method"]);
        });

    [Fact]
    public Task Should_not_include_undefined_o11y_headers() => WithEnvironmentAsync(NoEnvironment, async () =>
        {
            var provider = new E2E.Decision.Sdk.Gateway.GatewayProvider(
                new GatewayProviderSettings { BaseUrl = "https://api.example.com", ApiKey = "test-api-key" }
            );
            using var model = provider.LanguageModel("test-model");
            Assert.Equal("gateway", model.Config.Provider);
            Assert.Equal("https://api.example.com", model.Config.BaseUrl);

            // Test that the o11yHeaders function returns empty object
            var o11yHeaders = await model.Config.O11yHeaders(CancellationToken.None);
            Assert.Empty(o11yHeaders);
        });

    [Fact]
    public Task Should_fetch_available_models() => WithEnvironmentAsync(NoEnvironment, async () =>
        {
            var server = ServerFor("https://api.example.com", NoModelsBody);
            var provider = ProviderOn(server, baseUrl: "https://api.example.com", apiKey: "test-api-key");

            var models = await provider.GetAvailableModelsAsync();

            Assert.Empty(models.Models);
            // GatewayFetchMetadata gets the provider's baseURL.
            Assert.Equal("https://api.example.com/config", server.Calls[0].RequestUrl);
        });

    [Fact]
    public Task Should_pass_o11y_headers_to_the_model_when_environment_variables_are_set() =>
        WithEnvironmentAsync(
            new Dictionary<string, string?>
            {
                ["VERCEL_DEPLOYMENT_ID"] = "test-deployment",
                ["VERCEL_ENV"] = "test",
                ["VERCEL_REGION"] = "iad1",
                ["VERCEL_PROJECT_ID"] = "prj_test123",
            },
            async () =>
            {
                // Upstream reads config.o11yHeaders of the GatewayLanguageModel constructor call; the port reads the config of the model.
                using var _ = VercelRequestContext.Use(
                    new Dictionary<string, string?> { ["x-vercel-id"] = "test-request-id" }
                );
                var provider = new E2E.Decision.Sdk.Gateway.GatewayProvider(
                    new GatewayProviderSettings { BaseUrl = "https://api.example.com", ApiKey = "test-api-key" }
                );

                using var model = provider.LanguageModel("test-model");
                Assert.Equal("gateway", model.Config.Provider);
                Assert.Equal("https://api.example.com", model.Config.BaseUrl);

                // Test that the o11yHeaders function returns the expected result
                var o11yHeaders = await model.Config.O11yHeaders(CancellationToken.None);
                Assert.Equal(
                    new Dictionary<string, string?>
                    {
                        ["ai-o11y-deployment-id"] = "test-deployment",
                        ["ai-o11y-environment"] = "test",
                        ["ai-o11y-region"] = "iad1",
                        ["ai-o11y-request-id"] = "test-request-id",
                        ["ai-o11y-project-id"] = "prj_test123",
                    },
                    o11yHeaders.ToDictionary(pair => pair.Key, pair => pair.Value)
                );
            }
        );
}
