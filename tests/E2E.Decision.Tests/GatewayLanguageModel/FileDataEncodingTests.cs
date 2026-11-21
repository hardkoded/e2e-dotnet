// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using static E2E.Decision.Tests.GatewayLanguageModel.GatewayLanguageModelFixture;

namespace E2E.Decision.Tests.GatewayLanguageModel;

/// <summary>Upstream's <c>describe('file data encoding')</c> of <c>gateway-language-model.test.ts</c>.</summary>
public sealed class FileDataEncodingTests
{
    private static TestServer PrepareResponse()
    {
        var server = new TestServer();
        server.RespondJson(
            LanguageModelUrl,
            """
            {
              "id": "test-id",
              "created": 1711115037,
              "model": "test-model",
              "content": { "type": "text", "text": "" },
              "finish_reason": "stop",
              "usage": { "prompt_tokens": 4, "completion_tokens": 30 }
            }
            """
        );
        return server;
    }

    [Fact]
    public async Task Should_base64_encode_Uint8Array_data_in_a_reasoning_file_part()
    {
        var server = PrepareResponse();
        byte[] bytes = [1, 2, 3, 4];
        var expectedBase64 = Convert.ToBase64String(bytes);
        IReadOnlyList<LanguageModelV4Message> prompt =
        [
            new LanguageModelV4AssistantMessage([
                new LanguageModelV4ReasoningFilePart(new SharedV4FileDataData(bytes), "image/png"),
            ]),
        ];
        using var model = CreateTestModel(server);

        await model.DoGenerateAsync(Options(prompt));

        var requestBody = server.Calls[0].RequestBodyJson!;
        AssertJsonEqual(
            $$"""{"type":"data","data":"{{expectedBase64}}"}""",
            requestBody["prompt"]![0]!["content"]![0]!["data"]
        );
    }

    [Fact]
    public async Task Should_base64_encode_Uint8Array_data_in_tool_result_file_content()
    {
        var server = PrepareResponse();
        byte[] bytes = [5, 6, 7, 8];
        var expectedBase64 = Convert.ToBase64String(bytes);
        IReadOnlyList<LanguageModelV4Message> prompt =
        [
            new LanguageModelV4ToolMessage([
                new LanguageModelV4ToolResultPart(
                    "call_1",
                    "render",
                    new LanguageModelV4ToolResultOutputContent([
                        new LanguageModelV4ToolResultContentFile(new SharedV4FileDataData(bytes), "image/png"),
                    ])
                ),
            ]),
        ];
        using var model = CreateTestModel(server);

        await model.DoGenerateAsync(Options(prompt));

        var requestBody = server.Calls[0].RequestBodyJson!;
        AssertJsonEqual(
            $$"""{"type":"data","data":"{{expectedBase64}}"}""",
            requestBody["prompt"]![0]!["content"]![0]!["output"]!["value"]![0]!["data"]
        );
    }

    [Fact]
    public async Task Should_not_modify_reasoning_file_data_that_is_a_URL()
    {
        var server = PrepareResponse();
        IReadOnlyList<LanguageModelV4Message> prompt =
        [
            new LanguageModelV4AssistantMessage([
                new LanguageModelV4ReasoningFilePart(
                    new SharedV4FileDataUrl(new Uri("https://example.com/a.png")),
                    "image/png"
                ),
            ]),
        ];
        using var model = CreateTestModel(server);

        await model.DoGenerateAsync(Options(prompt));

        var requestBody = server.Calls[0].RequestBodyJson!;
        AssertJsonEqual(
            """{"type":"url","url":"https://example.com/a.png"}""",
            requestBody["prompt"]![0]!["content"]![0]!["data"]
        );
    }
}
