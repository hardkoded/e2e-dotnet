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

    /// <summary>Builds the step's opening prompt: the first user message.</summary>
    public required Func<StepExecutorContext, Task<string>> BuildPrompt { get; init; }

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

/// <summary>What a tool call answered: the text the model reads, and whether the call failed, which the loop counts toward its failure streak.</summary>
public readonly record struct ToolLoopResult(string Content, bool Failed = false);

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
    public static implicit operator PreparedMessages(List<ModelMessage> messages) => new(messages);
}

internal sealed class ToolLoopExecutor(ToolLoopExecutorOptions options) : IStepExecutor
{
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
        var messages = new List<ModelMessage>
        {
            new()
            {
                Role = "user",
                Content = await options.BuildPrompt(context).ConfigureAwait(false),
            },
        };

        var failures = 0;
        var stopped = false;
        string? noticed = null;
        var shrunkOnce = false;
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

            if (failures >= 5)
            {
                messages.Add(new ModelMessage { Role = "user", Content = "Stop acting. Call done with a verdict." });
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

            var offered = stopped ? definitions.Where(tool => string.Equals(tool.Name, "done", StringComparison.Ordinal)).ToList() : definitions;
            ModelResponse response;
            try
            {
                response = await CompleteAsync(model, system, messages, offered, context, redactor, token).ConfigureAwait(false);
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
                    response = await CompleteAsync(model, system, messages, offered, context, redactor, token).ConfigureAwait(false);
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
                previous = [];
                continue;
            }

            messages.Add(new ModelMessage { Role = "assistant", Content = response.Content, ToolCalls = response.ToolCalls, ReasoningItems = response.ReasoningItems });
            previous = response.ToolCalls.Select(toolCall => toolCall.Name).ToList();
            foreach (var toolCall in response.ToolCalls)
            {
                var outcome = stopped && !string.Equals(toolCall.Name, "done", StringComparison.Ordinal)
                    ? ToolOutcome.Fail("The loop guard stopped the step. Only done is available.")
                    : await RunToolAsync(toolCall, tools, token).ConfigureAwait(false);
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
                if (!outcome.Succeeded)
                {
                    failures++;
                }

                if (!outcome.Done)
                {
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

            if (failures is >= 3 and < 5)
            {
                messages.Add(new ModelMessage { Role = "user", Content = "The last actions failed. Change approach, then call done if you cannot." });
            }
        }

        // Asking for one more call than the budget allows is a hard stop, as upstream: blocked, not a verdict.
        throw hardStop ?? new AgentException("STEP_BUDGET_EXHAUSTED", "agent.act exhausted its model-call budget of " + maxCalls.ToString(CultureInfo.InvariantCulture), blocked: true);
    }

    // The executor's guidance, then the agent's project context: trusted, told to every turn.
    private string SystemFor(StepExecutorContext context)
    {
        var system = options.System?.Invoke(context) ?? "";
        return string.IsNullOrWhiteSpace(context.AgentContext) ? system : system + (system.Length == 0 ? "" : "\n\n") + "Project context:\n" + context.AgentContext;
    }

    // One model turn. A request the model refused outright was no turn, and a timeout or a cancel ends the loop like any hard stop.
    private async Task<ModelResponse> CompleteAsync(
        IAgentModel model,
        string system,
        List<ModelMessage> messages,
        IReadOnlyList<ModelTool> tools,
        StepExecutorContext context,
        Redactor redactor,
        CancellationToken token)
    {
        var started = DateTimeOffset.UtcNow;
        var response = await model.CompleteAsync(
            new ModelRequest
            {
                System = redactor.Redact(system),
                Messages = messages,
                Tools = tools,
                ProviderOptions = options.ProviderOptions ?? context.ProviderOptions,
                MarkLatest = true,
                Redactor = redactor,
            },
            token).ConfigureAwait(false);
        context.Budgets.RecordModelCall(new ExecutorModelCall
        {
            InputTokens = response.Usage?.InputTokens,
            OutputTokens = response.Usage?.OutputTokens,
            StartedAt = started,
            DurationMs = (int)(DateTimeOffset.UtcNow - started).TotalMilliseconds,
            ModelId = model.Name,
        });
        return response;
    }

    private static async Task<ToolOutcome> RunToolAsync(ModelToolCall call, IReadOnlyList<ToolLoopTool> tools, CancellationToken token)
    {
        if (string.Equals(call.Name, "done", StringComparison.Ordinal))
        {
            return Verdict.Execute(call);
        }

        var tool = tools.FirstOrDefault(item => string.Equals(item.Definition.Name, call.Name, StringComparison.Ordinal));
        if (tool is null)
        {
            return ToolOutcome.Fail("Unknown tool " + call.Name + ".");
        }

        try
        {
            var result = await tool.ExecuteAsync(call, token).ConfigureAwait(false);
            return new ToolOutcome { Content = result.Content, Succeeded = !result.Failed };
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
