// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.GatewayLanguageModel.GatewayLanguageModelFixture;

namespace E2E.Decision.Tests.GatewayLanguageModel;

/// <summary>Upstream's <c>describe('raw chunks filtering')</c> of <c>gateway-language-model.test.ts</c>.</summary>
public sealed class RawChunksFilteringTests
{
    [Fact]
    public async Task Should_filter_raw_chunks_based_on_includeRawChunks_option()
    {
        var server = new TestServer();
        server.RespondStreamChunks(
            LanguageModelUrl,
            [
                """data: {"type":"stream-start","warnings":[]}""" + "\n\n",
                """data: {"type":"raw","rawValue":{"id":"test-chunk","object":"chat.completion.chunk","choices":[{"delta":{"content":"Hello"}}]}}"""
                    + "\n\n",
                """data: {"type":"text-delta","textDelta":"Hello"}""" + "\n\n",
                """data: {"type":"raw","rawValue":{"id":"test-chunk-2","object":"chat.completion.chunk","choices":[{"delta":{"content":" world"}}]}}"""
                    + "\n\n",
                """data: {"type":"text-delta","textDelta":" world"}""" + "\n\n",
                """data: {"type":"finish","finishReason":"stop","usage":{"prompt_tokens":10,"completion_tokens":5}}"""
                    + "\n\n",
            ]
        );
        using var model = CreateTestModel(server);

        var result = await model.DoStreamAsync(Options(includeRawChunks: false)); // Raw chunks should be filtered out

        var chunks = await ConvertReadableStreamToArrayAsync(result.Stream);

        AssertJsonEqual(
            """
            [
              { "type": "stream-start", "warnings": [] },
              { "textDelta": "Hello", "type": "text-delta" },
              { "textDelta": " world", "type": "text-delta" },
              {
                "finishReason": "stop",
                "type": "finish",
                "usage": { "completion_tokens": 5, "prompt_tokens": 10 }
              }
            ]
            """,
            PartsJson(chunks)
        );
    }

    [Fact]
    public async Task Should_include_raw_chunks_when_includeRawChunks_is_true()
    {
        var server = new TestServer();
        server.RespondStreamChunks(
            LanguageModelUrl,
            [
                """data: {"type":"stream-start","warnings":[]}""" + "\n\n",
                """data: {"type":"raw","rawValue":{"id":"test-chunk","object":"chat.completion.chunk","choices":[{"delta":{"content":"Hello"}}]}}"""
                    + "\n\n",
                """data: {"type":"text-delta","textDelta":"Hello"}""" + "\n\n",
                """data: {"type":"finish","finishReason":"stop","usage":{"prompt_tokens":10,"completion_tokens":5}}"""
                    + "\n\n",
            ]
        );
        using var model = CreateTestModel(server);

        var result = await model.DoStreamAsync(Options(includeRawChunks: true)); // Raw chunks should be included

        var chunks = await ConvertReadableStreamToArrayAsync(result.Stream);

        AssertJsonEqual(
            """
            [
              { "type": "stream-start", "warnings": [] },
              {
                "rawValue": {
                  "choices": [ { "delta": { "content": "Hello" } } ],
                  "id": "test-chunk",
                  "object": "chat.completion.chunk"
                },
                "type": "raw"
              },
              { "textDelta": "Hello", "type": "text-delta" },
              {
                "finishReason": "stop",
                "type": "finish",
                "usage": { "completion_tokens": 5, "prompt_tokens": 10 }
              }
            ]
            """,
            PartsJson(chunks)
        );
    }
}
