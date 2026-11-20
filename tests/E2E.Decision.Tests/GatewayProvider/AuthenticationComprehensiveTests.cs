// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;
using Vercel.Oidc;
using static E2E.Decision.Tests.GatewayProvider.GatewayProviderFixture;
using Env = E2E.Decision.Sdk.Gateway.VercelEnvironment;

namespace E2E.Decision.Tests.GatewayProvider;

/// <summary>
/// Upstream's <c>describe('Authentication Comprehensive Tests')</c> of <c>gateway-provider.test.ts</c>, with its nested describes.
/// A <c>getVercelOidcToken</c> that rejects is an unset <c>VERCEL_OIDC_TOKEN</c> here: the port reads the variable and has no mock of it.
/// </summary>
[Collection("GatewayProviderEnvironment")]
public sealed class AuthenticationComprehensiveTests
{
    // authTestCases: name, VERCEL_OIDC_TOKEN, AI_GATEWAY_API_KEY, options apiKey, expected auth method (null: authentication fails).
    public static TheoryData<string, string?, string?, string?, string?> AuthTestCases =>
        new()
        {
            { "no auth at all", null, null, null, null },
            { "valid oidc, invalid api key", "valid-oidc-token-12345", null, "invalid-api-key", "api-key" },
            { "invalid oidc, valid api key", null, null, "gw_valid_api_key_12345", "api-key" },
            { "no oidc, invalid api key", null, "invalid-api-key", null, "api-key" },
            { "no oidc, valid api key", null, "gw_valid_api_key_12345", null, "api-key" },
            { "valid oidc, no api key", "valid-oidc-token-12345", null, null, "oidc" },
            { "valid oidc, valid api key", "valid-oidc-token-12345", "gw_valid_api_key_12345", null, "api-key" },
            {
                "valid oidc, valid options api key",
                "valid-oidc-token-12345",
                null,
                "gw_valid_options_api_key_12345",
                "api-key"
            },
            { "invalid oidc, no api key", null, null, null, null },
            { "invalid oidc, invalid api key", null, "invalid-api-key", null, "api-key" },
        };

    private static Dictionary<string, string?> Environment(string? oidc, string? apiKey) =>
        new() { ["VERCEL_OIDC_TOKEN"] = oidc, ["AI_GATEWAY_API_KEY"] = apiKey };

    // describe('getGatewayAuthToken function')
    [Theory]
    [MemberData(nameof(AuthTestCases))]
    public async Task Should_handle_auth_case_in_getGatewayAuthToken(
        string name,
        string? envOidcToken,
        string? envApiKey,
        string? optionsApiKey,
        string? expectedAuthMethod
    )
    {
        Assert.NotEmpty(name);
        await WithEnvironmentAsync(Environment(envOidcToken, envApiKey), async () =>
        {
            var options = new GatewayProviderSettings { ApiKey = optionsApiKey };

            if (expectedAuthMethod is null)
            {
                await Assert.ThrowsAnyAsync<Exception>(() =>
                    E2E.Decision.Sdk.Gateway.GatewayProvider.GetGatewayAuthTokenAsync(options)
                );
                return;
            }

            var result = await E2E.Decision.Sdk.Gateway.GatewayProvider.GetGatewayAuthTokenAsync(options);

            Assert.Equal(expectedAuthMethod, result.AuthMethod);
            Assert.Equal(expectedAuthMethod == "api-key" ? optionsApiKey ?? envApiKey : envOidcToken, result.Token);
        });
    }

    // describe('createGateway authentication')
    [Theory]
    [MemberData(nameof(AuthTestCases))]
    public async Task Should_handle_provider_creation_with_auth_case(
        string name,
        string? envOidcToken,
        string? envApiKey,
        string? optionsApiKey,
        string? expectedAuthMethod
    )
    {
        Assert.NotEmpty(name);
        await WithEnvironmentAsync(Environment(envOidcToken, envApiKey), async () =>
        {
            var options = new GatewayProviderSettings
            {
                BaseUrl = "https://test-gateway.example.com",
                ApiKey = optionsApiKey,
            };

            if (expectedAuthMethod is null)
            {
                var error = await Assert.ThrowsAsync<GatewayAuthenticationException>(() =>
                    ResolveHeadersAsync(options)
                );
                Assert.Matches("(?i)authentication|token", error.Message);
                return;
            }

            var headers = await ResolveHeadersAsync(options);

            Assert.Equal(expectedAuthMethod, headers["ai-gateway-auth-method"]);
        });
    }

    // describe('Environment variable edge cases')
    [Fact]
    public Task Should_handle_empty_string_environment_variables_as_undefined() =>
        WithEnvironmentAsync(Environment("", ""), async () =>
        {
            var error = await Assert.ThrowsAnyAsync<Exception>(() =>
                E2E.Decision.Sdk.Gateway.GatewayProvider.GetGatewayAuthTokenAsync(new GatewayProviderSettings())
            );

            Assert.IsNotType<GatewayAuthenticationException>(error);
        });

    [Fact]
    public Task Should_handle_whitespace_only_environment_variables() =>
        WithEnvironmentAsync(Environment("   ", "\t\n "), async () =>
        {
            // The whitespace API key is still used: it is treated as a valid value.
            var result = await E2E.Decision.Sdk.Gateway.GatewayProvider.GetGatewayAuthTokenAsync(
                new GatewayProviderSettings()
            );

            Assert.Equal("api-key", result.AuthMethod);
            Assert.Equal("\t\n ", result.Token);
        });

    [Fact]
    public Task Should_prioritize_options_apiKey_over_all_environment_variables() =>
        WithEnvironmentAsync(Environment("env-oidc-token", "env-api-key"), async () =>
        {
            var result = await E2E.Decision.Sdk.Gateway.GatewayProvider.GetGatewayAuthTokenAsync(
                new GatewayProviderSettings { ApiKey = "options-api-key" }
            );

            Assert.Equal("api-key", result.AuthMethod);
            Assert.Equal("options-api-key", result.Token);
        });

    [Fact]
    public Task Should_surface_OIDC_error_as_cause_when_authentication_fails() =>
        WithEnvironmentAsync(Environment("", ""), async () =>
        {
            var oidcError = new InvalidOperationException("OIDC token generation failed: project not linked");
            Env.OidcTokenOverride = _ => Task.FromException<string>(oidcError);
            var provider = new E2E.Decision.Sdk.Gateway.GatewayProvider();

            var error = await Assert.ThrowsAsync<GatewayAuthenticationException>(() =>
                provider.GetAvailableModelsAsync()
            );

            Assert.Same(oidcError, error.InnerException);
            Assert.Contains("No authentication provided", error.Message, StringComparison.Ordinal);
        });

    // describe('Authentication precedence')
    [Fact]
    public Task Should_prefer_options_apiKey_over_AI_GATEWAY_API_KEY() =>
        WithEnvironmentAsync(Environment(null, "env-api-key"), async () =>
        {
            var result = await E2E.Decision.Sdk.Gateway.GatewayProvider.GetGatewayAuthTokenAsync(
                new GatewayProviderSettings { ApiKey = "options-api-key" }
            );

            Assert.Equal("api-key", result.AuthMethod);
            Assert.Equal("options-api-key", result.Token);
        });

    [Fact]
    public Task Should_prefer_AI_GATEWAY_API_KEY_over_OIDC_token() =>
        WithEnvironmentAsync(Environment("oidc-token", "env-api-key"), async () =>
        {
            var result = await E2E.Decision.Sdk.Gateway.GatewayProvider.GetGatewayAuthTokenAsync(
                new GatewayProviderSettings()
            );

            Assert.Equal("api-key", result.AuthMethod);
            Assert.Equal("env-api-key", result.Token);
        });

    [Fact]
    public Task Should_fall_back_to_OIDC_when_no_API_keys_are_available() =>
        WithEnvironmentAsync(Environment("oidc-token", null), async () =>
        {
            var result = await E2E.Decision.Sdk.Gateway.GatewayProvider.GetGatewayAuthTokenAsync(
                new GatewayProviderSettings()
            );

            Assert.Equal("oidc", result.AuthMethod);
            Assert.Equal("oidc-token", result.Token);
        });

    // describe('Real-world usage scenarios')
    [Fact]
    public Task Should_work_in_Vercel_deployment_with_OIDC() =>
        WithEnvironmentAsync(
            new Dictionary<string, string?>
            {
                ["VERCEL_OIDC_TOKEN"] = "vercel-deployment-oidc-token",
                ["VERCEL_DEPLOYMENT_ID"] = "dpl_12345",
                ["VERCEL_ENV"] = "production",
                ["VERCEL_REGION"] = "iad1",
            },
            async () =>
            {
                var headers = await ResolveHeadersAsync(new GatewayProviderSettings());

                Assert.Equal("Bearer vercel-deployment-oidc-token", headers["authorization"]);
                Assert.Equal("oidc", headers["ai-gateway-auth-method"]);
            }
        );

    [Fact]
    public Task Should_work_in_local_development_with_API_key() =>
        WithEnvironmentAsync(Environment(null, "local-dev-api-key"), async () =>
        {
            var server = ServerFor(DefaultBaseUrl, NoModelsBody);
            var oidcCalls = 0;
            var provider = ProviderOn(server, getVercelOidcToken: _ =>
            {
                oidcCalls++;
                return Task.FromResult("never");
            });

            var models = await provider.GetAvailableModelsAsync();

            Assert.NotNull(models);
            Assert.Equal(0, oidcCalls);
        });

    [Fact]
    public Task Should_work_with_explicit_API_key_override() =>
        WithEnvironmentAsync(Environment("should-not-be-used", "should-not-be-used-either"), async () =>
        {
            var server = ServerFor(DefaultBaseUrl, NoModelsBody);
            var oidcCalls = 0;
            var provider = ProviderOn(server, apiKey: "explicit-user-api-key", getVercelOidcToken: _ =>
            {
                oidcCalls++;
                return Task.FromResult("never");
            });

            var models = await provider.GetAvailableModelsAsync();

            Assert.NotNull(models);
            Assert.Equal(0, oidcCalls);
            Assert.Equal("Bearer explicit-user-api-key", server.Calls[0].RequestHeaders["authorization"]);
        });
}
