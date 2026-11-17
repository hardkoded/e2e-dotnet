// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>
/// What <see cref="CoreAgents.CreateAgent"/> reads off a session it did not create: the attempt behind it, the host's project root, and the
/// session's close hooks. Upstream's <c>SessionInternals</c> (<c>core/internals.ts</c>), which keeps it in a <c>WeakMap</c> so that a session
/// object carries none of it; the port's <see cref="Session"/> holds it in an internal member.
/// </summary>
internal sealed class SessionInternals(StandaloneAttempt attempt, string projectRoot, FailureWatch failures)
{
    public StandaloneAttempt Attempt { get; } = attempt;

    /// <summary>The directory the host was launched from, which a relative cache directory resolves against. Upstream's <c>config.projectRoot</c>.</summary>
    public string ProjectRoot { get; } = projectRoot;

    /// <summary>Run in order when the session closes, before its attempt ends: an agent settles its replay cache here.</summary>
    public List<Func<Task>> Closers { get; } = [];

    /// <summary>How the session's steps went, as the replay cache settles what its agents recorded.</summary>
    public AttemptSettlement Settlement() => failures.Settlement(Attempt.Session.Recorder);
}

/// <summary>
/// Watches a session's steps for the agent steps that failed. A test ends at its failure; a program may catch one and go on. So each failure
/// settles only the recordings it found unconfirmed, as a run's failure does: dropped, unless only a missing model explains it. Recordings made
/// after it are confirmed by the checks that pass after them. Upstream's <c>FailureWatch</c> (<c>core/internals.ts</c>).
/// </summary>
internal sealed class FailureWatch
{
    private readonly List<SurvivedFailure> _failures = [];
    private StepRecorder? _steps;

    /// <summary>The <c>onProgress</c> hook the session's attempt reports its steps to.</summary>
    public void OnProgress(StepProgress progress)
    {
        // Only an agent step's failure implicates recordings: a program catches a locator's timeout on purpose, to branch.
        if (
            progress.Phase != StepProgressPhase.End
            || progress.Kind != StepKind.Agent
            || progress.Status is null or StepStatus.Passed
            || _steps is null
        )
        {
            return;
        }

        var code = progress.Error?.Code;
        lock (_failures)
        {
            _failures.Add(
                new SurvivedFailure(
                    _steps.LastVerifiedStepIndex,
                    progress.Identity?.StepIndex ?? int.MaxValue,
                    code is null || !AgentError.ModelUnreachableCodes.Contains(code)
                )
            );
        }
    }

    public void Watch(StepRecorder steps) => _steps = steps;

    /// <summary>
    /// How the session's recordings settle when it closes. A close is not a verdict: a program may end, or be interrupted, between a replay and
    /// the check after it, so what no check confirmed is left as it was, neither written nor evicted.
    /// </summary>
    public AttemptSettlement Settlement(StepRecorder steps)
    {
        lock (_failures)
        {
            return new AttemptSettlement(steps.LastVerifiedStepIndex, false, [.. _failures]);
        }
    }
}
