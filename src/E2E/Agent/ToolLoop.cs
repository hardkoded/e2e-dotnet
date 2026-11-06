// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using E2E.Internal;

namespace E2E;

/// <summary>
/// Upstream's <c>createToolLoopExecutor</c> (<c>agent/tool-loop.ts</c>): the chassis of an executor that thinks in a loop of model
/// turns and tool calls. It owns everything but the tool vocabulary: the <c>done</c> verdict tool and its closed code policy, the
/// hard stops (a spent budget, a timeout, or a cancel ends the loop, never the model), the loop guards, the shrink of a history
/// that overflowed the model, and the count of model calls. The built-in agent is this chassis plus the grammar tools
/// (<see cref="DefaultAgent.CreateBuiltInAgent"/>). An executor for a device or an API brings other tools and inherits the same discipline.
/// </summary>
public static class ToolLoop
{
    /// <summary>
    /// Builds a step executor from a tool vocabulary and the chassis. The loop runs at most
    /// <see cref="IExecutorBudgets.MaxModelCalls"/> model turns per step: the agents entry's <c>MaxModelCalls</c>.
    /// </summary>
    public static IStepExecutor CreateToolLoopExecutor(ToolLoopExecutorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Name))
        {
            throw new TestException("INVALID_ARGUMENT", "a tool loop executor needs a name");
        }

        if ((options.BuildPrompt is null) == (options.BuildMessages is null))
        {
            throw new TestException("INVALID_ARGUMENT", "a tool loop executor needs one of BuildPrompt and BuildMessages");
        }

        return new ToolLoopExecutor(options);
    }

    // The step's verdict on a refusal it could not recover from: the same error, saying what the retry did.
    internal static AgentException Overflowed(AgentException refusal, string note) =>
        new(
            refusal.Code,
            ModelHttp.OverflowMessage + note + (refusal.Message.StartsWith(ModelHttp.OverflowMessage, StringComparison.Ordinal) ? refusal.Message[ModelHttp.OverflowMessage.Length..] : ": " + refusal.Message),
            blocked: true,
            refusal);
}

/// <summary>Upstream's <c>ToolLoopExecutorOptions</c>.</summary>
public sealed class ToolLoopExecutorOptions
{
    public required string Name { get; init; }

    public string? Version { get; init; }

    /// <summary>
    /// The model the loop calls. Defaults to the one the agents entry configures (<see cref="StepExecutorContext.Model"/>). A
    /// model brought here is also the agent's model for <c>assert</c>, <c>waitFor</c>, and <c>extract</c>, unless the entry names a judge.
    /// </summary>
    public IAgentModel? Model { get; init; }

    /// <summary>
    /// Executor-specific guidance for the model: what the tools are and how to address targets. The chassis appends the agent's
    /// project context to it.
    /// </summary>
    public Func<StepExecutorContext, string>? System { get; init; }

    /// <summary>Builds the step's tool vocabulary. The chassis adds <c>done</c>.</summary>
    public required Func<StepExecutorContext, ToolLoopHelpers, IReadOnlyList<ToolLoopTool>> Tools { get; init; }

    /// <summary>Builds the step's opening prompt: the first user message. Set this or <see cref="BuildMessages"/>.</summary>
    public Func<StepExecutorContext, Task<string>>? BuildPrompt { get; init; }

    /// <summary>
    /// Builds the step's opening messages, for an opening that carries images (a screenshot beside the screen). Upstream's
    /// <c>buildPrompt</c> returns a string or an array of messages; C# has no union, so the array form has a property of its own.
    /// Set this or <see cref="BuildPrompt"/>.
    /// </summary>
    public Func<StepExecutorContext, Task<IReadOnlyList<ModelMessage>>>? BuildMessages { get; init; }

    /// <summary>The provider options every model call carries. Defaults to the agents entry's.</summary>
    public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }

    /// <summary>
    /// Between-turn history preparation, such as compaction. It runs before the loop's own notices, and the history it returns
    /// carries forward to later turns.
    /// </summary>
    public Func<IReadOnlyList<ModelMessage>, PreparedTurn, PreparedMessages>? PrepareMessages { get; init; }
}

/// <summary>
/// One tool of a loop's vocabulary: what the model is told, and what runs when it calls it. Upstream's tools are AI SDK tools,
/// which the port has no counterpart of, so this is the .NET shape of one entry of its <c>ToolSet</c>. A tool that throws
/// is read by the model as a failed call, except a hard stop of the step, which ends the loop.
/// </summary>
/// <param name="Definition">The name, the description, and the input schema the model sees.</param>
/// <param name="ExecuteAsync">Runs the call and answers what the model reads.</param>
public sealed record ToolLoopTool(ModelTool Definition, Func<ModelToolCall, CancellationToken, Task<ToolLoopResult>> ExecuteAsync);

/// <summary>
/// What a tool call answered: the text the model reads, whether the call failed, which the loop counts toward its failure streak, and the
/// screenshot that goes with the text, when there is one (upstream's tool result with a file part). The loop hands the image to the model
/// in a user turn right after the turn's tool results, since a tool message carries text only on every provider the port sends to.
/// </summary>
public readonly record struct ToolLoopResult(string Content, bool Failed = false, ExecutorPixels? Pixels = null);

/// <summary>Loop services for the tool bodies that run work of their own (upstream's <c>ToolLoopHelpers</c>).</summary>
public sealed class ToolLoopHelpers
{
    private readonly Func<bool> _concluding;
    private readonly Action<AgentException> _reportHardStop;

    internal ToolLoopHelpers(Func<bool> concluding, Action<AgentException> reportHardStop)
    {
        _concluding = concluding;
        _reportHardStop = reportHardStop;
    }

    /// <summary>True once a verdict or a hard stop landed. A late tool call has nothing left to do.</summary>
    public bool Concluding => _concluding();

    /// <summary>Records a hard stop of the step that ends the loop.</summary>
    public void ReportHardStop(AgentException error) => _reportHardStop(error);

    /// <summary>
    /// Runs one tool body under the loop's policy: nothing runs once the step is concluding, a hard stop of the step ends the
    /// loop, and any other failure goes back to the model as text it can react to, named by <paramref name="label"/>.
    /// </summary>
    public async Task<string> GuardAsync(Func<Task<string>> body, string? label = null)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (Concluding)
        {
            return "The step is concluding; nothing more runs.";
        }

        try
        {
            return await body().ConfigureAwait(false);
        }
        catch (E2EException ex) when (StepExecutors.IsRuntimeHardStop(ex))
        {
            if (ex is AgentException hardStop)
            {
                ReportHardStop(hardStop);
            }

            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (label is null ? "" : label + " ") + "failed: " + ex.Message;
        }
    }
}

/// <summary>What <see cref="ToolLoopExecutorOptions.PrepareMessages"/> learns about the turn that is about to run.</summary>
/// <param name="StepNumber">The zero-based index of the turn about to run.</param>
/// <param name="PreviousToolCalls">The names of the tools the previous turn called, in order. Empty on the first turn.</param>
public sealed record PreparedTurn(int StepNumber, IReadOnlyList<string> PreviousToolCalls);

/// <summary>
/// What <see cref="ToolLoopExecutorOptions.PrepareMessages"/> hands back: the history for the next turn, and optionally a reason to
/// stop. The executor has evidence the loop guards cannot see that the step is not progressing. A stop leaves the model only <c>done</c>, like a loop guard.
/// </summary>
public sealed record PreparedMessages(IReadOnlyList<ModelMessage> Messages, string? Stop = null)
{
    // The cast keeps `new` on the list constructor: a List would otherwise convert back through this operator and recurse.
    public static implicit operator PreparedMessages(List<ModelMessage> messages) => new((IReadOnlyList<ModelMessage>)messages);
}

internal sealed class ToolLoopExecutor(ToolLoopExecutorOptions options) : IStepExecutor
{
    // Upstream's WIND_DOWN_TURNS: turns remaining when the loop warns the model to wrap up.
    private const int WindDownTurns = 5;

    // Upstream's CLOCK_WIND_DOWN_MS and CLOCK_WIND_DOWN_FRACTION: the remaining step time under which the loop takes the verdict it can
    // get. A slow provider turn runs tens of seconds, and concluding with the model's own summary beats a verdict-less STEP_TIMEOUT.
    // The effective window is a quarter of the step's budget, capped here, so a short step keeps most of its clock for work.
    private const int ClockWindDownMs = 60_000;
    private const int ClockWindDownFraction = 4;

    // Upstream's FORCED_CONCLUSION_TURNS: turns from the budget ceiling at which only the conclusion tool is offered.
    private const int ForcedConclusionTurns = 2;

    // Upstream's TOOL_CALLS_ONLY_RULE, with the port's done: part of every step's instructions from the first turn.
    internal const string ToolCallsOnlyRule =
        "Reply with tool calls only. A reply without a tool call does nothing and spends a turn; the step ends only through done.";

    // Upstream's TEXT_REPLY_NOTICE, with the port's done: sent when a turn came back as text without a tool call.
    private const string TextReplyNotice =
        "[SYSTEM] Your last reply had no tool call, so nothing happened. Continue with a tool call, or call done with your verdict.";

    // Upstream's FREE_TOOL_CHOICE_MODELS: models that refused a forced tool choice once. A model that answers HTTP 400 to that request
    // shape gets auto for the rest of the process, so the refusal costs one round trip per model, not one per step. Upstream's WeakSet
    // is keyed by the SDK model object, which a worker builds once and shares across tests. The port builds a provider model per
    // session, so a model object is the key for a model the caller owns (a ScriptedModel, a custom IAgentModel), and the provider
    // type with the model id is the key for the built-in provider models, whose objects do not outlive a session. .NET-only.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IAgentModel, object> FreeToolChoiceModels = new();

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> FreeToolChoiceProviderModels = new(StringComparer.Ordinal);

    private static string? ProviderModelKey(IAgentModel model) =>
        model is IDisposable ? model.GetType().FullName + ":" + model.Name : null;

    private static bool IsFreeToolChoiceModel(IAgentModel model) =>
        ProviderModelKey(model) is { } key ? FreeToolChoiceProviderModels.ContainsKey(key) : FreeToolChoiceModels.TryGetValue(model, out _);

    private static void RememberFreeToolChoiceModel(IAgentModel model)
    {
        if (ProviderModelKey(model) is { } key)
        {
            FreeToolChoiceProviderModels[key] = 0;
        }
        else
        {
            FreeToolChoiceModels.AddOrUpdate(model, new object());
        }
    }

    // Only its definition and its stateless Decide policy are used: a shared instance must never run Execute, which keeps a verdict.
    private static readonly VerdictTool Verdict = AgentTools.CreateVerdictTool();

    public string Name => options.Name;

    public string? Version => options.Version;

    public IAgentModel? Model => options.Model;

    public async Task<StepVerdict> RunStepAsync(StepExecutorContext context, CancellationToken token)
    {
        // The context's model resolves on read, so an executor that brought its own never needs the configured one.
        var model = options.Model ?? context.Model
            ?? throw new AgentException(
                "MODEL_UNAVAILABLE",
                "No model is configured. Agent steps need an IAgentModel, or a replay that finishes on its own.",
                blocked: true);
        var redactor = context.Host?.Scope.Redactor ?? Redactor.None;
        var budget = context.Budgets;
        var maxCalls = budget.MaxModelCalls;
        AgentException? hardStop = null;
        var concluded = false;
        var tools = options.Tools(context, new ToolLoopHelpers(() => concluded || hardStop is not null, error => hardStop ??= error));
        var definitions = tools.Select(tool => tool.Definition).Append(Verdict.Tool).ToList();
        var system = SystemFor(context);
        var messages = options.BuildMessages is { } build
            ? [.. await build(context).ConfigureAwait(false)]
            : new List<ModelMessage> { new() { Role = "user", Content = await options.BuildPrompt!(context).ConfigureAwait(false) } };

        var turns = new TurnLog(context);
        var results = new List<GuardToolResult>();
        var stopped = false;
        string? noticed = null;
        var shrunkOnce = false;
        var noticedLowClock = false;

        // Upstream's LoopRun.toolChoice: how the step asks for tool calls. 'required' unless the model refused a forced choice before;
        // it flips to auto when the model rejects one (FreeToolChoiceRetry) or the provider downgrades one (NoticeToolChoiceDowngrade).
        var toolChoice = IsFreeToolChoiceModel(model) ? ModelToolChoice.Auto : ModelToolChoice.Required;
        var clockWindDownMs = Math.Min(ClockWindDownMs, budget.RemainingMs() / ClockWindDownFraction);
        string[]? lastShape = null;
        IReadOnlyList<string> previous = [];
        for (var call = 0; call < maxCalls; call++)
        {
            token.ThrowIfCancellationRequested();
            string? preparedStop = null;
            if (options.PrepareMessages is { } prepare)
            {
                var prepared = prepare(messages, new PreparedTurn(call, previous));
                var next = prepared.Messages.ToList();
                messages.Clear();
                messages.AddRange(next);
                preparedStop = prepared.Stop;
            }

            // A message rewritten since the last request (a history shrunk, or one the executor's PrepareMessages changed) edits the
            // prefix a provider may have bound the reasoning after it to: where it does, that reasoning is not sent again, and the
            // step's turns say so. Compared by value against what the last request sent, so an executor that clones an unchanged
            // history rewrites nothing.
            if (lastShape is not null && FirstRewritten(messages, lastShape) is { } rewritten)
            {
                var rebound = ProviderHints.AfterEdit(model, messages, rewritten);
                if (!ReferenceEquals(rebound, messages))
                {
                    turns.Note("before turn " + turns.Next.ToString(CultureInfo.InvariantCulture) + ": message " + (rewritten + 1).ToString(CultureInfo.InvariantCulture) + " was rewritten, so the reasoning after it is not sent again");
                    messages.Clear();
                    messages.AddRange(rebound);
                }
            }

            var turnsLeft = maxCalls - call;

            // Never on the very first turn: a deliberately short step timeout still deserves one working turn before the clock takes the verdict.
            var lowClock = call > 0 && budget.RemainingMs() < clockWindDownMs;
            if (lowClock && !noticedLowClock)
            {
                noticedLowClock = true;
                turns.Note("before turn " + turns.Next.ToString(CultureInfo.InvariantCulture) + ": the step is nearly out of time, asking for a verdict");
                messages.Add(new ModelMessage
                {
                    Role = "user",
                    Content = "[SYSTEM] The step is nearly out of time. Call done now with your best verdict: passed only if you verified the goal, failed if the application misbehaved, blocked (with a code) if something outside the application stopped you.",
                });
            }

            // A repeat warns first. A stop leaves the model only done, so it still writes its own verdict.
            var (stop, repeat) = preparedStop is not null
                ? (true, preparedStop)
                : stopped ? (true, null) : LoopGuards.CheckLoopGuards(LoopGuards.ExtractGuardCalls(messages));
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

            // A streak of failures with varied inputs is the same circle by another route: the approach is not working, whatever id it tries next.
            if (!stopped && preparedStop is null)
            {
                var (streakStop, streak) = LoopGuards.CheckFailureStreak(results);
                if (streak is not null && !string.Equals(streak, noticed, StringComparison.Ordinal))
                {
                    noticed = streak;
                    stopped = streakStop;
                    messages.Add(new ModelMessage
                    {
                        Role = "user",
                        Content = streakStop
                            ? "Loop guard: " + streak + ". Repeating it further will not make progress. Call done now with your best verdict: failed if the application misbehaved, blocked (with a code) if something outside the application stopped you."
                            : streak + ". Read the failure text and the current screen and change approach; if the step cannot be done, call done with your best verdict.",
                    });
                }
            }

            if (turnsLeft == WindDownTurns && !stopped)
            {
                turns.Note("before turn " + turns.Next.ToString(CultureInfo.InvariantCulture) + ": only " + turnsLeft.ToString(CultureInfo.InvariantCulture) + " turns remain, asking to wrap up");
                messages.Add(new ModelMessage
                {
                    Role = "user",
                    Content = "[SYSTEM NOTICE] Only " + turnsLeft.ToString(CultureInfo.InvariantCulture) + " turns remain for this step. Finish the remaining work now, or call done with your best verdict: failed if the application misbehaved, blocked (with a code) if something outside the application stopped you.",
                });
            }

            // Near the budget, after a loop guard stop, or on a low clock only the conclusion tool is offered. Two forced turns, not one,
            // so a rejected verdict (blocked without a code) can be repaired.
            var forced = turnsLeft <= ForcedConclusionTurns || stopped || lowClock;
            var offered = forced ? definitions.Where(tool => string.Equals(tool.Name, "done", StringComparison.Ordinal)).ToList() : definitions;

            // Upstream's prepareStep: every turn asks for a tool call, and a forced turn names the conclusion tool.
            var turnToolChoice = forced && toolChoice.Type == "required" ? ModelToolChoice.Tool("done") : toolChoice;
            AiTraceTurn traced;
            try
            {
                lastShape = [.. messages.Select(MessageShape.ShapeOf)];
                for (; ; )
                {
                    try
                    {
                        traced = await CompleteAsync(model, system, messages, offered, turnToolChoice, context, redactor, token).ConfigureAwait(false);
                        break;
                    }
                    catch (Exception rejected) when (turnToolChoice.Forced && !token.IsCancellationRequested && ToolChoice.IsForcedToolChoiceRejected(rejected))
                    {
                        // The refused request was no turn, so it spends no call of the budget: the same request goes out again under auto.
                        toolChoice = FreeToolChoiceRetry(model, turns, "the model rejected a forced tool choice before turn " + turns.Next.ToString(CultureInfo.InvariantCulture) + ": retrying with auto");
                        turnToolChoice = toolChoice;
                    }
                }
            }
            catch (AgentException overflow) when (string.Equals(overflow.Code, "CONTEXT_OVERFLOW", StringComparison.Ordinal))
            {
                // One recovery: the history shrinks and the request goes out again, through the same turn preparation as any
                // other, which notices the shrink. The refused request was no turn, so it spends no call of the budget, and a
                // second refusal is the step's verdict.
                if (shrunkOnce)
                {
                    throw ToolLoop.Overflowed(overflow, " again after the history was shrunk once");
                }

                var shrunk = OverflowShrink.ShrinkForOverflow(messages);
                if (OverflowShrink.TextChars(shrunk) >= OverflowShrink.TextChars(messages))
                {
                    // Nothing to elide and nothing to clip: sending the same request again would only spend another call on it.
                    turns.Note("context overflow before turn " + turns.Next.ToString(CultureInfo.InvariantCulture) + ": nothing to shrink");
                    throw ToolLoop.Overflowed(overflow, " and the step history had nothing left to shrink");
                }

                shrunkOnce = true;
                turns.Note("context overflow before turn " + turns.Next.ToString(CultureInfo.InvariantCulture) + ": history shrunk from " + OverflowShrink.TextChars(messages).ToString(CultureInfo.InvariantCulture) + " to " + OverflowShrink.TextChars(shrunk).ToString(CultureInfo.InvariantCulture) + " chars, retrying once");
                messages.Clear();
                messages.AddRange(shrunk);
                call--;
                continue;
            }

            // The generation stays open while this turn's tools run, as the AI SDK keeps it, so a generation a tool starts is its child.
            using var generation = traced;
            var response = traced.Response;
            var turn = turns.Begin(response);
            toolChoice = NoticeToolChoiceDowngrade(model, toolChoice, response, turns);
            if (response.ToolCalls.Count == 0)
            {
                // A refusal is a verdict on the prompt, not a turn to coax a tool call out of.
                if (string.Equals(response.FinishReason, ModelResponse.ContentFilter, StringComparison.Ordinal))
                {
                    throw ModelHttp.ModelRefusalError(model.Name, response.RawFinishReason, redactor);
                }

                // A finished reply without a tool call under a forced choice is a provider that downgraded the choice to auto and a model
                // that answered in prose. The turn was answered and billed; the same request goes out again under auto, and the reply is
                // not kept in the history.
                if (turnToolChoice.Forced && ToolChoice.IsForcedToolCallSkipped(new ToolChoiceViolationException(response.FinishReason ?? "stop")))
                {
                    toolChoice = FreeToolChoiceRetry(model, turns, "turn " + turn.Index.ToString(CultureInfo.InvariantCulture) + " answered without the forced tool call: retrying with auto");
                    continue;
                }

                turns.Note("turn " + turn.Index.ToString(CultureInfo.InvariantCulture) + " made no tool call: asking for one");
                messages.Add(new ModelMessage { Role = "assistant", Content = response.Content, ReasoningItems = response.ReasoningItems });
                messages.Add(new ModelMessage { Role = "user", Content = TextReplyNotice });
                results.Add(new GuardToolResult(Failed: true));
                previous = [];
                continue;
            }

            messages.Add(new ModelMessage { Role = "assistant", Content = response.Content, ToolCalls = response.ToolCalls, ReasoningItems = response.ReasoningItems });
            previous = response.ToolCalls.Select(toolCall => toolCall.Name).ToList();
            var screenshots = new List<(string Tool, ExecutorPixels Pixels)>();
            foreach (var toolCall in response.ToolCalls)
            {
                var outcome = ToolOutcome.Fail(toolCall.Name + " did not run: only done is available now. Call done with your verdict.");
                if (!forced || string.Equals(toolCall.Name, "done", StringComparison.Ordinal))
                {
                    await traced.ExecuteToolAsync(async () => outcome = await RunToolAsync(toolCall, tools, token).ConfigureAwait(false)).ConfigureAwait(false);
                }

                turns.AddResult(turn, toolCall.Name, outcome.Content);
                if (hardStop is not null)
                {
                    throw hardStop;
                }

                messages.Add(new ModelMessage
                {
                    Role = "tool",
                    ToolCallId = toolCall.Id,
                    Name = toolCall.Name,
                    Content = redactor.Redact(outcome.Content),
                });
                if (outcome.Pixels is { } shot)
                {
                    screenshots.Add((toolCall.Name, shot));
                }

                if (!outcome.Done)
                {
                    results.Add(new GuardToolResult(!outcome.Succeeded));
                    continue;
                }

                concluded = true;
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

            if (screenshots.Count > 0)
            {
                messages.Add(new ModelMessage
                {
                    Role = "user",
                    Content = "Screenshot" + (screenshots.Count == 1 ? "" : "s") + " attached to the result of " + string.Join(", ", screenshots.Select(shot => shot.Tool)) + ".",
                    Images = [.. screenshots.Select(shot => new ModelImage(shot.Pixels.Data, shot.Pixels.MediaType, shot.Pixels.Width, shot.Pixels.Height))],
                });
            }
        }

        if (hardStop is not null)
        {
            throw hardStop;
        }

        // Upstream's end of LoopRun.run: the turns ran out without a verdict, so the step fails, as any step that never concluded.
        return new StepVerdict(
            StepVerdictStatus.Failed,
            "the agent used " + turns.Used.ToString(CultureInfo.InvariantCulture) + " of " + maxCalls.ToString(CultureInfo.InvariantCulture) + " turn(s) without calling done",
            "STEP_NO_CONCLUSION");
    }

    // Upstream's LoopRun.freeToolChoiceRetry: a model that refuses a forced tool choice (an HTTP 400 naming tool_choice), or answers a
    // forced turn in prose, is asked again with auto on the same turn budget. The instructions already hold the tool-calls-only rule, so
    // nothing else in the request changes. The model is remembered, so later steps start in auto.
    private static ModelToolChoice FreeToolChoiceRetry(IAgentModel model, TurnLog turns, string note)
    {
        turns.Note(note);
        RememberFreeToolChoiceModel(model);
        return ModelToolChoice.Auto;
    }

    // Upstream's LoopRun.noticeToolChoiceDowngrade: a provider that downgraded a forced choice to auto on its own (a toolChoice warning
    // on a turn that otherwise succeeded) is treated like one that refused it: the rest of the step asks with auto under the
    // tool-calls-only rule, and the model is remembered, so later steps start in auto.
    private static ModelToolChoice NoticeToolChoiceDowngrade(IAgentModel model, ModelToolChoice toolChoice, ModelResponse response, TurnLog turns)
    {
        if (!toolChoice.Forced || !response.Warnings.Any(ToolChoice.IsForcedToolChoiceDowngraded))
        {
            return toolChoice;
        }

        turns.Note("the provider sent auto in place of a forced tool choice on turn " + turns.Used.ToString(CultureInfo.InvariantCulture) + ": continuing with auto");
        RememberFreeToolChoiceModel(model);
        return ModelToolChoice.Auto;
    }

    // Upstream's LoopRun.firstRewritten: the index of the first message whose content is not what the last request sent at that
    // position, if any: a message the executor's PrepareMessages or the overflow shrink rewrote. The comparison is by value,
    // against what was kept from the last request rather than its message objects.
    private static int? FirstRewritten(IReadOnlyList<ModelMessage> messages, IReadOnlyList<string> last)
    {
        for (var position = 0; position < messages.Count && position < last.Count; position++)
        {
            if (MessageShape.ShapeOf(messages[position]) != last[position])
            {
                return position;
            }
        }

        return null;
    }

    // The executor's guidance, then the agent's project context (trusted, told to every turn), then the verdict rules for the tool the loop adds.
    private string SystemFor(StepExecutorContext context)
    {
        var parts = new List<string>();
        if (options.System?.Invoke(context) is { } system && !string.IsNullOrWhiteSpace(system))
        {
            parts.Add(system);
        }

        if (!string.IsNullOrWhiteSpace(context.AgentContext))
        {
            parts.Add("Project context:\n" + context.AgentContext);
        }

        parts.Add(AgentTools.VerdictRules + "\n- " + ToolCallsOnlyRule);
        return string.Join("\n\n", parts);
    }

    // One model turn. A request the model refused outright was no turn, and a timeout or a cancel ends the loop like any hard stop.
    private async Task<AiTraceTurn> CompleteAsync(
        IAgentModel model,
        string system,
        List<ModelMessage> messages,
        IReadOnlyList<ModelTool> tools,
        ModelToolChoice toolChoice,
        StepExecutorContext context,
        Redactor redactor,
        CancellationToken token)
    {
        var started = DateTimeOffset.UtcNow;
        var traced = await (context.Host?.Scope.Debug ?? DebugTrace.Off).Time(
            Phases.Model,
            () => AiTraceModel.BeginAsync(
                context.Host?.Scope.AiTrace,
                model,
                new ModelRequest
                {
                    System = redactor.Redact(system),
                    Messages = messages,
                    Tools = tools,
                    ToolChoice = toolChoice,
                    ProviderOptions = options.ProviderOptions ?? context.ProviderOptions,
                    MarkLatest = true,
                    Redactor = redactor,
                },
                token)).ConfigureAwait(false);
        try
        {
            var response = traced.Response;
            context.Budgets.RecordModelCall(new ExecutorModelCall
            {
                InputTokens = response.Usage?.InputTokens,
                OutputTokens = response.Usage?.OutputTokens,
                CacheReadTokens = response.Usage?.CacheReadTokens,
                CacheWriteTokens = response.Usage?.CacheWriteTokens,
                EstimatedCostUsd = response.Usage?.EstimatedCostUsd,
                StartedAt = started,
                DurationMs = (int)(DateTimeOffset.UtcNow - started).TotalMilliseconds,
                ModelId = model.Name,
            });
            return traced;
        }
        catch
        {
            // The turn is still open here; a budget stop must not leave the step unfinished in the trace.
            traced.Dispose();
            throw;
        }
    }

    private static async Task<ToolOutcome> RunToolAsync(ModelToolCall call, IReadOnlyList<ToolLoopTool> tools, CancellationToken token)
    {
        if (string.Equals(call.Name, "done", StringComparison.Ordinal))
        {
            return Verdict.Decide(call);
        }

        var tool = tools.FirstOrDefault(item => string.Equals(item.Definition.Name, call.Name, StringComparison.Ordinal));
        if (tool is null)
        {
            return ToolOutcome.Fail("Unknown tool " + call.Name + ".");
        }

        try
        {
            var result = await tool.ExecuteAsync(call, token).ConfigureAwait(false);
            return new ToolOutcome { Content = result.Content, Succeeded = !result.Failed, Pixels = result.Pixels };
        }
        catch (E2EException ex) when (!StepExecutors.IsRuntimeHardStop(ex))
        {
            return ToolOutcome.Fail(ex);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or E2EException))
        {
            return ToolOutcome.Fail(call.Name + " failed: " + ex.Message);
        }
    }
}

/// <summary>
/// The model turns of one step, as upstream's <c>LoopRun</c> keeps them (<c>tool-loop.ts</c>): every turn that ran, the loop's
/// own notes beside the turn they followed, and the bounded tail handed to the step through <see cref="StepExecutorContext.AttachTurns"/>
/// each time one changes, so a step the clock ends keeps its turns.
/// </summary>
internal sealed class TurnLog(StepExecutorContext context)
{
    // Per-turn clips: the model's prose, one tool call's arguments, one tool result.
    private const int MaxTurnTextChars = 1_000;
    private const int MaxTurnCallChars = 400;
    private const int MaxTurnResultChars = 600;

    // The report keeps the last turns of a step, each clipped again to this.
    private const int MaxReportedTurns = 12;
    private const int MaxReportedCalls = 8;
    private const int MaxReportedCallChars = 200;
    private const int MaxReportedOutcomeChars = 600;

    // The loop's own notes on a reported turn, kept past the outcome clip.
    private const int MaxReportedNotesChars = 1_000;

    private const string LoopNote = "[loop] ";

    private readonly Redactor _redactor = context.Host?.Scope.Redactor ?? Redactor.None;
    private readonly List<LoopTurn> _turns = [];

    /// <summary>The number of the turn that runs next.</summary>
    public int Next => _turns.Count + 1;

    /// <summary>The number of turns that ran (upstream's <c>turnsUsed</c>).</summary>
    public int Used => _turns.Count;

    /// <summary>Records one turn: what the model said and called. The tool results are added as each lands.</summary>
    public LoopTurn Begin(ModelResponse response)
    {
        var turn = new LoopTurn(Next, [.. response.ToolCalls.Select(call => call.Name + "(" + Clipped(call.Arguments.ValueKind == JsonValueKind.Undefined ? "{}" : call.Arguments.GetRawText(), MaxTurnCallChars) + ")")]);
        if (response.Content?.Trim() is { Length: > 0 } text)
        {
            turn.Results.Add("assistant: " + Clipped(text, MaxTurnTextChars));
        }

        _turns.Add(turn);
        Attach();
        return turn;
    }

    public void AddResult(LoopTurn turn, string tool, string content)
    {
        turn.Results.Add("[" + tool + "] " + Clipped(content, MaxTurnResultChars));
        Attach();
    }

    /// <summary>A loop event between turns is written onto the turn it followed. Before the first turn there is none to write it on, and the port keeps no transcript to open with it.</summary>
    public void Note(string text)
    {
        if (_turns.Count == 0)
        {
            return;
        }

        _turns[^1].Notes.Add(LoopNote + text);
        Attach();
    }

    private void Attach()
    {
        if (_turns.Count == 0)
        {
            return;
        }

        context.AttachTurns([.. _turns.TakeLast(MaxReportedTurns).Select(turn => new StepTurn(
            turn.Index,
            [.. turn.Calls.Take(MaxReportedCalls).Select(call => Truncate(call, MaxReportedCallChars))],
            ReportedOutcome(turn)))]);
    }

    // The tool results clipped, and the loop's notes after them, clipped apart, since they come last and one clip would drop them.
    private static string ReportedOutcome(LoopTurn turn)
    {
        var outcome = Truncate(string.Join('\n', turn.Results), MaxReportedOutcomeChars);
        return turn.Notes.Count == 0 ? outcome : outcome + "\n" + Truncate(string.Join('\n', turn.Notes), MaxReportedNotesChars);
    }

    // Redacted before it is cut: a cut can split a secret in two, and the redactor no longer matches half of one.
    // Upstream clips first. This is a deliberate difference.
    private string Clipped(string text, int max) => Truncate(_redactor.Redact(text), max);

    /// <summary>Cuts <paramref name="text"/> to <paramref name="max"/> characters, never between the halves of a surrogate pair.</summary>
    internal static string Truncate(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        var end = max > 0 && char.IsHighSurrogate(text[max - 1]) ? max - 1 : max;
        return text[..end] + "…[truncated]";
    }
}

/// <summary>One turn as the loop keeps it: the loop's notes apart from what the model and the tools produced.</summary>
internal sealed class LoopTurn(int index, IReadOnlyList<string> calls)
{
    public int Index { get; } = index;

    public IReadOnlyList<string> Calls { get; } = calls;

    /// <summary>The model's prose, then one line per tool result.</summary>
    public List<string> Results { get; } = [];

    /// <summary>Loop events after the turn (a reply without a tool call, a shrunk history), each prefixed with <c>[loop] </c>.</summary>
    public List<string> Notes { get; } = [];
}
