// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Decision.Sdk;
using static E2E.Decision.Tests.GatewayLanguageModel.GatewayLanguageModelFixture;

namespace E2E.Decision.Tests.GatewayLanguageModel;

/// <summary>
/// Upstream's <c>describe('timestamp conversion')</c> of <c>gateway-language-model.test.ts</c>. A part's <c>Json</c> keeps the timestamp
/// string the Gateway sent; <c>Timestamp</c> is upstream's <c>Date</c>.
/// </summary>
public sealed class TimestampConversionTests
{
    private const string Finish =
        """data: {"type":"finish","finishReason":"stop","usage":{"prompt_tokens":10,"completion_tokens":5}}""" + "\n\n";

    private static async Task<List<LanguageModelV4StreamPart>> StreamAsync(params string[] chunks)
    {
        var server = new TestServer();
        server.RespondStreamChunks(LanguageModelUrl, chunks);
        using var model = CreateTestModel(server);

        var result = await model.DoStreamAsync(Options(includeRawChunks: false));

        return await ConvertReadableStreamToArrayAsync(result.Stream);
    }

    [Fact]
    public async Task Should_convert_timestamp_strings_to_Date_objects_in_response_metadata_chunks()
    {
        var timestampString = "2023-12-07T10:30:00.000Z";

        var chunks = await StreamAsync(
            """data: {"type":"stream-start","warnings":[]}""" + "\n\n",
            $$"""data: {"type":"response-metadata","id":"test-id","modelId":"test-model","timestamp":"{{timestampString}}"}"""
                + "\n\n",
            """data: {"type":"text-delta","textDelta":"Hello"}""" + "\n\n",
            Finish
        );

        Assert.Equal(4, chunks.Count);
        AssertJsonEqual(
            """{"type":"stream-start","warnings":[]}""",
            System.Text.Json.Nodes.JsonNode.Parse(chunks[0].Json.GetRawText())
        );

        // Check that the response-metadata chunk has timestamp converted to Date
        var responseMetadataChunk = chunks[1];
        Assert.Equal("response-metadata", responseMetadataChunk.Type);
        Assert.Equal("test-id", responseMetadataChunk.Json.GetProperty("id").GetString());
        Assert.Equal("test-model", responseMetadataChunk.Json.GetProperty("modelId").GetString());
        Assert.NotNull(responseMetadataChunk.Timestamp);
        Assert.Equal(
            timestampString,
            responseMetadataChunk.Timestamp.Value.UtcDateTime.ToString(
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                System.Globalization.CultureInfo.InvariantCulture
            )
        );
    }

    [Fact]
    public async Task Should_not_modify_timestamp_if_it_is_already_a_Date_object()
    {
        var timestampDate = new DateTimeOffset(2023, 12, 7, 10, 30, 0, TimeSpan.Zero);

        // Use standard stream-chunks format with Date serialized as string, then manually parse
        var chunks = await StreamAsync(
            """data: {"type":"stream-start","warnings":[]}""" + "\n\n",
            $$"""data: {"type":"response-metadata","id":"test-id","modelId":"test-model","timestamp":"{{timestampDate:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}}"}"""
                + "\n\n",
            """data: {"type":"text-delta","textDelta":"Hello"}""" + "\n\n",
            Finish
        );

        Assert.Equal(4, chunks.Count);

        // Check that the response-metadata chunk timestamp is converted to Date
        var responseMetadataChunk = chunks[1];
        Assert.Equal("response-metadata", responseMetadataChunk.Type);
        Assert.Equal("test-id", responseMetadataChunk.Json.GetProperty("id").GetString());
        Assert.Equal("test-model", responseMetadataChunk.Json.GetProperty("modelId").GetString());
        Assert.NotNull(responseMetadataChunk.Timestamp);
    }

    [Fact]
    public async Task Should_not_modify_response_metadata_chunks_without_timestamp()
    {
        var chunks = await StreamAsync(
            """data: {"type":"stream-start","warnings":[]}""" + "\n\n",
            """data: {"type":"response-metadata","id":"test-id","modelId":"test-model"}""" + "\n\n",
            """data: {"type":"text-delta","textDelta":"Hello"}""" + "\n\n",
            Finish
        );

        Assert.Equal(4, chunks.Count);

        // Check that the response-metadata chunk without timestamp is unchanged
        var responseMetadataChunk = chunks[1];
        AssertJsonEqual(
            """{"type":"response-metadata","id":"test-id","modelId":"test-model"}""",
            System.Text.Json.Nodes.JsonNode.Parse(responseMetadataChunk.Json.GetRawText())
        );
        Assert.False(responseMetadataChunk.Json.TryGetProperty("timestamp", out _));
        Assert.Null(responseMetadataChunk.Timestamp);
    }

    [Fact]
    public async Task Should_handle_null_timestamp_values_gracefully()
    {
        var chunks = await StreamAsync(
            """data: {"type":"stream-start","warnings":[]}""" + "\n\n",
            """data: {"type":"response-metadata","id":"test-id","modelId":"test-model","timestamp":null}""" + "\n\n",
            """data: {"type":"text-delta","textDelta":"Hello"}""" + "\n\n",
            Finish
        );

        Assert.Equal(4, chunks.Count);

        // Check that null timestamp is left as null
        var responseMetadataChunk = chunks[1];
        AssertJsonEqual(
            """{"type":"response-metadata","id":"test-id","modelId":"test-model","timestamp":null}""",
            System.Text.Json.Nodes.JsonNode.Parse(responseMetadataChunk.Json.GetRawText())
        );
        Assert.Equal(JsonValueKind.Null, responseMetadataChunk.Json.GetProperty("timestamp").ValueKind);
        Assert.Null(responseMetadataChunk.Timestamp);
    }

    [Fact]
    public async Task Should_only_convert_timestamps_for_response_metadata_chunks_not_other_chunk_types()
    {
        var timestampString = "2023-12-07T10:30:00.000Z";

        var chunks = await StreamAsync(
            """data: {"type":"stream-start","warnings":[]}""" + "\n\n",
            $$"""data: {"type":"text-delta","textDelta":"Hello","timestamp":"{{timestampString}}"}""" + "\n\n",
            $$"""data: {"type":"finish","finishReason":"stop","usage":{"prompt_tokens":10,"completion_tokens":5},"timestamp":"{{timestampString}}"}"""
                + "\n\n"
        );

        // Check that timestamps in non-response-metadata chunks are left as strings
        // Note: These chunks don't typically have timestamp properties in the real types,
        // but this test verifies our conversion logic only affects response-metadata chunks
        AssertJsonEqual(
            """
            [
              { "type": "stream-start", "warnings": [] },
              { "textDelta": "Hello", "timestamp": "2023-12-07T10:30:00.000Z", "type": "text-delta" },
              {
                "finishReason": "stop",
                "timestamp": "2023-12-07T10:30:00.000Z",
                "type": "finish",
                "usage": { "completion_tokens": 5, "prompt_tokens": 10 }
              }
            ]
            """,
            PartsJson(chunks)
        );
        Assert.All(chunks, chunk => Assert.Null(chunk.Timestamp));
    }
}
