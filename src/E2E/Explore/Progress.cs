// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>What an exploration was given to spend: its step cap and its wall clock.</summary>
/// <param name="MaxSteps">Exploration steps, upstream's <c>maxSteps</c>.</param>
/// <param name="Timeout">The run's wall clock, upstream's <c>timeoutMs</c>.</param>
public sealed record ExploreBudgets(int MaxSteps, TimeSpan Timeout);

/// <summary>A step as opened: title and charter bounded to the report's ceilings, which is what the step runs with.</summary>
/// <param name="Index">One-based position of the step.</param>
/// <param name="Title">The area or flow, at most 200 characters.</param>
/// <param name="Instruction">The charter the agent runs, at most 2000 characters.</param>
public sealed record OpenedStep(int Index, string Title, string Instruction);

/// <summary>
/// The exploration's progress, one moment at a time: the step in progress, each finding the moment it is reported, and the
/// end. <see cref="ReportExplore"/> is the record; these are its moments. Upstream carries them as <c>explore</c> run
/// events, which its list reporter renders; the port has no run events, so <see cref="ExploreOptions.OnProgress"/> receives them.
/// </summary>
public abstract record ExploreProgress
{
    private protected ExploreProgress()
    {
    }

    /// <summary>The exploration begins: its test has started and the app is about to open.</summary>
    public sealed record Started(string Goal, ExploreBudgets Budgets) : ExploreProgress;

    /// <summary>The planner is deciding the next charter, or (<paramref name="Closing"/>) the assessment that ends the run.</summary>
    public sealed record Planning(bool Closing) : ExploreProgress;

    public sealed record StepStarted(OpenedStep Step) : ExploreProgress;

    public sealed record StepFinished(ReportExploreStep Step) : ExploreProgress;

    /// <summary>One finding, as recorded; its evidence screenshot, when kept, is the one <see cref="ReportExploreFinding.ArtifactId"/> names. Upstream's <c>finding</c> phase.</summary>
    public sealed record FindingReported(ReportExploreFinding Finding) : ExploreProgress;

    public sealed record Finished(ReportExploreEnded Ended, string? Summary = null) : ExploreProgress;
}

/// <summary>Receives every change of the exploration, in order.</summary>
public delegate void ExploreListener(ExploreProgress progress);
