// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using static E2E.Decision.Sdk.Gateway.GatewayRealtimeAuth;
using static E2E.Decision.Tests.GatewayRealtimeModelLive.GatewayRealtimeModelLiveFixture;

namespace E2E.Decision.Tests.GatewayRealtimeModelLive;

/// <summary>Upstream's <c>describe('GatewayRealtimeModelLive')</c> of <c>gateway-realtime-model-live.test.ts</c>.</summary>
public sealed class GatewayRealtimeModelLiveTests
{
    [Fact]
    public void Declares_continuous_Live_capabilities()
    {
        var model = CreateTestModel();
        Assert.Equal("v4", model.SpecificationVersion);
        Assert.Equal("gateway.live", model.Provider);
        Assert.Equal("openai/gpt-live-1.1", model.ModelId);
        var capabilities = model.Capabilities!;
        Assert.Equal("continuous", capabilities.Conversation);
        Assert.Equal(["websocket"], capabilities.Transports);
        Assert.Equal(["client-secret-websocket", "server-websocket"], capabilities.Connections);
        Assert.Equal("session-start", capabilities.Startup);
        Assert.Equal("session-close", capabilities.Finalization);
    }

    [Fact]
    public void Carries_the_token_and_team_scope_in_WebSocket_subprotocols()
    {
        var config = CreateTestModel(teamIdOrSlug: "my-team").GetWebSocketConfig("vcst_x", LiveUrl);
        Assert.Equal(LiveUrl, config.Url);
        Assert.Equal([GatewayRealtimeSubprotocol, GatewayAuthSubprotocolPrefix + "vcst_x"], config.Protocols!.Take(2));
        Assert.Equal("my-team", GetGatewayRealtimeTeamIdOrSlug(string.Join(", ", config.Protocols!)));
    }

    [Fact]
    public async Task Returns_the_live_model_url_and_defined_headers_for_server_connections()
    {
        var descriptor = await CreateTestModel().GetServerWebSocketConfigAsync();
        Assert.Equal(LiveUrl, descriptor.Url);
        Assert.Equal(
            new Dictionary<string, string> { ["Authorization"] = "Bearer vck_test-token" },
            descriptor.Headers
        );
    }

    [Fact]
    public async Task Rejects_non_conversation_intents()
    {
        var model = CreateTestModel();
        const string intent = RealtimeModelV4SessionIntent.Transcription;
        var server = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            model.GetServerWebSocketConfigAsync(intent)
        );
        Assert.Contains(
            "does not support the 'transcription' session intent",
            server.Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "does not support",
            Assert
                .Throws<InvalidOperationException>(() =>
                    model.SerializeClientEvent(new RealtimeModelV4SessionClose(), intent)
                )
                .Message,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "does not support",
            Assert
                .Throws<InvalidOperationException>(() =>
                    model.BuildSessionConfig(new RealtimeModelV4SessionConfig(), intent)
                )
                .Message,
            StringComparison.Ordinal
        );
    }
}
