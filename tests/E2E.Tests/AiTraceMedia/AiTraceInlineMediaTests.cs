// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json.Nodes;
using E2E.Internal;
using static E2E.Tests.AiTrace.AiTraceTestSupport;

namespace E2E.Tests.AiTraceMedia;

/// <summary>Upstream's <c>AI trace inline media</c> describe.</summary>
public sealed class AiTraceInlineMediaTests
{
    /// <summary>Records message parts at both telemetry boundaries without making a model call.</summary>
    private static (AiTraceDocument Document, AiTraceStep Step, JsonNode Input, JsonNode Output) RecordMessages(List<object?> messages, object? rawOutput = null)
    {
        var recorder = new AiTraceRecorder();
        recorder.OnStart(new AiTraceStartEvent("media", "ai.generateText"));
        recorder.OnStepStart(new AiTraceStepStartEvent("media", 0, "test", "media", null, messages));
        recorder.OnLanguageModelCallStart(new AiTraceModelCallStartEvent("media", Messages: messages));
        recorder.OnStepEnd(new AiTraceStepEndEvent(
            "media",
            0,
            A(O(("type", "tool-result"), ("toolName", "inspect"), ("output", rawOutput))),
            "stop",
            O(("inputTokens", 1), ("outputTokens", 1)),
            new AiTraceResponse(Messages: messages),
            O(("test", rawOutput))));
        recorder.OnEnd(new AiTraceEndEvent("media"));
        var document = recorder.Drain();
        recorder.Dispose();
        var step = document.Steps[0];
        return (document, step, Parse(step.Input), Parse(step.Output));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(102_400)]
    public void Records_the_decoded_byte_count_for_N_bytes_in_SDK_content_parts(int size)
    {
        var bytes = new byte[size];
        Array.Fill(bytes, (byte)165);
        var encoded = Convert.ToBase64String(bytes);
        var note = $"[binary {size} bytes omitted]";
        var media = new List<object?>
        {
            O(("type", "file"), ("mediaType", "image/png"), ("filename", "capture.png"), ("data", O(("type", "data"), ("data", encoded)))),
            O(("type", "file"), ("mediaType", "application/pdf"), ("data", encoded)),
            O(("type", "image"), ("image", encoded)),
            O(("type", "reasoning-file"), ("mediaType", "image/png"), ("data", O(("type", "data"), ("data", encoded)))),
            O(("type", "image-data"), ("mediaType", "image/png"), ("data", encoded)),
            O(("type", "file-data"), ("mediaType", "application/pdf"), ("data", encoded)),
        };
        var expectedMedia = new List<object?>
        {
            O(("type", "file"), ("mediaType", "image/png"), ("filename", "capture.png"), ("data", O(("type", "data"), ("data", note)))),
            O(("type", "file"), ("mediaType", "application/pdf"), ("data", note)),
            O(("type", "image"), ("image", note)),
            O(("type", "reasoning-file"), ("mediaType", "image/png"), ("data", O(("type", "data"), ("data", note)))),
            O(("type", "image-data"), ("mediaType", "image/png"), ("data", note)),
            O(("type", "file-data"), ("mediaType", "application/pdf"), ("data", note)),
        };
        static List<object?> Messages(List<object?> parts) =>
        [
            O(("role", "user"), ("content", parts.GetRange(0, 3))),
            O(("role", "assistant"), ("content", A(parts[3]))),
            O(("role", "tool"), ("content", A(O(("type", "tool-result"), ("toolCallId", "capture"), ("toolName", "inspect"), ("output", O(("type", "content"), ("value", parts))))))),
        ];
        var messages = Messages(media);
        var before = Internal.AiTrace.Stringify(messages);

        var recorded = RecordMessages(messages);

        var expected = Json(Messages(expectedMedia));
        Assert.True(JsonNode.DeepEquals(expected, recorded.Input["prompt"]));
        Assert.True(JsonNode.DeepEquals(expected, recorded.Output["response"]!["messages"]));
        Assert.Equal(before, Internal.AiTrace.Stringify(messages));
        Assert.True(Encoding.UTF8.GetByteCount(recorded.Step.Input) < 2_000);
        Assert.True(Encoding.UTF8.GetByteCount(recorded.Step.Output!) < 2_000);
        var run = Assert.Single(recorded.Document.Runs);
        Assert.Equal(run.Id, recorded.Step.RunId);
        Assert.Equal("generate", recorded.Step.Type);
        Assert.Equal(1, recorded.Step.StepNumber);
        Assert.True(Same(O(("inputTokens", 1), ("outputTokens", 1)), recorded.Step.Usage));
        if (size > 3)
        {
            Assert.DoesNotContain(encoded, recorded.Step.Input, StringComparison.Ordinal);
            Assert.DoesNotContain(encoded, recorded.Step.Output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Counts_unpadded_and_whitespace_separated_base64_binary_views_and_base64_data_URLs()
    {
        const string dataUrl = "data:image/png;base64,Zg==";
        const string note = "[binary 1 bytes omitted]";
        object?[] files = ["Zg", " Z g==\n", new byte[] { 102 }, new byte[] { 102 }, new byte[1], dataUrl];
        var content = new List<object?>();
        content.AddRange(files.Select(data => O(("type", "file"), ("mediaType", "image/png"), ("data", O(("type", "data"), ("data", data))))));
        content.Add(O(("type", "image"), ("image", new Uri(dataUrl))));
        content.Add(O(("type", "file"), ("mediaType", "image/png"), ("data", O(("type", "url"), ("url", new Uri(dataUrl))))));
        content.Add(O(("type", "image-url"), ("url", dataUrl)));
        content.Add(O(("type", "file-url"), ("url", dataUrl), ("mediaType", "image/png")));

        var recorded = RecordMessages([O(("role", "user"), ("content", content))]);

        var expectedContent = new List<object?>();
        expectedContent.AddRange(files.Select(_ => O(("type", "file"), ("mediaType", "image/png"), ("data", O(("type", "data"), ("data", note))))));
        expectedContent.Add(O(("type", "image"), ("image", note)));
        expectedContent.Add(O(("type", "file"), ("mediaType", "image/png"), ("data", O(("type", "url"), ("url", note)))));
        expectedContent.Add(O(("type", "image-url"), ("url", note)));
        expectedContent.Add(O(("type", "file-url"), ("url", note), ("mediaType", "image/png")));
        var expected = Json(A(O(("role", "user"), ("content", expectedContent))));
        Assert.True(JsonNode.DeepEquals(expected, recorded.Input["prompt"]));
        Assert.True(JsonNode.DeepEquals(expected, recorded.Output["response"]!["messages"]));
    }

    [Fact]
    public void Preserves_text_URLs_references_raw_tool_JSON_and_lookalike_objects_in_tool_arguments()
    {
        const string encoded = "ZmlsZSBjb250ZW50";
        var lookalike = O(("type", "file"), ("mediaType", "image/png"), ("data", O(("type", "data"), ("data", encoded))));
        var rawOutput = O(("png", encoded), ("nested", lookalike), ("messages", A(O(("role", "user"), ("content", A(lookalike))))));
        var url = new Uri("https://example.com/image.png");
        var reference = O(("provider", "uploaded-file-id"));
        var messages = new List<object?>
        {
            O(("role", "user"), ("content", A(
                O(("type", "text"), ("text", encoded)),
                O(("type", "file"), ("mediaType", "text/plain"), ("data", O(("type", "text"), ("text", encoded)))),
                O(("type", "image"), ("image", url)),
                O(("type", "image"), ("image", reference)),
                O(("type", "file"), ("mediaType", "image/png"), ("data", url)),
                O(("type", "file"), ("mediaType", "image/png"), ("data", url.AbsoluteUri)),
                O(("type", "file"), ("mediaType", "image/png"), ("data", O(("type", "url"), ("url", url)))),
                O(("type", "file"), ("mediaType", "image/png"), ("data", O(("type", "reference"), ("reference", reference)))),
                O(("type", "file"), ("mediaType", "text/plain"), ("data", "data:text/plain,hello"))))),
            O(("role", "assistant"), ("content", A(O(("type", "tool-call"), ("toolName", "inspect"), ("input", rawOutput))))),
            O(("role", "tool"), ("content", A(
                O(("type", "tool-result"), ("toolName", "inspect"), ("output", O(("type", "json"), ("value", rawOutput)))),
                O(("type", "tool-result"), ("toolName", "inspect"), ("output", O(("type", "error-json"), ("value", rawOutput)))),
                O(("type", "tool-result"), ("toolName", "inspect"), ("output", O(("type", "text"), ("value", encoded))))))),
        };

        var recorded = RecordMessages(messages, rawOutput);

        var expected = Json(messages);
        Assert.True(JsonNode.DeepEquals(expected, recorded.Input["prompt"]));
        Assert.True(JsonNode.DeepEquals(expected, recorded.Output["response"]!["messages"]));
        Assert.True(JsonNode.DeepEquals(Json(rawOutput), recorded.Output["content"]![0]!["output"]));
        Assert.True(JsonNode.DeepEquals(Json(rawOutput), recorded.Output["providerMetadata"]!["test"]));
    }
}
