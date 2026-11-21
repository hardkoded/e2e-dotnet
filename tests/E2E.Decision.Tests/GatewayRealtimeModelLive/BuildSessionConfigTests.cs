// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using static E2E.Decision.Tests.GatewayRealtimeModelLive.GatewayRealtimeModelLiveFixture;

namespace E2E.Decision.Tests.GatewayRealtimeModelLive;

/// <summary>Upstream's <c>describe('buildSessionConfig')</c> of <c>gateway-realtime-model-live.test.ts</c>.</summary>
public sealed class BuildSessionConfigTests
{
    [Theory]
    [InlineData("tools")]
    [InlineData("turnDetection")]
    [InlineData("outputModalities")]
    public void Rejects_the_unsupported_setting(string key)
    {
        var config = key switch
        {
            "tools" => new RealtimeModelV4SessionConfig { Tools = [] },
            "turnDetection" => new RealtimeModelV4SessionConfig
            {
                TurnDetection = new RealtimeModelV4TurnDetection("server-vad"),
            },
            _ => new RealtimeModelV4SessionConfig { OutputModalities = [] },
        };

        var error = Assert.Throws<UnsupportedFunctionalityException>(() =>
            CreateTestModel().BuildSessionConfig(config)
        );
        Assert.Contains("AI Gateway Live session setting: " + key, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"openai":{"client":{"dataChannel":{}}}}""", typeof(UnsupportedFunctionalityException))]
    [InlineData("""{"openai":{"delegation":{"type":"responses"}}}""", typeof(UnsupportedFunctionalityException))]
    [InlineData("""{"openai":{"delegation":"client"}}""", typeof(UnsupportedFunctionalityException))]
    [InlineData("""{"openai":{"voice":null}}""", typeof(InvalidArgumentException))]
    [InlineData("""{"openai":{"store":null}}""", typeof(InvalidArgumentException))]
    [InlineData("""{"openai":null}""", typeof(InvalidArgumentException))]
    [InlineData("""{"gateway":"tags"}""", typeof(InvalidArgumentException))]
    [InlineData("""{"google":{}}""", typeof(UnsupportedFunctionalityException))]
    public void Rejects_the_provider_options(string providerOptions, Type error) =>
        Assert.Throws(error, () =>
            CreateTestModel()
                .BuildSessionConfig(
                    new RealtimeModelV4SessionConfig { ProviderOptions = ProviderOptions(providerOptions) }
                )
        );

    public static TheoryData<string, string> MalformedOpenAiOptions
    {
        get
        {
            var data = new TheoryData<string, string>
            {
                { "voice", "\"marin\"" },
                { "voice", """{"id":""}""" },
                { "voice", """{"id":"v","name":"extra"}""" },
                { "store", "\"false\"" },
                { "input", "{}" },
                { "input", "[null]" },
                { "input", "[" + Message("user", "text").ToJsonString() + "]" },
                { "input", "[" + Message("assistant", "input_text").ToJsonString() + "]" },
                { "input", "[" + Message("system", "input_text").ToJsonString() + "]" },
                {
                    "input",
                    """[{"type":"message","role":"user","content":[{"type":"input_text","text":"Hi"}],"extra":true}]"""
                },
                { "input", """[{"type":"message","role":"user","content":[]}]""" },
                {
                    "input",
                    "[" + string.Join(",", Enumerable.Repeat(Message("user", "input_text").ToJsonString(), 129)) + "]"
                },
            };
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(MalformedOpenAiOptions))]
    public void Rejects_the_malformed_openai_value(string key, string value) =>
        Assert.Throws<InvalidArgumentException>(() =>
            CreateTestModel()
                .BuildSessionConfig(
                    new RealtimeModelV4SessionConfig
                    {
                        ProviderOptions = ProviderOptions("{\"openai\":{\"" + key + "\":" + value + "}}"),
                    }
                )
        );

    [Fact]
    public void Rejects_delegation_objects_with_extra_keys() =>
        Assert.Throws<UnsupportedFunctionalityException>(() =>
            CreateTestModel()
                .BuildSessionConfig(
                    new RealtimeModelV4SessionConfig
                    {
                        ProviderOptions = ProviderOptions(
                            """{"openai":{"delegation":{"type":"client","extra":true}}}"""
                        ),
                    }
                )
        );

    [Fact]
    public void Accepts_128_input_messages_and_every_supported_role_and_part_type()
    {
        var input = new JsonArray(
            Message("developer", "input_text"),
            Message("user", "input_text"),
            Message("assistant", "text"),
            Message("assistant", "output_text")
        );
        foreach (var _ in Enumerable.Range(0, 124))
        {
            input.Add(Message("user", "input_text"));
        }

        var config = new RealtimeModelV4SessionConfig
        {
            ProviderOptions = new Dictionary<string, JsonNode?> { ["openai"] = new JsonObject { ["input"] = input } },
        };
        Assert.Same(config, CreateTestModel().BuildSessionConfig(config));
    }

    [Theory]
    [InlineData("commentary")]
    [InlineData("instructions")]
    [InlineData("thinking")]
    public void Accepts_the_context_channel(string channel)
    {
        var actual = Assert.IsAssignableFrom<JsonObject>(
            CreateTestModel()
                .SerializeClientEvent(
                    new RealtimeModelV4ContextAppend(
                        "Note",
                        null,
                        ProviderOptions: ProviderOptions("{\"openai\":{\"channel\":\"" + channel + "\"}}")
                    )
                )
        );
        Assert.Equal(channel, (string?)actual["providerOptions"]!["openai"]!["channel"]);
    }

    [Fact]
    public void Accepts_supported_provider_options_and_leaves_gateway_options_open()
    {
        var providerOptions = ProviderOptions(
            """
            {"gateway":{"tags":["demo"],"futureOption":{"nested":true}},
             "openai":{"delegation":null,
               "input":[{"type":"message","role":"user","content":[{"type":"input_text","text":"Hi"}]}],
               "store":false,"voice":{"id":"voice_1"}}}
            """
        );
        var config = new RealtimeModelV4SessionConfig { ProviderOptions = providerOptions };
        Assert.Same(
            providerOptions,
            CreateTestModel().BuildSessionConfig(config) is RealtimeModelV4SessionConfig built
                ? built.ProviderOptions
                : null
        );
    }

    [Fact]
    public void Ignores_unset_settings()
    {
        var config = new RealtimeModelV4SessionConfig
        {
            Instructions = "Hi",
            Tools = null,
            TurnDetection = null,
        };
        var built = Assert.IsType<RealtimeModelV4SessionConfig>(CreateTestModel().BuildSessionConfig(config));
        Assert.Equal("Hi", built.Instructions);
        Assert.Null(built.Tools);
        Assert.Null(built.TurnDetection);
    }

    [Theory]
    [InlineData("audio/pcm", 48000)]
    [InlineData("audio/pcm", null)]
    [InlineData("audio/pcmu", 16000)]
    [InlineData("audio/opus", 24000)]
    public void Rejects_the_unsupported_audio_format(string type, int? rate) =>
        Assert.Throws<UnsupportedFunctionalityException>(() =>
            CreateTestModel()
                .BuildSessionConfig(
                    new RealtimeModelV4SessionConfig { InputAudioFormat = new SharedV4AudioFormat(type, rate) }
                )
        );

    [Fact]
    public void Accepts_the_supported_audio_formats()
    {
        foreach (
            var format in new[]
            {
                new SharedV4AudioFormat("audio/pcm", 16000),
                new SharedV4AudioFormat("audio/pcm", 24000),
                new SharedV4AudioFormat("audio/pcma", 8000),
                new SharedV4AudioFormat("audio/pcmu", 8000),
            }
        )
        {
            var built = Assert.IsType<RealtimeModelV4SessionConfig>(
                CreateTestModel()
                    .BuildSessionConfig(
                        new RealtimeModelV4SessionConfig { InputAudioFormat = format, OutputAudioFormat = format }
                    )
            );
            Assert.Equal(format, built.InputAudioFormat);
            Assert.Equal(format, built.OutputAudioFormat);
        }
    }

    [Fact]
    public void Requires_matching_input_and_output_formats() =>
        Assert.Throws<InvalidArgumentException>(() =>
            CreateTestModel()
                .BuildSessionConfig(
                    new RealtimeModelV4SessionConfig
                    {
                        InputAudioFormat = new SharedV4AudioFormat("audio/pcm", 16000),
                        OutputAudioFormat = new SharedV4AudioFormat("audio/pcm", 24000),
                    }
                )
        );
}
