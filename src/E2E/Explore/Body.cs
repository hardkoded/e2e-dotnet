// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace E2E;

/// <summary>
/// The two calls of the agent the exploration body makes. Upstream's body takes the <c>agent</c> fixture, an object a test can
/// replace with a script; <see cref="Agent"/> is sealed, so the body reads it through this seam, which only the exploration and
/// its tests implement.
/// </summary>
internal interface IExploreAgent
{
    Task<ActResult> ActAsync(string instruction, ActOptions? options, CancellationToken cancellationToken);

    Task<T> ExtractAsync<T>(string instruction, ExtractOptions? options, CancellationToken cancellationToken);
}

/// <summary>The fixtures the exploration body drives: upstream's <c>agent</c> and <c>app</c>.</summary>
/// <param name="Agent">The agent that plans and runs the steps.</param>
/// <param name="OpenAppAsync">Upstream's <c>app.open()</c>.</param>
internal sealed record ExploreFixtures(IExploreAgent Agent, Func<CancellationToken, Task> OpenAppAsync);

/// <summary>What the exploration body is built from.</summary>
/// <param name="State">The record the body writes.</param>
/// <param name="StepTimeout">Each step's own deadline, before the run's remaining time caps it.</param>
/// <param name="OpenApp">
/// Whether to open the app first. <c>App.OpenAsync</c> needs a target with a declared URL; a target without one is explored where
/// the last test left the app.
/// </param>
/// <param name="Credentials">
/// The configured accounts, by name. Each travels with every charter as a step secret, so the explorer can sign in with
/// <c>fill_secret</c> by name. The password itself never reaches the model. Upstream resolves them from its credential registry
/// when the body runs.
/// </param>
/// <param name="Agent">The agent the steps and the planner run as. Null is <c>default</c>.</param>
/// <param name="Time">The clock. Defaults to the system's.</param>
internal sealed record ExploreBodyOptions(
    ExploreState State,
    TimeSpan StepTimeout,
    bool OpenApp,
    IReadOnlyDictionary<string, UserCredential>? Credentials = null,
    string? Agent = null,
    TimeProvider? Time = null);

/// <summary>
/// The exploration body: the test <see cref="Explore"/> runs. A loop of ordinary <c>ActAsync</c> steps, one per charter the planner
/// writes, under the exploration's own budgets. The outcome policy is the one TesterArmy's discovery runs settled on: a step that
/// ends at its budget is a time-boxed charter and not a failure; a failed step is a finding and the run goes on; three failed or
/// blocked steps in a row, the step cap, or the clock end the run with a closing assessment; findings of kind <c>issue</c> fail
/// the run; a run that explored nothing and found nothing, or whose charters were all blocked, stays blocked.
/// </summary>
internal static class ExploreBody
{
    /// <summary>The most finding titles a step's <c>reportedFindings</c> lists, the newest kept: a long run's list must not grow the prompt without limit. Not in upstream.</summary>
    private const int MaxReportedFindings = 50;

    /// <summary>Failed or blocked steps in a row, none with a finding, that end the run: the approach is not working.</summary>
    public const int ConsecutiveFailureLimit = 3;

    /// <summary>Time kept back for the closing assessment once no step can start.</summary>
    private static readonly TimeSpan FinishReserve = TimeSpan.FromSeconds(60);

    /// <summary>The least time a step is worth starting with.</summary>
    private static readonly TimeSpan MinStep = TimeSpan.FromSeconds(45);

    /// <summary>The planner's deadline: one structured call plus a repair round, on a slow provider.</summary>
    private static readonly TimeSpan PlanTimeout = TimeSpan.FromSeconds(90);

    /// <summary>Below this, not even the closing assessment is asked for.</summary>
    private static readonly TimeSpan MinPlanTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Planner failures that are the model's, not the run's: no plan in the grammar, or a plan the model said it could not find.</summary>
    private static readonly HashSet<string> PlannerShortcomings = new(["MODEL_OUTPUT_INVALID", "ASSERTION_INCONCLUSIVE"], StringComparer.Ordinal);

    /// <summary>The first charter when the planner could not write one: look around.</summary>
    private static readonly PlanDecision.Step SurveyStep = new(
        "Survey the app",
        "Survey the app: visit each top-level page or screen the navigation offers once, note what each one is for and which controls it has, and try one obvious action on each. Report any defect you see on the way.");

    /// <summary>A later charter when the planner could not write one: carry on with what the goal names and no step covered.</summary>
    private static readonly PlanDecision.Step ContinueStep = new(
        "Continue exploring",
        "Continue the exploration: from the goal in the project context and the previously completed steps, pick the flow or screen the goal names that no step has covered yet, exercise it end to end with realistic inputs, and report any defect you see on the way.");

    /// <summary>Builds the test body for one exploration.</summary>
    public static Func<ExploreFixtures, CancellationToken, Task> Create(ExploreBodyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var state = options.State;
        var time = options.Time ?? TimeProvider.System;
        var credentials = options.Credentials ?? new Dictionary<string, UserCredential>();
        return async (fixtures, cancellationToken) =>
        {
            var deadline = time.GetUtcNow() + state.Budgets.Timeout;
            state.Start();
            if (options.OpenApp)
            {
                await fixtures.OpenAppAsync(cancellationToken).ConfigureAwait(false);
            }

            var loop = new Loop(
                fixtures.Agent,
                state,
                [.. credentials.Select(pair => new PlanAccount(pair.Key, pair.Value.Username))],
                credentials,
                options.Agent,
                options.StepTimeout,
                () => deadline - time.GetUtcNow());
            var ending = await RunStepsAsync(loop, cancellationToken).ConfigureAwait(false);
            state.End(ending.Ended, ending.Summary);
            Conclude(state);
        };
    }

    /// <summary>Why the loop stopped, with the closing assessment when the planner gave one.</summary>
    private sealed record Ending(ReportExploreEnded Ended, string? Summary = null);

    /// <summary>What the loop runs with.</summary>
    private sealed record Loop(
        IExploreAgent Agent,
        ExploreState State,
        IReadOnlyList<PlanAccount> Accounts,
        IReadOnlyDictionary<string, UserCredential> Credentials,
        string? AgentName,
        TimeSpan StepTimeout,
        Func<TimeSpan> Remaining);

    /// <summary>
    /// Plan, act, repeat, until something ends the run, and returns why. A cancelled step or a failing provider throws out
    /// instead, and the record keeps its <see cref="ReportExploreEnded.Aborted"/> default.
    /// </summary>
    private static async Task<Ending> RunStepsAsync(Loop loop, CancellationToken cancellationToken)
    {
        var state = loop.State;

        // Planner answers outside the grammar in a row; one is covered, two end the run.
        var plannerFailures = 0;
        while (true)
        {
            var stop = MustFinish(state, loop.Remaining());
            if (stop is not null && loop.Remaining() < MinPlanTimeout)
            {
                return new Ending(stop.Ended);
            }

            PlanDecision plan;
            state.Planning(stop is not null);
            try
            {
                plan = await ExplorePlan.PlanNextAsync(
                    loop.Agent,
                    state,
                    new PlanRequest(
                        stop is not null,
                        loop.Remaining(),
                        Max(MinPlanTimeout, Min(PlanTimeout, loop.Remaining())),
                        loop.Accounts,
                        stop?.Reason,
                        loop.AgentName),
                    cancellationToken).ConfigureAwait(false);
                plannerFailures = 0;
            }
            catch (AgentException cause) when (PlannerShortcomings.Contains(cause.Code))
            {
                // A model that cannot produce a plan in the grammar, even after the repair round, or answers that the screen shows
                // none (an extract's not-found), is a model shortcoming, not the end of the world: one failure is covered by a
                // built-in charter (a survey first, a continuation later); two in a row end the exploration with the record so
                // far; a failed closing assessment leaves the run to end for the reason it had to, without one. Anything else
                // (the provider, the clock, a cancellation) is the run's error.
                if (stop is not null)
                {
                    return new Ending(stop.Ended);
                }

                plannerFailures++;
                if (plannerFailures > 1)
                {
                    return new Ending(ReportExploreEnded.Aborted);
                }

                plan = state.Steps.Count == 0 ? SurveyStep : ContinueStep;
            }

            if (plan is PlanDecision.Finish finish)
            {
                return new Ending(stop?.Ended ?? ReportExploreEnded.Finished, finish.Summary);
            }

            // Asked to finish, the model planned a step anyway: the budget wins.
            if (stop is not null)
            {
                return new Ending(stop.Ended);
            }

            // The plan call took its own time; a step still has to fit before the reserve, or the run is out of time whatever the check above said.
            var room = loop.Remaining() - FinishReserve;
            if (room < MinStep)
            {
                return new Ending(ReportExploreEnded.Time);
            }

            var charter = (PlanDecision.Step)plan;
            var step = state.BeginStep(charter.Title, charter.Instruction);
            try
            {
                var result = await loop.Agent.ActAsync(
                    step.Instruction,
                    new ActOptions { Timeout = Min(loop.StepTimeout, room), Params = StepParams(state, loop.Credentials), Agent = loop.AgentName },
                    cancellationToken).ConfigureAwait(false);
                state.EndStep(ReportExploreStepStatus.Passed, result.Summary);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A caller cancel throws out, and the record keeps the step it ended in.
                state.EndStep(ReportExploreStepStatus.Blocked, null, "CANCELLED");
                throw;
            }
            catch (AgentException cause)
            {
                state.EndStep(StepStatus(cause), cause.Explanation, cause.Code);
                if (string.Equals(cause.Code, "CANCELLED", StringComparison.Ordinal))
                {
                    throw;
                }
            }
            catch (E2EException cause)
            {
                // An engine or app failure ends the run, and the record keeps the step it ended in.
                state.EndStep(ReportExploreStepStatus.Failed, cause.Message, cause.Code);
                throw;
            }
        }
    }

    /// <summary>Why the loop can no longer start a step, if it cannot.</summary>
    private static StopReason? MustFinish(ExploreState state, TimeSpan remaining)
    {
        if (state.Steps.Count >= state.Budgets.MaxSteps)
        {
            return new StopReason(ReportExploreEnded.StepLimit, "the step limit of " + state.Budgets.MaxSteps.ToString(CultureInfo.InvariantCulture) + " is reached");
        }

        var streak = state.ConsecutiveFailures();
        if (streak >= ConsecutiveFailureLimit)
        {
            return new StopReason(ReportExploreEnded.Stuck, streak.ToString(CultureInfo.InvariantCulture) + " steps failed in a row without a finding; exploration is stuck");
        }

        if (remaining < FinishReserve + MinStep)
        {
            return new StopReason(ReportExploreEnded.Time, "the time budget is nearly spent");
        }

        return null;
    }

    private sealed record StopReason(ReportExploreEnded Ended, string Reason);

    /// <summary>
    /// The step parameters a charter carries: the findings so far, so the agent neither reports one twice nor spends the step
    /// re-confirming it, and the configured accounts as secrets it can fill by name. Null when there is nothing to carry.
    /// </summary>
    private static Dictionary<string, object?>? StepParams(ExploreState state, IReadOnlyDictionary<string, UserCredential> credentials)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);
        var reported = state.Findings.Select(finding => finding.Title).TakeLast(MaxReportedFindings).ToList();
        if (reported.Count > 0)
        {
            parameters["reportedFindings"] = reported;
        }

        if (credentials.Count > 0)
        {
            parameters["credentials"] = credentials.ToDictionary(
                pair => pair.Key,
                pair => (object?)new Dictionary<string, object?> { ["username"] = pair.Value.Username, ["password"] = pair.Value.Password },
                StringComparer.Ordinal);
        }

        return parameters.Count == 0 ? null : parameters;
    }

    /// <summary>How a step that threw ended: a charter that ran out of room ended, it did not fail.</summary>
    private static ReportExploreStepStatus StepStatus(AgentException cause)
    {
        if (cause.Code is "STEP_BUDGET_EXHAUSTED" or "STEP_TIMEOUT")
        {
            return ReportExploreStepStatus.Exhausted;
        }

        return cause.Code == "CANCELLED" || cause.Blocked ? ReportExploreStepStatus.Blocked : ReportExploreStepStatus.Failed;
    }

    /// <summary>Turns the record into the run's verdict.</summary>
    private static void Conclude(ExploreState state)
    {
        var steps = state.Steps;
        if (steps.Count == 0 && state.Findings.Count == 0)
        {
            throw new AgentException("AUTOMATION_UNSUPPORTED", "exploration concluded nothing: no step ran and no finding was recorded", blocked: true);
        }

        var issues = state.Issues;
        if (issues.Count > 0)
        {
            var titles = string.Join("; ", issues.Select(finding => "[severity " + finding.Severity.ToString(CultureInfo.InvariantCulture) + "] " + finding.Title));
            throw new AgentException("ASSERTION_FAILED", "exploration found " + issues.Count.ToString(CultureInfo.InvariantCulture) + " issue(s): " + titles);
        }

        if (steps.Count > 0 && steps.All(step => step.Status == ReportExploreStepStatus.Blocked))
        {
            var first = steps[0];
            var code = first.ErrorCode is not null && StepExecutors.BlockableCodes.Contains(first.ErrorCode) ? first.ErrorCode : "AUTOMATION_UNSUPPORTED";
            throw new AgentException(code, "exploration could not complete a charter: " + (first.Summary ?? first.Title), blocked: true);
        }
    }

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left < right ? left : right;

    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left > right ? left : right;
}
