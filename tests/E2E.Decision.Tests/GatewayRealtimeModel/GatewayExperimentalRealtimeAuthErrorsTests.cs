// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayProvider.GatewayProviderFixture;

namespace E2E.Decision.Tests.GatewayRealtimeModel;

/// <summary>Upstream's <c>describe('gateway.experimental_realtime auth errors')</c> of <c>gateway-realtime-model.test.ts</c>.</summary>
[Collection("GatewayProviderEnvironment")]
public sealed class GatewayExperimentalRealtimeAuthErrorsTests
{
    [Fact]
    public Task Reuses_HTTP_auth_headers_for_OIDC_server_connections() =>
        WithEnvironmentAsync(new Dictionary<string, string?> { ["VERCEL_OIDC_TOKEN"] = "oidc-test-token" }, async () =>
        {
            var gateway = new E2E.Decision.Sdk.Gateway.GatewayProvider(
                new GatewayProviderSettings
                {
                    TeamIdOrSlug = "team-example",
                    Headers = new Dictionary<string, string> { ["x-custom"] = "example" },
                }
            );
            var model = gateway.ExperimentalRealtime.Create("openai/gpt-realtime");
            var descriptor = await model.GetServerWebSocketConfigAsync()!;

            Assert.Equal("Bearer oidc-test-token", descriptor.Headers["authorization"]);
            Assert.Equal("oidc", descriptor.Headers["ai-gateway-auth-method"]);
            Assert.Equal("0.0.1", descriptor.Headers["ai-gateway-protocol-version"]);
            Assert.Equal("team-example", descriptor.Headers["x-vercel-ai-gateway-team"]);
            Assert.Equal("example", descriptor.Headers["x-custom"]);
            // Upstream's user agent names ai-sdk-gateway; the port's names e2e-dotnet.
            Assert.Contains("e2e-dotnet/", descriptor.Headers["user-agent"], StringComparison.Ordinal);
        });

    [Fact]
    public Task Wraps_getToken_auth_failures_in_a_GatewayAuthenticationError() =>
        // No API key and no OIDC token: the realtime auth path must surface the same GatewayAuthenticationError the other gateway
        // models throw.
        WithEnvironmentAsync(new Dictionary<string, string?>(), async () =>
        {
            var gateway = new E2E.Decision.Sdk.Gateway.GatewayProvider(new GatewayProviderSettings());
            await Assert.ThrowsAsync<GatewayAuthenticationException>(() =>
                gateway.ExperimentalRealtime.GetTokenAsync(
                    new GatewayRealtimeGetTokenOptions { Model = "openai/gpt-realtime-2" }
                )
            );
        });
}
