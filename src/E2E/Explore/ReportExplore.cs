// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>Whether a finding fails the exploration.</summary>
public enum ReportExploreFindingKind
{
    /// <summary>A defect a user would hit. It fails the run.</summary>
    Issue,

    /// <summary>Cosmetic or polish. It is recorded and does not fail the run.</summary>
    Warning,
}

/// <summary>How an exploration step ended.</summary>
public enum ReportExploreStepStatus
{
    Passed,

    Failed,

    Blocked,

    /// <summary>The step ended at its action or time budget, which is not a failure.</summary>
    Exhausted,
}

/// <summary>Why an exploration stopped.</summary>
public enum ReportExploreEnded
{
    Finished,

    StepLimit,

    Time,

    Stuck,

    /// <summary>The run was cut short: a cancelled step, a failing provider, or a planner that failed twice in a row.</summary>
    Aborted,
}

/// <summary>One finding <see cref="Explore"/> recorded through its <c>report_finding</c> tool.</summary>
/// <param name="Id">A time-ordered unique id.</param>
/// <param name="Index">Zero-based position among the findings.</param>
/// <param name="Step">One-based exploration step the finding was reported in. Null when reported between steps.</param>
/// <param name="Kind">An issue fails the run; a warning is recorded and does not.</param>
/// <param name="Severity">5 is a core journey made impossible or data made wrong; 1 is a polish item.</param>
/// <param name="Title">A specific headline, at most 200 characters.</param>
/// <param name="Expected">What a user would expect, at most 2000 characters.</param>
/// <param name="Actual">What the screen shows instead, at most 2000 characters.</param>
/// <param name="Reproduction">The actions that reach the defect, at most 20, each at most 500 characters.</param>
/// <param name="ReportedAt">When the finding was recorded.</param>
/// <param name="Path">The redacted location of the screen, when the engine reports one.</param>
/// <param name="ObservationRevision">The revision of the screen the finding was reported on (<see cref="ExecutorObservation.Revision"/>), when the finding tool observed one.  The tool takes its own capture, as upstream does, so the revision is that capture's and is not one an agent step recorded.</param>
/// <param name="ArtifactId">Where the evidence screenshot was kept, as a path relative to the project root, when pixels were granted. Upstream names an artifact of the report; the port has no report, so it names the file.</param>
public sealed record ReportExploreFinding(
    string Id,
    int Index,
    int? Step,
    ReportExploreFindingKind Kind,
    int Severity,
    string Title,
    string Expected,
    string Actual,
    IReadOnlyList<string> Reproduction,
    DateTimeOffset ReportedAt,
    string? Path = null,
    string? ArtifactId = null,
    string? ObservationRevision = null);

/// <summary>One exploration step: a charter the agent planned and then executed as an <c>ActAsync</c> step.</summary>
/// <param name="Index">One-based position of the step.</param>
/// <param name="Title">The area or flow, at most 200 characters.</param>
/// <param name="Instruction">The charter the agent ran, at most 2000 characters.</param>
/// <param name="Status">How the step ended.</param>
/// <param name="StartedAt">When the step began.</param>
/// <param name="Duration">How long the step ran.</param>
/// <param name="Summary">What the step covered, at most 4000 characters.</param>
/// <param name="ErrorCode">The code of the error that ended the step, when one did.</param>
public sealed record ReportExploreStep(
    int Index,
    string Title,
    string Instruction,
    ReportExploreStepStatus Status,
    DateTimeOffset StartedAt,
    TimeSpan Duration,
    string? Summary = null,
    string? ErrorCode = null);

/// <summary>What an exploration was asked, did, and found: upstream's <c>run.explore</c> block of the report.</summary>
/// <param name="Goal">The goal, with the secret values it spelled out redacted.</param>
/// <param name="Budgets">The step cap and the clock the exploration ran under.</param>
/// <param name="Ended">Why exploration stopped.</param>
/// <param name="Steps">The steps the agent planned and ran, in order.</param>
/// <param name="Findings">The findings the agent reported, in order.</param>
/// <param name="Summary">The agent's closing assessment, when it gave one, at most 4000 characters.</param>
public sealed record ReportExplore(
    string Goal,
    ExploreBudgets Budgets,
    ReportExploreEnded Ended,
    IReadOnlyList<ReportExploreStep> Steps,
    IReadOnlyList<ReportExploreFinding> Findings,
    string? Summary = null);
