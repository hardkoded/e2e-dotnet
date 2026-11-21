// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using static E2E.Decision.Tests.GatewayExtractionModel.GatewayExtractionModelFixture;

namespace E2E.Decision.Tests.GatewayExtractionModel;

/// <summary>Upstream's <c>describe('doExtract')</c> of <c>gateway-extraction-model.test.ts</c>.</summary>
[Collection("GatewayProviderEnvironment")]
public sealed class DoExtractTests
{
    private const string ProviderOptions = """
        {
          "gateway": { "models": ["openai/gpt-5.4-nano"], "user": "user-123" },
          "scaledown": { "threshold": 0.4 }
        }
        """;

    [Fact]
    public async Task Should_post_to_the_extraction_model_endpoint()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoExtractAsync(Call(), CancellationToken.None);

        Assert.Equal("POST", server.Calls[0].RequestMethod);
        Assert.Equal("https://api.test.com/extraction-model", server.Calls[0].RequestUrl);
    }

    [Fact]
    public async Task Should_send_every_call_option_in_the_request_body()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoExtractAsync(
            Call(referenceDate: "2026-11-11", maxOutputTokens: 512, reasoning: "low", providerOptions: ProviderOptions),
            CancellationToken.None
        );

        AssertJsonEqual(
            new JsonObject
            {
                ["stateParts"] = TestStateJson.DeepClone(),
                ["fields"] = TestFieldsJson.DeepClone(),
                ["referenceDate"] = "2026-11-11",
                ["maxOutputTokens"] = 512,
                ["reasoning"] = "low",
                ["providerOptions"] = JsonNode.Parse(ProviderOptions),
            },
            server.Calls[0].RequestBodyJson
        );
    }

    [Fact]
    public async Task Should_omit_options_that_are_not_set()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoExtractAsync(Call(), CancellationToken.None);

        AssertJsonEqual(
            new JsonObject { ["stateParts"] = TestStateJson.DeepClone(), ["fields"] = TestFieldsJson.DeepClone() },
            server.Calls[0].RequestBodyJson
        );
    }

    [Fact]
    public async Task Should_pass_headers_correctly()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server, new Dictionary<string, string?> { ["ai-o11y-region"] = "iad1" });

        await model.DoExtractAsync(
            Call(headers: new Dictionary<string, string> { ["Custom-Header"] = "test-value" }),
            CancellationToken.None
        );

        var headers = server.Calls[0].RequestHeaders;
        Assert.Equal("Bearer test-token", headers["authorization"]);
        Assert.Equal("test-value", headers["custom-header"]);
        Assert.Equal("4", headers["ai-extraction-model-specification-version"]);
        Assert.Equal("scaledown/extract-1", headers["ai-model-id"]);
        Assert.Equal("iad1", headers["ai-o11y-region"]);
    }

    [Fact]
    public async Task Should_return_answers_of_every_type()
    {
        const string answers = """
            {
              "text": {
                "type": "text",
                "value": "INV-2041",
                "guess": "INV-2041",
                "pNull": 0.01,
                "confidence": 0.98,
                "source": { "type": "text", "part": 0, "start": 8, "end": 16 }
              },
              "number": { "type": "number", "value": 1250 },
              "date": { "type": "date", "value": "2026-03-03" },
              "time": { "type": "time", "value": "17:30" },
              "datetime": { "type": "datetime", "value": "2026-03-03T17:30:00Z" },
              "duration": { "type": "duration", "value": 120 },
              "missing": { "type": "text", "value": null, "guess": "PO-1", "pNull": 0.9 },
              "fromFile": { "type": "text", "value": "Acme", "source": { "type": "file", "part": 1 } },
              "texts": {
                "type": "list",
                "value": ["Ana", "Bo"],
                "confidence": 0.9,
                "items": [
                  { "confidence": 0.95, "source": { "type": "text", "part": 0, "start": 1, "end": 4 } },
                  {}
                ]
              },
              "numbers": { "type": "list", "value": [1, 2.5] },
              "emptyList": { "type": "list", "value": [] },
              "missingList": { "type": "list", "value": null, "pNull": 0.8 }
            }
            """;
        var server = new TestServer();
        PrepareJsonResponse(server, """{"answers":""" + answers + "}");
        using var model = CreateTestModel(server);

        var result = await model.DoExtractAsync(Call(), CancellationToken.None);

        AssertJsonEqual(JsonNode.Parse(answers), AnswersJson(result.Answers));
    }

    [Fact]
    public async Task Should_return_refusal_answers()
    {
        const string answers = """
            {
              "invoiceNumber": { "type": "refusal" },
              "total": { "type": "number", "value": 1250 },
              "contacts": { "type": "refusal" }
            }
            """;
        var server = new TestServer();
        PrepareJsonResponse(server, """{"answers":""" + answers + "}");
        using var model = CreateTestModel(server);

        var result = await model.DoExtractAsync(Call(), CancellationToken.None);

        AssertJsonEqual(JsonNode.Parse(answers), AnswersJson(result.Answers));
    }

    [Fact]
    public async Task Should_return_usage_warnings_and_provider_metadata()
    {
        var server = new TestServer();
        PrepareJsonResponse(
            server,
            """
            {
              "usage": { "inputTokens": 120, "outputTokens": 30 },
              "warnings": [
                { "type": "unsupported", "feature": "reasoning", "details": "Ignored." },
                { "type": "other", "message": "Something else." }
              ],
              "providerMetadata": { "gateway": { "cost": "0.002" } }
            }
            """
        );
        using var model = CreateTestModel(server);

        var result = await model.DoExtractAsync(Call(), CancellationToken.None);

        Assert.Equal(new DecisionUsage(120, 30), result.Usage);
        Assert.Equal(
            [
                new SharedV4Warning
                {
                    Type = "unsupported",
                    Feature = "reasoning",
                    Details = "Ignored.",
                },
                new SharedV4Warning { Type = "other", Message = "Something else." },
            ],
            result.Warnings
        );
        AssertJsonEqual(
            JsonNode.Parse("""{"gateway":{"cost":"0.002"}}"""),
            JsonNode.Parse(result.ProviderMetadata!.Value.GetRawText())
        );
    }

    [Fact]
    public async Task Should_omit_usage_and_default_warnings_when_absent()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        var result = await model.DoExtractAsync(Call(), CancellationToken.None);

        Assert.Null(result.Usage);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Should_drop_null_optional_fields_from_the_response()
    {
        var server = new TestServer();
        PrepareJsonResponse(
            server,
            """
            {
              "answers": {
                "invoiceNumber": {
                  "type": "text",
                  "value": null,
                  "guess": null,
                  "pNull": 0.9,
                  "confidence": null,
                  "source": null
                },
                "total": { "type": "number", "value": 1250, "guess": null, "source": null },
                "contacts": {
                  "type": "list",
                  "value": ["Ana"],
                  "pNull": null,
                  "items": [{ "confidence": null, "source": null }]
                }
              },
              "model": null,
              "usage": { "inputTokens": 120, "outputTokens": null },
              "warnings": null,
              "providerMetadata": null
            }
            """
        );
        using var model = CreateTestModel(server);

        var result = await model.DoExtractAsync(Call(), CancellationToken.None);

        AssertJsonEqual(
            JsonNode.Parse(
                """
                {
                  "invoiceNumber": { "type": "text", "value": null, "pNull": 0.9 },
                  "total": { "type": "number", "value": 1250 },
                  "contacts": { "type": "list", "value": ["Ana"], "items": [{}] }
                }
                """
            ),
            AnswersJson(result.Answers)
        );
        Assert.Equal(new DecisionUsage(120, null), result.Usage);
        Assert.Empty(result.Warnings);
        Assert.Null(result.ProviderMetadata);
        Assert.Equal("scaledown/extract-1", result.ResponseModelId);
    }

    [Fact]
    public async Task Should_keep_an_unknown_warning_as_an_other_warning()
    {
        var server = new TestServer();
        PrepareJsonResponse(
            server,
            """
            {
              "warnings": [
                { "type": "other", "message": "Known." },
                { "type": "future", "detail": "New." }
              ]
            }
            """
        );
        using var model = CreateTestModel(server);

        var result = await model.DoExtractAsync(Call(), CancellationToken.None);

        Assert.Equal(
            [
                new SharedV4Warning { Type = "other", Message = "Known." },
                new SharedV4Warning
                {
                    Type = "other",
                    Message = """Unknown warning: {"type":"future","detail":"New."}""",
                },
            ],
            result.Warnings
        );
    }

    [Fact]
    public async Task Should_return_response_metadata()
    {
        var server = new TestServer();
        PrepareJsonResponse(
            server,
            """{"model":"scaledown/extract-1-fallback"}""",
            new Dictionary<string, string> { ["x-request-id"] = "req-1" }
        );
        using var model = CreateTestModel(server);

        var result = await model.DoExtractAsync(Call(), CancellationToken.None);

        Assert.Equal("scaledown/extract-1-fallback", result.ResponseModelId);
        Assert.Equal("req-1", result.ResponseHeaders!["x-request-id"]);
        AssertJsonEqual(
            new JsonObject { ["answers"] = JsonNode.Parse(DummyAnswers), ["model"] = "scaledown/extract-1-fallback" },
            JsonNode.Parse(result.ResponseBody!.Value.GetRawText())
        );
    }

    [Fact]
    public async Task Should_attribute_the_response_to_the_requested_model_when_none_is_returned()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        var result = await model.DoExtractAsync(Call(), CancellationToken.None);

        Assert.Equal("scaledown/extract-1", result.ResponseModelId);
    }
}
