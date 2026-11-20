// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayProvider.GatewayProviderFixture;

namespace E2E.Decision.Tests.GatewayProvider;

/// <summary>Upstream's <c>describe('getCredits method')</c> of <c>gateway-provider.test.ts</c>.</summary>
[Collection("GatewayProviderEnvironment")]
public sealed class GetCreditsMethodTests
{
    private static readonly Dictionary<string, string?> NoEnvironment = [];

    [Fact]
    public Task Should_fetch_credits_successfully() => WithEnvironmentAsync(NoEnvironment, async () =>
        {
            var server = ServerFor(DefaultBaseUrl, """{"balance":"150.50","total_used":"75.25"}""");
            var provider = ProviderOn(server, apiKey: "test-key");

            var credits = await provider.GetCreditsAsync();

            Assert.Equal(new GatewayCreditsResponse("150.50", "75.25"), credits);
            // The credits fetcher gets the default baseURL and the provider's headers.
            Assert.Equal("https://ai-gateway.vercel.sh/v1/credits", server.Calls[0].RequestUrl);
            Assert.Equal("Bearer test-key", server.Calls[0].RequestHeaders["authorization"]);
        });

    [Fact]
    public Task Should_pass_teamIdOrSlug_to_the_credits_fetcher_via_the_team_header() =>
        WithEnvironmentAsync(NoEnvironment, async () =>
        {
            var server = ServerFor(DefaultBaseUrl, CreditsBody);
            var provider = ProviderOn(server, apiKey: "vca_test-token", teamIdOrSlug: "vercel");

            await provider.GetCreditsAsync();

            Assert.Equal("Bearer vca_test-token", server.Calls[^1].RequestHeaders["authorization"]);
            Assert.Equal("vercel", server.Calls[^1].RequestHeaders["x-vercel-ai-gateway-team"]);
        });

    [Fact]
    public Task Should_work_with_custom_baseURL() => WithEnvironmentAsync(NoEnvironment, async () =>
        {
            const string customBaseUrl = "https://custom-gateway.example.com/v4/ai";
            var server = ServerFor(customBaseUrl, CreditsBody);
            var provider = ProviderOn(server, baseUrl: customBaseUrl, apiKey: "test-key");

            await provider.GetCreditsAsync();

            Assert.Equal("https://custom-gateway.example.com/v1/credits", server.Calls[0].RequestUrl);
        });

    [Fact]
    public Task Should_work_with_OIDC_authentication_in_getCredits() => WithEnvironmentAsync(NoEnvironment, async () =>
        {
            var server = ServerFor(DefaultBaseUrl, CreditsBody);
            var oidcCalls = 0;
            var provider = ProviderOn(server, getVercelOidcToken: _ =>
            {
                oidcCalls++;
                return Task.FromResult("oidc-token");
            });

            var credits = await provider.GetCreditsAsync();

            Assert.NotNull(credits);
            Assert.True(oidcCalls > 0);
            Assert.Equal("Bearer oidc-token", server.Calls[0].RequestHeaders["authorization"]);
        });

    [Fact]
    public Task Should_handle_errors_from_the_credits_endpoint() => WithEnvironmentAsync(NoEnvironment, async () =>
        {
            var server = new TestServer();
            server.RespondError(
                "https://ai-gateway.vercel.sh/*",
                500,
                """{"error":{"message":"Credits service unavailable","type":"internal_server_error"}}"""
            );
            var provider = ProviderOn(server, apiKey: "test-key");

            var error = await Assert.ThrowsAsync<GatewayInternalServerException>(() => provider.GetCreditsAsync());

            Assert.Contains("Credits service unavailable", error.Message, StringComparison.Ordinal);
        });

    [Fact]
    public Task Should_include_proper_headers_for_credits_request() => WithEnvironmentAsync(NoEnvironment, async () =>
        {
            var server = ServerFor(DefaultBaseUrl, CreditsBody);
            var provider = ProviderOn(
                server,
                apiKey: "test-key",
                headers: new Dictionary<string, string> { ["custom-header"] = "custom-value" }
            );

            await provider.GetCreditsAsync();

            var headers = server.Calls[0].RequestHeaders;
            Assert.Equal("Bearer test-key", headers["authorization"]);
            Assert.Equal("0.0.1", headers["ai-gateway-protocol-version"]);
            Assert.Equal("api-key", headers["ai-gateway-auth-method"]);
            Assert.Equal("custom-value", headers["custom-header"]);
            Assert.StartsWith("e2e-dotnet/", headers["user-agent"], StringComparison.Ordinal);
            Assert.Equal(5, headers.Count);
        });

    [Fact]
    public Task Should_be_available_on_the_provider_interface() => WithEnvironmentAsync(NoEnvironment, () =>
        {
            var provider = new E2E.Decision.Sdk.Gateway.GatewayProvider(
                new GatewayProviderSettings { ApiKey = "test-key" }
            );

            Func<CancellationToken, Task<GatewayCreditsResponse>> getCredits = provider.GetCreditsAsync;

            Assert.NotNull(getCredits);
            return Task.CompletedTask;
        });
}
