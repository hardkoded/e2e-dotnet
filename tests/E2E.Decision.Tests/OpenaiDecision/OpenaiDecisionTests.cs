// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Decision.Sdk;
using E2E.Decision.Tests.DecisionModels;

namespace E2E.Decision.Tests.OpenaiDecision;

/// <summary>Ports <c>packages/openai/src/openai-decision.test.ts</c> of the AI SDK (<c>vercel/ai</c>).</summary>
public sealed class OpenaiDecisionTests
{
    [Fact]
    public async Task Serializes_structured_input_and_rubrics_omits_null_descriptions_and_preserves_boolean_criteria()
    {
        var handler = new OpenAIDecisionModelTests.ScriptedHandler(
            """
            {"answers":[{"type":"refusal","name":"department"},{"type":"refusal","name":"severity"},{"type":"refusal","name":"refund"}]}
            """
        );
        using var model = OpenAIDecisionModelTests.Model(handler);

        await model.DoDecideAsync(
            new DecisionModelCall
            {
                State = [new DecisionJsonPart(Json("""{"ticket":["charged twice"]}"""))],
                Questions = new Dictionary<string, DecisionQuestion>
                {
                    ["department"] = DecisionQuestion.Choice(
                        DecisionInput.FromJson(Json("""{"task":"route"}""")),
                        [
                            new DecisionChoice("technical"),
                            new DecisionChoice("billing", DecisionInput.FromJson(Json("""{"rubric":["charges"]}"""))),
                        ]
                    ),
                    ["severity"] = DecisionQuestion.Score(
                        DecisionInput.FromJson(Json("""["severity"]""")),
                        [null, DecisionInput.FromJson(Json("""{"meaning":"high"}"""))]
                    ),
                    ["refund"] = DecisionQuestion.Boolean(
                        "Refund?",
                        DecisionInput.FromJson(Json("""{"meaning":"Explicit request for money back"}""")),
                        "The customer only asks about refund status."
                    ),
                },
            },
            CancellationToken.None
        );

        var body = JsonDocument.Parse(handler.Body!).RootElement;
        Assert.Equal("gpt-6-luna", body.GetProperty("model").GetString());
        var content = body.GetProperty("input")[0].GetProperty("content");
        Assert.Equal(1, content.GetArrayLength());
        Assert.Equal("input_json", content[0].GetProperty("type").GetString());
        var questions = body.GetProperty("questions");

        Assert.Equal("department", questions[0].GetProperty("name").GetString());
        Assert.Equal("""{"task":"route"}""", questions[0].GetProperty("instructions").GetString());
        var choices = questions[0].GetProperty("choices");
        Assert.False(choices[0].TryGetProperty("description", out _));
        Assert.Equal("""{"rubric":["charges"]}""", choices[1].GetProperty("description").GetString());

        Assert.Equal("""["severity"]""", questions[1].GetProperty("instructions").GetString());
        var levels = questions[1].GetProperty("levels");
        Assert.Equal("0", levels[0].GetProperty("label").GetString());
        Assert.False(levels[0].TryGetProperty("description", out _));
        Assert.Equal("""{"meaning":"high"}""", levels[1].GetProperty("description").GetString());

        Assert.Equal("predicate", questions[2].GetProperty("type").GetString());
        Assert.Equal(
            "Refund?\n\nCriteria for true:\n{\"meaning\":\"Explicit request for money back\"}\n\nCriteria for false:\nThe customer only asks about refund status.",
            questions[2].GetProperty("instructions").GetString()
        );
    }

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement;
}
