// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayProvider.GatewayProviderFixture;

namespace E2E.Decision.Tests.GatewayProvider;

/// <summary>Upstream's <c>describe('Error handling in metadata fetching')</c> of <c>gateway-provider.test.ts</c> (the metadata parts).</summary>
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

    private sealed class ThrowingHandler(Exception error) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => throw error;
    }
}
