// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using static E2E.Decision.Tests.GatewayRealtimeModel.GatewayRealtimeModelFixture;

namespace E2E.Decision.Tests.GatewayRealtimeModel;

/// <summary>Upstream's <c>describe('normalized identity codec')</c> of <c>gateway-realtime-model.test.ts</c>.</summary>
public sealed class NormalizedIdentityCodecTests
{
    [Fact]
    public void Passes_server_events_through_unchanged()
    {
        var serverEvent = new RealtimeModelV4ResponseCreated("resp_1") { Raw = new JsonObject() };
        Assert.Same(serverEvent, CreateTestModel().ParseServerEvent(serverEvent));
    }

    [Fact]
    public void Passes_client_events_through_unchanged()
    {
        var clientEvent = new RealtimeModelV4ResponseCreate();
        Assert.Same(clientEvent, CreateTestModel().SerializeClientEvent(clientEvent));
    }

    [Fact]
    public void Passes_session_config_through_unchanged()
    {
        var config = new RealtimeModelV4SessionConfig { Instructions = "be concise", Voice = "alloy" };
        Assert.Same(config, CreateTestModel().BuildSessionConfig(config));
    }

    [Fact]
    public void Passes_session_update_provider_options_through_unchanged()
    {
        // "Support all the things": gateway provider options (tags/user/byok/...) ride session.update exactly as they ride the request
        // body on the non-realtime routes. The identity codec must not strip them.
        var clientEvent = new RealtimeModelV4SessionUpdate(
            new RealtimeModelV4SessionConfig
            {
                Instructions = "be concise",
                ProviderOptions = new Dictionary<string, JsonNode?>
                {
                    ["gateway"] = new JsonObject { ["tags"] = new JsonArray("demo"), ["user"] = "user-1" },
                },
            }
        );
        Assert.Same(clientEvent, CreateTestModel().SerializeClientEvent(clientEvent));
    }
}
