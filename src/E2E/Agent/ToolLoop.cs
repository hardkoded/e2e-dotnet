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

/// <summary>Upstream's <c>createToolLoopExecutor</c> (<c>agent/tool-loop.ts</c>): the loop of model turns and tool calls an act step runs.</summary>
internal static class ToolLoop
{
    public static IStepExecutor CreateToolLoopExecutor(ToolLoopExecutorOptions options) => new ToolLoopExecutor(options);

    // The step's verdict on a refusal it could not recover from: the same error, saying what the retry did.
    internal static AgentException Overflowed(AgentException refusal, string note) =>
        new(
            refusal.Code,
            ModelHttp.OverflowMessage + note + (refusal.Message.StartsWith(ModelHttp.OverflowMessage, StringComparison.Ordinal) ? refusal.Message[ModelHttp.OverflowMessage.Length..] : ": " + refusal.Message),
            blocked: true,
            refusal);
}

/// <summary>
/// Upstream's <c>ToolLoopExecutorOptions</c>. <see cref="System"/> and <see cref="Opening"/> are what upstream's built-in
/// agent hands its loop as <c>prepareTurn</c>. The rest is what the port's loop reads from the step's runtime: the
/// agent that calls the model, the attempt, the looks at the screen, and the tools the project added.
/// </summary>
internal sealed class ToolLoopExecutorOptions
{
    public required string Name { get; init; }

    public required string Version { get; init; }

    public required string System { get; init; }

    /// <summary>The first message of the step: its goal, the screen, and what a replay already did.</summary>
    public required Func<StepExecutorContext, string> Opening { get; init; }

    public required Agent Host { get; init; }

    public required AttemptScope Scope { get; init; }

    public required ObservationFeed Feed { get; init; }

    public required ResolvedAgent Agent { get; init; }

    public required IReadOnlyDictionary<string, AgentTool> ProjectTools { get; init; }

    public required IReadOnlyList<ModelTool> OfferedProject { get; init; }

    /// <summary>The caller's token, apart from the step's linked one: a model call told them apart to say a timeout from a cancel.</summary>
    public required CancellationToken CancellationToken { get; init; }
}

internal sealed class ToolLoopExecutor(ToolLoopExecutorOptions options) : IStepExecutor
{
    public string Name => options.Name;

    public string Version => options.Version;

    public async Task<StepVerdict> RunStepAsync(StepExecutorContext context, CancellationToken token)
    {
        var agent = options.Agent;
        var budget = context.Budgets;
        var maxCalls = budget.MaxModelCalls;
        var tools = new ActTools(options.Scope, options.Feed);
        var messages = new List<ModelMessage>
        {
            new()
            {
                Role = "user",
                Content = options.Opening(context),
            },
        };

        var failures = 0;
        var stopped = false;
        string? noticed = null;
        var shrunkOnce = false;
        for (var call = 0; call < maxCalls; call++)
        {
            if (failures >= 5)
            {
                messages.Add(new ModelMessage { Role = "user", Content = "Stop acting. Call done with a verdict." });
            }

            // A repeat warns first. A stop leaves the model only done, so it still writes its own verdict.
            var (stop, repeat) = stopped ? (true, null) : LoopGuards.CheckLoopGuards(LoopGuards.ExtractGuardCalls(messages));
            if (repeat is not null && !string.Equals(repeat, noticed, StringComparison.Ordinal))
            {
                noticed = repeat;
                stopped = stop;
                messages.Add(new ModelMessage
                {
                    Role = "user",
                    Content = stop
                        ? "Loop guard: " + repeat + ". Repeating it further will not make progress. Call done now with your best verdict: failed if the application misbehaved, blocked (with a code) if something outside the application stopped you."
                        : "You appear to be going in circles: " + repeat + ". Change approach, or call done with your best verdict.",
                });
            }

            var offeredTools = AgentTools.ActFor(options.Scope.EngineCapabilities, options.OfferedProject);
            var offered = stopped ? offeredTools.Where(tool => string.Equals(tool.Name, "done", StringComparison.Ordinal)).ToList() : offeredTools;
            ModelResponse response;
            try
            {
                response = await options.Host.CallModelAsync(agent.Model, options.System, messages, offered, agent, token, options.CancellationToken, markLatest: true).ConfigureAwait(false);
            }
            catch (AgentException overflow) when (string.Equals(overflow.Code, "CONTEXT_OVERFLOW", StringComparison.Ordinal))
            {
                // One recovery: the history shrinks and the request goes out again. The refused request was no turn,
                // so it spends no call of the budget, and a second refusal is the step's verdict.
                if (shrunkOnce)
                {
                    throw ToolLoop.Overflowed(overflow, " again after the history was shrunk once");
                }

                var shrunk = OverflowShrink.ShrinkForOverflow(messages);
                if (OverflowShrink.TextChars(shrunk) >= OverflowShrink.TextChars(messages))
                {
                    // Nothing to elide and nothing to clip: sending the same request again would only spend another call on it.
                    throw ToolLoop.Overflowed(overflow, " and the step history had nothing left to shrink");
                }

                shrunkOnce = true;
                messages.Clear();
                messages.AddRange(shrunk);
                try
                {
                    response = await options.Host.CallModelAsync(agent.Model, options.System, messages, offered, agent, token, options.CancellationToken, markLatest: true).ConfigureAwait(false);
                }
                catch (AgentException again) when (string.Equals(again.Code, "CONTEXT_OVERFLOW", StringComparison.Ordinal))
                {
                    throw ToolLoop.Overflowed(again, " again after the history was shrunk once");
                }
            }

            if (response.ToolCalls.Count == 0)
            {
                messages.Add(new ModelMessage { Role = "assistant", Content = response.Content, ReasoningItems = response.ReasoningItems });
                messages.Add(new ModelMessage { Role = "user", Content = "Call a tool. End the step with done." });
                failures++;
                continue;
            }

            messages.Add(new ModelMessage { Role = "assistant", Content = response.Content, ToolCalls = response.ToolCalls, ReasoningItems = response.ReasoningItems });
            foreach (var toolCall in response.ToolCalls)
            {
                var outcome = stopped && !string.Equals(toolCall.Name, "done", StringComparison.Ordinal)
                    ? ToolOutcome.Fail("The loop guard stopped the step. Only done is available.")
                    : await tools.ExecuteAsync(toolCall, context.Actions, budget, options.ProjectTools, token).ConfigureAwait(false);
                messages.Add(new ModelMessage
                {
                    Role = "tool",
                    ToolCallId = toolCall.Id,
                    Name = toolCall.Name,
                    Content = options.Scope.Redactor.Redact(outcome.Content),
                });
                if (!outcome.Succeeded)
                {
                    failures++;
                }

                if (!outcome.Done)
                {
                    continue;
                }

                return new StepVerdict(
                    outcome.Status switch
                    {
                        "passed" => StepVerdictStatus.Passed,
                        "blocked" => StepVerdictStatus.Blocked,
                        _ => StepVerdictStatus.Failed,
                    },
                    outcome.Summary ?? "",
                    outcome.Code);
            }

            if (failures >= 3 && failures < 5)
            {
                messages.Add(new ModelMessage { Role = "user", Content = "The last actions failed. Change approach, then call done if you cannot." });
            }
        }

        if (budget.Exhausted)
        {
            throw budget.Stop(null);
        }

        // Asking for one more call than the budget allows is a hard stop, as upstream: blocked, not a verdict.
        throw new AgentException("STEP_BUDGET_EXHAUSTED", "agent.act exhausted its model-call budget of " + maxCalls.ToString(CultureInfo.InvariantCulture), blocked: true);
    }
}

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

    public async Task<ToolOutcome> ExecuteAsync(
        ModelToolCall call,
        IExecutorActions actions,
        StepAccounting budget,
        IReadOnlyDictionary<string, AgentTool>? projectTools,
        CancellationToken token)
    {
        if (string.Equals(call.Name, "done", StringComparison.Ordinal))
        {
            var status = Args.String(call.Arguments, "status");
            if (status is not ("passed" or "failed" or "blocked"))
            {
                return ToolOutcome.Fail("done status must be passed, failed, or blocked.");
            }

            // The model picks only from a closed set of codes. The runtime assigns the rest.
            var code = Args.String(call.Arguments, "code");
            if (string.Equals(status, "passed", StringComparison.Ordinal) || !AgentTools.ModelErrorCodes.Contains(code))
            {
                code = null;
            }

            if (string.Equals(status, "blocked", StringComparison.Ordinal) && !AgentTools.BlockableCodes.Contains(code))
            {
                return ToolOutcome.Fail(
                    "a blocked verdict requires a code naming what blocked you (one of " + string.Join(", ", AgentTools.BlockableCodes) + "). " +
                    "If the application itself misbehaved, use status failed instead.");
            }

            return ToolOutcome.Finish(status, Args.String(call.Arguments, "summary"), code);
        }

        // observe looks and records nothing, so it takes no action slot.
        if (string.Equals(call.Name, "observe", StringComparison.Ordinal))
        {
            return ToolOutcome.Ok(await DescribeAsync("observed", null, token).ConfigureAwait(false));
        }

        if (projectTools is not null && projectTools.TryGetValue(call.Name, out var projectTool))
        {
            return await RunProjectToolAsync(call, projectTool, budget, token).ConfigureAwait(false);
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

        // Every action that runs claims a budget slot before it runs. A failed action was still an attempt.
        if (!budget.TryReserve())
        {
            return ToolOutcome.Fail(budget.Message + ". Take no more actions. Call done with a verdict.");
        }

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
            catch (E2EException ex)
            {
                return ToolOutcome.Fail(ex);
            }

            return await DescribeOutcomeAsync("navigated to " + Routes.PathOf(url), null, token).ConfigureAwait(false);
        }

        if (string.Equals(call.Name, "press", StringComparison.Ordinal) && !HasTarget(call.Arguments))
        {
            var key = Args.String(call.Arguments, "key") ?? "Enter";
            try
            {
                await actions.PressAsync(null, key, token).ConfigureAwait(false);
            }
            catch (E2EException ex)
            {
                return ToolOutcome.Fail(ex);
            }

            return await DescribeOutcomeAsync("pressed " + key, null, token).ConfigureAwait(false);
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
                    return ToolOutcome.Ok(await DescribeAsync("tapped " + Label(node), null, token).ConfigureAwait(false));
                case "double_tap":
                    await actions.DoubleTapAsync(target, token).ConfigureAwait(false);
                    return ToolOutcome.Ok(await DescribeAsync("double-tapped " + Label(node), null, token).ConfigureAwait(false));
                case "fill":
                    var value = Args.String(call.Arguments, "value") ?? "";
                    await actions.TypeAsync(target, value, token).ConfigureAwait(false);
                    return ToolOutcome.Ok(await DescribeAsync("filled " + Label(node), null, token).ConfigureAwait(false));
                case "fill_secret":
                    return await FillSecretAsync(target, call.Arguments, actions, token).ConfigureAwait(false);
                case "press":
                    var key = Args.String(call.Arguments, "key") ?? "Enter";
                    await actions.PressAsync(target, key, token).ConfigureAwait(false);
                    return ToolOutcome.Ok(await DescribeAsync("pressed " + key + " on " + Label(node), null, token).ConfigureAwait(false));
                case "select":
                    var selected = Args.String(call.Arguments, "value") ?? "";
                    await actions.SelectAsync(target, selected, token).ConfigureAwait(false);
                    return ToolOutcome.Ok(await DescribeAsync("selected " + selected, null, token).ConfigureAwait(false));
                case "check":
                    await actions.CheckAsync(target, true, token).ConfigureAwait(false);
                    return ToolOutcome.Ok(await DescribeAsync("checked " + Label(node), null, token).ConfigureAwait(false));
                case "uncheck":
                    await actions.CheckAsync(target, false, token).ConfigureAwait(false);
                    return ToolOutcome.Ok(await DescribeAsync("unchecked " + Label(node), null, token).ConfigureAwait(false));
                case "clear":
                    await actions.ClearAsync(target, token).ConfigureAwait(false);
                    return ToolOutcome.Ok(await DescribeAsync("cleared " + Label(node), null, token).ConfigureAwait(false));
                default:
                    return ToolOutcome.Fail("Unknown tool " + call.Name + ".");
            }
        }
        catch (E2EException ex)
        {
            return ToolOutcome.Fail(ex);
        }
    }

    // A mutating tool takes an action slot and is recorded as a gap: the grammar cannot reproduce it, so a replay ends there
    // and hands the step to the model. The gap is recorded before the tool runs, since a failed tool may have changed the app.
    private async Task<ToolOutcome> RunProjectToolAsync(ModelToolCall call, AgentTool tool, StepAccounting budget, CancellationToken token)
    {
        var actions = budget.Recorded;
        if (call.Arguments.ValueKind is not (JsonValueKind.Object or JsonValueKind.Undefined or JsonValueKind.Null))
        {
            return ToolOutcome.Fail(call.Name + " takes an object of arguments.");
        }

        if (tool.Mutates && !budget.TryReserve())
        {
            return ToolOutcome.Fail(budget.Message + ". Take no more actions. Call done with a verdict.");
        }

        RecordedAction? gap = null;
        if (tool.Mutates)
        {
            gap = new RecordedAction { Kind = "tool", Text = Redact(call.Name, _scope.Redactor) };
            actions.Add(gap);
        }

        string output;
        try
        {
            var input = call.Arguments.ValueKind == JsonValueKind.Object ? (JsonObject)JsonNode.Parse(call.Arguments.GetRawText())! : new JsonObject();
            output = await tool.ExecuteAsync(input, token).ConfigureAwait(false);
        }
        catch (E2EException ex)
        {
            return ToolOutcome.Fail(ex);
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            return ToolOutcome.Fail(call.Name + " failed: " + ex.Message);
        }

        output = output.Length > OverflowShrink.TextClipChars ? output[..OverflowShrink.TextClipChars] + "\n[output cut]" : output;
        return gap is null ? ToolOutcome.Ok(output) : await DescribeOutcomeAsync(output, gap, token).ConfigureAwait(false);
    }

    private async Task<ToolOutcome> BackAsync(IExecutorActions actions, CancellationToken token)
    {
        try
        {
            await actions.BackAsync(token).ConfigureAwait(false);
        }
        catch (E2EException ex)
        {
            return ToolOutcome.Fail(ex);
        }

        return await DescribeOutcomeAsync("navigated back", null, token).ConfigureAwait(false);
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

            return ToolOutcome.Ok(await DescribeAsync(done, null, token).ConfigureAwait(false));
        }
        catch (E2EException ex)
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
                return ToolOutcome.Ok(await DescribeAsync("scrolled " + DirectionName(direction) + " until \"" + text + "\" was in view", null, token).ConfigureAwait(false));
            }

            if (!HasTarget(arguments))
            {
                return ToolOutcome.Fail("scroll_to takes a target, a text to reach, or both.");
            }

            var node = await ResolveAsync(arguments, token).ConfigureAwait(false);
            await actions.ScrollToAsync(new ExecutorTarget(node), token).ConfigureAwait(false);
            return ToolOutcome.Ok(await DescribeAsync("scrolled " + Label(node) + " into view", null, token).ConfigureAwait(false));
        }
        catch (E2EException ex)
        {
            return ToolOutcome.Fail(ex);
        }
    }

    // The sink policy of upstream's authorizeSecretFill (agent/secrets.ts), with its reasons. The port's tool is
    // fill_secret where upstream's is type_secret, and a denial is a tool failure the model reads, not a thrown POLICY_DENIED.
    private static string? AuthorizeSecretFill(Secret secret, SemanticNode node)
    {
        if (node.States.Disabled)
        {
            return "the target field is disabled";
        }

        if (!Roles.IsEditable(node))
        {
            return "the target is not an editable input (role " + (node.Role ?? "none") + ")";
        }

        // A password belongs in a password field, where the surface masks it. A generic secret has no field of its
        // own: it goes wherever the test says, and redaction covers the value the moment it shows on screen.
        var inputPurpose = string.IsNullOrEmpty(node.InputPurpose) ? "none" : node.InputPurpose;
        return secret.Purpose == "password" && inputPurpose != "password"
            ? "field purpose " + inputPurpose + " is incompatible with secret purpose " + secret.Purpose
            : null;
    }

    private async Task<ToolOutcome> FillSecretAsync(ExecutorTarget target, JsonElement arguments, IExecutorActions actions, CancellationToken token)
    {
        var node = target.Node;
        var name = Args.String(arguments, "secret");
        var secret = _scope.Secrets.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal));
        if (secret is null)
        {
            return ToolOutcome.Fail("No secret named '" + (name ?? "") + "' was passed to this step.");
        }

        if (AuthorizeSecretFill(secret, node) is { } denied)
        {
            return ToolOutcome.Fail("Secret fill refused: " + Label(node) + ": " + denied + ".");
        }

        await actions.TypeSecretAsync(target, secret.Name, token).ConfigureAwait(false);
        return ToolOutcome.Ok(await DescribeAsync("filled secret <secret:" + secret.Name + "> into " + Label(node), null, token).ConfigureAwait(false));
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
    private async Task<ToolOutcome> DescribeOutcomeAsync(string action, RecordedAction? committed, CancellationToken token)
    {
        try
        {
            return ToolOutcome.Ok(await DescribeAsync(action, committed, token).ConfigureAwait(false));
        }
        catch (E2EException ex) when (!token.IsCancellationRequested && !StepExecutors.IsRuntimeHardStop(ex))
        {
            return ToolOutcome.Fail(ex);
        }
    }

    // The screen after a committed action, as the model reads it: the action's change wait armed, then a held-still look.
    // An action the dispatcher ran armed its own wait already; a project tool's gap arms here.
    private async Task<string> DescribeAsync(string action, RecordedAction? committed, CancellationToken token)
    {
        if (committed is not null)
        {
            _feed.ArmAfter(committed);
        }

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
