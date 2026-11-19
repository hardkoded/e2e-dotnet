// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;
using static E2E.Decision.Tests.ExecutorVision.VisionTestHelpers;
using static E2E.Decision.Tests.Helpers;

namespace E2E.Decision.Tests.ExecutorVision;

/// <summary>Upstream's <c>describe('vision details')</c> of <c>executor-vision.test.ts</c>.</summary>
public sealed class VisionDetailsTests
{
    private static readonly string[] Both = ["choice", "score"];

    private static byte[] Frame(bool dot)
    {
        var rgba = Enumerable.Repeat((byte)255, 800 * 600 * 4).ToArray();
        if (dot)
        {
            for (var i = 0; i < 4; i++)
            {
                rgba[(((100 * 800) + 100) * 4) + i] = 0;
            }
        }

        return PngHelpers.PngOf(800, 600, rgba);
    }

    [Fact]
    public async Task Sends_the_plain_screenshot_to_the_first_look_and_draws_the_grid_only_on_the_zoomed_looks()
    {
        var (model, requests) = ScriptedDecision(
            (id, keys, call) =>
                id switch
                {
                    "operation" => new ScriptedAnswer(call == 0 ? "tap_at" : "blocked"),
                    "x" or "y" => new ScriptedAnswer("4"),
                    _ => new ScriptedAnswer(keys.FirstOrDefault() ?? ""),
                },
            Both
        );
        var text = Targets(1);
        var fixture = Context(
            new ContextOptions
            {
                Tree = Buttons,
                Observation = WithPixels(WhitePixels()),
                Model = text,
            }
        );

        await Run(Build(model, text), fixture);

        (int Width, int Height, Func<int, int, int[]> At) Sent(int index)
        {
            var (width, height, data) = PngHelpers.RgbaOf(requests[index].Files[0].Data);
            return (width, height, (x, y) =>
                [data[((y * width) + x) * 4], data[(((y * width) + x) * 4) + 1], data[(((y * width) + x) * 4) + 2]]
            );
        }

        var full = Sent(1);
        Assert.Equal((800, 600), (full.Width, full.Height));
        Assert.Equal([255, 255, 255], full.At(159, 300));
        Assert.Equal([255, 255, 255], full.At(2, 2));
        Assert.Contains("no grid is drawn on it", requests[1].Questions["x"].Instructions, StringComparison.Ordinal);
        var zoomed = Sent(2);
        Assert.Equal((800, 800), (zoomed.Width, zoomed.Height));
        Assert.Equal([255, 0, 255], zoomed.At(100, 300));
        Assert.Equal([255, 255, 255], zoomed.At(2, 2));
        Assert.Equal([255, 255, 255], zoomed.At(250, 250));
        // Column 4 of 10 over 800px and row 4 of 8 over 600px is (360, 338); the 400px zoom around it starts at (160, 138)
        // and settles on its cell 4 center (385, 363); the 200px zoom around that starts at (285, 263) and settles 112.5px in.
        Assert.Contains(
            "The image is 800 pixels wide and 600 pixels tall",
            requests[1].Questions["x"].Instructions,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "2x zoom of a 400 by 400 pixel region",
            requests[2].Questions["x"].Instructions,
            StringComparison.Ordinal
        );
        var closest = Sent(3);
        Assert.Equal((400, 400), (closest.Width, closest.Height));
        Assert.Equal([255, 0, 255], closest.At(50, 150));
        Assert.Equal([255, 255, 255], closest.At(125, 125));
        Assert.Contains(
            "2x zoom of a 200 by 200 pixel region",
            requests[3].Questions["x"].Instructions,
            StringComparison.Ordinal
        );
        Assert.Equal(new Point(398, 376), TapPoint(fixture, 0));
    }

    [Fact]
    public async Task Judges_a_vision_only_assert_from_the_pixels_when_the_tree_is_unavailable()
    {
        var (model, requests) = ScriptedDecision((_, _, _) => new ScriptedAnswer("holds"));
        var fixture = Context(
            new ContextOptions
            {
                Kind = ExecutorStepKind.Assert,
                Observation = o => o with { TreeUnavailable = true, Tree = null, Pixels = WhitePixels() },
                Vision = VisionMode.Only,
            }
        );

        var verdict = await Run(Build(model), fixture);

        Assert.Equal(StepVerdictStatus.Passed, verdict.Status);
        Assert.Equal(0, Prop(requests[0].State, "elements").GetArrayLength());
        Assert.Equal("", Prop(requests[0].State, "page", "text").GetString());
        var noPixels = Context(
            new ContextOptions
            {
                Kind = ExecutorStepKind.Assert,
                Observation = o => o with { TreeUnavailable = true, Tree = null },
                Vision = VisionMode.Only,
            }
        );
        var inconclusive = await Run(Build(model), noPixels);
        Assert.Equal(StepVerdictStatus.Failed, inconclusive.Status);
        Assert.Equal("ASSERTION_INCONCLUSIVE", inconclusive.ErrorCode);
    }

    [Fact]
    public async Task Tells_the_model_where_a_point_tap_landed()
    {
        var (model, requests) = ScriptedDecision(
            (id, keys, call) =>
                id switch
                {
                    "operation" => new ScriptedAnswer(call == 0 ? "tap_at" : "blocked"),
                    "x" or "y" => new ScriptedAnswer("0"),
                    _ => new ScriptedAnswer(keys.FirstOrDefault() ?? ""),
                },
            Both
        );
        var text = Targets(1);
        var fixture = Context(
            new ContextOptions
            {
                Tree = Buttons,
                Observation = WithPixels(WhitePixels()),
                Model = text,
            }
        );
        fixture.Actions.TapAtResult = _ => new PointTapResult(
            new Point(80, 80),
            new ExecutorTarget("n9"),
            "tapped button \"Add\""
        );

        await Run(Build(model, text), fixture);

        // Operation, coarse point, two zoomed points, then the next operation.
        Assert.Equal(
            [
                ["operation"],
                ["x", "y"],
                ["x", "y"],
                ["x", "y"],
                ["operation"],
            ],
            requests.Take(5).Select(request => request.Questions.Keys.ToArray())
        );
        Assert.Contains(
            "2x zoom of a 400 by 400 pixel region",
            requests[2].Questions["x"].Instructions,
            StringComparison.Ordinal
        );
        var recent = Prop(requests[4].State, "recentActions");
        Assert.Equal(
            "landed on listed control n9",
            recent[recent.GetArrayLength() - 1].GetProperty("note").GetString()
        );
        Assert.Contains(
            "(landed on listed control n9)",
            requests[4].Questions["operation"].Instructions,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Lets_the_text_model_name_the_drawn_control_the_point_looks_locate()
    {
        var (model, requests) = ScriptedDecision(
            (id, keys, call) =>
                id switch
                {
                    "operation" => new ScriptedAnswer(call == 0 ? "tap_at" : "blocked"),
                    "x" or "y" => new ScriptedAnswer("0"),
                    _ => new ScriptedAnswer(keys.FirstOrDefault() ?? ""),
                },
            Both
        );
        var text = ScriptedOutputs([new Dictionary<string, object?> { ["target"] = "the 3 key on the keypad" }]);
        var fixture = Context(
            new ContextOptions
            {
                Tree = Buttons,
                Observation = WithPixels(WhitePixels()),
                Model = text,
            }
        );

        await Run(Build(model, text), fixture);

        Assert.Single(text.Requests);
        Assert.Contains("Do the thing", text.Prompts, StringComparison.Ordinal);
        Assert.Contains(
            "The control to tap now: the 3 key on the keypad",
            requests[1].Questions["x"].Instructions,
            StringComparison.Ordinal
        );
        Assert.Equal("the 3 key on the keypad", Prop(requests[1].State, "target").GetString());
        Assert.Matches(@"^tap the 3 key on the keypad at \(\d+, \d+\)\z", fixture.Turns[0].Calls[0]);
        // Operation, sub-target, three looks, next operation: six model calls recorded.
        Assert.Equal(6, fixture.Usage.Count);
    }

    [Fact]
    public async Task Replays_a_landed_point_for_the_same_control_on_the_same_page_and_relocates_one_that_missed()
    {
        // Two screenshots that differ only inside the tapped region.
        ExecutorPixels FramePixels(bool dot) => new(Frame(dot), "image/png", 800, 600, 1, 0);
        var (model, requests) = ScriptedDecision(
            (id, keys, call) =>
                id switch
                {
                    "operation" => new ScriptedAnswer(call < 12 ? "tap_at" : "blocked"),
                    "x" or "y" => new ScriptedAnswer("1"),
                    _ => new ScriptedAnswer(keys.FirstOrDefault() ?? ""),
                },
            Both
        );
        var text = Targets(12, "the 1 key");
        var fixture = Context(
            new ContextOptions
            {
                Tree = Buttons,
                Observation = WithPixels(FramePixels(false)),
                Model = text,
            }
        );
        // The first tap changes the pixels under it for good; the second changes nothing.
        fixture.ObserveWith = index => Look(index + 1, Buttons, FramePixels(index + 1 >= 2));

        await Run(Build(model, text), fixture);

        var kinds = requests.Select(request => string.Join('+', request.Questions.Keys)).ToList();
        // Tap 1 locates (three looks) and lands; tap 2 replays the point with no looks; tap 2 misses, so tap 3 locates again.
        Assert.Equal(
            ["operation", "x+y", "x+y", "x+y", "operation", "operation", "x+y", "x+y", "x+y", "operation"],
            kinds.Take(10)
        );
        // Level 1 of 10 over 800px and of 8 over 600px is (120, 113); the 400px zoom around it clamps to the corner,
        // and level 1 of its 8 cells is (150, 150) at 2x, so (75, 75); the 200px zoom clamps too, and its level 1 is (38, 38).
        Assert.Equal(new Point(38, 38), TapPoint(fixture, 0));
        Assert.Equal(new Point(38, 38), TapPoint(fixture, 1));
    }

    [Fact]
    public async Task Counts_a_pixel_change_as_a_page_change_under_vision_so_a_drawn_control_is_progress()
    {
        var (model, _) = ScriptedDecision(
            (id, keys, call) =>
                id switch
                {
                    "operation" => new ScriptedAnswer(call < 9 ? "tap_at" : "blocked"),
                    "x" or "y" => new ScriptedAnswer("1"),
                    _ => new ScriptedAnswer(keys.FirstOrDefault() ?? ""),
                },
            Both
        );
        var text = ScriptedOutputs(
            Enumerable
                .Range(0, 9)
                .Select(index => (object?)new Dictionary<string, object?> { ["target"] = $"the {index} key" })
        );
        var fixture = Context(new ContextOptions { Tree = Buttons, Model = text });
        var looks = 0;
        Func<int, ExecutorObservation> observe = _ =>
        {
            looks++;
            var rgba = Enumerable.Repeat((byte)255, 800 * 600 * 4).ToArray();
            rgba[looks] = 0;
            return Look(
                looks,
                Buttons,
                new ExecutorPixels(PngHelpers.PngOf(800, 600, rgba), "image/png", 800, 600, 1, 0)
            );
        };
        fixture.ObserveWith = observe;

        var verdict = await Run(Build(model, text), fixture);

        Assert.Equal(StepVerdictStatus.Blocked, verdict.Status);
        Assert.Equal("The decision model cannot make progress.", verdict.Summary);
        Assert.Equal(3, fixture.Actions.Count("tapAt"));
        var noVision = Context(new ContextOptions { Tree = Buttons });
        noVision.ObserveWith = observe;
        var (plain, _) = ScriptedDecision(
            (id, keys, call) =>
                new ScriptedAnswer(id == "operation" ? (call < 9 ? "tap" : "blocked") : keys.FirstOrDefault() ?? "")
        );
        var plainVerdict = await Run(Build(plain, vision: false), noVision);
        Assert.Equal("Three actions in a row failed or changed nothing on screen.", plainVerdict.Summary);
    }

    [Fact]
    public async Task Gates_a_point_on_the_mass_around_the_scored_position_not_the_top_level()
    {
        var (model, _) = ScriptedDecision(
            (id, keys, _) =>
                id switch
                {
                    "operation" => new ScriptedAnswer("tap_at"),
                    // Half the mass on level 2 and half on level 3: a precise position between them.
                    "x" or "y" => new ScriptedAnswer(
                        "2",
                        keys.ToDictionary(key => key, key => key is "2" or "3" ? 0.5 : 0.0),
                        Score: 2.5
                    ),
                    _ => new ScriptedAnswer(keys.FirstOrDefault() ?? ""),
                },
            Both
        );
        var text = Targets(25);
        var fixture = Context(
            new ContextOptions
            {
                Tree = Buttons,
                Observation = WithPixels(WhitePixels()),
                Model = text,
            }
        );

        await Run(Build(model, text, minProbability: 0.9), fixture);

        Assert.True(fixture.Actions.Count("tapAt") > 0);
        var (spread, _) = ScriptedDecision(
            (id, keys, _) =>
                id switch
                {
                    "operation" => new ScriptedAnswer("tap_at"),
                    // Mass split far apart: the weighted mean is 4, with nothing near it.
                    "x" or "y" => new ScriptedAnswer(
                        "2",
                        keys.ToDictionary(key => key, key => key is "2" or "6" ? 0.5 : 0.0),
                        Score: 4
                    ),
                    _ => new ScriptedAnswer(keys.FirstOrDefault() ?? ""),
                },
            Both
        );
        var wideText = Targets(25);
        var wide = Context(
            new ContextOptions
            {
                Tree = Buttons,
                Observation = WithPixels(WhitePixels()),
                Model = wideText,
            }
        );

        var verdict = await Run(Build(spread, wideText, minProbability: 0.5), wide);

        Assert.Equal(StepVerdictStatus.Blocked, verdict.Status);
        Assert.Contains("tap point is uncertain", verdict.Summary, StringComparison.Ordinal);
        Assert.Equal(0, wide.Actions.Count("tapAt"));
    }

    [Fact]
    public async Task Taps_a_committed_score_without_a_distribution_under_a_probability_gate_and_blocks_it_under_a_confidence_gate()
    {
        (ScriptedChoiceModel Model, List<EvalRequest> Requests) Committed() =>
            ScriptedDecision(
                (id, keys, _) =>
                    id switch
                    {
                        "operation" => new ScriptedAnswer("tap_at", Confidence: 1),
                        "x" or "y" => new ScriptedAnswer("2", Score: 2, Bare: true),
                        _ => new ScriptedAnswer(keys.FirstOrDefault() ?? "", Confidence: 1),
                    },
                Both
            );
        var text = Targets(25);
        var fixture = Context(
            new ContextOptions
            {
                Tree = Buttons,
                Observation = WithPixels(WhitePixels()),
                Model = text,
            }
        );

        await Run(Build(Committed().Model, text, minProbability: 0.9), fixture);

        Assert.True(fixture.Actions.Count("tapAt") > 0);
        var gatedText = Targets(25);
        var gated = Context(
            new ContextOptions
            {
                Tree = Buttons,
                Observation = WithPixels(WhitePixels()),
                Model = gatedText,
            }
        );

        var verdict = await Run(Build(Committed().Model, gatedText, minConfidence: 0.5), gated);

        Assert.Equal(StepVerdictStatus.Blocked, verdict.Status);
        Assert.Contains("tap point is uncertain", verdict.Summary, StringComparison.Ordinal);
        Assert.Equal(0, gated.Actions.Count("tapAt"));
    }
}
