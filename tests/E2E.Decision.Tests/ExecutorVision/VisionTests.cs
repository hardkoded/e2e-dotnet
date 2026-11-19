// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Decision.Tests.ExecutorVision.VisionTestHelpers;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.ExecutorVision;

/// <summary>Upstream's <c>describe('vision')</c> of <c>executor-vision.test.ts</c>.</summary>
public sealed class VisionTests
{
    private static readonly string[] Both = ["choice", "score"];

    [Fact]
    public async Task With_vision_asks_for_pixels_sends_the_screenshot_and_taps_the_point_two_score_questions_locate()
    {
        var (model, requests) = ScriptedDecision(
            (id, keys, call) =>
                id switch
                {
                    "operation" => new ScriptedAnswer(call == 0 ? "tap_at" : "done"),
                    "verdict" => new ScriptedAnswer("holds"),
                    "x" => new ScriptedAnswer("2", Score: 2.5),
                    "y" => new ScriptedAnswer("3", Score: 3),
                    _ => new ScriptedAnswer(keys.FirstOrDefault() ?? ""),
                },
            Both
        );
        var text = Targets(1);
        var fixture = Context(
            new ContextOptions
            {
                Tree = Buttons,
                Observation = WithPixels(Undecodable),
                Model = text,
            }
        );
        var executor = Build(model, text);
        Assert.True(executor.Vision);

        var verdict = await Run(executor, fixture);

        Assert.Equal(StepVerdictStatus.Passed, verdict.Status);
        Assert.Contains(fixture.Looks, look => look.Tree && look.Pixels);
        // (2.5 + 0.5) * 80 wide cells, (3 + 0.5) * 75 tall cells.
        Assert.Equal(new Point(240, 263), TapPoint(fixture, 0));
        Assert.Contains("tap_at", Criteria(requests[0]));
        Assert.DoesNotContain("tap_at_target", requests[0].Questions.Keys);
        // The operation question reads the tree alone; the point looks get the pixels.
        Assert.Empty(requests[0].Files);
        Assert.Equal("score", requests[1].Questions["x"].Type);
        Assert.Equal(8, requests[1].Questions["y"].Levels!.Count);
        var file = Assert.Single(requests[1].Files);
        Assert.Equal("image/png", file.MediaType);
        // Undecodable pixels go through untouched.
        Assert.Equal(new byte[] { 1, 2, 3 }, file.Data);
        Assert.Contains("\"screenshot\":\"800x600\"", fixture.Transcripts[0], StringComparison.Ordinal);
        Assert.Equal("tap the Add key at (240, 263)", fixture.Turns[0].Calls[0]);
    }

    [Fact]
    public async Task Types_into_a_drawn_field_one_text_ask_names_it_and_its_value_the_locator_finds_it_a_tap_focuses_it()
    {
        var (model, requests) = ScriptedDecision(
            (id, keys, call) =>
                id switch
                {
                    "operation" => new ScriptedAnswer(call == 0 ? "type_at" : "done"),
                    "verdict" => new ScriptedAnswer("holds"),
                    "x" => new ScriptedAnswer("2", Score: 2.5),
                    "y" => new ScriptedAnswer("3", Score: 3),
                    _ => new ScriptedAnswer(keys.FirstOrDefault() ?? ""),
                },
            Both
        );
        var text = ScriptedOutputs([
            new Dictionary<string, object?> { ["target"] = "the Name field", ["text"] = "Ada" },
        ]);
        var fixture = Context(
            new ContextOptions
            {
                Tree = Buttons,
                Observation = WithPixels(Undecodable),
                Model = text,
            }
        );

        var verdict = await Run(Build(model, text), fixture);

        Assert.Equal(StepVerdictStatus.Passed, verdict.Status);
        Assert.Contains("type_at", Criteria(requests[0]));
        Assert.Equal("the Name field", Prop(requests[1].State, "target").GetString());
        Assert.Equal(new Point(240, 263), TapPoint(fixture, 0));
        Assert.Equal("Ada", Assert.Single(fixture.Actions.Of("typeText"))[0]);
        Assert.Single(text.Requests);
        Assert.Equal("type into the Name field at (240, 263)", fixture.Turns[0].Calls[0]);
    }

    [Fact]
    public async Task Skips_a_drawn_field_the_goal_gives_no_value_for_and_never_taps_it()
    {
        var (model, _) = ScriptedDecision(
            (id, keys, call) =>
                id == "operation"
                    ? new ScriptedAnswer(call == 0 ? "type_at" : "blocked")
                    : new ScriptedAnswer(keys.FirstOrDefault() ?? ""),
            Both
        );
        var text = ScriptedOutputs([
            new Dictionary<string, object?> { ["target"] = "the City field", ["text"] = null },
        ]);
        var fixture = Context(
            new ContextOptions
            {
                Tree = Buttons,
                Observation = WithPixels(Undecodable),
                Model = text,
            }
        );

        await Run(Build(model, text), fixture);

        Assert.Equal(0, fixture.Actions.Count("tapAt"));
        Assert.Equal(0, fixture.Actions.Count("typeText"));
    }

    [Fact]
    public async Task Offers_type_at_only_when_the_engine_types_into_whatever_holds_focus()
    {
        var (model, requests) = ScriptedDecision(
            (id, keys, _) => new ScriptedAnswer(id == "operation" ? "blocked" : keys.FirstOrDefault() ?? ""),
            Both
        );
        var text = ScriptedOutputs([]);
        var fixture = Context(
            new ContextOptions
            {
                Tree = Buttons,
                Observation = WithPixels(Undecodable),
                Model = text,
                Verbs = new HashSet<ExecutorVerb> { ExecutorVerb.Tap, ExecutorVerb.TapAt },
            }
        );

        await Run(Build(model, text), fixture);

        var offered = Criteria(requests[0]);
        Assert.Contains("tap_at", offered);
        Assert.DoesNotContain("type_at", offered);
    }

    [Fact]
    public async Task Never_offers_tap_at_to_a_model_that_does_not_score()
    {
        var (model, requests) = ScriptedDecision(
            (id, keys, _) => new ScriptedAnswer(id == "operation" ? "blocked" : keys.FirstOrDefault() ?? "")
        );
        var fixture = Context(new ContextOptions { Tree = Buttons, Observation = WithPixels(Undecodable) });

        await Run(Build(model), fixture);

        Assert.DoesNotContain("tap_at", Criteria(requests[0]));
    }

    [Fact]
    public async Task Never_offers_tap_at_without_a_text_model_to_name_the_control()
    {
        var (model, requests) = ScriptedDecision(
            (id, keys, _) => new ScriptedAnswer(id == "operation" ? "blocked" : keys.FirstOrDefault() ?? ""),
            Both
        );
        var fixture = Context(new ContextOptions { Tree = Buttons, Observation = WithPixels(Undecodable) });

        await Run(Build(model), fixture);

        Assert.DoesNotContain("tap_at", Criteria(requests[0]));
    }

    [Fact]
    public async Task Without_vision_never_asks_for_pixels_nor_offers_tap_at()
    {
        var (model, requests) = ScriptedDecision(
            (id, keys, _) => new ScriptedAnswer(id == "operation" ? "blocked" : keys.FirstOrDefault() ?? "")
        );
        var fixture = Context(new ContextOptions { Tree = Buttons, Observation = WithPixels(Undecodable) });
        Assert.False(Build(model, vision: false).Vision);

        await Run(Build(model, vision: false), fixture);

        Assert.Contains(fixture.Looks, look => look.Tree && !look.Pixels);
        Assert.DoesNotContain("tap_at", Criteria(requests[0]));
    }

    [Fact]
    public async Task Judges_an_assert_with_vision_only_on_the_pixels_alone()
    {
        var (model, requests) = ScriptedDecision((_, _, _) => new ScriptedAnswer("holds"));
        var fixture = Context(
            new ContextOptions
            {
                Kind = ExecutorStepKind.Assert,
                Tree = Buttons,
                Observation = WithPixels(Undecodable),
                Vision = VisionMode.Only,
            }
        );

        var verdict = await Run(Build(model), fixture);

        Assert.Equal(StepVerdictStatus.Passed, verdict.Status);
        Assert.Equal(0, Prop(requests[0].State, "elements").GetArrayLength());
        Assert.Equal("", Prop(requests[0].State, "page", "text").GetString());
        Assert.Equal(["image/png"], requests[0].Files.Select(file => file.MediaType));
    }
}
