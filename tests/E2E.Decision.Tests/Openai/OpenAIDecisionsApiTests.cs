// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using static E2E.Decision.Tests.Helpers;
using static E2E.Decision.Tests.Openai.OpenAiDecision;

namespace E2E.Decision.Tests.Openai;

/// <summary>Upstream's <c>describe('OpenAI Decisions API')</c> of <c>openai.test.ts</c>.</summary>
public sealed class OpenAIDecisionsApiTests
{
    [Fact]
    public void Is_accepted_at_config_load()
    {
        var (model, _) = Create((_, _) => []);

        Assert.Equal(
            "decision",
            global::E2E.Decision.Executor.DecisionExecutor(new DecisionExecutorOptions { Model = model }).Name
        );
    }

    [Fact]
    public async Task Taps_through_the_wire_shape_and_reads_native_confidence()
    {
        var (model, requests) = Create(
            (request, call) =>
                [
                    .. request
                        .GetProperty("questions")
                        .EnumerateArray()
                        .Select(question =>
                        {
                            var name = question.GetProperty("name").GetString();
                            if (name == "operation")
                            {
                                if (call != 0)
                                {
                                    return Unanimous(question, "done", 0.9);
                                }

                                return new JsonObject
                                {
                                    ["type"] = "choice",
                                    ["name"] = "operation",
                                    ["choice"] = "tap",
                                    ["confidence"] = 0.93,
                                    ["probabilities"] = new JsonArray([
                                        .. Choices(question)
                                            .Select(option =>
                                            {
                                                var value = option.GetProperty("value").GetString();
                                                return (JsonNode)
                                                    new JsonObject
                                                    {
                                                        ["value"] = value,
                                                        ["probability"] =
                                                            value == "tap" ? 0.95
                                                            : value == "done" ? 0.05
                                                            : 0,
                                                    };
                                            }),
                                    ]),
                                };
                            }

                            return name == "verdict"
                                ? Unanimous(question, "holds", 0.9)
                                : Unanimous(question, FirstChoice(question), 0.6);
                        }),
                ]
        );
        var fixture = Context(new ContextOptions { Tree = Buttons });

        var verdict = await global::E2E
            .Decision.Executor.DecisionExecutor(
                new DecisionExecutorOptions
                {
                    Model = model,
                    MinProbability = 0.9,
                    MinConfidence = 0.5,
                }
            )
            .RunStepAsync(fixture.Ctx, CancellationToken.None);

        Assert.Equal(StepVerdictStatus.Passed, verdict.Status);
        Assert.Equal(1, fixture.Actions.Count("tap"));
        var first = requests[0];
        Assert.Equal("gpt-6-luna", first.GetProperty("model").GetString());
        var parts = first
            .GetProperty("input")
            .EnumerateArray()
            .Select(message =>
                message
                    .GetProperty("content")
                    .EnumerateArray()
                    .Select(part => part.GetProperty("type").GetString())
                    .ToList()
            );
        Assert.Equal(
            [
                ["input_json"],
            ],
            parts
        );
        var data = first.GetProperty("input")[0].GetProperty("content")[0].GetProperty("data");
        Assert.Equal("Do the thing", data.GetProperty("goal").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Array, data.GetProperty("elements").ValueKind);
        Assert.Equal(
            ["operation"],
            first.GetProperty("questions").EnumerateArray().Select(question => question.GetProperty("name").GetString())
        );
        var second = requests[1];
        Assert.Equal(
            ["target"],
            second
                .GetProperty("questions")
                .EnumerateArray()
                .Select(question => question.GetProperty("name").GetString())
        );
        var choices = Choices(second.GetProperty("questions")[0])
            .Select(option => (option.GetProperty("value").GetString(), option.GetProperty("description").GetString()))
            .ToList();
        Assert.Equal(
            [
                ("1", "[1] button \"Save\""),
                ("2", "[2] button \"Cancel\""),
                ("none", "No offered target fits the goal."),
            ],
            choices
        );
        Assert.Contains(
            "Goal of this step: Do the thing",
            first.GetProperty("questions")[0].GetProperty("instructions").GetString(),
            StringComparison.Ordinal
        );
        Assert.Contains(
            "The next operation is \"tap\"",
            second.GetProperty("questions")[0].GetProperty("instructions").GetString(),
            StringComparison.Ordinal
        );
        Assert.Equal(
            ("openai.decision", "gpt-6-luna-2026-09-01", 321),
            (fixture.Usage[0].Provider, fixture.Usage[0].ModelId, fixture.Usage[0].InputTokens)
        );
        Assert.Contains("op p=0.950, target p=1.000", fixture.Turns[0].Outcome, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Blocks_under_the_confidence_gate_when_the_endpoint_reports_none()
    {
        var (model, _) = Create(
            (request, _) =>
                [
                    .. request
                        .GetProperty("questions")
                        .EnumerateArray()
                        .Select(question =>
                            Unanimous(
                                question,
                                question.GetProperty("name").GetString() == "operation" ? "tap" : FirstChoice(question)
                            )
                        ),
                ]
        );
        var fixture = Context(new ContextOptions { Tree = Buttons });

        var verdict = await global::E2E
            .Decision.Executor.DecisionExecutor(new DecisionExecutorOptions { Model = model, MinConfidence = 0.5 })
            .RunStepAsync(fixture.Ctx, CancellationToken.None);

        Assert.Equal(StepVerdictStatus.Blocked, verdict.Status);
        Assert.Equal("AUTOMATION_UNSUPPORTED", verdict.ErrorCode);
        Assert.Equal(0, fixture.Actions.Count("tap"));
    }

    [Fact]
    public async Task Names_a_refusal_as_MODEL_OUTPUT_INVALID()
    {
        var (model, _) = Create(
            (request, _) =>
                [
                    .. request
                        .GetProperty("questions")
                        .EnumerateArray()
                        .Select(question =>
                            question.GetProperty("name").GetString() == "operation"
                                ? new JsonObject { ["type"] = "refusal", ["name"] = "operation" }
                                : Unanimous(question, FirstChoice(question))
                        ),
                ]
        );
        var fixture = Context(new ContextOptions { Tree = Buttons });

        var error = await Assert.ThrowsAsync<AgentException>(() =>
            global::E2E
                .Decision.Executor.DecisionExecutor(new DecisionExecutorOptions { Model = model })
                .RunStepAsync(fixture.Ctx, CancellationToken.None)
        );

        Assert.Equal("MODEL_OUTPUT_INVALID", error.Code);
        Assert.Equal("The decision model refused to answer \"operation\".", error.Message);
    }
}
