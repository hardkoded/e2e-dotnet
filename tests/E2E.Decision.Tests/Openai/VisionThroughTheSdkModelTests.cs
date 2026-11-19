// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using static E2E.Decision.Tests.Helpers;
using static E2E.Decision.Tests.Openai.OpenAiDecision;

namespace E2E.Decision.Tests.Openai;

/// <summary>Upstream's <c>describe('vision through the SDK model')</c> of <c>openai.test.ts</c>.</summary>
public sealed class VisionThroughTheSdkModelTests
{
    [Fact]
    public async Task Sends_the_screenshot_as_an_input_image_beside_the_JSON_state_on_the_point_looks_and_plain_JSON_otherwise()
    {
        var (model, requests) = Create(
            (request, _) =>
                [
                    .. request
                        .GetProperty("questions")
                        .EnumerateArray()
                        .Select(question =>
                        {
                            if (question.GetProperty("type").GetString() == "score")
                            {
                                return (JsonNode)
                                    new JsonObject
                                    {
                                        ["type"] = "score",
                                        ["name"] = question.GetProperty("name").GetString(),
                                        ["score"] = 1,
                                        ["probabilities"] = new JsonArray([
                                            .. question
                                                .GetProperty("levels")
                                                .EnumerateArray()
                                                .Select(
                                                    (_, value) =>
                                                        (JsonNode)
                                                            new JsonObject
                                                            {
                                                                ["value"] = value,
                                                                ["probability"] = value == 1 ? 1 : 0,
                                                            }
                                                ),
                                        ]),
                                    };
                            }

                            return Unanimous(
                                question,
                                question.GetProperty("name").GetString() == "operation"
                                    ? "tap_at"
                                    : FirstChoice(question)
                            );
                        }),
                ]
        );
        var text = ScriptedOutputs([new Dictionary<string, object?> { ["target"] = "the Add key" }]);
        var fixture = Context(
            new ContextOptions
            {
                Tree = Buttons,
                Observation = observation => observation with { Pixels = WhitePixels() },
                MaxModelCalls = 4,
                Model = text,
            }
        );

        await global::E2E
            .Decision.Executor.DecisionExecutor(
                new DecisionExecutorOptions
                {
                    Model = model,
                    TextModel = text,
                    Vision = true,
                }
            )
            .RunStepAsync(fixture.Ctx, CancellationToken.None);

        var types = (int index) =>
            requests[index]
                .GetProperty("input")[0]
                .GetProperty("content")
                .EnumerateArray()
                .Select(part => part.GetProperty("type").GetString())
                .ToList();
        Assert.Equal(["input_json"], types(0));
        var look = requests[1].GetProperty("input");
        Assert.Equal("user", look[0].GetProperty("role").GetString());
        Assert.Equal(["input_json", "input_image"], types(1));
        Assert.StartsWith(
            "data:image/png;base64,",
            look[0].GetProperty("content")[1].GetProperty("image_url").GetString(),
            StringComparison.Ordinal
        );
        Assert.Equal(
            "Do the thing",
            look[0].GetProperty("content")[0].GetProperty("data").GetProperty("goal").GetString()
        );
    }
}
