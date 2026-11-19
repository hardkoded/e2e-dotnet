// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayDecisionModel.GatewayDecisionModelFixture;

namespace E2E.Decision.Tests.GatewayDecisionModel;

/// <summary>Upstream's <c>describe('GatewayDecisionModel')</c> of <c>gateway-decision-model.test.ts</c>.</summary>
public sealed class GatewayDecisionModelTests
{
    public static TheoryData<string, string> StatePartsCases =>
        new()
        {
            { "a text part", """[{"type":"text","text":"Inspect."}]""" },
            { "a JSON object part", """[{"type":"json","value":{"product":"vase","history":["new"]}}]""" },
            { "a JSON array part", """[{"type":"json","value":["vase",null]}]""" },
            { "empty parts", "[]" },
            {
                "mixed parts",
                """[{"type":"text","text":"Inspect."},{"type":"json","value":{"product":"vase"}},{"type":"json","value":[1,null]}]"""
            },
            { "a JSON number", """[{"type":"json","value":1}]""" },
            { "JSON null", """[{"type":"json","value":null}]""" },
        };

    [Theory]
    [MemberData(nameof(StatePartsCases))]
    public async Task Sends_name_as_ordered_state_parts(string name, string state)
    {
        Assert.NotEmpty(name);
        var server = new TestServer();
        PrepareJsonResponse(server, "{}");
        using var model = CreateTestModel(server);

        await model.DoDecideAsync(Call(State(state)), CancellationToken.None);

        var body = server.Calls[0].RequestBodyJson!;
        AssertJsonEqual(
            new JsonObject { ["stateParts"] = JsonNode.Parse(state), ["questions"] = TestQuestionsJson.DeepClone() },
            body
        );
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{ instructions: undefined }")]
    [InlineData("{ instructions: null }")]
    public async Task Forwards_criteria_only_questions_with_instructionFields(string instructionFields)
    {
        var server = new TestServer();
        PrepareJsonResponse(server, "{}");
        using var model = CreateTestModel(server);
        // A C# null leaves the field out, as undefined does; DecisionInput.Null writes the null.
        DecisionInput? instructions = instructionFields.Contains("null", StringComparison.Ordinal)
            ? DecisionInput.Null
            : null;
        var questions = new Dictionary<string, DecisionQuestion>
        {
            ["correct"] = DecisionQuestion.Boolean(instructions, trueCriteria: "Factually correct"),
            ["tone"] = DecisionQuestion.Choice(
                instructions,
                [new DecisionChoice("neutral", "Plain and factual."), new DecisionChoice("playful")]
            ),
            ["quality"] = DecisionQuestion.Score(instructions, ["Poor.", "Acceptable.", "Excellent."]),
        };

        await model.DoDecideAsync(Call(questions: questions), CancellationToken.None);

        var expected = JsonNode.Parse(
            """
            {
              "correct": { "type": "boolean", "criteria": { "true": "Factually correct" } },
              "tone": { "type": "choice", "criteria": { "neutral": "Plain and factual.", "playful": null } },
              "quality": { "type": "score", "criteria": ["Poor.", "Acceptable.", "Excellent."] }
            }
            """
        )!;
        if (instructions is not null)
        {
            foreach (var (_, question) in expected.AsObject())
            {
                question!["instructions"] = null;
            }
        }

        AssertJsonEqual(expected, server.Calls[0].RequestBodyJson!["questions"]);
    }

    [Fact]
    public async Task Sends_file_bytes_as_base64_in_ordered_state_parts()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoDecideAsync(
            Call([
                new DecisionTextPart("Inspect this package."),
                new DecisionFilePart("image/png", [1, 2, 3]) { Filename = "box.png" },
                new DecisionJsonPart(System.Text.Json.JsonDocument.Parse("""{"product":"vase"}""").RootElement),
            ]),
            CancellationToken.None
        );

        AssertJsonEqual(
            JsonNode.Parse(
                """
                {
                  "stateParts": [
                    { "type": "text", "text": "Inspect this package." },
                    { "type": "file", "mediaType": "image/png", "filename": "box.png", "data": { "type": "data", "data": "AQID" } },
                    { "type": "json", "value": { "product": "vase" } }
                  ]
                }
                """
            )!["stateParts"],
            server.Calls[0].RequestBodyJson!["stateParts"]
        );
        AssertJsonEqual(TestQuestionsJson, server.Calls[0].RequestBodyJson!["questions"]);
    }

    [Fact]
    public async Task Sends_base64_file_strings_unchanged()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoDecideAsync(
            Call([new DecisionFilePart("image/jpeg", []) { Base64 = "AQID" }]),
            CancellationToken.None
        );

        AssertJsonEqual(
            JsonNode.Parse(
                """[{ "type": "file", "mediaType": "image/jpeg", "data": { "type": "data", "data": "AQID" } }]"""
            ),
            server.Calls[0].RequestBodyJson!["stateParts"]
        );
        AssertJsonEqual(TestQuestionsJson, server.Calls[0].RequestBodyJson!["questions"]);
    }

    [Fact]
    public async Task Rejects_URL_file_data_before_sending_a_Gateway_request()
    {
        var server = new TestServer();
        using var model = CreateTestModel(server);

        var error = await Assert.ThrowsAsync<UnsupportedFunctionalityException>(() =>
            model.DoDecideAsync(
                Call([
                    new DecisionFilePart("image/png", [])
                    {
                        DataType = "url",
                        Url = new Uri("https://example.com/a.png"),
                    },
                ]),
                CancellationToken.None
            )
        );

        Assert.Equal("Gateway decision file input: url data", error.Functionality);
        Assert.Empty(server.Calls);
    }

    [Fact]
    public void Should_report_all_question_types_as_supported()
    {
        using var model = CreateTestModel(new TestServer());

        Assert.Equal(["choice", "score", "boolean"], model.SupportedQuestionTypes);
    }

    [Fact]
    public async Task Routes_the_deprecated_factory_and_model_method_through_the_decision_protocol()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        var provider = new E2E.Decision.Sdk.Gateway.GatewayProvider(
            new GatewayProviderSettings
            {
                BaseUrl = "https://api.test.com",
                ApiKey = "test-token",
                HttpClient = server.Client,
            }
        );
        // The deprecated factories are the same factories as the decision ones.
        Assert.Equal(
            provider.DecisionModel("typesafe-ai/jev").Provider,
            provider.EvaluationModel("typesafe-ai/jev").Provider
        );
        Assert.Equal(provider.Decision("typesafe-ai/jev").ModelId, provider.Evaluation("typesafe-ai/jev").ModelId);

        await provider.EvaluationModel("typesafe-ai/jev").DoEvaluateAsync(Call(), CancellationToken.None);

        Assert.Equal(DecisionUrl, server.Calls[0].RequestUrl);
        Assert.Equal("4", server.Calls[0].RequestHeaders["ai-decision-model-specification-version"]);
        Assert.False(server.Calls[0].RequestHeaders.ContainsKey("ai-evaluation-model-specification-version"));
    }
}
