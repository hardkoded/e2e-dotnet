// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Acp;

/// <summary>The step a session serves now. Upstream's <c>ActiveStep</c> (<c>tools.ts</c>).</summary>
internal sealed class ActiveStep(StepExecutorContext context)
{
    private readonly object _gate = new();
    private readonly List<string> _calls = [];

    public StepExecutorContext Context { get; } = context;

    /// <summary>The step's <c>done</c>, which holds its verdict once the agent concludes.</summary>
    public VerdictTool Conclusion { get; } = AgentPrimitives.CreateVerdictTool();

    /// <summary>A runtime error a tool hit; the turn is cancelled and the step rethrows it.</summary>
    public Exception? Halted { get; set; }

    /// <summary>A tool of the agent's own that ran without asking; the turn is cancelled and the step fails.</summary>
    public string? Denied { get; set; }

    /// <summary>The step's own action tools, built on its first look.</summary>
    public IReadOnlyList<GrammarTool>? Tools { get; set; }

    /// <summary>Runs the step's action tools in call order, so a call queued behind a verdict sees it.</summary>
    public SemaphoreSlim Queue { get; } = new(1, 1);

    /// <summary>Tool names in call order, for the transcript.</summary>
    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    public void RecordCall(string name)
    {
        lock (_gate)
        {
            _calls.Add(name);
        }
    }
}

/// <summary>Where the tools find the active step, and how a tool ends the turn early. Upstream's <c>StepSlot</c>.</summary>
internal sealed class StepSlot
{
    public volatile ActiveStep? Active;

    /// <summary>Called when a tool hit a runtime error: cancels the agent's turn.</summary>
    public Action Halt { get; set; } = () => { };
}

/// <summary>
/// The tools a session offers: the built-in agent's action tools (<see cref="AgentPrimitives.CreateGrammarTools"/>) and its
/// <c>done</c> (<see cref="AgentPrimitives.CreateVerdictTool"/>). An ACP session lists its tools once, while e2e builds them per
/// step, so the session lists them from its first step and each call runs the active step's own tool of that name. Every action goes
/// through <see cref="StepExecutorContext.Actions"/>, where the runner authorizes, counts, and records it, so steps replay from the
/// cache as the built-in agent's do. Upstream's <c>tools.ts</c>.
/// </summary>
internal static class StepTools
{
    private const string StepEnded = "The step has ended. Wait for the next instruction.";

    /// <summary>The step's first screen, whole: the first look of its action tools, which later results report changes against.</summary>
    public static async Task<string> OpeningScreenAsync(ActiveStep active, CancellationToken cancellationToken)
    {
        var observe = ToolsOf(active).First(tool => tool.Name == "observe");
        return await observe.ExecuteAsync(JsonDocument.Parse("{}").RootElement, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The listed tools the active step does not offer, by the reason it would give.</summary>
    public static List<string> MissingTools(ActiveStep active, IReadOnlyList<ServedTool> listed)
    {
        var own = ToolsOf(active);
        return [.. listed.Where(tool => tool.Name != VerdictName && own.All(candidate => candidate.Name != tool.Name)).Select(tool => tool.Name + " (" + Unavailable(tool.Name) + ")")];
    }

    /// <summary>The verdict the agent concluded the step with, if it did.</summary>
    public static StepVerdict? VerdictOf(ActiveStep active) => active.Conclusion.Verdict;

    /// <summary>The session's tools, listed from its first step.</summary>
    public static List<ServedTool> Create(StepSlot slot, StepExecutorContext first)
    {
        ActiveStep Current(string name)
        {
            var active = slot.Active;
            if (active is null || Stopped(active))
            {
                throw new InvalidOperationException("No step is active. Wait for the next instruction.");
            }

            active.RecordCall(name);
            return active;
        }

        // Runs a tool body against the active step; a runtime error ends the turn, anything else goes back to the agent as the call's error.
        async Task<ToolResult> Guarded(string name, Func<ActiveStep, Task<ToolResult>> body)
        {
            ActiveStep? active = null;
            try
            {
                active = Current(name);
                return await body(active).ConfigureAwait(false);
            }
            catch (E2EException error) when (active is not null && StepExecutors.IsRuntimeHardStop(error))
            {
                active.Halted = error;
                slot.Halt();
                return new ToolResult("The step has ended (" + error.Code + "). Stop and wait for the next instruction.", IsError: true);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                return new ToolResult(name + " failed: " + FirstLine(error), IsError: true);
            }
        }

        var tools = new List<ServedTool>();
        foreach (var tool in AgentPrimitives.CreateGrammarTools(first))
        {
            var name = tool.Name;
            var readOnly = tool.ReadOnly;
            tools.Add(new ServedTool
            {
                Name = name,
                Description = tool.Description,
                InputSchema = tool.InputSchema,
                ReadOnly = readOnly,
                RunAsync = (args, token) => Guarded(name, async active =>
                {
                    if (!readOnly && active.Context.Step.Kind == ExecutorStepKind.Assert)
                    {
                        throw new InvalidOperationException("this step is an assertion: read the screen and conclude, do not change it");
                    }

                    var own = ToolsOf(active).FirstOrDefault(candidate => candidate.Name == name)
                        ?? throw new InvalidOperationException(Unavailable(name));
                    return new ToolResult(await own.ExecuteAsync(args, token).ConfigureAwait(false));
                }),
            });
        }

        var conclusion = AgentPrimitives.CreateVerdictTool();
        tools.Add(new ServedTool
        {
            Name = VerdictName,
            Description = conclusion.Description,
            InputSchema = conclusion.InputSchema,
            ReadOnly = true,
            RunAsync = (args, _) => Guarded(VerdictName, active =>
            {
                // Two parallel verdicts must not both pass the check: the first one stands.
                lock (active.Conclusion)
                {
                    var text = active.Conclusion.Concluded ? "Step concluded." : active.Conclusion.Execute(args);
                    return Task.FromResult(new ToolResult(active.Conclusion.Concluded ? text + " Wait for the next instruction." : text));
                }
            }),
        });
        return tools;
    }

    private static string VerdictName => AgentPrimitives.CreateVerdictTool().Name;

    // The active step's action tools, built once per step so its screens report changes against the one before. The guard runs when
    // a call's turn in the queue comes, so a call batched behind the verdict or a stop is skipped.
    private static IReadOnlyList<GrammarTool> ToolsOf(ActiveStep active) =>
        active.Tools ??= AgentPrimitives.CreateGrammarTools(
            active.Context,
            new GrammarToolOptions { Guard = body => QueuedAsync(active, body) });

    private static async Task<string> QueuedAsync(ActiveStep active, Func<Task<string>> body)
    {
        await active.Queue.WaitAsync().ConfigureAwait(false);
        try
        {
            return Stopped(active) ? StepEnded : await body().ConfigureAwait(false);
        }
        finally
        {
            active.Queue.Release();
        }
    }

    /// <summary>Whether the step takes no more actions: concluded, halted by a runtime error, or stopped for a tool of the agent's own.</summary>
    private static bool Stopped(ActiveStep active) => active.Conclusion.Concluded || active.Halted is not null || active.Denied is not null;

    private static string Unavailable(string name) => name + " is not available in this step";

    private static string FirstLine(Exception error) => error.Message.Split('\n')[0];
}
