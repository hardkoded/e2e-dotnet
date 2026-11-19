// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayDecisionModel.GatewayDecisionModelFixture;
using Env = E2E.Decision.Sdk.Gateway.VercelEnvironment;

namespace E2E.Decision.Tests.GatewayDecisionModel;

/// <summary>
/// The Vercel OIDC credential of <c>getGatewayAuthToken</c> and the <c>ai-o11y-request-id</c> header of <c>createO11yHeaders</c>, observed on the wire.
/// Upstream's <c>gateway-provider.test.ts</c> reads them off a mocked model config instead.
/// </summary>
public sealed class OidcAuthenticationTests
{
    [Fact]
    public async Task Resolves_the_Vercel_OIDC_token_when_no_API_key_is_provided()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        var provider = new GatewayProvider(
            new GatewayProviderSettings { BaseUrl = "https://api.test.com", HttpClient = server.Client }
        );

        using var _ = WithoutApiKey();
        Env.OidcTokenOverride = _ => Task.FromResult("mock-oidc-token");
        try
        {
            await provider.DecisionModel("typesafe-ai/jev").DoDecideAsync(Call(), CancellationToken.None);
        }
        finally
        {
            Env.OidcTokenOverride = null;
        }

        Assert.Equal("Bearer mock-oidc-token", server.Calls[0].RequestHeaders["authorization"]);
        Assert.Equal("oidc", server.Calls[0].RequestHeaders["ai-gateway-auth-method"]);
    }

    [Fact]
    public async Task Fails_with_an_authentication_error_when_the_OIDC_token_cannot_be_resolved()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        var provider = new GatewayProvider(
            new GatewayProviderSettings { BaseUrl = "https://api.test.com", HttpClient = server.Client }
        );

        using var _ = WithoutApiKey();
        Env.OidcTokenOverride = _ => throw new InvalidOperationException("OIDC token not available");
        try
        {
            var error = await Assert.ThrowsAsync<GatewayAuthenticationException>(() =>
                provider.DecisionModel("typesafe-ai/jev").DoDecideAsync(Call(), CancellationToken.None)
            );

            Assert.Equal("OIDC token not available", error.InnerException?.Message);
        }
        finally
        {
            Env.OidcTokenOverride = null;
        }

        Assert.Empty(server.Calls);
    }

    [Fact]
    public async Task Sends_the_x_vercel_id_of_the_request_context_as_ai_o11y_request_id()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        var provider = new GatewayProvider(
            new GatewayProviderSettings
            {
                BaseUrl = "https://api.test.com",
                ApiKey = "test-api-key",
                HttpClient = server.Client,
            }
        );

        using (VercelRequestContext.Use(new Dictionary<string, string?> { ["x-vercel-id"] = "test-request-id" }))
        {
            await provider.DecisionModel("typesafe-ai/jev").DoDecideAsync(Call(), CancellationToken.None);
        }

        Assert.Equal("test-request-id", server.Calls[0].RequestHeaders["ai-o11y-request-id"]);
    }

    private static IDisposable WithoutApiKey()
    {
        var previous = Environment.GetEnvironmentVariable("AI_GATEWAY_API_KEY");
        Environment.SetEnvironmentVariable("AI_GATEWAY_API_KEY", null);
        return new Restore(previous);
    }

    private sealed class Restore(string? previous) : IDisposable
    {
        public void Dispose() => Environment.SetEnvironmentVariable("AI_GATEWAY_API_KEY", previous);
    }
}
