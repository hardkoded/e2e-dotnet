// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;
using Vercel.AI.Gateway;

namespace E2E.Decision.Tests.GatewayDecisionProvider;

/// <summary>
/// .NET-only: <see cref="E2E.Decision.Sdk.Gateway.GatewayDecisionProvider"/> hands the decision call to <c>Vercel.AI</c>'s Gateway and
/// converts the result back. There is no upstream <c>it</c> for the conversion: <c>@ai-sdk/gateway</c>'s own tests are <c>Vercel.AI</c>'s.
/// </summary>
public sealed class GatewayDecisionProviderTests
{
    private const string DecisionUrl = "https://api.test.com/decision-model";

    private static E2E.Decision.Sdk.Gateway.GatewayDecisionProvider Provider(TestServer server) =>
        new(
            GatewayProvider.Create(new GatewayOptions { ApiKey = "test-key", BaseUrl = "https://api.test.com" }, server)
        );

    private static IDecisionModel Model(TestServer server) => Provider(server).DecisionModel("typesafe-ai/jev")!;

    [Fact]
    public async Task Sends_the_state_and_the_questions_to_the_decision_model_route_and_reads_the_answers()
    {
        var server = new TestServer();
        server.RespondJson(
            DecisionUrl,
            """
            {
              "answers": {
                "correct": { "type": "boolean", "probability": 0.97 },
                "tone": { "type": "choice", "choice": "neutral", "probabilities": { "neutral": 0.9, "playful": 0.1 } },
                "quality": { "type": "score", "score": 1.8, "probabilities": { "0": 0.05, "1": 0.1, "2": 0.85 } }
              },
              "rounding": { "probabilityDecimals": 2 },
              "usage": { "inputTokens": 12, "outputTokens": 3 },
              "warnings": [{ "type": "unsupported", "feature": "reasoning" }]
            }
            """
        );

        var result = await Model(server)
            .DoDecideAsync(
                new E2E.Decision.Sdk.DecisionModelCall
                {
                    State = [new DecisionTextPart("The capital of France is Paris.")],
                    Questions = new Dictionary<string, E2E.Decision.Sdk.DecisionQuestion>
                    {
                        ["correct"] = E2E.Decision.Sdk.DecisionQuestion.Boolean("Is the statement factually correct?"),
                        ["tone"] = E2E.Decision.Sdk.DecisionQuestion.Choice(
                            "What is the tone?",
                            [new DecisionChoice("neutral", "Plain and factual."), new DecisionChoice("playful")]
                        ),
                        ["quality"] = E2E.Decision.Sdk.DecisionQuestion.Score(
                            "Rate the quality.",
                            ["Poor.", "Acceptable.", "Excellent."]
                        ),
                    },
                    MaxOutputTokens = 512,
                    Reasoning = "low",
                },
                CancellationToken.None
            );

        var call = Assert.Single(server.Calls);
        Assert.Equal(DecisionUrl, call.RequestUrl);
        Assert.Equal("Bearer test-key", call.RequestHeaders["authorization"]);
        var body = call.RequestBodyJson!.AsObject();
        Assert.Equal("The capital of France is Paris.", (string?)body["stateParts"]![0]!["text"]);
        Assert.Equal("boolean", (string?)body["questions"]!["correct"]!["type"]);
        Assert.Equal("Plain and factual.", (string?)body["questions"]!["tone"]!["criteria"]!["neutral"]);
        Assert.Null(body["questions"]!["tone"]!["criteria"]!["playful"]);
        Assert.Equal("Excellent.", (string?)body["questions"]!["quality"]!["criteria"]![2]);
        Assert.Equal(512, (int?)body["maxOutputTokens"]);
        Assert.Equal("low", (string?)body["reasoning"]);

        Assert.Equal(0.97, result.Answers["correct"].Probability);
        Assert.Equal("neutral", result.Answers["tone"].Choice);
        Assert.Equal(0.9, result.Answers["tone"].Probabilities!["neutral"]);
        Assert.Equal(1.8, result.Answers["quality"].Score);
        Assert.Equal(2, result.Rounding!.ProbabilityDecimals);
        Assert.Equal(12, result.Usage!.InputTokens);
        Assert.Equal(3, result.Usage.OutputTokens);
        Assert.Equal("unsupported setting: reasoning", Assert.Single(result.Warnings));
    }

    [Fact]
    public async Task Sends_a_screenshot_as_a_base64_file_part_and_a_json_part_as_json()
    {
        var server = new TestServer();
        server.RespondJson(DecisionUrl, """{ "answers": { "done": { "type": "boolean", "probability": 1 } } }""");

        await Model(server)
            .DoDecideAsync(
                new E2E.Decision.Sdk.DecisionModelCall
                {
                    State =
                    [
                        new DecisionJsonPart(JsonDocument.Parse("""{ "url": "https://example.com" }""").RootElement),
                        new DecisionFilePart("image/png", [1, 2, 3]),
                    ],
                    Questions = new Dictionary<string, E2E.Decision.Sdk.DecisionQuestion>
                    {
                        ["done"] = E2E.Decision.Sdk.DecisionQuestion.Boolean(null),
                    },
                },
                CancellationToken.None
            );

        var state = Assert.Single(server.Calls).RequestBodyJson!["stateParts"]!;
        Assert.Equal("json", (string?)state[0]!["type"]);
        Assert.Equal("https://example.com", (string?)state[0]!["value"]!["url"]);
        Assert.Equal("file", (string?)state[1]!["type"]);
        Assert.Equal("image/png", (string?)state[1]!["mediaType"]);
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), (string?)state[1]!["data"]!["data"]);
    }

    [Fact]
    public async Task Surfaces_a_retryable_Gateway_error_as_the_Vercel_AI_GatewayError()
    {
        var server = new TestServer();
        server.RespondError(
            DecisionUrl,
            429,
            """{ "error": { "message": "Slow down.", "type": "rate_limit_exceeded" } }"""
        );

        var error = await Assert.ThrowsAnyAsync<GatewayError>(() =>
            Model(server)
                .DoDecideAsync(
                    new E2E.Decision.Sdk.DecisionModelCall
                    {
                        State = [new DecisionTextPart("x")],
                        Questions = new Dictionary<string, E2E.Decision.Sdk.DecisionQuestion>
                        {
                            ["done"] = E2E.Decision.Sdk.DecisionQuestion.Boolean(null),
                        },
                    },
                    CancellationToken.None
                )
        );

        Assert.True(error.IsRetryable);
        Assert.Equal(429, error.StatusCode);
    }

    [Fact]
    public void Resolves_a_model_id_through_the_default_provider_as_the_Gateway()
    {
        var model = ResolveModel.ResolveDecisionModel("typesafe-ai/jev");

        Assert.Equal("typesafe-ai/jev", model.ModelId);
    }

    [Fact]
    public async Task Rejects_invalid_gateway_provider_options_before_any_request()
    {
        var server = new TestServer();
        server.RespondJson(DecisionUrl, """{ "answers": {} }""");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            Model(server)
                .DoDecideAsync(
                    new E2E.Decision.Sdk.DecisionModelCall
                    {
                        State = [new DecisionTextPart("x")],
                        Questions = new Dictionary<string, E2E.Decision.Sdk.DecisionQuestion>
                        {
                            ["done"] = E2E.Decision.Sdk.DecisionQuestion.Boolean(null),
                        },
                        ProviderOptions = new Dictionary<string, JsonElement>
                        {
                            ["gateway"] = JsonDocument
                                .Parse("""{ "models": [{ "model": "a/b", "when": { "confidenceBelow": 2 } }] }""")
                                .RootElement,
                        },
                    },
                    CancellationToken.None
                )
        );

        Assert.Empty(server.Calls);
    }

    [Fact]
    public async Task ExperimentalDecide_retries_a_retryable_GatewayError()
    {
        var (model, calls) = Decide.DecideTestHelpers.Setup();
        model.FailOnce(new GatewayRateLimitError("Rate limited"));

        var result = await ExperimentalDecide.RunAsync(
            new DecideOptions
            {
                Model = model,
                State = [new DecisionTextPart("text")],
                Questions = Decide.DecideTestHelpers.QuestionMap,
                MaxRetries = 1,
            }
        );

        Assert.NotNull(result);
        Assert.Equal(2, calls.Count);
    }
}
