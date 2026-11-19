// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayProvider.GatewayProviderFixture;

namespace E2E.Decision.Tests.GatewayProvider;

/// <summary>Upstream's <c>describe('createGateway')</c> of <c>gateway-provider.test.ts</c> (the decision-model factories).</summary>
[Collection("GatewayProviderEnvironment")]
public sealed class CreateGatewayTests
{
    private static readonly Dictionary<string, string?> NoEnvironment = [];

    [Fact]
    public Task Should_create_provider_with_correct_configuration() => WithEnvironmentAsync(NoEnvironment, async () =>
        {
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
            var headers = await ResolveHeadersAsync(
                new GatewayProviderSettings { BaseUrl = BaseUrl, ApiKey = "test-api-key" }
            );

            Assert.DoesNotContain(headers.Keys, name => name.StartsWith("ai-o11y-", StringComparison.Ordinal));
        });
}
