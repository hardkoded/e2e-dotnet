// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;

namespace E2E.Acp;

/// <summary>
/// The tools a session offers (upstream's <c>tools.ts</c>): the built-in agent's action tools (<see cref="AgentTools.CreateGrammarTools"/>)
/// and its verdict tool (<see cref="AgentTools.CreateVerdictTool"/>), which is <c>done</c> where upstream's is <c>complete_step</c>. An ACP
/// session lists its tools once, while e2e builds them per step, so the session lists them from its first step and each call runs the
/// active step's own tool of that name. Every action goes through the step's actions, where the runner authorizes, counts, and records it,
/// so steps replay from the cache as the built-in agent's do.
/// </summary>
internal static class StepTools
{
    private const string StepEnded = "The step has ended. Wait for the next instruction.";

    /// <summary>The session's tools, listed from its first step. Upstream's <c>stepTools</c>.</summary>
    public static IReadOnlyList<ServedTool> Create(StepSlot slot, StepExecutorContext first)
    {
        var tools = new List<ServedTool>();
        // The tools an assertion offers change nothing in the app (upstream's `name in readOnlyTools`); the listing is of an act's, the widest.
        var readOnlyTools = AgentTools.CreateGrammarTools(first, new GrammarToolsOptions { Kind = ExecutorStepKind.Assert }).Select(tool => tool.Definition.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var listed in AgentTools.CreateGrammarTools(first, new GrammarToolsOptions { Kind = ExecutorStepKind.Act }))
        {
            var name = listed.Definition.Name;
            var readOnly = readOnlyTools.Contains(name);
            tools.Add(new ServedTool(
                name,
                listed.Definition.Description,
                listed.Definition.Parameters,
                readOnly,
                (arguments, token) => GuardedAsync(slot, name, async active =>
                {
                    if (!readOnly && active.Context.Step.Kind == ExecutorStepKind.Assert)
                    {
                        throw new InvalidOperationException("this step is an assertion: read the screen and conclude, do not change it");
                    }

                    // One action at a time, in the order they arrived, as the runner's queue runs them: a call batched behind the verdict meets it.
                    await active.Gate.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        return ResultOf(await RunAsync(active, name, arguments, token).ConfigureAwait(false));
                    }
                    finally
                    {
                        active.Gate.Release();
                    }
                })));
        }

        var verdict = AgentTools.CreateVerdictTool().Tool;
        tools.Add(new ServedTool(
            verdict.Name,
            verdict.Description,
            verdict.Parameters,
            true,
            (arguments, token) => GuardedAsync(slot, verdict.Name, active =>
            {
                var answer = active.Conclusion.Execute(CallOf(verdict.Name, arguments));
                return Task.FromResult(new ToolResult(active.Conclusion.Concluded ? $"{answer.Content} Wait for the next instruction." : answer.Content, IsError: answer.Failed));
            })));
        return tools;
    }

    /// <summary>The step's first screen, whole: the first look of its action tools, which later results report changes against. Upstream's <c>openingScreen</c>.</summary>
    public static async Task<string> OpeningScreenAsync(ActiveStep active, CancellationToken cancellationToken) =>
        (await RunAsync(active, "observe", JsonSerializer.SerializeToElement(new { }), cancellationToken).ConfigureAwait(false)).Content;

    /// <summary>The listed tools the active step does not offer, by the reason it would give. Upstream's <c>missingTools</c>.</summary>
    public static IReadOnlyList<string> MissingTools(ActiveStep active, IReadOnlyList<ServedTool> listed)
    {
        var own = ToolsOf(active);
        return [.. listed.Where(tool => tool.Name != VerdictTool.Name && !own.ContainsKey(tool.Name)).Select(tool => $"{tool.Name} ({Unavailable(tool.Name)})")];
    }

    /// <summary>The verdict the agent concluded the step with, if it did. Upstream's <c>verdictOf</c>.</summary>
    public static StepVerdict? VerdictOf(ActiveStep active) => active.Conclusion.Verdict;

    /// <summary>Whether the step takes no more actions: concluded, halted by a runtime error, or stopped for a tool of the agent's own.</summary>
    private static bool Stopped(ActiveStep active) => active.Conclusion.Concluded || active.Halted is not null || active.Denied is not null;

    /// <summary>The active step's action tools, built once per step so its screens report changes against the one before.</summary>
    private static IReadOnlyDictionary<string, ToolLoopTool> ToolsOf(ActiveStep active) => active.Tools.Value;

    /// <summary>The action tools of a step. The guard runs when a call's turn comes, so a call batched behind the verdict or a stop is skipped.</summary>
    internal static IReadOnlyDictionary<string, ToolLoopTool> BuildTools(ActiveStep active) =>
        AgentTools
            .CreateGrammarTools(active.Context, new GrammarToolsOptions { Guard = body => Stopped(active) ? Task.FromResult(StepEnded) : body() })
            .ToDictionary(tool => tool.Definition.Name, StringComparer.Ordinal);

    private static async Task<ToolLoopResult> RunAsync(ActiveStep active, string name, JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!ToolsOf(active).TryGetValue(name, out var tool))
        {
            throw new InvalidOperationException(Unavailable(name));
        }

        return await tool.ExecuteAsync(CallOf(name, arguments), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Runs a tool body against the active step. A runtime error ends the turn; anything else goes back to the agent as the call's error.</summary>
    private static async Task<ToolResult> GuardedAsync(StepSlot slot, string name, Func<ActiveStep, Task<ToolResult>> body)
    {
        ActiveStep? active = null;
        try
        {
            active = Current(slot, name);
            return await body(active).ConfigureAwait(false);
        }
        catch (E2EException error) when (active is not null && StepExecutors.IsRuntimeHardStop(error))
        {
            active.Halted = error;
            slot.Halt();
            return new ToolResult($"The step has ended ({error.Code}). Stop and wait for the next instruction.", IsError: true);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return new ToolResult($"{name} failed: {error.Message.Split('\n')[0]}", IsError: true);
        }
    }

    private static ActiveStep Current(StepSlot slot, string name)
    {
        var active = slot.Active;
        if (active is null || Stopped(active))
        {
            throw new InvalidOperationException("No step is active. Wait for the next instruction.");
        }

        active.Record(name);
        return active;
    }

    private static string Unavailable(string name) => $"{name} is not available in this step";

    private static ModelToolCall CallOf(string name, JsonElement arguments) => new()
    {
        Id = name + "-" + Guid.NewGuid().ToString("N"),
        Name = name,
        Arguments = arguments,
    };

    /// <summary>A rendered screen as an MCP result: its text, and the screenshot as an image when it carries one.</summary>
    private static ToolResult ResultOf(ToolLoopResult output) =>
        new(output.Content, output.Pixels is { } pixels ? (Convert.ToBase64String(pixels.Data), pixels.MediaType) : null, output.Failed);
}

/// <summary>The step a session serves now. Upstream's <c>ActiveStep</c>.</summary>
internal sealed class ActiveStep
{
    private readonly Lock _gate = new();
    private readonly List<string> _calls = [];
    private Exception? _halted;
    private string? _denied;

    public ActiveStep(StepExecutorContext context)
    {
        Context = context;
        Conclusion = AgentTools.CreateVerdictTool(context.Step.Kind);
        Tools = new Lazy<IReadOnlyDictionary<string, ToolLoopTool>>(() => StepTools.BuildTools(this), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public StepExecutorContext Context { get; }

    /// <summary>The step's <c>done</c>, which holds its verdict once the agent concludes. Upstream's <c>conclusion</c>.</summary>
    public VerdictTool Conclusion { get; }

    /// <summary>Runs one action at a time.</summary>
    public SemaphoreSlim Gate { get; } = new(1, 1);

    /// <summary>The step's own action tools, built on its first look.</summary>
    public Lazy<IReadOnlyDictionary<string, ToolLoopTool>> Tools { get; }

    /// <summary>A runtime error a tool hit; the turn is cancelled and the step rethrows it.</summary>
    public Exception? Halted
    {
        get { lock (_gate) { return _halted; } }
        set { lock (_gate) { _halted ??= value; } }
    }

    /// <summary>A tool of the agent's own that ran without asking; the turn is cancelled and the step fails.</summary>
    public string? Denied
    {
        get { lock (_gate) { return _denied; } }
    }

    /// <summary>Tool names in call order, for the transcript.</summary>
    public IReadOnlyList<string> Calls
    {
        get { lock (_gate) { return [.. _calls]; } }
    }

    public void Record(string name)
    {
        lock (_gate)
        {
            _calls.Add(name);
        }
    }

    /// <summary>Notes the first tool of the agent's own that ran.</summary>
    public void Deny(string title)
    {
        lock (_gate)
        {
            _denied ??= title;
        }
    }
}

/// <summary>Where the tools find the active step, and how a tool ends the turn early. Upstream's <c>StepSlot</c>.</summary>
internal sealed class StepSlot(Action halt)
{
    private ActiveStep? _active;

    public ActiveStep? Active
    {
        get => Volatile.Read(ref _active);
        set => Volatile.Write(ref _active, value);
    }

    /// <summary>Called when a tool hit a runtime error: cancels the agent's turn.</summary>
    public void Halt() => halt();
}
