// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayDecisionModel.GatewayDecisionModelFixture;
using Env = E2E.Decision.Sdk.Gateway.VercelEnvironment;

namespace E2E.Decision.Tests.GatewayProvider;

/// <summary>The helpers at the top of upstream's <c>gateway-provider.test.ts</c>.</summary>
internal static class GatewayProviderFixture
{
    public const string BaseUrl = "https://api.example.com";

    /// <summary>Runs <paramref name="action"/> with the environment variables set (null unsets them), then restores them.</summary>
    public static async Task WithEnvironmentAsync(IReadOnlyDictionary<string, string?> variables, Func<Task> action)
    {
        var names = new[]
        {
            "AI_GATEWAY_API_KEY",
            "VERCEL_OIDC_TOKEN",
            "VERCEL_DEPLOYMENT_ID",
            "VERCEL_ENV",
            "VERCEL_REGION",
            "VERCEL_PROJECT_ID",
        };
        var previous = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            // Upstream's beforeEach removes AI_GATEWAY_API_KEY; the other variables start unset here too.
            foreach (var name in names)
            {
                Environment.SetEnvironmentVariable(name, variables.GetValueOrDefault(name));
            }

            // Upstream mocks getVercelOidcToken (vi.mocked): it gives the VERCEL_OIDC_TOKEN set here, or fails like an unavailable token.
            Env.OidcTokenOverride = _ =>
                Environment.GetEnvironmentVariable("VERCEL_OIDC_TOKEN") is { Length: > 0 } token
                    ? Task.FromResult(token)
                    : throw new InvalidOperationException("OIDC token not available");

            await action();
        }
        finally
        {
            Env.OidcTokenOverride = null;
            foreach (var (name, value) in previous)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }

    /// <summary>
    /// The headers the provider's decision model sends: upstream resolves <c>config.headers</c> of the model the provider constructs,
    /// the port reads them from the request the model makes.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, string>> ResolveHeadersAsync(GatewayProviderSettings settings)
    {
        var baseUrl = settings.BaseUrl ?? BaseUrl;
        var server = new TestServer();
        server.RespondJson(
            baseUrl + "/decision-model",
            """{"answers":{"correct":{"type":"boolean","probability":0.97}}}"""
        );
        var provider = new E2E.Decision.Sdk.Gateway.GatewayProvider(
            new GatewayProviderSettings
            {
                BaseUrl = baseUrl,
                ApiKey = settings.ApiKey,
                Headers = settings.Headers,
                TeamIdOrSlug = settings.TeamIdOrSlug,
                HttpClient = server.Client,
            }
        );
        using var model = provider.DecisionModel("test-model");
        await model.DoDecideAsync(Call(), CancellationToken.None);
        return server.Calls[0].RequestHeaders;
    }
}
