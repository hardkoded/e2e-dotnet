// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using static E2E.Decision.Tests.GatewayRealtimeModelLive.GatewayRealtimeModelLiveFixture;

namespace E2E.Decision.Tests.GatewayRealtimeModelLive;

/// <summary>Upstream's <c>describe('parseServerEvent')</c> of <c>gateway-realtime-model-live.test.ts</c>.</summary>
public sealed class ParseServerEventTests
{
    [Fact]
    public void Passes_normalized_events_through()
    {
        var serverEvent = new RealtimeModelV4SessionClosed(
            "live_1",
            new RealtimeModelV4SessionSeconds(14),
            "close_requested"
        )
        {
            Raw = Json("""{"type":"session.closed"}"""),
        };
        Assert.Same(serverEvent, CreateTestModel().ParseServerEvent(serverEvent));

        // A JSON frame is read as the normalized event.
        var parsed = Assert.IsType<RealtimeModelV4SessionClosed>(
            CreateTestModel()
                .ParseServerEvent(
                    Json(
                        """{"type":"session-closed","sessionId":"live_1","usage":{"seconds":14},"reason":"close_requested","raw":{"type":"session.closed"}}"""
                    )
                )
        );
        Assert.Equal("live_1", parsed.SessionId);
        Assert.Equal(14, parsed.Usage.Seconds);
        Assert.Equal("close_requested", parsed.Reason);
        Assert.True(JsonNode.DeepEquals(Json("""{"type":"session.closed"}"""), parsed.Raw));
    }

    [Fact]
    public void Keeps_Gateway_error_frames_as_raw()
    {
        var frame = Json(
            """{"type":"error","code":"invalid_request_error","message":"Live requires a canonical model slug"}"""
        );

        var error = Assert.IsType<RealtimeModelV4Error>(CreateTestModel().ParseServerEvent(frame));

        Assert.Equal("invalid_request_error", error.Code);
        Assert.Equal("Live requires a canonical model slug", error.Message);
        Assert.Same(frame, error.Raw);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"text\"")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{"type":1}""")]
    public void Reports_the_value_as_an_invalid_server_event(string json)
    {
        var raw = JsonNode.Parse(json);

        var error = Assert.IsType<RealtimeModelV4Error>(CreateTestModel().ParseServerEvent(raw));

        Assert.Equal("invalid_server_event", error.Code);
        Assert.Equal("Invalid AI Gateway Live server event.", error.Message);
        Assert.Same(raw, error.Raw);
    }
}
