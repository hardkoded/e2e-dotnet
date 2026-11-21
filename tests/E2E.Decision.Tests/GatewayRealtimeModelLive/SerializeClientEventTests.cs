// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using static E2E.Decision.Tests.GatewayRealtimeModelLive.GatewayRealtimeModelLiveFixture;

namespace E2E.Decision.Tests.GatewayRealtimeModelLive;

/// <summary>Upstream's <c>describe('serializeClientEvent')</c> of <c>gateway-realtime-model-live.test.ts</c>.</summary>
public sealed class SerializeClientEventTests
{
    [Fact]
    public void Adds_the_model_id_to_session_start()
    {
        var actual = CreateTestModel()
            .SerializeClientEvent(
                new RealtimeModelV4SessionStart(
                    new RealtimeModelV4SessionConfig
                    {
                        Instructions = "Be brief.",
                        Voice = "marin",
                        InputAudioFormat = new SharedV4AudioFormat("audio/pcm", 24000),
                        OutputAudioFormat = new SharedV4AudioFormat("audio/pcm", 24000),
                        ProviderOptions = ProviderOptions(
                            """{"gateway":{"tags":["demo"]},"openai":{"delegation":{"type":"client"}}}"""
                        ),
                    },
                    "start-1"
                )
            );

        Assert.True(
            JsonNode.DeepEquals(
                Json(
                    """
                    {"type":"session-start","modelId":"openai/gpt-live-1.1","eventId":"start-1","config":{
                      "instructions":"Be brief.","voice":"marin",
                      "inputAudioFormat":{"type":"audio/pcm","rate":24000},
                      "outputAudioFormat":{"type":"audio/pcm","rate":24000},
                      "providerOptions":{"gateway":{"tags":["demo"]},"openai":{"delegation":{"type":"client"}}}}}
                    """
                ),
                Assert.IsAssignableFrom<JsonNode>(actual)
            )
        );
    }

    public static TheoryData<RealtimeModelV4ClientEvent, string> MappedEvents =>
        new()
        {
            { new RealtimeModelV4SessionClose(), """{"type":"session-close"}""" },
            { new RealtimeModelV4SessionClose("close-1"), """{"type":"session-close","eventId":"close-1"}""" },
            { new RealtimeModelV4InputAudioMute(), """{"type":"input-audio-mute"}""" },
            { new RealtimeModelV4InputAudioUnmute(), """{"type":"input-audio-unmute"}""" },
            { new RealtimeModelV4InputAudioAppend("AAAA"), """{"type":"input-audio-append","audio":"AAAA"}""" },
            {
                new RealtimeModelV4ContextAppend("Note", null),
                """{"type":"context-append","content":"Note","delegationId":null}"""
            },
            {
                new RealtimeModelV4ContextAppend(
                    "Checking",
                    "d1",
                    "ctx-1",
                    ProviderOptions("""{"openai":{"channel":"thinking"}}""")
                ),
                """{"type":"context-append","content":"Checking","delegationId":"d1","eventId":"ctx-1","providerOptions":{"openai":{"channel":"thinking"}}}"""
            },
        };

    [Theory]
    [MemberData(nameof(MappedEvents))]
    public void Maps_the_event_to_the_normalized_Live_event(RealtimeModelV4ClientEvent clientEvent, string expected) =>
        Assert.True(
            JsonNode.DeepEquals(
                Json(expected),
                Assert.IsAssignableFrom<JsonNode>(CreateTestModel().SerializeClientEvent(clientEvent))
            )
        );

    [Theory]
    [InlineData("""{"openai":{"channel":"shouting"}}""")]
    [InlineData("""{"openai":{"channel":null}}""")]
    [InlineData("""{"openai":{"priority":"high"}}""")]
    [InlineData("""{"google":{"channel":"thinking"}}""")]
    public void Rejects_context_append_provider_options(string providerOptions) =>
        Assert.Throws<UnsupportedFunctionalityException>(() =>
            CreateTestModel()
                .SerializeClientEvent(
                    new RealtimeModelV4ContextAppend("Note", null, ProviderOptions: ProviderOptions(providerOptions))
                )
        );

    public static TheoryData<RealtimeModelV4ClientEvent> UnsupportedEvents =>
        new()
        {
            new RealtimeModelV4SessionUpdate(new RealtimeModelV4SessionConfig()),
            new RealtimeModelV4InputAudioCommit(),
            new RealtimeModelV4InputAudioClear(),
            new RealtimeModelV4ResponseCreate(),
            new RealtimeModelV4ResponseCancel(),
        };

    [Theory]
    [MemberData(nameof(UnsupportedEvents))]
    public void Rejects_the_event_which_Live_does_not_accept(RealtimeModelV4ClientEvent clientEvent) =>
        Assert.Throws<UnsupportedFunctionalityException>(() => CreateTestModel().SerializeClientEvent(clientEvent));
}
