// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;

namespace E2E.Tests.AgentActPixelTools;

// The point-tool its of upstream's describe('agent.act pixel verbs'), run on the web engine over the canvas fixture pages with a scripted
// model that cannot see.
public sealed partial class AgentActPixelVerbsTests
{
    private sealed record WebRun(List<Sent> Requests, StepRecord Step, Exception? Error);

    // The act model: a screenshot, then point verbs in it, then a verdict. `calls` is the number of tool results the loop holds.
    private static async Task<WebRun> ActOnAsync(string path, string instruction, Func<int, ModelResponse> script, Func<Screen, Task>? check = null, Func<Screen, Task>? before = null, ActionHook? onAction = null)
    {
        using var site = await CanvasPages.StartAsync();
        var requests = new List<Sent>();
        var model = new ScriptedModel(request =>
        {
            requests.Add(new Sent([.. request.Messages], [.. request.Tools]) { System = request.System });
            return script(request.Messages.Count(message => message.Role == "tool"));
        });
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(headless: true),
            Model = model,
            BaseUrl = site.Url,
            TestTitle = "pixel verbs > " + instruction,
            CacheEnabled = false,
            AssertionTimeout = TimeSpan.FromSeconds(5),
            ActionTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(30),
            TestTimeout = TimeSpan.FromSeconds(60),
        });
        Exception? error = null;
        try
        {
            await session.Context.App.OpenAsync(path);
            if (before is not null)
            {
                await before(session.Context.Screen);
            }

            await session.Context.Agent.ActAsync(instruction, new ActOptions { OnAction = onAction });
            if (check is not null)
            {
                await check(session.Context.Screen);
            }
        }
        catch (Exception ex)
        {
            error = ex;
        }

        session.Complete(error);
        return new WebRun(requests, session.StepRecords.Single(step => step.Api == "agent.act"), error);
    }

    private static ModelResponse Done(bool passed = true) => ModelResponses.Done(passed ? "passed" : "failed", "done");

    private static string Result(WebRun run, int turn) => Tools(run.Requests[turn]).Last().Content!;

    private static List<StepEvent> EngineEvents(WebRun run) => [.. run.Step.Events.Where(item => item.Kind == StepEventKind.Engine)];

    private static Task HasStatus(Screen screen, string text) => Expect.That(screen.GetByRole("status")).ToHaveTextAsync(text);

    [Fact]
    public async Task Opens_tree_first_with_every_pixel_verb_on_offer_and_the_rules_say_to_prefer_ids()
    {
        var run = await ActOnAsync("/canvas", "pick the red pin", _ => Done());
        Assert.Null(run.Error);
        var first = run.Requests[0];
        Assert.Matches("#e\\d+ button \"Reset\"", first.Messages[0].Content);
        Assert.DoesNotContain("Screenshot attached", first.Messages[0].Content, StringComparison.Ordinal);
        foreach (var verb in new[] { "screenshot", "tap_at", "hover_at", "drag_at", "type_at", "press_at", "select_at" })
        {
            Assert.Contains(first.Tools, tool => tool.Name == verb);
        }

        Assert.Contains("use the point tools only for a target the screen does not list", first.System, StringComparison.Ordinal);
        Assert.Contains("take a screenshot before guessing a point", first.System, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Attaches_the_screenshot_to_the_tool_result_as_an_image_then_taps_the_bare_point_it_named()
    {
        // The red pin is drawn at CSS (300, 60); the model taps in the 768-wide screenshot, 0.6 image px per CSS px.
        var run = await ActOnAsync(
            "/canvas",
            "pick the red pin",
            calls => calls switch
            {
                0 => ModelResponses.Call("screenshot", new { }),
                1 => ModelResponses.Call("tap_at", new { x = 180, y = 36 }),
                _ => Done(),
            },
            screen => HasStatus(screen, "red"),
            onAction: e =>
            {
                Assert.Equal(new Point(300, 60), Assert.IsType<TapAtActionEvent>(e).Point);
                return Task.CompletedTask;
            });
        Assert.Null(run.Error);
        var actions = EngineEvents(run);
        Assert.Equal(["tapAt"], actions.Select(item => item.Name));
        Assert.Equal("tap the point (300, 60)", actions[0].Detail);
        // No harness-made model call: the pixels went to the act model itself.
        Assert.Equal(3, run.Step.Metrics?.ModelCalls);
        Assert.True(run.Step.VisionInput);
        Assert.True(run.Step.Metrics?.PixelBytes > 0);
        Assert.Contains("Point coordinates (tap_at and the other _at verbs) are pixels of this image", Result(run, 1), StringComparison.Ordinal);
        Assert.Equal(1, run.Requests[1].Messages[^1].Images?.Count);
        // In pixel mode the tap result carries the screen and a fresh screenshot.
        Assert.Contains("Tapped the point (300, 60); no listed control is there", Result(run, 2), StringComparison.Ordinal);
        Assert.Contains("status \"Hit\" text=\"red\"", Result(run, 2), StringComparison.Ordinal);
        Assert.Equal(2, run.Requests[2].Messages.Sum(message => message.Images?.Count ?? 0));
    }

    [Fact]
    public async Task Taps_the_bare_point_when_it_lands_on_a_listed_control_naming_the_control()
    {
        // The Reset button is fixed at left 0, top 260, 100 by 30: CSS (50, 275) is image (30, 165).
        var run = await ActOnAsync(
            "/canvas",
            "press the reset button under the map",
            calls => calls switch
            {
                0 => ModelResponses.Call("screenshot", new { }),
                1 => ModelResponses.Call("tap_at", new { x = 30, y = 165 }),
                _ => Done(),
            },
            screen => HasStatus(screen, "reset"));
        Assert.Null(run.Error);
        Assert.Equal(["tapAt"], EngineEvents(run).Select(item => item.Name));
        Assert.Matches("Tapped the point \\(50, 275\\) inside #?\\S* ?button \"Reset\"", Result(run, 2));
    }

    [Fact]
    public async Task Taps_the_bare_point_not_the_center_of_a_control_something_else_covers()
    {
        // CSS (100, 100), outside the popover, is image (60, 60): inside only the dismiss control, whose center the popover covers.
        var run = await ActOnAsync(
            "/popover",
            "dismiss the calendar popover",
            calls => calls switch
            {
                0 => ModelResponses.Call("screenshot", new { }),
                1 => ModelResponses.Call("tap_at", new { x = 60, y = 60 }),
                _ => Done(),
            },
            screen => HasStatus(screen, "dismissed"));
        Assert.Null(run.Error);
        Assert.Equal(["tapAt"], EngineEvents(run).Select(item => item.Name));
        Assert.Matches("Tapped the point \\(100, 100\\) inside #?\\S* ?button \"Dismiss popover\"", Result(run, 2));
    }

    [Fact]
    public async Task Drags_between_two_bare_points_through_the_engine_when_no_listed_controls_hold_them()
    {
        // The red pin at CSS (300, 60) is image (180, 36); the map's middle, CSS (200, 100), is image (120, 60).
        var run = await ActOnAsync(
            "/canvas",
            "drag the red pin to the middle of the map",
            calls => calls switch
            {
                0 => ModelResponses.Call("screenshot", new { }),
                1 => ModelResponses.Call("drag_at", new { x = 180, y = 36, to_x = 120, to_y = 60 }),
                _ => Done(),
            },
            async screen =>
            {
                await HasStatus(screen, "moved red to 200,100");
                await screen.TapAtAsync(new Point(200, 100));
                await HasStatus(screen, "red");
            },
            onAction: e =>
            {
                var drag = Assert.IsType<DragAtActionEvent>(e);
                Assert.Equal(new Point(300, 60), drag.From);
                Assert.Equal(new Point(200, 100), drag.To);
                return Task.CompletedTask;
            });
        Assert.Null(run.Error);
        var actions = EngineEvents(run);
        Assert.Equal(["dragAt"], actions.Select(item => item.Name));
        Assert.Equal("drag from the point (300, 60) to the point (200, 100)", actions[0].Detail);
        Assert.Contains("Dragged from the point (300, 60) to the point (200, 100); no listed control is at either end.", Result(run, 2), StringComparison.Ordinal);
        Assert.Equal(2, run.Requests[2].Messages.Sum(message => message.Images?.Count ?? 0));
    }

    [Fact]
    public async Task Drags_between_the_bare_points_when_they_land_on_listed_controls_naming_the_controls()
    {
        // Reset's center, CSS (50, 275), is image (30, 165); the Note field's, CSS (100, 315), is image (60, 189).
        var run = await ActOnAsync(
            "/canvas",
            "drag the reset button onto the note field",
            calls => calls switch
            {
                0 => ModelResponses.Call("screenshot", new { }),
                1 => ModelResponses.Call("drag_at", new { x = 30, y = 165, to_x = 60, to_y = 189 }),
                _ => Done(),
            },
            screen => HasStatus(screen, "none"));
        Assert.Null(run.Error);
        var actions = EngineEvents(run);
        Assert.Equal(["dragAt"], actions.Select(item => item.Name));
        Assert.Equal("drag from the point (50, 275) on button \"Reset\" to the point (100, 315) on textbox \"Note\"", actions[0].Detail);
        Assert.Matches("Dragged from the point \\(50, 275\\) inside \\S* ?button \"Reset\"\\S* to the point \\(100, 315\\) inside \\S* ?textbox \"Note\"", Result(run, 2));
    }

    [Fact]
    public async Task Attaches_a_screenshot_to_the_opening_prompt_of_a_screen_with_nothing_to_tap_by_id()
    {
        // The opening prompt carried the screenshot: the blue pin at CSS (80, 140) is image (48, 84), no screenshot call.
        var run = await ActOnAsync(
            "/canvas-bare",
            "pick the blue pin on the bare map",
            calls => calls == 0 ? ModelResponses.Call("tap_at", new { x = 48, y = 84 }) : Done(),
            screen => HasStatus(screen, "blue"));
        Assert.Null(run.Error);
        Assert.Equal(["tapAt"], EngineEvents(run).Select(item => item.Name));
        Assert.Equal(2, run.Step.Metrics?.ModelCalls);
        Assert.True(run.Step.VisionInput);
        Assert.Contains("Screenshot attached: 768 by 432 pixels (0.6 per CSS pixel)", run.Requests[0].Messages[0].Content, StringComparison.Ordinal);
        // An ordinary page opens tree-only: one listed control is enough to act by id.
        var home = await ActOnAsync("/", "tap blind", calls => calls == 0 ? ModelResponses.Call("tap_at", new { x = 300, y = 60 }) : Done(passed: false));
        Assert.DoesNotContain("Screenshot attached", home.Requests[0].Messages[0].Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("tap blind", "tap_at")]
    [InlineData("type at a point before any screenshot", "type_at")]
    public async Task Answers_a_point_verb_before_any_screenshot_with_the_line_to_read_spending_no_action(string instruction, string verb)
    {
        var run = await ActOnAsync(
            "/",
            instruction,
            calls => calls == 0 ? ModelResponses.Call(verb, verb == "tap_at" ? new { x = 300, y = 60 } : new { x = 60, y = 189, value = "nope" }) : Done(passed: false));
        Assert.NotNull(run.Error);
        Assert.Contains($"No screenshot has been taken in this step: {verb} coordinates are pixels of the latest screenshot", Result(run, 1), StringComparison.Ordinal);
        Assert.Empty(EngineEvents(run));
        Assert.Equal(0, run.Step.Metrics?.ActionSteps);
    }

    [Fact]
    public async Task Types_presses_and_selects_through_points_acting_on_the_listed_control_by_id_underneath()
    {
        // Note input at CSS (0..200, 300..330): its center (100, 315) is image (60, 189).
        // Tint select at CSS (0..200, 340..370): center (100, 355) is image (60, 213).
        var run = await ActOnAsync(
            "/canvas",
            "write a note and pick the warm tint",
            calls => calls switch
            {
                0 => ModelResponses.Call("screenshot", new { }),
                1 => ModelResponses.Call("type_at", new { x = 60, y = 189, value = "hello" }),
                2 => ModelResponses.Call("press_at", new { x = 60, y = 189, key = "Enter" }),
                3 => ModelResponses.Call("select_at", new { x = 60, y = 213, value = "warm" }),
                _ => Done(),
            },
            screen => HasStatus(screen, "tint: warm"));
        Assert.Null(run.Error);
        var actions = EngineEvents(run);
        Assert.Equal(["type", "press", "selectOption"], actions.Select(item => item.Name));
        // The port words an event detail as `<verb> <node>`, with no value or key, where upstream's reads `type "hello" into <node>`.
        Assert.Equal("fill textbox \"Note\"", actions[0].Detail);
        Assert.Equal("press textbox \"Note\"", actions[1].Detail);
        Assert.Equal("select combobox \"Tint\"", actions[2].Detail);
        Assert.Matches("Typed into \\S* ?textbox \"Note\"[^,]*, the control at \\(100, 315\\)\\.", Result(run, 2));
        Assert.Matches("Pressed Enter on \\S* ?textbox \"Note\"[^,]*, the control at \\(100, 315\\)\\.", Result(run, 3));
        Assert.Matches("Selected \"warm\" in \\S* ?combobox \"Tint\"[^,]*, the control at \\(100, 355\\)\\.", Result(run, 4));
    }

    [Fact]
    public async Task Reports_a_point_with_nothing_selectable_under_it_as_an_action_failure_the_model_can_read()
    {
        // A point on the status line: nothing listed there is a control.
        var run = await ActOnAsync(
            "/canvas",
            "select on the map",
            calls => calls switch
            {
                0 => ModelResponses.Call("screenshot", new { }),
                1 => ModelResponses.Call("select_at", new { x = 30, y = 30, value = "x" }),
                _ => Done(passed: false),
            });
        Assert.NotNull(run.Error);
        // The lead echoes the model's image coordinates; the hit summary speaks in viewport pixels. The port's lead names the error code too.
        Assert.Contains(
            "select_at (30, 30) failed: ACTION_FAILED: nothing the screen lists is at (50, 50); select_at needs a control the screen lists.",
            Result(run, 2),
            StringComparison.Ordinal);
        Assert.Empty(EngineEvents(run));
        Assert.Equal(0, run.Step.Metrics?.ActionSteps);
    }

    // The port's tool for typing is fill, where upstream's is type, and an engine event is worded `<verb> <node>`, so these differ from upstream's in names only.
    [Fact]
    public async Task Taps_a_drawn_field_to_focus_it_and_types_through_the_keyboard_then_presses_Enter_on_the_focused_field()
    {
        // The canvas at CSS (200, 100) is image (120, 60): no listed control there, so type_at taps to focus it and types.
        var run = await ActOnAsync(
            "/canvas",
            "type hi into the drawn field and submit",
            calls => calls switch
            {
                0 => ModelResponses.Call("screenshot", new { }),
                1 => ModelResponses.Call("type_at", new { x = 120, y = 60, value = "hi" }),
                2 => ModelResponses.Call("press", new { key = "Enter" }),
                _ => Done(),
            },
            screen => HasStatus(screen, "drawn: hi"));
        Assert.Null(run.Error);
        var actions = EngineEvents(run);
        Assert.Equal(["tapAt", "typeText", "pressKey"], actions.Select(item => item.Name));
        Assert.Equal("type \"hi\" into the focused field", actions[1].Detail);
        Assert.Equal("press \"Enter\" on the focused field", actions[2].Detail);
        Assert.Contains("Typed into the field at (120, 60) (tapped to focus it; nothing the screen lists is at (200, 100)).", Result(run, 2), StringComparison.Ordinal);
        Assert.Contains("Pressed Enter on the focused field.", Result(run, 3), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Offers_fill_and_press_without_a_target_typing_into_the_focused_drawn_field()
    {
        var run = await ActOnAsync(
            "/canvas",
            "focus the drawn field, type ok, submit",
            calls => calls switch
            {
                0 => ModelResponses.Call("screenshot", new { }),
                1 => ModelResponses.Call("tap_at", new { x = 120, y = 60 }),
                2 => ModelResponses.Call("fill", new { value = "ok" }),
                3 => ModelResponses.Call("press", new { key = "Enter" }),
                _ => Done(),
            },
            screen => HasStatus(screen, "drawn: ok"));
        Assert.Null(run.Error);
        Assert.Equal(["tapAt", "typeText", "pressKey"], EngineEvents(run).Select(item => item.Name));
        Assert.Contains(run.Requests[0].Tools, tool => tool.Name == "fill");
        Assert.DoesNotContain(run.Requests[0].Tools, tool => tool.Name == "dismiss_keyboard");
        Assert.Contains("Typed into the focused field.", Result(run, 3), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Falls_back_to_focusing_an_unfillable_listed_node_and_typing_through_the_keyboard()
    {
        var run = await ActOnAsync(
            "/canvas",
            "type yo into the pad",
            calls => calls switch
            {
                0 => ModelResponses.Call("fill", new { role = "application", name = "Pad", value = "yo" }),
                1 => ModelResponses.Call("press", new { role = "application", name = "Pad", key = "Enter" }),
                _ => Done(),
            },
            screen => HasStatus(screen, "pad: yo"));
        Assert.Null(run.Error);
        var actions = EngineEvents(run);
        // The fill was refused, so the node was tapped to hold focus and typed into through the keyboard.
        Assert.Equal(["type:Failed", "tap:Passed", "typeText:Passed", "press:Passed"], actions.Select(item => item.Name + ":" + item.Status));
        Assert.Matches("is not an input; tapped it to focus it and typed through the keyboard\\.", Result(run, 1));
        // The pad's text is nowhere in the tree, so the unchanged listing is not read as the typing having failed.
        Assert.Contains("The tree does not list this node's text, so an unchanged listing says nothing about the typing: verify it through the app's reaction or a screenshot.", Result(run, 1), StringComparison.Ordinal);
        Assert.DoesNotContain("had no visible effect", Result(run, 1), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refuses_focused_typing_into_nothing_editable_as_an_action_failure_the_model_reads()
    {
        var run = await ActOnAsync(
            "/",
            "type blind",
            calls => calls == 0 ? ModelResponses.Call("fill", new { value = "nope" }) : Done(passed: false));
        Assert.NotNull(run.Error);
        // The engine refused before any keystroke: the action was reserved, not committed.
        Assert.DoesNotContain(EngineEvents(run), item => item.Status == StepEventStatus.Passed);
        Assert.Contains("nothing that takes keystrokes has focus: the typed text would reach no field", Result(run, 1), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Counts_failing_point_verbs_toward_the_failure_streak_a_warning_at_three_a_forced_verdict_at_five()
    {
        // Five select_at calls on the blank page right of the controls: nothing listed is there, so each fails, and each names a new point so
        // only the failure streak can stop the loop.
        var run = await ActOnAsync(
            "/canvas",
            "pick options off the empty map margin",
            calls => calls switch
            {
                0 => ModelResponses.Call("screenshot", new { }),
                <= 5 => ModelResponses.Call("select_at", new { x = 380 + (calls * 20), y = 180 + (calls * 10), value = "x" }),
                _ => Done(passed: false),
            });
        Assert.NotNull(run.Error);
        Assert.True(run.Step.Metrics?.ModelCalls <= 8);
        // Every refusal reads `select_at (x, y) failed: ...` beside a fresh screenshot: pixel mode held.
        Assert.Contains("select_at (400, 190) failed: ACTION_FAILED: nothing the screen lists is at", Result(run, 2), StringComparison.Ordinal);
        Assert.Equal(1, run.Requests[2].Messages[^1].Images?.Count);
        Assert.Contains(run.Requests, request => request.Messages.Any(message => message.Role == "user" && message.Content?.Contains("[SYSTEM NOTICE] the last 3 actions failed in a row", StringComparison.Ordinal) == true));
        var last = run.Requests[^1];
        Assert.Equal(["done"], last.Tools.Select(tool => tool.Name));
        Assert.Contains("[SYSTEM] Loop guard: the last 5 actions failed in a row", last.Messages[^1].Content, StringComparison.Ordinal);
        // The loop notes the warning and the stop on the turn they followed, as upstream's note() does.
        var turns = Assert.IsAssignableFrom<IReadOnlyList<StepTurn>>(run.Step.Turns);
        Assert.Contains(turns, turn => turn.Outcome.Contains("[loop] loop guard warned before turn", StringComparison.Ordinal) && turn.Outcome.Contains(": the last 3 actions failed in a row", StringComparison.Ordinal));
        Assert.Contains(turns, turn => turn.Outcome.Contains("[loop] the step was stopped before turn", StringComparison.Ordinal) && turn.Outcome.Contains(": the last 5 actions failed in a row", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Keeps_every_pixel_verb_in_the_vocabulary_after_a_secret_fill()
    {
        var run = await ActOnAsync(
            "/",
            "note the page",
            _ => Done(),
            before: screen => screen.GetByLabel("Password").FillAsync(Secret.Create("memberPassword", "hunter2-secret")));
        Assert.Null(run.Error);
        var first = run.Requests[0];
        foreach (var verb in new[] { "screenshot", "tap_at", "drag_at", "type_at", "press_at", "select_at", "tap", "fill" })
        {
            Assert.Contains(first.Tools, tool => tool.Name == verb);
        }
    }

    [Fact]
    public async Task Reports_a_moved_screenshot_under_an_unchanged_listing_and_blames_the_control_only_when_the_image_stood_still_too()
    {
        // After the red pin, the same miss point at CSS (100, 100) twice: the status text repeats, so the listing stands still while the map
        // paints a new tap count. Then the blank margin at CSS (700, 500): nothing there repaints.
        var run = await ActOnAsync(
            "/canvas",
            "tap the map twice, then the margin once",
            calls => calls switch
            {
                0 => ModelResponses.Call("screenshot", new { }),
                1 => ModelResponses.Call("tap_at", new { x = 180, y = 36 }),
                <= 3 => ModelResponses.Call("tap_at", new { x = 60, y = 60 }),
                4 => ModelResponses.Call("tap_at", new { x = 420, y = 300 }),
                _ => Done(),
            });
        Assert.Null(run.Error);
        // The first miss changed the listed status text; the second repeated it, and only the drawn tap count moved.
        Assert.Matches("changed #\\S+ status \"Hit\" text=\"miss at 100,100\"", Result(run, 3));
        Assert.DoesNotContain("listed nodes unchanged", Result(run, 3), StringComparison.Ordinal);
        Assert.Contains("listed nodes unchanged; the screenshot changed", Result(run, 4), StringComparison.Ordinal);
        Assert.DoesNotContain("had no visible effect", Result(run, 4), StringComparison.Ordinal);
        Assert.Equal(1, run.Requests[4].Messages[^1].Images?.Count);
        // The margin tap changed neither the listing nor a pixel: the same PNG, so the tap is blamed as before.
        Assert.Contains("had no visible effect", Result(run, 5), StringComparison.Ordinal);
        Assert.DoesNotContain("the screenshot changed", Result(run, 5), StringComparison.Ordinal);
        Assert.Equal(1, run.Requests[5].Messages[^1].Images?.Count);
    }
}
