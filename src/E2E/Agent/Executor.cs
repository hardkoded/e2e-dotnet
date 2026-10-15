// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E;

/// <summary>
/// Upstream's <c>StepExecutor</c> (<c>agent/executor.ts</c>): the part of an act step that thinks. The runtime owns
/// the step's cache, budget, deadline, and verdict; an executor reads the screen, acts through the context, and
/// returns a verdict. The built-in tool loop is the only one, and it stays internal for now.
/// </summary>
internal interface IStepExecutor
{
    string Name { get; }

    string Version { get; }

    Task<StepVerdict> RunStepAsync(StepExecutorContext context, CancellationToken cancellationToken);
}

/// <summary>What an executor receives for one step (upstream's <c>StepExecutorContext</c>).</summary>
internal sealed class StepExecutorContext
{
    public required ExecutorStep Step { get; init; }

    /// <summary>The actions a cache replay ran before it handed the step over, or null when the step runs live from the start.</summary>
    public ReplayedPrefix? ReplayedPrefix { get; init; }

    /// <summary>
    /// The screen the step began on, rendered as the model reads it. The runtime settled it once for the replay and the
    /// recording, and the executor reads it instead of taking another look. No upstream equivalent: upstream's
    /// executor takes its first look with <c>observe</c>.
    /// </summary>
    public required ExecutorObservation Start { get; init; }

    public required IExecutorActions Actions { get; init; }

    public required StepAccounting Budgets { get; init; }
}

/// <summary>Upstream's <c>ExecutorStep</c>.</summary>
internal sealed class ExecutorStep
{
    public required ExecutorStepKind Kind { get; init; }

    public required string Instruction { get; init; }

    public IReadOnlyDictionary<string, object?>? Params { get; init; }
}

/// <summary>Upstream's <c>ExecutorStep.kind</c>.</summary>
internal enum ExecutorStepKind
{
    Act,
    Assert,
}

/// <summary>Upstream's <c>ExecutorObservation</c>, reduced to the text the model reads.</summary>
internal sealed record ExecutorObservation(string Text);

/// <summary>
/// Upstream's <c>ExecutorTarget</c>: a node an action is aimed at, named by its ref. <see cref="Node"/> is the node the
/// runtime resolved it to. An action that finds the node again after the page changed it (a scroll that pages a list)
/// leaves the node it found last there.
/// </summary>
internal sealed class ExecutorTarget(SemanticNode node)
{
    public string Id => Node.Ref;

    public SemanticNode Node { get; set; } = node;
}

/// <summary>
/// Upstream's <c>ExecutorActions</c>, with the verbs the port's engines support. Each verb performs the action and
/// records it for the step's cache entry, and arms the change wait the next settled look reads. An engine failure
/// is thrown as an <see cref="E2EException"/>. <see cref="ClearAsync"/> is a verb the port's grammar has and upstream's
/// does not (upstream clears with <c>type</c>).
/// </summary>
internal interface IExecutorActions
{
    Task TapAsync(ExecutorTarget target, CancellationToken cancellationToken);

    Task DoubleTapAsync(ExecutorTarget target, CancellationToken cancellationToken);

    Task TypeAsync(ExecutorTarget target, string value, CancellationToken cancellationToken);

    Task TypeSecretAsync(ExecutorTarget target, string secretName, CancellationToken cancellationToken);

    Task ClearAsync(ExecutorTarget target, CancellationToken cancellationToken);

    /// <summary>Presses <paramref name="key"/> on <paramref name="target"/>, or on the focused control when there is none.</summary>
    Task PressAsync(ExecutorTarget? target, string key, CancellationToken cancellationToken);

    Task SelectAsync(ExecutorTarget target, string value, CancellationToken cancellationToken);

    Task CheckAsync(ExecutorTarget target, bool check, CancellationToken cancellationToken);

    Task ScrollToAsync(ExecutorTarget target, CancellationToken cancellationToken);

    /// <summary>Pages <paramref name="list"/>, or the viewport when there is none, until a node reads <paramref name="text"/>.</summary>
    Task ScrollUntilAsync(string text, ScrollDirection direction, ExecutorTarget? list, CancellationToken cancellationToken);

    /// <summary>Scrolls <paramref name="list"/>, or the viewport when there is none, <paramref name="times"/> times.</summary>
    Task ScrollAsync(ExecutorTarget? list, ScrollDirection direction, int times, CancellationToken cancellationToken);

    Task NavigateAsync(string url, CancellationToken cancellationToken);

    Task BackAsync(CancellationToken cancellationToken);
}

/// <summary>Upstream's <c>ReplayedPrefix</c>: what a replay ran before it handed the step to the executor, and why it stopped.</summary>
internal sealed record ReplayedPrefix(IReadOnlyList<RecordedAction> Actions, ReplayHandOffReason StopReason);

/// <summary>
/// Upstream's <c>ReplayHandOffReason</c>. <see cref="ActionBudget"/> and <see cref="InvalidEntry"/> are the port's:
/// its replay draws on the step's action budget, and reads a recording back from a store that can hold any shape.
/// </summary>
internal enum ReplayHandOffReason
{
    Gap,
    TargetNotFound,
    TargetAmbiguous,
    ActionFailed,
    EndMismatch,
    ActionBudget,
    InvalidEntry,
}

/// <summary>The names a hand-off reason carries in a cache report and in the prompt the executor reads.</summary>
internal static class ReplayHandOffReasons
{
    private static readonly (ReplayHandOffReason Reason, string Name)[] Names =
    [
        (ReplayHandOffReason.Gap, "gap"),
        (ReplayHandOffReason.TargetNotFound, "target-not-found"),
        (ReplayHandOffReason.TargetAmbiguous, "target-ambiguous"),
        (ReplayHandOffReason.ActionFailed, "action-failed"),
        (ReplayHandOffReason.EndMismatch, "end-mismatch"),
        (ReplayHandOffReason.ActionBudget, "action-budget"),
        (ReplayHandOffReason.InvalidEntry, "invalid-entry"),
    ];

    public static string Name(ReplayHandOffReason reason) => Names.First(item => item.Reason == reason).Name;

    public static ReplayHandOffReason Parse(string? name) => Names.First(item => string.Equals(item.Name, name, StringComparison.Ordinal)).Reason;
}

/// <summary>Upstream's <c>StepVerdict</c>: how an executor ended the step.</summary>
internal readonly record struct StepVerdict(StepVerdictStatus Status, string Summary, string? ErrorCode = null);

/// <summary>Upstream's <c>StepVerdict.status</c>.</summary>
internal enum StepVerdictStatus
{
    Passed,
    Failed,
    Blocked,
}

/// <summary>Upstream's <c>isRuntimeHardStop</c> (<c>agent/executor.ts</c>).</summary>
internal static class StepExecutors
{
    // The runtime's own hard stops are the step's truth wherever they land, so a replayed action surfaces them instead of handing off.
    public static bool IsRuntimeHardStop(E2EException error) =>
        error is AgentException { Code: "STEP_BUDGET_EXHAUSTED" or "STEP_TIMEOUT" or "CANCELLED" } or EngineException { Code: EngineErrorCodes.Cancelled };
}
