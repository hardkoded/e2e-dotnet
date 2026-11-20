// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayProvider.GatewayProviderFixture;

namespace E2E.Decision.Tests.GatewayProvider;

/// <summary>Upstream's <c>describe('getGenerationInfo method')</c> of <c>gateway-provider.test.ts</c>.</summary>
[Collection("GatewayProviderEnvironment")]
public sealed class GetGenerationInfoMethodTests
{
    private static readonly Dictionary<string, string?> NoEnvironment = [];

    [Fact]
    public Task Should_fetch_generation_info_successfully() => WithEnvironmentAsync(NoEnvironment, async () =>
        {
            var server = ServerFor(DefaultBaseUrl, GenerationInfoBody);
            var provider = ProviderOn(server, apiKey: "test-key");

            var generationInfo = await provider.GetGenerationInfoAsync(
                new GatewayGenerationInfoParams { Id = "gen_test" }
            );

            Assert.Equal("gen_test", generationInfo.Id);
            Assert.Equal("https://ai-gateway.vercel.sh/v1/generation?id=gen_test", server.Calls[0].RequestUrl);
        });

    [Fact]
    public Task Should_work_with_OIDC_authentication_in_generation_info() =>
        WithEnvironmentAsync(NoEnvironment, async () =>
        {
            var server = ServerFor(DefaultBaseUrl, GenerationInfoBody);
            var oidcCalls = 0;
            var provider = ProviderOn(server, getVercelOidcToken: _ =>
            {
                oidcCalls++;
                return Task.FromResult("oidc-token");
            });

            var generationInfo = await provider.GetGenerationInfoAsync(
                new GatewayGenerationInfoParams { Id = "gen_test" }
            );

            Assert.NotNull(generationInfo);
            Assert.True(oidcCalls > 0);
        });
}
