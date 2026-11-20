// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayDecisionModel.GatewayDecisionModelFixture;
using static E2E.Decision.Tests.GatewayProvider.GatewayProviderFixture;

namespace E2E.Decision.Tests.GatewayProvider;

/// <summary>Upstream's <c>describe('default exported provider')</c> of <c>gateway-provider.test.ts</c> (the decision-model parts).</summary>
[Collection("GatewayProviderEnvironment")]
public sealed class DefaultExportedProviderTests
{
    [Fact]
    public Task Should_use_the_default_baseURL_when_none_is_provided() =>
        WithEnvironmentAsync(new Dictionary<string, string?>(), async () =>
        {
            var server = new TestServer();
            server.RespondJson(
                "https://ai-gateway.vercel.sh/v4/ai/decision-model",
                """{"answers":{"correct":{"type":"boolean","probability":0.97}}}"""
            );
            var provider = new E2E.Decision.Sdk.Gateway.GatewayProvider(
                new GatewayProviderSettings { ApiKey = "test-key", HttpClient = server.Client }
            );

            using var model = provider.DecisionModel("test-model");
            await model.DoDecideAsync(Call(), CancellationToken.None);

            Assert.Equal("https://ai-gateway.vercel.sh/v4/ai/decision-model", server.Calls[0].RequestUrl);
        });

    [Fact]
    public void Should_accept_empty_options()
    {
        var provider = new E2E.Decision.Sdk.Gateway.GatewayProvider();

        Assert.NotNull(provider);
        Assert.NotNull(provider.DecisionModel("test-model"));
    }

    [Fact]
    public Task Should_override_default_baseURL_when_provided() =>
        WithEnvironmentAsync(new Dictionary<string, string?>(), async () =>
        {
            var customBaseUrl = "https://custom-api.example.com";
            var server = new TestServer();
            server.RespondJson(
                customBaseUrl + "/decision-model",
                """{"answers":{"correct":{"type":"boolean","probability":0.97}}}"""
            );
            var provider = new E2E.Decision.Sdk.Gateway.GatewayProvider(
                new GatewayProviderSettings
                {
                    BaseUrl = customBaseUrl,
                    ApiKey = "test-key",
                    HttpClient = server.Client,
                }
            );

            using var model = provider.DecisionModel("test-model");
            await model.DoDecideAsync(Call(), CancellationToken.None);

            Assert.Equal(customBaseUrl + "/decision-model", server.Calls[0].RequestUrl);
        });

    [Fact]
    public Task Should_use_apiKey_over_OIDC_token_when_provided() =>
        WithEnvironmentAsync(new Dictionary<string, string?> { ["VERCEL_OIDC_TOKEN"] = "must-not-be-used" }, async () =>
        {
            var testApiKey = "test-api-key-123";

            var headers = await ResolveHeadersAsync(new GatewayProviderSettings { ApiKey = testApiKey });

            Assert.Equal("Bearer " + testApiKey, headers["authorization"]);
            Assert.Equal("api-key", headers["ai-gateway-auth-method"]);
            Assert.StartsWith("e2e-dotnet/", headers["user-agent"], StringComparison.Ordinal);
        });

    [Fact]
    public void Should_export_a_default_provider_instance()
    {
        var gateway = E2E.Decision.Sdk.Gateway.GatewayProvider.Gateway;

        Assert.NotNull(gateway);
        Func<string, E2E.Decision.Sdk.Gateway.GatewayDecisionModel> decisionModel = gateway.DecisionModel;
        Assert.NotNull(decisionModel);
        Func<CancellationToken, Task<GatewayFetchMetadataResponse>> getAvailableModels =
            gateway.GetAvailableModelsAsync;
        Assert.NotNull(getAvailableModels);
    }

    [Fact]
    public Task Should_use_apiKey_and_teamIdOrSlug_for_getAvailableModels() =>
        WithEnvironmentAsync(new Dictionary<string, string?> { ["VERCEL_OIDC_TOKEN"] = "must-not-be-used" }, async () =>
        {
            var server = ServerFor(DefaultBaseUrl, NoModelsBody);
            var provider = ProviderOn(server, apiKey: "vca_test-token", teamIdOrSlug: "vercel");

            await provider.GetAvailableModelsAsync();

            var headers = server.Calls[0].RequestHeaders;
            Assert.Equal("Bearer vca_test-token", headers["authorization"]);
            Assert.Equal("vercel", headers["x-vercel-ai-gateway-team"]);
        });

    [Fact]
    public Task Should_use_OIDC_and_teamIdOrSlug_for_getAvailableModels_when_no_apiKey_is_configured() =>
        WithEnvironmentAsync(new Dictionary<string, string?>(), async () =>
        {
            var server = ServerFor(DefaultBaseUrl, NoModelsBody);
            var oidcCalls = 0;
            var provider = ProviderOn(server, teamIdOrSlug: "vercel", getVercelOidcToken: _ =>
            {
                oidcCalls++;
                return Task.FromResult("oidc-token");
            });

            await provider.GetAvailableModelsAsync();

            var headers = server.Calls[0].RequestHeaders;
            Assert.Equal("Bearer oidc-token", headers["authorization"]);
            Assert.Equal("oidc", headers["ai-gateway-auth-method"]);
            Assert.Equal("vercel", headers["x-vercel-ai-gateway-team"]);
            Assert.True(oidcCalls > 0);
        });
}
