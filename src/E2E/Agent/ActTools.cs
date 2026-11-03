// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using E2E.Cache;
using E2E.Engine;
using E2E.Internal;
using static E2E.Agent;

namespace E2E;

/// <summary>
/// The act tools as the model calls them: each call is checked, aimed at a control, and run through the step's
/// <see cref="IExecutorActions"/>, and the answer is the screen after it. A coding agent that drives a live session over
/// MCP runs the same tools through <see cref="Agent.RunToolAsync"/>, and passes its <see cref="ScreenPresenter"/>: the answer is then the
/// changes since the screen the coding agent holds, as upstream's session answers. No upstream equivalent: upstream's tool loop holds
/// these as closures over its <c>ToolLoopHelpers</c>.
/// </summary>
internal sealed partial class ActTools(AttemptScope scope, ObservationFeed feed, ScreenPresenter? screen = null, PixelMode? pixels = null)
{
    private static readonly HashSet<string> ActionTools =
        new(["navigate", "tap", "double_tap", "long_press", "right_click", "hover", "drag", "fill", "fill_secret", "press", "select", "check", "uncheck", "clear", "upload", "back", "scroll", "scroll_to", "tap_at", "hover_at", "drag_at", "type_at", "press_at", "select_at"], StringComparer.Ordinal);

    private readonly AttemptScope _scope = scope;
    private readonly ObservationFeed _feed = feed;
    private readonly ScreenPresenter? _screen = screen;
    private readonly PixelMode? _pixels = pixels;

    // Whether the engine has a keyboard, which a fill with no node and a point on nothing listed reach focused fields through.
    private bool Keyboard => _scope.EngineCapabilities.HasFlag(EngineCapabilities.Keyboard);

    // context: the step the tools run in, which a project tool reaches through AgentToolContext. Null outside a step.
    public async Task<ToolOutcome> ExecuteAsync(
        ModelToolCall call,
        IExecutorActions actions,
        StepAccounting budget,
        IReadOnlyDictionary<string, AgentTool>? projectTools,
        CancellationToken token,
        StepExecutorContext? context = null)
    {
        // observe looks and records nothing, so it takes no action slot.
        if (string.Equals(call.Name, "observe", StringComparison.Ordinal))
        {
            return _screen is null ? Outcome(await DescribeAsync("Observed.", token).ConfigureAwait(false)) : ToolOutcome.Ok(await WholeScreenAsync(token).ConfigureAwait(false));
        }

        // screenshot looks too: it attaches the viewport's pixels, and from then on every result carries a fresh one.
        if (string.Equals(call.Name, AgentTools.ScreenshotName, StringComparison.Ordinal) && _screen is null)
        {
            try
            {
                return Outcome(await DescribeAsync("Screenshot taken.", token, pixels: true).ConfigureAwait(false));
            }
            catch (E2EException ex) when (!token.IsCancellationRequested && !StepExecutors.IsRuntimeHardStop(ex))
            {
                return ToolOutcome.Fail(ex);
            }
        }

        if (projectTools is not null && projectTools.TryGetValue(call.Name, out var projectTool))
        {
            return await RunProjectToolAsync(call, projectTool, budget, token, context).ConfigureAwait(false);
        }

        // A pointer verb is a tool only where the engine declared its action kind, so a model that calls one anyway is told so.
        if (!ActionTools.Contains(call.Name) || (AgentTools.PointerToolKinds.TryGetValue(call.Name, out var kind) && _scope.ActionKinds?.Contains(kind) != true))
        {
            return ToolOutcome.Fail("Unknown tool " + call.Name + ".");
        }

        // Arguments that are no JSON object (text that did not parse, an array, a bare value) run nothing: a missing key would fall back to a default.
        if (call.Arguments.ValueKind is not (JsonValueKind.Object or JsonValueKind.Undefined or JsonValueKind.Null))
        {
            return ToolOutcome.Fail(call.Name + " takes a JSON object of arguments.");
        }

        // Every action that runs claims a budget slot before it is checked, so a call the checks turn away was still an attempt.
        // The one dispatcher action the call makes spends the slot.
        if (!budget.TryPrepay())
        {
            return ToolOutcome.Fail(budget.Message + ". Take no more actions. Call done with a verdict.");
        }

        try
        {
            return await RunActionAsync(call, actions, budget, token).ConfigureAwait(false);
        }
        finally
        {
            budget.ClearPrepaid();
        }
    }

    private async Task<ToolOutcome> RunActionAsync(ModelToolCall call, IExecutorActions actions, StepAccounting budget, CancellationToken token)
    {
        var arguments = call.Arguments;
        switch (call.Name)
        {
            case "back":
                return await ActingAsync(new ActionLabel("Navigated back.", "back"), () => actions.BackAsync(token), token).ConfigureAwait(false);
            case "scroll":
                return await ScrollAsync(arguments, actions, token).ConfigureAwait(false);
            case "scroll_to":
                return await ScrollToAsync(arguments, actions, token).ConfigureAwait(false);
            case "upload":
                return await UploadAsync(arguments, actions, budget, token).ConfigureAwait(false);
            case "drag":
                return await DragAsync(arguments, actions, token).ConfigureAwait(false);
            case "navigate":
                var url = Args.String(arguments, "url") ?? "/";
                return await ActingAsync(new ActionLabel("Navigated to " + url + ".", "navigate " + url), () => actions.NavigateAsync(url, token), token).ConfigureAwait(false);
            case "tap_at" or "hover_at" or "drag_at" or "type_at" or "press_at" or "select_at":
                return await PointToolAsync(call.Name, arguments, actions, budget, token).ConfigureAwait(false);
            case "press" when !HasTarget(arguments):
                var focusedKey = Args.String(arguments, "key") ?? "Enter";
                return await ActingAsync(NodeLabel(call.Name, arguments, "the focused field"), () => actions.PressKeyAsync(focusedKey, token), token).ConfigureAwait(false);
            case "fill" when !HasTarget(arguments) && Keyboard:
                // Keyboard input to whatever holds focus: how a field the tree does not list gets its text after a tap_at gave it focus.
                var typed = Args.String(arguments, "value") ?? "";
                var replace = Args.Bool(arguments, "replace") == true;
                return await ActingAsync(
                    new ActionLabel("Typed into the focused field.", "fill into the focused field"),
                    () => actions.TypeTextAsync(typed, new TypeTextOptions { Replace = replace }, token),
                    token).ConfigureAwait(false);
        }

        SemanticNode node;
        try
        {
            node = await ResolveAsync(arguments, token).ConfigureAwait(false);
        }
        catch (TestException ex)
        {
            return await FailedAsync(NodeLabel(call.Name, arguments, ReferenceOf(arguments)), ex, token).ConfigureAwait(false);
        }

        var target = new ExecutorTarget(node);
        if (call.Name == "fill" && Keyboard)
        {
            var value = Args.String(arguments, "value") ?? "";
            return await ActingAsync(NodeLabel(call.Name, arguments, "#" + node.Ref), () => TypeIntoNodeAsync(actions, target, value, "#" + node.Ref, token), token).ConfigureAwait(false);
        }

        return await ActingAsync(
            NodeLabel(call.Name, arguments, "#" + node.Ref),
            () => call.Name switch
            {
                "tap" => actions.TapAsync(target, ClickOptionsOf(arguments), token),
                "double_tap" => actions.DoubleTapAsync(target, ClickOptionsOf(arguments), token),
                "long_press" => actions.LongPressAsync(target, token),
                "right_click" => actions.SecondaryTapAsync(target, ClickOptionsOf(arguments), token),
                "hover" => actions.HoverAsync(target, token),
                "fill" => actions.TypeAsync(target, Args.String(arguments, "value") ?? "", token),
                "fill_secret" => actions.TypeSecretAsync(target, Args.String(arguments, "secret") ?? "", token),
                "press" => actions.PressAsync(target, Args.String(arguments, "key") ?? "Enter", token),
                "select" => actions.SelectAsync(target, Args.String(arguments, "value") ?? "", token),
                "check" => actions.CheckAsync(target, true, token),
                "uncheck" => actions.CheckAsync(target, false, token),
                "clear" => actions.ClearAsync(target, token),
                _ => throw new TestException("INVALID_ARGUMENT", "Unknown tool " + call.Name + "."),
            },
            token).ConfigureAwait(false);
    }

    // Upstream's createPointTools: a point the model read off the latest screenshot, scaled into the viewport, then the verb the tool names.
    // Before a screenshot the call costs nothing: it answers with the line that says to take one. The action's own summary leads the
    // result, and a call the point verb turned away before it took a slot gives the slot back.
    private async Task<ToolOutcome> PointToolAsync(string tool, JsonElement arguments, IExecutorActions actions, StepAccounting budget, CancellationToken token)
    {
        var coordinates = new (string Name, double? Value)[] { ("x", Args.Number(arguments, "x")), ("y", Args.Number(arguments, "y")) };
        var drag = tool == "drag_at";
        if (drag)
        {
            coordinates = [.. coordinates, ("to_x", Args.Number(arguments, "to_x")), ("to_y", Args.Number(arguments, "to_y"))];
        }

        if (coordinates.FirstOrDefault(pair => pair.Value is null).Name is { } missing)
        {
            budget.RefundPrepaid();
            return ToolOutcome.Fail(tool + " takes a number for " + missing + ".");
        }

        // Upstream's schemas: type_at needs a value (empty is allowed), select_at a non-empty value, press_at a key of 1 to 64 characters.
        var text = tool switch { "type_at" or "select_at" => Args.String(arguments, "value"), "press_at" => Args.String(arguments, "key"), _ => "" };
        if (text is null || (tool == "select_at" && text.Length == 0) || (tool == "press_at" && text.Length is 0 or > 64))
        {
            budget.RefundPrepaid();
            return ToolOutcome.Fail(tool + " takes " + (tool == "press_at" ? "a key of 1 to 64 characters." : tool == "select_at" ? "a non-empty value." : "a value."));
        }

        var px = coordinates[0].Value!.Value;
        var py = coordinates[1].Value!.Value;
        if (_pixels?.Latest is not var (shot, viewport))
        {
            budget.RefundPrepaid();
            return ToolOutcome.Ok(_pixels?.Showing == true
                ? "The screenshot has no known viewport size, so " + tool + " coordinates cannot be placed. Act on a listed node by id."
                : "No screenshot has been taken in this step: " + tool + " coordinates are pixels of the latest screenshot. Call screenshot first, or act on a listed node by id.");
        }

        var point = PointTap.ImagePointToViewport(new Point(px, py), shot, viewport);
        var attempt = tool + " " + At(px, py) + (drag ? " to " + At(coordinates[2].Value!.Value, coordinates[3].Value!.Value) : "");
        var label = new ActionLabel(attempt, attempt);
        try
        {
            var lead = tool switch
            {
                "tap_at" => (await actions.TapAtAsync(point, token).ConfigureAwait(false)).Summary,
                "hover_at" => (await actions.HoverAtAsync(point, token).ConfigureAwait(false)).Summary,
                "drag_at" => (await actions.DragAtAsync(point, PointTap.ImagePointToViewport(new Point(coordinates[2].Value!.Value, coordinates[3].Value!.Value), shot, viewport), token).ConfigureAwait(false)).Summary,
                "select_at" => await SelectAtAsync(point, text, actions, token).ConfigureAwait(false),
                "type_at" => await TypeAtAsync(point, At(px, py), text, Args.Bool(arguments, "replace") == true, actions, token).ConfigureAwait(false),
                _ => await PressAtAsync(point, At(px, py), text, actions, token).ConfigureAwait(false),
            };
            return await DescribeOutcomeAsync(lead, token).ConfigureAwait(false);
        }
        catch (E2EException ex) when (!StepExecutors.IsRuntimeHardStop(ex))
        {
            // A point verb turned away before it took its slot has spent nothing; one that ran has taken the prepaid slot already.
            budget.RefundPrepaid();
            return await FailedAsync(label, ex, token).ConfigureAwait(false);
        }
    }

    // Upstream's controlAt: the listed control at a point, for a verb that needs one. Nothing listed there is an ordinary action failure the model reads and works around.
    private static async Task<(ExecutorTarget Control, string Summary)> ControlAtAsync(Point point, string verb, IExecutorActions actions, CancellationToken token)
    {
        var hit = await actions.HitTestAsync(point, token).ConfigureAwait(false);
        return hit.Control is { } control
            ? (control, hit.Summary)
            : throw new TestException("ACTION_FAILED", hit.Summary + "; " + verb + " needs a control the screen lists. Tap the target first, or aim at the control itself.");
    }

    private static async Task<string> SelectAtAsync(Point point, string value, IExecutorActions actions, CancellationToken token)
    {
        var (control, summary) = await ControlAtAsync(point, "select_at", actions, token).ConfigureAwait(false);
        await actions.SelectAsync(control, value, token).ConfigureAwait(false);
        return "Selected \"" + value + "\" in " + summary + ".";
    }

    // Upstream's focusAt: the control at a point for a verb that needs one, or, with a keyboard, focus given to the point by a tap so the
    // keyboard can reach whatever the app put there: a field drawn on a canvas, an input flattened out of the tree.
    private async Task<(ExecutorTarget? Control, string Summary)> FocusAtAsync(Point point, string at, string verb, IExecutorActions actions, CancellationToken token)
    {
        var hit = await actions.HitTestAsync(point, token).ConfigureAwait(false);
        if (hit.Control is { } control)
        {
            return (control, hit.Summary);
        }

        if (!Keyboard)
        {
            throw new TestException("ACTION_FAILED", hit.Summary + "; " + verb + " needs a control the screen lists. Tap the target first, or aim at the control itself.");
        }

        await actions.TapAtAsync(point, token).ConfigureAwait(false);
        return (null, "the field at " + at + " (tapped to focus it; " + hit.Summary + ")");
    }

    private async Task<string> TypeAtAsync(Point point, string at, string value, bool replace, IExecutorActions actions, CancellationToken token)
    {
        var (control, summary) = await FocusAtAsync(point, at, "type_at", actions, token).ConfigureAwait(false);
        if (control is null)
        {
            await actions.TypeTextAsync(value, new TypeTextOptions { Replace = replace }, token).ConfigureAwait(false);
        }
        else if (await TypeIntoNodeAsync(actions, control, value, summary, token).ConfigureAwait(false) is { } note)
        {
            return note.Lead;
        }

        return "Typed into " + summary + ".";
    }

    private async Task<string> PressAtAsync(Point point, string at, string key, IExecutorActions actions, CancellationToken token)
    {
        var (control, summary) = await FocusAtAsync(point, at, "press_at", actions, token).ConfigureAwait(false);
        if (control is null)
        {
            await actions.PressKeyAsync(key, token).ConfigureAwait(false);
        }
        else
        {
            await actions.PressAsync(control, key, token).ConfigureAwait(false);
        }

        return "Pressed " + key + " on " + summary + ".";
    }

    /// <summary>How the lead of a <c>fill</c> that fell back to the keyboard says so (upstream's <c>TYPED_THROUGH_KEYBOARD</c>).</summary>
    private const string TypedThroughKeyboardLead = "is not an input; tapped it to focus it and typed through the keyboard";

    /// <summary>True for the result of a fill whose node took the value through the keyboard, not as a fill. Upstream's <c>typedThroughKeyboard</c>.</summary>
    internal static bool TypedThroughKeyboard(string result) => result.Split('\n', 2)[0].Contains(TypedThroughKeyboardLead, StringComparison.Ordinal);

    // Upstream's typeIntoNode. Types into one listed node. A node the engine cannot fill (a focusable canvas, a custom widget with its own
    // key handling, or a container the model named after a tap_at) still takes keystrokes when the engine has a keyboard: the node is
    // tapped so it holds focus, then the value goes to the focused field. The tap comes first because whatever had focus before may be an
    // unrelated field an earlier action left focused, and typing into it would corrupt that field while reporting success on this node.
    // Returns that path's outcome: its lead, and no change expected of the tree, which does not list what such a node holds, so an
    // unchanged listing would otherwise read as the typing having failed. Null when the node took the value as a fill.
    private async Task<ActionOutcome?> TypeIntoNodeAsync(IExecutorActions actions, ExecutorTarget target, string value, string label, CancellationToken token)
    {
        try
        {
            await actions.TypeAsync(target, value, token).ConfigureAwait(false);
            return null;
        }
        catch (E2EException ex) when (Keyboard && !StepExecutors.IsRuntimeHardStop(ex) && IsNotFillable(ex))
        {
            // The node is no input: the keyboard takes over below.
        }

        await actions.TapAsync(target, token).ConfigureAwait(false);
        await actions.TypeTextAsync(value, new TypeTextOptions { Replace = false }, token).ConfigureAwait(false);
        return new ActionOutcome(
            label + " " + TypedThroughKeyboardLead + ". The tree does not list this node's text, so an unchanged listing says nothing about the typing: verify it through the app's reaction or a screenshot.",
            false);
    }

    // An engine's refusal to fill a node that is not a text input. The engine codes it NOT_ACTIONABLE; the harness may hand the executor an
    // ACTION_FAILED wrapping it, so the chain is walked for the code and the message.
    private static bool IsNotFillable(Exception cause)
    {
        for (var current = cause; current is not null; current = current.InnerException)
        {
            if (current is E2EException { Code: EngineErrorCodes.NotActionable } && NotFillable().IsMatch(current.Message))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"not an? <?(input|textarea)|not editable", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NotFillable();

    // What an action says about itself beyond its label: the lead its result opens with, and whether the tree is expected to show a change.
    private readonly record struct ActionOutcome(string Lead, bool ExpectChange);

    // How a point reads in a lead: upstream's `(x, y)`.
    private static string At(double x, double y) => "(" + x.ToString("R", CultureInfo.InvariantCulture) + ", " + y.ToString("R", CultureInfo.InvariantCulture) + ")";

    // Upstream's `acting`: the action runs, then the screen follows under the label's done lead, or under the failure lead when it threw.
    private Task<ToolOutcome> ActingAsync(ActionLabel label, Func<Task> action, CancellationToken token) =>
        ActingAsync(
            label,
            async () =>
            {
                await action().ConfigureAwait(false);
                return null;
            },
            token);

    // The same for an action that may report its own lead and whether a change is expected (upstream's ActionOutcome).
    private async Task<ToolOutcome> ActingAsync(ActionLabel label, Func<Task<ActionOutcome?>> action, CancellationToken token)
    {
        ActionOutcome? outcome;
        try
        {
            outcome = await action().ConfigureAwait(false);
        }
        catch (E2EException ex) when (!StepExecutors.IsRuntimeHardStop(ex))
        {
            return await FailedAsync(label, ex, token).ConfigureAwait(false);
        }

        return await DescribeOutcomeAsync(outcome?.Lead ?? label.Done, token, outcome?.ExpectChange ?? label.ExpectChange).ConfigureAwait(false);
    }

    // Upstream's `failureLead` on a screen: a failed action still returns the screen, so the model can act on a stale-id or not-found failure at once.
    private async Task<ToolOutcome> FailedAsync(ActionLabel label, E2EException error, CancellationToken token)
    {
        var screen = await DescribeAsync(FailureLead(label, error), token).ConfigureAwait(false);
        return new() { Content = screen.Text, Pixels = screen.Pixels, Succeeded = false, Code = error.Code, Error = error.Message };
    }

    private static ToolOutcome Outcome(ScreenText screen) => new() { Content = screen.Text, Pixels = screen.Pixels, Succeeded = true };

    // Upstream's failureLead: the attempt, then the error's code, then its message. `navigate file:///etc/passwd failed: POLICY_DENIED: forbidden URL scheme: file:`.
    private static string FailureLead(ActionLabel label, E2EException error) => label.Attempt + " failed: " + error.Code + ": " + error.Message;

    // The done and attempt leads of a node tool, upstream's wording; `reference` stands where upstream's `#id` does. fill and fill_secret
    // keep the port's tool names in the attempt, where upstream's are type and type_secret. clear has no upstream tool.
    private static ActionLabel NodeLabel(string tool, JsonElement arguments, string reference)
    {
        var key = Args.String(arguments, "key") ?? "Enter";
        var value = Args.String(arguments, "value") ?? "";
        var secret = Args.String(arguments, "secret") ?? "";
        return tool switch
        {
            "tap" => new("Tapped " + reference + ".", "tap " + reference),
            "double_tap" => new("Double-tapped " + reference + ".", "double_tap " + reference),
            "long_press" => new("Long-pressed " + reference + ".", "long_press " + reference),
            "right_click" => new("Right-clicked " + reference + ".", "right_click " + reference),
            "hover" => new("Hovered over " + reference + ".", "hover " + reference),
            "fill" => new("Typed into " + reference + ".", "fill " + reference),
            "fill_secret" => new(
                "Filled secret \"" + secret + "\" into " + reference + "; its value is masked in every observation.",
                "fill_secret \"" + secret + "\" into " + reference,
                false),
            "press" => new("Pressed " + key + " on " + reference + ".", "press " + key + " on " + reference, !MovesFocusOnly(key)),
            "select" => new("Selected \"" + value + "\" in " + reference + ".", "select \"" + value + "\" in " + reference),
            "check" => new("Checked " + reference + ".", "check " + reference),
            "uncheck" => new("Unchecked " + reference + ".", "check " + reference),
            "clear" => new("Cleared " + reference + ".", "clear " + reference),
            _ => new(tool + " " + reference, tool + " " + reference),
        };
    }

    // The control a call names before it is found, where upstream's `#id` stands: the ref when the call has one.
    private static string ReferenceOf(JsonElement arguments) =>
        Args.String(arguments, "ref") is { } reference
            ? "#" + reference
            : Args.String(arguments, "name") ?? Args.String(arguments, "role") ?? Args.String(arguments, "testId") ?? "target";

    // A mutating tool takes an action slot and is recorded as a gap: the grammar cannot reproduce it, so a replay ends there
    // and hands the step to the model. The budgets record the gap before the tool runs, since a failed tool may have changed the app.
    private async Task<ToolOutcome> RunProjectToolAsync(ModelToolCall call, AgentTool tool, StepAccounting budget, CancellationToken token, StepExecutorContext? context)
    {
        if (call.Arguments.ValueKind is not (JsonValueKind.Object or JsonValueKind.Undefined or JsonValueKind.Null))
        {
            return ToolOutcome.Fail(call.Name + " takes an object of arguments.");
        }

        if (tool.Mutates && !budget.TryPrepay())
        {
            return ToolOutcome.Fail(budget.Message + ". Take no more actions. Call done with a verdict.");
        }

        string output;
        try
        {
            var input = call.Arguments.ValueKind == JsonValueKind.Object ? (JsonObject)JsonNode.Parse(call.Arguments.GetRawText())! : new JsonObject();
            var toolContext = context is null ? AgentToolContext.Unavailable : new AgentToolContext(context, tool.Mutates);
            output = await budget.RunToolAsync(
                call.Name,
                tool.Mutates,
                () => Phases.InstrumentPhaseAsync(
                    new PhaseHost(_scope.Steps, _scope.Debug),
                    new PhaseSpec(StepEventKind.Engine, Phases.Action, ActionNames.ProjectToolEventPrefix + call.Name),
                    () => tool.RunAsync(input, toolContext, token))).ConfigureAwait(false);
        }
        catch (E2EException ex) when (!StepExecutors.IsRuntimeHardStop(ex))
        {
            return ToolOutcome.Fail(ex);
        }
        catch (Exception ex) when (!token.IsCancellationRequested && ex is not E2EException)
        {
            return ToolOutcome.Fail(call.Name + " failed: " + ex.Message);
        }
        finally
        {
            budget.ClearPrepaid();
        }

        output = output.Length > OverflowShrink.TextClipChars ? output[..OverflowShrink.TextClipChars] + "\n[output cut]" : output;
        return tool.Mutates ? await DescribeOutcomeAsync(output, token).ConfigureAwait(false) : ToolOutcome.Ok(output);
    }

    private async Task<ToolOutcome> ScrollAsync(JsonElement arguments, IExecutorActions actions, CancellationToken token)
    {
        if (!TryDirection(Args.String(arguments, "direction"), out var direction))
        {
            return ToolOutcome.Fail("scroll direction must be up, down, left, or right.");
        }

        var times = Args.Int(arguments, "times") ?? 1;
        if (times is < 1 or > AgentTools.MaxScrollTimes)
        {
            return ToolOutcome.Fail("scroll times must be 1 to " + AgentTools.MaxScrollTimes.ToString(CultureInfo.InvariantCulture) + ".");
        }

        // Upstream's `scrolled`: the done lead names the way and the screens, the attempt the way and the node.
        var way = DirectionName(direction);
        var label = new ActionLabel(
            times == 1 ? "Scrolled " + way + "." : "Scrolled " + way + " " + times.ToString(CultureInfo.InvariantCulture) + " screens.",
            "scroll " + way + (HasTarget(arguments) ? " " + ReferenceOf(arguments) : ""),
            false);
        return await ActingAsync(
            label,
            async () =>
            {
                var target = HasTarget(arguments) ? new ExecutorTarget(await ResolveAsync(arguments, token).ConfigureAwait(false)) : null;
                await actions.ScrollAsync(target, direction, times, token).ConfigureAwait(false);
            },
            token).ConfigureAwait(false);
    }

    // Upstream's drag tool: the drop target is named like the node dragged, and both are found before anything moves. `Dragged #a to #b.` on
    // success, `drag #a to #b failed: <code>: <message>` otherwise, each followed by the screen.
    private async Task<ToolOutcome> DragAsync(JsonElement arguments, IExecutorActions actions, CancellationToken token)
    {
        var drop = arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty("to", out var named) && named.ValueKind == JsonValueKind.Object ? named : default;
        var label = new ActionLabel("", "drag " + ReferenceOf(arguments) + " to " + ReferenceOf(drop));
        SemanticNode source;
        SemanticNode destination;
        try
        {
            if (drop.ValueKind != JsonValueKind.Object || !HasTarget(drop))
            {
                throw new TestException("INVALID_ARGUMENT", "drag requires a to target: the node to drop on");
            }

            // Both ends come from one look, so a screen that changes between two looks cannot join a node of one with a node of the other.
            var observation = await _feed.ObserveAsync(SettleMode.Raw, token).ConfigureAwait(false);
            source = ResolveIn(arguments, observation);
            destination = ResolveIn(drop, observation);
        }
        catch (TestException ex)
        {
            return await FailedAsync(label, ex, token).ConfigureAwait(false);
        }

        label = new ActionLabel("Dragged #" + source.Ref + " to #" + destination.Ref + ".", "drag #" + source.Ref + " to #" + destination.Ref);
        return await ActingAsync(label, () => actions.DragAsync(new ExecutorTarget(source), new ExecutorTarget(destination), token), token).ConfigureAwait(false);
    }

    private async Task<ToolOutcome> ScrollToAsync(JsonElement arguments, IExecutorActions actions, CancellationToken token)
    {
        var text = Args.String(arguments, "text");
        if (text is not null)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > AgentTools.MaxScrollToText)
            {
                return ToolOutcome.Fail("scroll_to text must be the text to reach, up to " + AgentTools.MaxScrollToText.ToString(CultureInfo.InvariantCulture) + " characters.");
            }

            var way = Args.String(arguments, "direction");
            var direction = ScrollDirection.Down;
            if (way is not null && !TryDirection(way, out direction))
            {
                return ToolOutcome.Fail("scroll_to direction must be up, down, left, or right.");
            }

            var quoted = UploadPaths.Quote(text);
            return await ActingAsync(
                new ActionLabel("Scrolled " + DirectionName(direction) + " until " + quoted + " was in view.", "scroll_to " + quoted, false),
                async () =>
                {
                    var list = HasTarget(arguments) ? new ExecutorTarget(await ResolveAsync(arguments, token).ConfigureAwait(false)) : null;
                    await actions.ScrollUntilAsync(text, direction, list, token).ConfigureAwait(false);
                },
                token).ConfigureAwait(false);
        }

        if (!HasTarget(arguments))
        {
            return ToolOutcome.Fail("scroll_to takes a target, a text to reach, or both.");
        }

        var reference = ReferenceOf(arguments);
        try
        {
            var node = await ResolveAsync(arguments, token).ConfigureAwait(false);
            reference = "#" + node.Ref;
            await actions.ScrollToAsync(new ExecutorTarget(node), token).ConfigureAwait(false);
        }
        catch (E2EException ex) when (!StepExecutors.IsRuntimeHardStop(ex))
        {
            return await FailedAsync(new ActionLabel("", "scroll_to " + reference), ex, token).ConfigureAwait(false);
        }

        return await DescribeOutcomeAsync("Scrolled into view " + reference + ".", token, false).ConfigureAwait(false);
    }

    // Upstream's upload tool: the paths are authorized before the node is looked for, so a refused path is the policy's decision whatever the
    // target, and costs no slot. The wording is upstream's too: `Uploaded "x" to #id.` on success, `upload "x" to #id failed: <code>:
    // <message>` on a refusal, each followed by the screen. The resolved node's ref stands where upstream's `#id` does.
    private async Task<ToolOutcome> UploadAsync(JsonElement arguments, IExecutorActions actions, StepAccounting budget, CancellationToken token)
    {
        var reference = ReferenceOf(arguments);
        var label = new ActionLabel("", "upload to " + reference);
        List<string> files;
        try
        {
            files = FilesOf(arguments);
        }
        catch (E2EException ex) when (!StepExecutors.IsRuntimeHardStop(ex))
        {
            budget.RefundPrepaid();
            return await FailedAsync(label, ex, token).ConfigureAwait(false);
        }

        var listed = string.Join(", ", files.Select(UploadPaths.Quote));
        label = new ActionLabel("", "upload " + listed + " to " + reference);
        SemanticNode node;
        try
        {
            node = await ResolveAsync(arguments, token).ConfigureAwait(false);
        }
        catch (TestException missing)
        {
            // The paths are authorized before the node is looked for, so the policy records its decision whatever the target; a refused
            // path wins and costs no slot, and a target upstream cannot find has spent its slot.
            try
            {
                ActionDispatcher.AuthorizeUpload(_scope, files);
            }
            catch (E2EException refused) when (!StepExecutors.IsRuntimeHardStop(refused))
            {
                budget.RefundPrepaid();
                return await FailedAsync(label, refused, token).ConfigureAwait(false);
            }

            return await FailedAsync(label, missing, token).ConfigureAwait(false);
        }

        reference = "#" + node.Ref;
        label = new ActionLabel("Uploaded " + listed + " to " + reference + ".", "upload " + listed + " to " + reference);
        try
        {
            await actions.UploadAsync(new ExecutorTarget(node), files, token).ConfigureAwait(false);
        }
        catch (E2EException ex) when (!StepExecutors.IsRuntimeHardStop(ex))
        {
            // The path policy turns the call away before the dispatcher takes its slot, as upstream's does; a failure after it keeps the slot.
            budget.RefundPrepaid();
            return await FailedAsync(label, ex, token).ConfigureAwait(false);
        }

        return Outcome(await DescribeAsync(label.Done, token).ConfigureAwait(false));
    }

    // The files an upload call names. Anything but a list of strings is the model's error, which the policy words as it does an empty list.
    private static List<string> FilesOf(JsonElement arguments)
    {
        if (arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty("files", out var files)
            && files.ValueKind == JsonValueKind.Array
            && files.EnumerateArray().All(item => item.ValueKind == JsonValueKind.String))
        {
            return [.. files.EnumerateArray().Select(item => item.GetString()!)];
        }

        throw new TestException("INVALID_ARGUMENT", "upload requires one or more non-empty project-relative file paths");
    }

    // The keys a tap call holds: absent for none. A value that is no list of key modifier names is the model's error.
    private static ClickOptions? ClickOptionsOf(JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object || !arguments.TryGetProperty("modifiers", out var held) || held.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var invalid = new TestException("INVALID_ARGUMENT", "modifiers must be an array of " + string.Join(", ", KeyModifiers.Names));
        if (held.ValueKind != JsonValueKind.Array)
        {
            throw invalid;
        }

        var modifiers = new List<KeyModifier>();
        foreach (var item in held.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || !KeyModifiers.TryParse(item.GetString(), out var modifier))
            {
                throw invalid;
            }

            modifiers.Add(modifier);
        }

        return new ClickOptions { Modifiers = modifiers };
    }

    private async Task<SemanticNode> ResolveAsync(JsonElement arguments, CancellationToken token) =>
        ResolveIn(arguments, await _feed.ObserveAsync(SettleMode.Raw, token).ConfigureAwait(false));

    private SemanticNode ResolveIn(JsonElement arguments, Observation observation)
    {
        var matches = Find(observation, Args.String(arguments, "role"), Args.String(arguments, "name"), Args.String(arguments, "testId"), Args.String(arguments, "ref"), _scope.Redactor);
        if (matches.Count == 0 && Args.String(arguments, "ref") is { } reference && _feed.LastSeen(reference) is { } seen && !LocatorResolver.Walk(observation.Roots).Any(node => node.Ref == reference))
        {
            // Several actions of one turn name the screen the turn saw. A ref the newest screen no longer carries,
            // because the element remounted, is the one node that reads the same now.
            matches = ObservationFeed.FindLike(observation, seen.Node, _scope.Redactor);
        }

        if (matches.Count == 0)
        {
            throw new TestException("LOCATOR_NOT_FOUND", "No control matched role=" + Args.String(arguments, "role") + " name=" + Args.String(arguments, "name") + ".");
        }

        if (matches.Count > 1)
        {
            throw new TestException("LOCATOR_AMBIGUOUS", "Matched " + matches.Count.ToString(CultureInfo.InvariantCulture) + " controls. Name the control more specifically.");
        }

        _feed.MarkActing();
        return matches[0];
    }

    // An action that ran but whose look failed is a failed tool for the model, as in the targeted paths, not the end of the act.
    private async Task<ToolOutcome> DescribeOutcomeAsync(string action, CancellationToken token, bool expectChange = false)
    {
        try
        {
            return Outcome(await DescribeAsync(action, token, expectChange).ConfigureAwait(false));
        }
        catch (E2EException ex) when (!token.IsCancellationRequested && !StepExecutors.IsRuntimeHardStop(ex))
        {
            return ToolOutcome.Fail(ex);
        }
    }

    // The screen after an action, as the model reads it: a held-still look. An action the dispatcher ran armed its own change wait already,
    // and so did a project tool's gap.
    // With a presenter (a session over MCP) the screen is the changes since the one the coding agent holds, the action's lead first;
    // `expectChange` says an unchanged screen after this action is worth pointing out.
    // Once the step shows pixels (a screenshot went to the model), every look carries a fresh one (upstream's `showingPixels`).
    // `pixels` asks for one beside a look that would not carry it (the screenshot tool).
    private async Task<ScreenText> DescribeAsync(string action, CancellationToken token, bool expectChange = false, bool pixels = false)
    {
        if (_screen is not null)
        {
            var observation = await _feed.ObserveAsync(SettleMode.HeldStill, token).ConfigureAwait(false);
            return new ScreenText(_screen.Update(_feed.ListedView(observation, _scope), new ScreenUpdateOptions { Lead = action, ExpectChange = expectChange }), null);
        }

        if (!pixels && _pixels?.Showing != true)
        {
            // A screen the engine cannot read as a tree is shown by its pixels alone, whether or not the step asked for one.
            var observation = await _feed.ObserveAsync(SettleMode.HeldStill, token).ConfigureAwait(false);
            if (!observation.TreeUnavailable)
            {
                return new ScreenText(action + "\n" + _feed.Text(observation), null);
            }
        }

        var view = await _feed.ObserveAsync(new ExecutorObserveOptions { Pixels = true }, token).ConfigureAwait(false);
        if (view.Pixels is { } shot)
        {
            _pixels?.Shown(shot, _feed.Latest?.Viewport);

            return new ScreenText(action + "\n" + view.Text + "\n\n" + PixelMode.Note(shot), shot);
        }

        // The pixels the step asked for did not come: say why once and keep working from the tree.
        return new ScreenText(action + "\n" + view.Text + (view.PixelsWithheld is { } withheld ? "\n\nNo screenshot: " + PixelMode.WithheldAdvice(withheld) : ""), null);
    }

    // A session's `observe`: the whole screen, every time. The presenter takes it as the baseline for the changes that follow.
    private async Task<string> WholeScreenAsync(CancellationToken token) =>
        _screen!.Initial(_feed.ListedView(await _feed.ObserveAsync(SettleMode.HeldStill, token).ConfigureAwait(false), _scope));

    // Keys whose whole effect is where the focus or the caret sits, which the tree does not record: an unchanged screen after one
    // of these is the normal outcome, not a control that did nothing (upstream's `movesFocusOnly`).
    private static bool MovesFocusOnly(string key) => FocusOnlyKey().IsMatch(key.Trim());

    [GeneratedRegex(@"^(?:(?:Shift|Control|Alt|Meta)\+)*(?:Tab|Arrow(?:Left|Right|Up|Down)|Home|End|PageUp|PageDown)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FocusOnlyKey();

    // The screen a look rendered, and the screenshot that goes with it, when the look took one.
    private readonly record struct ScreenText(string Text, ExecutorPixels? Pixels);

    // Upstream's ActionLabel: what an action's result opens with, `Done` when it ran, and `Attempt`, the tool and what it was aimed at,
    // for the lead of a failure, so a failed action never reads as done.
    // `ExpectChange` is whether an unchanged screen after the action is worth pointing out (a session's `expectChange`).
    private readonly record struct ActionLabel(string Done, string Attempt, bool ExpectChange = true);
}
