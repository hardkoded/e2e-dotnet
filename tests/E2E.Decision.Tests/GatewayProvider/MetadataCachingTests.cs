// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayProvider.GatewayProviderFixture;

namespace E2E.Decision.Tests.GatewayProvider;

/// <summary>Upstream's <c>describe('metadata caching')</c> of <c>gateway-provider.test.ts</c>.</summary>
[Collection("GatewayProviderEnvironment")]
public sealed class MetadataCachingTests
{
    private const string OneModel = """
        {"models":[{"id":"test-model","name":"test-model","specification":{"specificationVersion":"v4","provider":"test","modelId":"test-model"}}]}
        """;

    private static readonly Dictionary<string, string?> NoEnvironment = [];

    [Fact]
    public Task Should_cache_metadata_for_the_specified_refresh_interval() =>
        WithEnvironmentAsync(NoEnvironment, async () =>
        {
            var server = ServerFor("https://api.example.com", OneModel);
            var currentTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var provider = ProviderOn(
                server,
                baseUrl: "https://api.example.com",
                apiKey: "test-api-key",
                metadataCacheRefreshMillis: 10000,
                currentDate: () => currentTime
            );

            // First call should fetch metadata.
            await provider.GetAvailableModelsAsync();
            Assert.Single(server.Calls);

            // Second immediate call should use cache.
            await provider.GetAvailableModelsAsync();
            Assert.Single(server.Calls);

            // Advance time by 9 seconds (should still use cache).
            currentTime += TimeSpan.FromMilliseconds(9000);
            await provider.GetAvailableModelsAsync();
            Assert.Single(server.Calls);

            // Advance time past 10 seconds (should refresh).
            currentTime += TimeSpan.FromMilliseconds(2000);
            await provider.GetAvailableModelsAsync();
            Assert.Equal(2, server.Calls.Count);
        });

    [Fact]
    public Task Should_use_default_5_minute_refresh_interval_when_not_specified() =>
        WithEnvironmentAsync(NoEnvironment, async () =>
        {
            var server = ServerFor("https://api.example.com", OneModel);
            var currentTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var provider = ProviderOn(
                server,
                baseUrl: "https://api.example.com",
                apiKey: "test-api-key",
                currentDate: () => currentTime
            );

            // First call should fetch metadata.
            await provider.GetAvailableModelsAsync();
            Assert.Single(server.Calls);

            // Advance time by 4 minutes (should still use cache).
            currentTime += TimeSpan.FromMinutes(4);
            await provider.GetAvailableModelsAsync();
            Assert.Single(server.Calls);

            // Advance time past 5 minutes (should refresh).
            currentTime += TimeSpan.FromMinutes(2);
            await provider.GetAvailableModelsAsync();
            Assert.Equal(2, server.Calls.Count);
        });
}
