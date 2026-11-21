// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.GatewayRealtimeModel.GatewayRealtimeModelFixture;

namespace E2E.Decision.Tests.GatewayRealtimeModel;

/// <summary>Upstream's <c>describe('getServerWebSocketConfig')</c> of <c>gateway-realtime-model.test.ts</c>.</summary>
public sealed class GetServerWebSocketConfigTests
{
    [Fact]
    public async Task Returns_a_header_authenticated_Gateway_WebSocket_connection()
    {
        var connection = await CreateTestModel().GetServerWebSocketConfigAsync();

        Assert.Equal(RealtimeUrl, connection.Url);
        Assert.Equal(
            new Dictionary<string, string> { ["Authorization"] = "Bearer vcst_test-secret" },
            connection.Headers
        );
    }

    [Fact]
    public async Task Includes_team_scoping_as_a_header_for_server_side_connections()
    {
        var connection = await CreateTestModel(
                "openai/gpt-realtime",
                "https://ai-gateway.vercel.sh/v4/ai",
                "vck_test-token",
                "my-team"
            )
            .GetServerWebSocketConfigAsync();

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer vck_test-token",
                ["x-vercel-ai-gateway-team"] = "my-team",
            },
            connection.Headers
        );
    }

    [Fact]
    public async Task Rejects_unsupported_endpoint_intents()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateTestModel().GetServerWebSocketConfigAsync("transcription")
        );
        Assert.Contains("does not support the 'transcription' session intent", error.Message, StringComparison.Ordinal);
    }
}
