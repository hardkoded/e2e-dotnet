// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Reports;

/// <summary>What a progress notification says (upstream's <c>StepProgress['phase']</c>).</summary>
public enum StepProgressPhase
{
    Start,
    End,
    Event,

    /// <summary>What the running step waits on right now. Emitted as each begins; the matching event reports its end.</summary>
    Activity,

    /// <summary>The trace cache took the step (<see cref="StepProgress.Active"/>), or handed it to the model after a replay that could not finish it.</summary>
    Replay,
}

/// <summary>The work a running agent step can be waiting on (upstream's <c>StepActivity</c>).</summary>
public enum StepActivity
{
    Observe,
    Model,
    Action,
}

/// <summary>Whose progress a notification is: IDs join live progress to the report. Step indexes are local to each test (upstream's <c>StepProgressIdentity</c>).</summary>
public sealed record StepProgressIdentity(string AttemptId, int AttemptIndex, string StepId, int StepIndex);

/// <summary>
/// Live progress notification for one step, streamed as the step runs (upstream's <c>StepProgress</c>, <c>run/steps.ts</c>). Derived from the same
/// records the report persists; the report stays the canonical record. Which members are set depends on <see cref="Phase"/>. It lives in
/// <c>E2E.Reports</c> with the report types it uses, because the port's own records for the same parts keep upstream's names in <c>E2E</c>.
/// </summary>
public sealed record StepProgress
{
    public StepProgressPhase Phase { get; init; }

    public StepProgressIdentity? Identity { get; init; }

    public string Api { get; init; } = "";

    /// <summary>Start and end.</summary>
    public StepKind? Kind { get; init; }

    /// <summary>Start and end.</summary>
    public string? Label { get; init; }

    /// <summary>End.</summary>
    public StepStatus? Status { get; init; }

    /// <summary>End.</summary>
    public long DurationMs { get; init; }

    /// <summary>End.</summary>
    public int ModelCalls { get; init; }

    /// <summary>End: the same redacted error the finished step records.</summary>
    public ReportError? Error { get; init; }

    /// <summary>End.</summary>
    public string? Explanation { get; init; }

    /// <summary>Event.</summary>
    public StepEvent? Event { get; init; }

    /// <summary>Activity.</summary>
    public StepActivity? Activity { get; init; }

    /// <summary>Replay: whether the trace cache has the step.</summary>
    public bool Active { get; init; }
}
