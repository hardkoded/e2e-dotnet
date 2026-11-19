// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.GatewayDecisionModel;

/// <summary>The constants and helpers at the top of upstream's <c>gateway-decision-model.test.ts</c>.</summary>
internal static class GatewayDecisionModelFixture
{
    public const string DecisionUrl = "https://api.test.com/decision-model";

    public static readonly IReadOnlyList<DecisionStatePart> TestState =
    [
        new DecisionTextPart("The capital of France is Paris."),
    ];

    public static readonly JsonNode TestStateJson = JsonNode.Parse(
        """[{"type":"text","text":"The capital of France is Paris."}]"""
    )!;

    public static readonly IReadOnlyDictionary<string, DecisionQuestion> TestQuestions = new Dictionary<
        string,
        DecisionQuestion
    >
    {
        ["correct"] = DecisionQuestion.Boolean("Is the statement factually correct?"),
        ["tone"] = DecisionQuestion.Choice(
            "What is the tone?",
            [new DecisionChoice("neutral", "Plain and factual."), new DecisionChoice("playful")]
        ),
        ["quality"] = DecisionQuestion.Score("Rate the quality.", ["Poor.", "Acceptable.", "Excellent."]),
    };

    public static readonly JsonNode TestQuestionsJson = JsonNode.Parse(
        """
        {
          "correct": { "type": "boolean", "instructions": "Is the statement factually correct?" },
          "tone": {
            "type": "choice",
            "instructions": "What is the tone?",
            "criteria": { "neutral": "Plain and factual.", "playful": null }
          },
          "quality": {
            "type": "score",
            "instructions": "Rate the quality.",
            "criteria": ["Poor.", "Acceptable.", "Excellent."]
          }
        }
        """
    )!;

    public const string DummyAnswers = """
        {
          "correct": { "type": "boolean", "probability": 0.97 },
          "tone": { "type": "choice", "choice": "neutral", "probabilities": { "neutral": 0.9, "playful": 0.1 } },
          "quality": { "type": "score", "score": 1.8, "probabilities": { "0": 0.05, "1": 0.1, "2": 0.85 } }
        }
        """;

    /// <summary>Upstream's <c>createTestModel</c>.</summary>
    public static E2E.Decision.Sdk.Gateway.GatewayDecisionModel CreateTestModel(
        TestServer server,
        IReadOnlyDictionary<string, string?>? o11yHeaders = null
    ) =>
        new(
            "typesafe-ai/jev",
            new GatewayDecisionModelConfig
            {
                Provider = "gateway",
                BaseUrl = "https://api.test.com",
                Headers = _ =>
                    Task.FromResult<IReadOnlyDictionary<string, string?>>(
                        new Dictionary<string, string?>
                        {
                            ["Authorization"] = "Bearer test-token",
                            ["ai-gateway-auth-method"] = "api-key",
                        }
                    ),
                HttpClient = server.Client,
                O11yHeaders = _ => Task.FromResult(o11yHeaders ?? new Dictionary<string, string?>()),
            }
        );

    /// <summary>Upstream's <c>prepareJsonResponse</c>: the response the server gives the next call.</summary>
    public static void PrepareJsonResponse(
        TestServer server,
        string answers = DummyAnswers,
        string? model = null,
        string? rounding = null,
        string? usage = null,
        string? warnings = null,
        IReadOnlyDictionary<string, string>? headers = null,
        string? providerMetadata = null
    )
    {
        var body = new JsonObject { ["answers"] = JsonNode.Parse(answers) };
        if (model is not null)
        {
            body["model"] = model;
        }

        if (rounding is not null)
        {
            body["rounding"] = JsonNode.Parse(rounding);
        }

        if (usage is not null)
        {
            body["usage"] = JsonNode.Parse(usage);
        }

        if (warnings is not null)
        {
            body["warnings"] = JsonNode.Parse(warnings);
        }

        if (providerMetadata is not null)
        {
            body["providerMetadata"] = JsonNode.Parse(providerMetadata);
        }

        server.RespondJson(DecisionUrl, body.ToJsonString(), headers);
    }

    /// <summary>The decision call of the tests: the test state and questions, unless a test replaces them.</summary>
    public static DecisionModelCall Call(
        IReadOnlyList<DecisionStatePart>? state = null,
        IReadOnlyDictionary<string, DecisionQuestion>? questions = null,
        int? maxOutputTokens = null,
        string? reasoning = null,
        IReadOnlyDictionary<string, string>? headers = null,
        string? providerOptions = null
    ) =>
        new()
        {
            State = state ?? TestState,
            Questions = questions ?? TestQuestions,
            MaxOutputTokens = maxOutputTokens,
            Reasoning = reasoning,
            Headers = headers ?? new Dictionary<string, string>(),
            ProviderOptions = providerOptions is null ? null : Options(providerOptions),
        };

    /// <summary>Provider options from JSON: provider names mapped to option objects.</summary>
    public static IReadOnlyDictionary<string, JsonElement> Options(string json) =>
        JsonDocument.Parse(json).RootElement.Clone().EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());

    /// <summary>State parts from the JSON a test case writes: <c>text</c> and <c>json</c> parts.</summary>
    public static IReadOnlyList<DecisionStatePart> State(string json) =>
        JsonDocument
            .Parse(json)
            .RootElement.EnumerateArray()
            .Select<JsonElement, DecisionStatePart>(part =>
                part.GetProperty("type").GetString() == "text"
                    ? new DecisionTextPart(part.GetProperty("text").GetString()!)
                    : new DecisionJsonPart(part.GetProperty("value").Clone())
            )
            .ToList();

    public static void AssertJsonEqual(JsonNode? expected, JsonNode? actual) =>
        Assert.True(
            JsonNode.DeepEquals(expected, actual),
            "Expected: " + expected?.ToJsonString() + "\nActual:   " + actual?.ToJsonString()
        );
}
