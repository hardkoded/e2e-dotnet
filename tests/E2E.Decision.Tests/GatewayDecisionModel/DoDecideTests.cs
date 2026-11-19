// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayDecisionModel.GatewayDecisionModelFixture;

namespace E2E.Decision.Tests.GatewayDecisionModel;

/// <summary>Upstream's <c>describe('doDecide')</c> of <c>gateway-decision-model.test.ts</c>.</summary>
public sealed class DoDecideTests
{
    private const string ConditionalFallbacks = """
        {
          "gateway": {
            "models": [
              {
                "model": "openai/gpt-5.6-sol",
                "when": { "any": [
                  { "question": "tone", "confidenceBelow": 0.6 },
                  { "question": "correct", "probabilityBetween": [0.4, 0.6] }
                ] }
              },
              "anthropic/claude-sonnet-5"
            ]
          }
        }
        """;

    [Theory]
    [InlineData(null)]
    [InlineData("provider-default")]
    [InlineData("none")]
    [InlineData("minimal")]
    [InlineData("low")]
    [InlineData("medium")]
    [InlineData("high")]
    [InlineData("xhigh")]
    [InlineData("max")]
    public async Task Forwards_reasoning_when_supplied(string? reasoning)
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoDecideAsync(Call(reasoning: reasoning), CancellationToken.None);

        var body = server.Calls[0].RequestBodyJson!.AsObject();
        if (reasoning is null)
        {
            Assert.False(body.ContainsKey("reasoning"));
        }
        else
        {
            Assert.Equal(reasoning, (string?)body["reasoning"]);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData(512)]
    public async Task Forwards_maxOutputTokens_when_supplied(int? maxOutputTokens)
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoDecideAsync(Call(maxOutputTokens: maxOutputTokens), CancellationToken.None);

        var body = server.Calls[0].RequestBodyJson!.AsObject();
        if (maxOutputTokens is null)
        {
            Assert.False(body.ContainsKey("maxOutputTokens"));
        }
        else
        {
            Assert.Equal(maxOutputTokens, (int?)body["maxOutputTokens"]);
        }
    }

    [Fact]
    public async Task Should_pass_headers_correctly()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoDecideAsync(
            Call(headers: new Dictionary<string, string> { ["Custom-Header"] = "test-value" }),
            CancellationToken.None
        );

        var headers = server.Calls[0].RequestHeaders;
        Assert.Equal("Bearer test-token", headers["authorization"]);
        Assert.Equal("test-value", headers["custom-header"]);
        Assert.Equal("4", headers["ai-decision-model-specification-version"]);
        Assert.Equal("typesafe-ai/jev", headers["ai-model-id"]);
    }

    [Fact]
    public async Task Should_include_o11y_headers()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        var o11yHeaders = new Dictionary<string, string?>
        {
            ["ai-o11y-deployment-id"] = "deployment-1",
            ["ai-o11y-environment"] = "production",
            ["ai-o11y-region"] = "iad1",
        };
        using var model = CreateTestModel(server, o11yHeaders);

        await model.DoDecideAsync(Call(), CancellationToken.None);

        var headers = server.Calls[0].RequestHeaders;
        foreach (var (name, value) in o11yHeaders)
        {
            Assert.Equal(value, headers[name]);
        }
    }

    [Fact]
    public async Task Should_send_state_and_questions_in_request_body()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoDecideAsync(Call(), CancellationToken.None);

        AssertJsonEqual(
            new JsonObject
            {
                ["stateParts"] = TestStateJson.DeepClone(),
                ["questions"] = TestQuestionsJson.DeepClone(),
            },
            server.Calls[0].RequestBodyJson
        );
    }

    [Fact]
    public async Task Should_pass_providerOptions_into_request_body()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoDecideAsync(Call(providerOptions: """{"typesafe":{"effort":"high"}}"""), CancellationToken.None);

        AssertJsonEqual(
            JsonNode.Parse("""{"typesafe":{"effort":"high"}}"""),
            server.Calls[0].RequestBodyJson!["providerOptions"]
        );
    }

    [Fact]
    public async Task Should_pass_conditional_model_fallbacks_into_request_body()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoDecideAsync(Call(providerOptions: ConditionalFallbacks), CancellationToken.None);

        AssertJsonEqual(JsonNode.Parse(ConditionalFallbacks), server.Calls[0].RequestBodyJson!["providerOptions"]);
    }

    [Fact]
    public async Task Should_pass_service_owned_gateway_options_through_with_conditional_fallbacks()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);
        const string providerOptions = """
            {
              "gateway": {
                "models": [{ "model": "openai/gpt-5.6-sol", "when": { "question": "tone", "confidenceBelow": 0.6 } }],
                "order": ["openai"],
                "serviceOwnedOption": { "nested": ["value", 1, true] }
              },
              "typesafe": { "effort": "high" }
            }
            """;

        await model.DoDecideAsync(Call(providerOptions: providerOptions), CancellationToken.None);

        AssertJsonEqual(
            new JsonObject
            {
                ["stateParts"] = TestStateJson.DeepClone(),
                ["questions"] = TestQuestionsJson.DeepClone(),
                ["providerOptions"] = JsonNode.Parse(providerOptions),
            },
            server.Calls[0].RequestBodyJson
        );
    }

    [Fact]
    public async Task Should_reject_invalid_conditional_model_fallbacks()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        var error = await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            model.DoDecideAsync(
                Call(
                    providerOptions: """
                    {"gateway":{"models":[{"model":"openai/gpt-5.6-sol","when":{"question":"correct","probabilityBetween":[0.7,0.3]}}]}}
                    """
                ),
                CancellationToken.None
            )
        );

        Assert.Contains("invalid gateway provider options", error.Message, StringComparison.Ordinal);
        Assert.Empty(server.Calls);
    }

    [Fact]
    public async Task Should_extract_choice_score_and_boolean_answers()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, DummyAnswers);
        using var model = CreateTestModel(server);

        var result = await model.DoDecideAsync(Call(), CancellationToken.None);

        Assert.Equal(["correct", "tone", "quality"], result.Answers.Keys);
        Assert.Equal("boolean", result.Answers["correct"].Type);
        Assert.Equal(0.97, result.Answers["correct"].Probability);
        Assert.Equal("choice", result.Answers["tone"].Type);
        Assert.Equal("neutral", result.Answers["tone"].Choice);
        Assert.Equal(
            new Dictionary<string, double> { ["neutral"] = 0.9, ["playful"] = 0.1 },
            result.Answers["tone"].Probabilities
        );
        Assert.Equal("score", result.Answers["quality"].Type);
        Assert.Equal(1.8, result.Answers["quality"].Score);
        Assert.Equal(
            new Dictionary<string, double>
            {
                ["0"] = 0.05,
                ["1"] = 0.1,
                ["2"] = 0.85,
            },
            result.Answers["quality"].Probabilities
        );
    }

    [Fact]
    public async Task Should_extract_refusal_answers()
    {
        var server = new TestServer();
        PrepareJsonResponse(
            server,
            """
            {
              "correct": { "type": "boolean", "probability": 0.97 },
              "tone": { "type": "refusal" },
              "quality": { "type": "score", "score": 1.8, "probabilities": { "0": 0.05, "1": 0.1, "2": 0.85 } }
            }
            """
        );
        using var model = CreateTestModel(server);

        var result = await model.DoDecideAsync(Call(), CancellationToken.None);

        Assert.Equal("refusal", result.Answers["tone"].Type);
        Assert.Null(result.Answers["tone"].Choice);
        Assert.Equal(0.97, result.Answers["correct"].Probability);
    }

    [Fact]
    public async Task Should_extract_rounding()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, rounding: """{"probabilityDecimals":2,"scoreDecimals":2}""");
        using var model = CreateTestModel(server);

        var result = await model.DoDecideAsync(Call(), CancellationToken.None);

        Assert.Equal(new DecisionRounding(2, 2), result.Rounding);
    }

    [Fact]
    public async Task Should_extract_usage()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, usage: """{"inputTokens":42,"outputTokens":7}""");
        using var model = CreateTestModel(server);

        var result = await model.DoDecideAsync(Call(), CancellationToken.None);

        Assert.Equal(new DecisionUsage(42, 7), result.Usage);
    }

    [Fact]
    public async Task Should_omit_rounding_and_usage_when_absent()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        var result = await model.DoDecideAsync(Call(), CancellationToken.None);

        Assert.Null(result.Rounding);
        Assert.Null(result.Usage);
    }

    [Fact]
    public async Task Should_extract_warnings()
    {
        var server = new TestServer();
        PrepareJsonResponse(
            server,
            warnings: """
            [{"type":"unsupported","feature":"providerOptions.typesafe.effort","details":"This model ignores effort."}]
            """
        );
        using var model = CreateTestModel(server);

        var result = await model.DoDecideAsync(Call(), CancellationToken.None);

        // Warnings are text in the port: an unsupported feature reads "unsupported setting: <feature>: <details>".
        Assert.Equal(
            ["unsupported setting: providerOptions.typesafe.effort: This model ignores effort."],
            result.Warnings
        );
    }

    [Fact]
    public async Task Should_default_warnings_to_an_empty_array_when_absent()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        var result = await model.DoDecideAsync(Call(), CancellationToken.None);

        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Should_return_response_metadata()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, headers: new Dictionary<string, string> { ["x-request-id"] = "req-123" });
        using var model = CreateTestModel(server);

        var result = await model.DoDecideAsync(Call(), CancellationToken.None);

        Assert.Equal("typesafe-ai/jev", result.ResponseModelId);
        Assert.Equal("req-123", result.ResponseHeaders!["x-request-id"]);
    }

    [Fact]
    public async Task Should_attribute_the_response_to_the_returned_model_after_a_fallback()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, model: "anthropic/claude-sonnet-5");
        using var model = CreateTestModel(server);

        var result = await model.DoDecideAsync(Call(), CancellationToken.None);

        Assert.Equal("anthropic/claude-sonnet-5", result.ResponseModelId);
    }

    [Fact]
    public async Task Should_attribute_the_response_to_the_requested_model_when_none_is_returned()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        var result = await model.DoDecideAsync(Call(), CancellationToken.None);

        Assert.Equal("typesafe-ai/jev", result.ResponseModelId);
    }

    [Fact]
    public async Task Should_return_provider_metadata()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, providerMetadata: """{"gateway":{"cost":"0.002"}}""");
        using var model = CreateTestModel(server);

        var result = await model.DoDecideAsync(Call(), CancellationToken.None);

        AssertJsonEqual(
            JsonNode.Parse("""{"gateway":{"cost":"0.002"}}"""),
            JsonNode.Parse(result.ProviderMetadata!.Value.GetRawText())
        );
    }
}
