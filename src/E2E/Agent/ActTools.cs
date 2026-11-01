// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Cache;
using E2E.Engine;
using E2E.Internal;
using static E2E.Agent;

namespace E2E;

/// <summary>
/// The act tools as the model calls them: each call is checked, aimed at a control, and run through the step's
/// <see cref="IExecutorActions"/>, and the answer is the screen after it. A coding agent that drives a live session over
/// MCP runs the same tools through <see cref="Agent.RunToolAsync"/>. No upstream equivalent: upstream's tool loop holds
/// these as closures over its <c>ToolLoopHelpers</c>.
/// </summary>
internal sealed class ActTools(AttemptScope scope, ObservationFeed feed)
{
    private static readonly HashSet<string> ActionTools =
        new(["navigate", "tap", "double_tap", "fill", "fill_secret", "press", "select", "check", "uncheck", "clear", "upload", "back", "scroll", "scroll_to"], StringComparer.Ordinal);

    private readonly AttemptScope _scope = scope;
    private readonly ObservationFeed _feed = feed;

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
            return ToolOutcome.Ok(await DescribeAsync("Observed.", token).ConfigureAwait(false));
        }

        if (projectTools is not null && projectTools.TryGetValue(call.Name, out var projectTool))
        {
            return await RunProjectToolAsync(call, projectTool, budget, token, context).ConfigureAwait(false);
        }

        if (!ActionTools.Contains(call.Name))
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
            case "navigate":
                var url = Args.String(arguments, "url") ?? "/";
                return await ActingAsync(new ActionLabel("Navigated to " + url + ".", "navigate " + url), () => actions.NavigateAsync(url, token), token).ConfigureAwait(false);
            case "press" when !HasTarget(arguments):
                var focusedKey = Args.String(arguments, "key") ?? "Enter";
                return await ActingAsync(NodeLabel(call.Name, arguments, "the focused field"), () => actions.PressAsync(null, focusedKey, token), token).ConfigureAwait(false);
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
        return await ActingAsync(
            NodeLabel(call.Name, arguments, "#" + node.Ref),
            () => call.Name switch
            {
                "tap" => actions.TapAsync(target, ClickOptionsOf(arguments), token),
                "double_tap" => actions.DoubleTapAsync(target, ClickOptionsOf(arguments), token),
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

    // Upstream's `acting`: the action runs, then the screen follows under the label's done lead, or under the failure lead when it threw.
    private async Task<ToolOutcome> ActingAsync(ActionLabel label, Func<Task> action, CancellationToken token)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (E2EException ex) when (!StepExecutors.IsRuntimeHardStop(ex))
        {
            return await FailedAsync(label, ex, token).ConfigureAwait(false);
        }

        return await DescribeOutcomeAsync(label.Done, token).ConfigureAwait(false);
    }

    // Upstream's `failureLead` on a screen: a failed action still returns the screen, so the model can act on a stale-id or not-found failure at once.
    private async Task<ToolOutcome> FailedAsync(ActionLabel label, E2EException error, CancellationToken token) =>
        new() { Content = await DescribeAsync(FailureLead(label, error), token).ConfigureAwait(false), Succeeded = false, Code = error.Code, Error = error.Message };

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
            "fill" => new("Typed into " + reference + ".", "fill " + reference),
            "fill_secret" => new(
                "Filled secret \"" + secret + "\" into " + reference + "; its value is masked in every observation.",
                "fill_secret \"" + secret + "\" into " + reference),
            "press" => new("Pressed " + key + " on " + reference + ".", "press " + key + " on " + reference),
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
            "scroll " + way + (HasTarget(arguments) ? " " + ReferenceOf(arguments) : ""));
        return await ActingAsync(
            label,
            async () =>
            {
                var target = HasTarget(arguments) ? new ExecutorTarget(await ResolveAsync(arguments, token).ConfigureAwait(false)) : null;
                await actions.ScrollAsync(target, direction, times, token).ConfigureAwait(false);
            },
            token).ConfigureAwait(false);
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
                new ActionLabel("Scrolled " + DirectionName(direction) + " until " + quoted + " was in view.", "scroll_to " + quoted),
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

        return await DescribeOutcomeAsync("Scrolled into view " + reference + ".", token).ConfigureAwait(false);
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

        return ToolOutcome.Ok(await DescribeAsync(label.Done, token).ConfigureAwait(false));
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

    private async Task<SemanticNode> ResolveAsync(JsonElement arguments, CancellationToken token)
    {
        var observation = await _feed.ObserveAsync(SettleMode.Raw, token).ConfigureAwait(false);
        var matches = Find(observation, Args.String(arguments, "role"), Args.String(arguments, "name"), Args.String(arguments, "testId"), Args.String(arguments, "ref"), _scope.Redactor);
        if (matches.Count == 0 && Args.String(arguments, "ref") is { } reference && _feed.LastSeen(reference) is { } seen && !LocatorResolver.Walk(observation.Roots).Any(node => node.Ref == reference))
        {
            // Several actions of one turn name the screen the turn saw. A ref the newest screen no longer carries,
            // because the element remounted, is the one node that reads the same now.
            matches = ObservationFeed.FindLike(observation, seen.Node, _scope.Redactor);
        }

        if (matches.Count == 0)
        {
            throw new TestException("NOT_FOUND", "No control matched role=" + Args.String(arguments, "role") + " name=" + Args.String(arguments, "name") + ".");
        }

        if (matches.Count > 1)
        {
            throw new TestException("STRICT_MODE", "Matched " + matches.Count.ToString(CultureInfo.InvariantCulture) + " controls. Name the control more specifically.");
        }

        _feed.MarkActing();
        return matches[0];
    }

    // An action that ran but whose look failed is a failed tool for the model, as in the targeted paths, not the end of the act.
    private async Task<ToolOutcome> DescribeOutcomeAsync(string action, CancellationToken token)
    {
        try
        {
            return ToolOutcome.Ok(await DescribeAsync(action, token).ConfigureAwait(false));
        }
        catch (E2EException ex) when (!token.IsCancellationRequested && !StepExecutors.IsRuntimeHardStop(ex))
        {
            return ToolOutcome.Fail(ex);
        }
    }

    // The screen after an action, as the model reads it: a held-still look. An action the dispatcher ran armed its own change wait already,
    // and so did a project tool's gap.
    private async Task<string> DescribeAsync(string action, CancellationToken token)
    {
        var observation = await _feed.ObserveAsync(SettleMode.HeldStill, token).ConfigureAwait(false);
        return action + "\n" + _feed.Text(observation);
    }

    // Upstream's ActionLabel: what an action's result opens with, `Done` when it ran, and `Attempt`, the tool and what it was aimed at,
    // for the lead of a failure, so a failed action never reads as done.
    private readonly record struct ActionLabel(string Done, string Attempt);
}
