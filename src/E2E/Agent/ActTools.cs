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
        new(["navigate", "tap", "double_tap", "fill", "fill_secret", "press", "select", "check", "uncheck", "clear", "back", "scroll", "scroll_to"], StringComparer.Ordinal);

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
            return ToolOutcome.Ok(await DescribeAsync("observed", token).ConfigureAwait(false));
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
            return await RunActionAsync(call, actions, token).ConfigureAwait(false);
        }
        finally
        {
            budget.ClearPrepaid();
        }
    }

    private async Task<ToolOutcome> RunActionAsync(ModelToolCall call, IExecutorActions actions, CancellationToken token)
    {
        switch (call.Name)
        {
            case "back":
                return await BackAsync(actions, token).ConfigureAwait(false);
            case "scroll":
                return await ScrollAsync(call.Arguments, actions, token).ConfigureAwait(false);
            case "scroll_to":
                return await ScrollToAsync(call.Arguments, actions, token).ConfigureAwait(false);
        }

        if (string.Equals(call.Name, "navigate", StringComparison.Ordinal))
        {
            var url = Args.String(call.Arguments, "url") ?? "/";
            try
            {
                await actions.NavigateAsync(url, token).ConfigureAwait(false);
            }
            catch (E2EException ex) when (!StepExecutors.IsRuntimeHardStop(ex))
            {
                return ToolOutcome.Fail(ex);
            }

            return await DescribeOutcomeAsync("navigated to " + Routes.PathOf(url), token).ConfigureAwait(false);
        }

        if (string.Equals(call.Name, "press", StringComparison.Ordinal) && !HasTarget(call.Arguments))
        {
            var key = Args.String(call.Arguments, "key") ?? "Enter";
            try
            {
                await actions.PressAsync(null, key, token).ConfigureAwait(false);
            }
            catch (E2EException ex) when (!StepExecutors.IsRuntimeHardStop(ex))
            {
                return ToolOutcome.Fail(ex);
            }

            return await DescribeOutcomeAsync("pressed " + key, token).ConfigureAwait(false);
        }

        SemanticNode node;
        try
        {
            node = await ResolveAsync(call.Arguments, token).ConfigureAwait(false);
        }
        catch (TestException ex)
        {
            return ToolOutcome.Fail(ex);
        }

        var target = new ExecutorTarget(node);
        try
        {
            switch (call.Name)
            {
                case "tap":
                    await actions.TapAsync(target, token).ConfigureAwait(false);
                    return ToolOutcome.Ok(await DescribeAsync("tapped " + Label(node), token).ConfigureAwait(false));
                case "double_tap":
                    await actions.DoubleTapAsync(target, token).ConfigureAwait(false);
                    return ToolOutcome.Ok(await DescribeAsync("double-tapped " + Label(node), token).ConfigureAwait(false));
                case "fill":
                    var value = Args.String(call.Arguments, "value") ?? "";
                    await actions.TypeAsync(target, value, token).ConfigureAwait(false);
                    return ToolOutcome.Ok(await DescribeAsync("filled " + Label(node), token).ConfigureAwait(false));
                case "fill_secret":
                    return await FillSecretAsync(target, call.Arguments, actions, token).ConfigureAwait(false);
                case "press":
                    var key = Args.String(call.Arguments, "key") ?? "Enter";
                    await actions.PressAsync(target, key, token).ConfigureAwait(false);
                    return ToolOutcome.Ok(await DescribeAsync("pressed " + key + " on " + Label(node), token).ConfigureAwait(false));
                case "select":
                    var selected = Args.String(call.Arguments, "value") ?? "";
                    await actions.SelectAsync(target, selected, token).ConfigureAwait(false);
                    return ToolOutcome.Ok(await DescribeAsync("selected " + selected, token).ConfigureAwait(false));
                case "check":
                    await actions.CheckAsync(target, true, token).ConfigureAwait(false);
                    return ToolOutcome.Ok(await DescribeAsync("checked " + Label(node), token).ConfigureAwait(false));
                case "uncheck":
                    await actions.CheckAsync(target, false, token).ConfigureAwait(false);
                    return ToolOutcome.Ok(await DescribeAsync("unchecked " + Label(node), token).ConfigureAwait(false));
                case "clear":
                    await actions.ClearAsync(target, token).ConfigureAwait(false);
                    return ToolOutcome.Ok(await DescribeAsync("cleared " + Label(node), token).ConfigureAwait(false));
                default:
                    return ToolOutcome.Fail("Unknown tool " + call.Name + ".");
            }
        }
        catch (E2EException ex) when (!StepExecutors.IsRuntimeHardStop(ex))
        {
            return ToolOutcome.Fail(ex);
        }
    }

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
            output = await budget.RunToolAsync(call.Name, tool.Mutates, () => tool.RunAsync(input, toolContext, token)).ConfigureAwait(false);
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

    private async Task<ToolOutcome> BackAsync(IExecutorActions actions, CancellationToken token)
    {
        try
        {
            await actions.BackAsync(token).ConfigureAwait(false);
        }
        catch (E2EException ex) when (!StepExecutors.IsRuntimeHardStop(ex))
        {
            return ToolOutcome.Fail(ex);
        }

        return await DescribeOutcomeAsync("navigated back", token).ConfigureAwait(false);
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

        try
        {
            var target = HasTarget(arguments) ? new ExecutorTarget(await ResolveAsync(arguments, token).ConfigureAwait(false)) : null;
            await actions.ScrollAsync(target, direction, times, token).ConfigureAwait(false);
            var list = target?.Node;
            var done = "scrolled" + (list is null ? "" : " " + Label(list)) + " " + DirectionName(direction);
            if (times > 1)
            {
                done += " " + times.ToString(CultureInfo.InvariantCulture) + " screens";
            }

            return ToolOutcome.Ok(await DescribeAsync(done, token).ConfigureAwait(false));
        }
        catch (E2EException ex) when (!StepExecutors.IsRuntimeHardStop(ex))
        {
            return ToolOutcome.Fail(ex);
        }
    }

    private async Task<ToolOutcome> ScrollToAsync(JsonElement arguments, IExecutorActions actions, CancellationToken token)
    {
        var text = Args.String(arguments, "text");
        try
        {
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

                var list = HasTarget(arguments) ? new ExecutorTarget(await ResolveAsync(arguments, token).ConfigureAwait(false)) : null;
                await actions.ScrollUntilAsync(text, direction, list, token).ConfigureAwait(false);
                return ToolOutcome.Ok(await DescribeAsync("scrolled " + DirectionName(direction) + " until \"" + text + "\" was in view", token).ConfigureAwait(false));
            }

            if (!HasTarget(arguments))
            {
                return ToolOutcome.Fail("scroll_to takes a target, a text to reach, or both.");
            }

            var node = await ResolveAsync(arguments, token).ConfigureAwait(false);
            await actions.ScrollToAsync(new ExecutorTarget(node), token).ConfigureAwait(false);
            return ToolOutcome.Ok(await DescribeAsync("scrolled " + Label(node) + " into view", token).ConfigureAwait(false));
        }
        catch (E2EException ex) when (!StepExecutors.IsRuntimeHardStop(ex))
        {
            return ToolOutcome.Fail(ex);
        }
    }

    private async Task<ToolOutcome> FillSecretAsync(ExecutorTarget target, JsonElement arguments, IExecutorActions actions, CancellationToken token)
    {
        var name = Args.String(arguments, "secret") ?? "";
        await actions.TypeSecretAsync(target, name, token).ConfigureAwait(false);
        return ToolOutcome.Ok(await DescribeAsync("filled secret <secret:" + name + "> into " + Label(target.Node!), token).ConfigureAwait(false));
    }

    private async Task<SemanticNode> ResolveAsync(JsonElement arguments, CancellationToken token)
    {
        var observation = await _feed.ObserveAsync(SettleMode.Raw, token).ConfigureAwait(false);
        var matches = Find(observation, Args.String(arguments, "role"), Args.String(arguments, "name"), Args.String(arguments, "testId"), Args.String(arguments, "ref"), _scope.Redactor);
        if (matches.Count == 0 && Args.String(arguments, "ref") is { } reference && _feed.LastSeen(reference) is { } seen && !LocatorResolver.Walk(observation.Roots).Any(node => node.Ref == reference))
        {
            // Several actions of one turn name the screen the turn saw. A ref the newest screen no longer carries,
            // because the element remounted, is the one node that reads the same now.
            matches = ObservationFeed.FindLike(observation, seen, _scope.Redactor);
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
        return action + "\n" + SnapshotText.Render(observation, _scope.Redactor, _scope.AppOrigin);
    }

    // A name the engine cut at its limit can end with the leading part of a secret, so it is redacted as a cut field.
    private string Label(SemanticNode node)
    {
        var role = node.Role ?? "node";
        return node.Name is null ? role : role + " \"" + AgentObservation.RedactText(node.Name, _scope.Redactor, ObservationLimits.Name) + "\"";
    }
}
