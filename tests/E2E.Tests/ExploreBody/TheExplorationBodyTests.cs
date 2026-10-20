// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using Microsoft.Extensions.Time.Testing;
using Body = E2E.ExploreBody;
using State = E2E.ExploreState;

namespace E2E.Tests.ExploreBody;

/// <summary>An agent that replays a script and records what the body asked. The C# shape of upstream's fixture objects.</summary>
internal sealed class ScriptedExploreAgent(IEnumerable<PlanAnswer> plans, IEnumerable<ActOutcome>? acts = null) : IExploreAgent
{
    private readonly Queue<PlanAnswer> _plans = new(plans);
    private readonly Queue<ActOutcome> _acts = new(acts ?? []);

    public List<string> PlanInstructions { get; } = [];

    public List<(string Instruction, TimeSpan? Timeout)> ActCalls { get; } = [];

    public List<IReadOnlyDictionary<string, object?>?> ActParams { get; } = [];

    public Action? OnAct { get; init; }

    public Task<ActResult> ActAsync(string instruction, ActOptions? options, CancellationToken cancellationToken)
    {
        ActCalls.Add((instruction, options?.Timeout));
        ActParams.Add(options?.Params);
        OnAct?.Invoke();
        if (!_acts.TryDequeue(out var outcome))
        {
            throw new InvalidOperationException("unscripted act " + (ActCalls.Count));
        }

        if (outcome.Throws is not null)
        {
            throw outcome.Throws;
        }

        // Findings a real step would report through the tool land elsewhere.
        return Task.FromResult(new ActResult { Summary = outcome.Summary!, ModelCalls = 3, Actions = 2 });
    }

    public Task<T> ExtractAsync<T>(string instruction, ExtractOptions? options, CancellationToken cancellationToken)
    {
        PlanInstructions.Add(instruction);
        if (!_plans.TryDequeue(out var answer))
        {
            throw new InvalidOperationException("unscripted planner call " + (PlanInstructions.Count));
        }

        return Task.FromResult((T)(object)answer);
    }
}

internal sealed record ActOutcome(string? Summary = null, AgentException? Throws = null)
{
    public static ActOutcome Passed(string summary) => new(summary);

    public static ActOutcome Fails(AgentException error) => new(Throws: error);
}

internal static class PlanScript
{
    public static PlanAnswer Step(string title, string instruction) => new(PlanKind.Step, title, instruction, "");

    public static PlanAnswer Finish(string summary) => new(PlanKind.Finish, "", "", summary);
}

public sealed class TheExplorationBodyTests
{
    private static readonly ExploreBudgets Budgets = new(4, TimeSpan.FromMilliseconds(600_000));

    private static readonly TimeSpan StepTimeout = TimeSpan.FromMilliseconds(240_000);

    private static Task RunAsync(ExploreBodyOptions options, IExploreAgent agent, List<string>? opened = null) =>
        Body.Create(options)(new ExploreFixtures(agent, _ =>
        {
            opened?.Add("/");
            return Task.CompletedTask;
        }), CancellationToken.None);

    private static ExploreBodyOptions Options(State state, bool openApp = true, IReadOnlyDictionary<string, UserCredential>? credentials = null, TimeProvider? time = null) =>
        new(state, StepTimeout, openApp, credentials, Time: time);

    private static AgentException Blocked(string code, string message) => new(code, message, blocked: true);

    [Fact]
    public async Task Opens_the_app_runs_the_planned_steps_and_finishes_with_the_assessment()
    {
        var state = new State("goal", Budgets);
        var agent = new ScriptedExploreAgent(
            [PlanScript.Step("Cart", "Add two items and open the cart"), PlanScript.Step("Checkout", "Pay for the cart"), PlanScript.Finish("Both flows work.")],
            [ActOutcome.Passed("Cart holds two items"), ActOutcome.Passed("Paid")]);
        var opened = new List<string>();
        await RunAsync(Options(state), agent, opened);
        Assert.Equal(["/"], opened);
        Assert.Equal(["Add two items and open the cart", "Pay for the cart"], agent.ActCalls.Select(call => call.Instruction));
        Assert.Equal(StepTimeout, agent.ActCalls[0].Timeout);
        Assert.Equal(
            [("Cart", ReportExploreStepStatus.Passed, "Cart holds two items"), ("Checkout", ReportExploreStepStatus.Passed, "Paid")],
            state.Steps.Select(step => (step.Title, step.Status, step.Summary)));
        Assert.Equal("Both flows work.", state.Summary);
        Assert.Equal(ReportExploreEnded.Finished, state.Ended);
    }

    [Fact]
    public async Task Keeps_exploring_after_a_failed_step_and_fails_the_run_only_for_issues()
    {
        var state = new State("goal", Budgets);
        var agent = new ScriptedExploreAgent(
            [PlanScript.Step("Pay", "Pay for the cart"), PlanScript.Step("Search", "Search for shoes"), PlanScript.Finish("Payment is broken.")],
            [ActOutcome.Fails(new AgentException("ACTION_FAILED", "agent.act failed: the pay button does nothing")), ActOutcome.Passed("Search works")]);
        await RunAsync(Options(state), agent);
        Assert.Equal([ReportExploreStepStatus.Failed, ReportExploreStepStatus.Passed], state.Steps.Select(step => step.Status));
        Assert.Equal("ACTION_FAILED", state.Steps[0].ErrorCode);
        Assert.Equal(ReportExploreEnded.Finished, state.Ended);
    }

    [Fact]
    public async Task Treats_a_step_that_ran_out_of_budget_or_time_as_ended_not_failed()
    {
        var state = new State("goal", Budgets);
        var agent = new ScriptedExploreAgent(
            [PlanScript.Step("Long", "Tour every page"), PlanScript.Step("Slow", "Wait for the report"), PlanScript.Finish("Covered.")],
            [ActOutcome.Fails(Blocked("STEP_BUDGET_EXHAUSTED", "agent.act blocked: out of actions")), ActOutcome.Fails(Blocked("STEP_TIMEOUT", "agent.act blocked: out of time"))]);
        await RunAsync(Options(state), agent);
        Assert.Equal([ReportExploreStepStatus.Exhausted, ReportExploreStepStatus.Exhausted], state.Steps.Select(step => step.Status));
        Assert.Equal(0, state.ConsecutiveFailures());
    }

    [Fact]
    public async Task Asks_for_the_assessment_once_steps_keep_failing_and_records_why_it_stopped()
    {
        var state = new State("goal", new ExploreBudgets(8, TimeSpan.FromMilliseconds(600_000)));
        var failing = Enumerable.Range(1, Body.ConsecutiveFailureLimit).Select(index => PlanScript.Step("Try " + index, "attempt " + index));
        var agent = new ScriptedExploreAgent(
            [.. failing, PlanScript.Finish("Nothing worked.")],
            [
                ActOutcome.Fails(new AgentException("ACTION_FAILED", "failed once")),
                ActOutcome.Fails(Blocked("AUTH_CREDENTIAL_UNAVAILABLE", "no credential")),
                ActOutcome.Fails(new AgentException("ASSERTION_FAILED", "failed again")),
            ]);
        await RunAsync(Options(state), agent);
        Assert.Equal([ReportExploreStepStatus.Failed, ReportExploreStepStatus.Blocked, ReportExploreStepStatus.Failed], state.Steps.Select(step => step.Status));
        Assert.Contains("3 steps failed in a row without a finding; exploration is stuck", agent.PlanInstructions[^1], StringComparison.Ordinal);
        Assert.Equal(ReportExploreEnded.Stuck, state.Ended);
        Assert.Equal("Nothing worked.", state.Summary);
    }

    [Fact]
    public async Task Stops_at_the_step_limit_even_when_the_planner_proposes_another_step()
    {
        var state = new State("goal", new ExploreBudgets(1, TimeSpan.FromMilliseconds(600_000)));
        var agent = new ScriptedExploreAgent(
            [PlanScript.Step("One", "do the one thing"), PlanScript.Step("Two", "the model ignores the limit")],
            [ActOutcome.Passed("done")]);
        await RunAsync(Options(state), agent);
        Assert.Single(agent.ActCalls);
        Assert.Contains("the step limit of 1 is reached", agent.PlanInstructions[^1], StringComparison.Ordinal);
        Assert.Equal(ReportExploreEnded.StepLimit, state.Ended);
        Assert.Null(state.Summary);
    }

    [Fact]
    public async Task Caps_a_step_at_the_time_left_and_ends_on_the_clock()
    {
        var clock = new FakeTimeProvider();
        var state = new State("goal", new ExploreBudgets(8, TimeSpan.FromMilliseconds(300_000)), clock);
        var agent = new ScriptedExploreAgent(
            [PlanScript.Step("Tour", "tour the app"), PlanScript.Finish("Time is up.")],
            [ActOutcome.Passed("toured")])
        {
            // The step eats most of the clock.
            OnAct = () => clock.Advance(TimeSpan.FromMilliseconds(200_000)),
        };
        await RunAsync(Options(state, time: clock), agent);

        // 300 s left minus the 60 s finish reserve.
        Assert.Equal([TimeSpan.FromMilliseconds(240_000)], agent.ActCalls.Select(call => call.Timeout));
        Assert.Equal(2, agent.PlanInstructions.Count);
        Assert.Contains("the time budget is nearly spent", agent.PlanInstructions[1], StringComparison.Ordinal);
        Assert.Equal(ReportExploreEnded.Time, state.Ended);
        Assert.Equal("Time is up.", state.Summary);
    }

    [Fact]
    public async Task Skips_the_app_open_when_told_the_target_has_no_URL_and_ends_on_the_clock_when_a_slow_plan_leaves_no_room_for_a_step()
    {
        var clock = new FakeTimeProvider();
        var state = new State("goal", new ExploreBudgets(8, TimeSpan.FromMilliseconds(300_000)), clock);
        var opened = new List<string>();

        // The plan alone eats what was left above the reserve.
        var agent = new SlowPlanner(clock, PlanScript.Step("Late", "too late to start"));

        // Nothing ran and nothing was found, so the body concludes blocked. The clock is what this test is about.
        var error = await Assert.ThrowsAsync<AgentException>(() => RunAsync(Options(state, openApp: false, time: clock), agent, opened));
        Assert.Equal("AUTOMATION_UNSUPPORTED", error.Code);
        Assert.Empty(opened);
        Assert.Empty(agent.ActCalls);
        Assert.Empty(state.Steps);
        Assert.Equal(ReportExploreEnded.Time, state.Ended);
    }

    [Fact]
    public async Task Hands_the_configured_credentials_to_every_charter_as_secrets_and_tells_the_planner_which_accounts_exist()
    {
        var state = new State("goal", Budgets);
        var password = Secret.Create("ada.password", "bookworm", "password");
        var credentials = new Dictionary<string, UserCredential> { ["ada"] = new("ada@example.test", password) };
        var agent = new ScriptedExploreAgent(
            [PlanScript.Step("Sign in", "sign in as ada"), PlanScript.Finish("Signed in fine.")],
            [ActOutcome.Passed("signed in")]);
        await RunAsync(Options(state, credentials: credentials), agent);
        Assert.Contains("- ada (username: ada@example.test)", agent.PlanInstructions[0], StringComparison.Ordinal);
        Assert.DoesNotContain("bookworm", agent.PlanInstructions[0], StringComparison.Ordinal);
        var parameters = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(agent.ActParams[0]);
        var carried = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(parameters["credentials"])["ada"]);
        Assert.Equal("ada@example.test", carried["username"]);
        Assert.IsType<Secret>(carried["password"]);
        Assert.DoesNotContain("bookworm", ActParams.Projected(parameters)!.ToJsonString(), StringComparison.Ordinal);
    }

    // .NET-only: upstream lists every title.
    [Fact]
    public async Task Carries_only_the_newest_finding_titles_to_a_charter()
    {
        var state = new State("goal", Budgets);
        for (var number = 1; number <= 60; number++)
        {
            state.AddFinding(new FindingInput(ReportExploreFindingKind.Warning, 2, "Finding " + number, "aligned", "off", ["look"]));
        }

        var agent = new ScriptedExploreAgent([PlanScript.Step("Cart", "open the cart"), PlanScript.Finish("Done.")], [ActOutcome.Passed("cart opened")]);
        await RunAsync(Options(state), agent);
        var titles = Assert.IsAssignableFrom<IReadOnlyList<string>>(Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(agent.ActParams[0])["reportedFindings"]);
        Assert.Equal(50, titles.Count);
        Assert.Equal("Finding 11", titles[0]);
        Assert.Equal("Finding 60", titles[^1]);
    }

    [Fact]
    public async Task Fails_the_run_for_issues_naming_them()
    {
        var state = new State("goal", Budgets);
        var agent = new ScriptedExploreAgent(
            [PlanScript.Step("Cart", "open the cart"), PlanScript.Finish("Broken cart.")],
            [ActOutcome.Passed("cart opened")]);

        // The finding tool records findings during a step. Here they are seeded ahead of the run.
        state.AddFinding(new FindingInput(ReportExploreFindingKind.Issue, 4, "Total shows $0.00", "a total", "$0.00", ["open the cart"]));
        state.AddFinding(new FindingInput(ReportExploreFindingKind.Warning, 2, "Icon misaligned", "aligned", "off by 2px", ["look"]));
        var error = await Assert.ThrowsAsync<AgentException>(() => RunAsync(Options(state), agent));
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Contains("exploration found 1 issue(s): [severity 4] Total shows $0.00", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Is_blocked_never_a_pass_when_nothing_ran_and_nothing_was_found()
    {
        var state = new State("goal", Budgets);
        var agent = new ScriptedExploreAgent([PlanScript.Finish("Nothing to explore here.")]);
        var error = await Assert.ThrowsAsync<AgentException>(() => RunAsync(Options(state), agent));
        Assert.Equal("AUTOMATION_UNSUPPORTED", error.Code);
        Assert.True(error.Blocked);
        Assert.Equal(ReportExploreEnded.Finished, state.Ended);
    }

    [Fact]
    public async Task Keeps_the_first_blocker_when_every_charter_was_blocked_with_warnings()
    {
        var state = new State("goal", Budgets);
        var agent = new ScriptedExploreAgent(
            [PlanScript.Step("Account", "Open the account"), PlanScript.Step("Orders", "Open the orders"), PlanScript.Finish("Neither area was reachable.")],
            [ActOutcome.Fails(Blocked("AUTH_CREDENTIAL_UNAVAILABLE", "Account needs a configured login")), ActOutcome.Fails(Blocked("ENVIRONMENT_UNAVAILABLE", "Orders service is down"))]);
        state.AddFinding(new FindingInput(ReportExploreFindingKind.Warning, 2, "Icon misaligned", "aligned", "off by 2px", ["look"]));

        var error = await Assert.ThrowsAsync<AgentException>(() => RunAsync(Options(state), agent));
        Assert.Equal("AUTH_CREDENTIAL_UNAVAILABLE", error.Code);
        Assert.True(error.Blocked);
        Assert.Contains("Account needs a configured login", error.Message, StringComparison.Ordinal);
        Assert.Equal([ReportExploreStepStatus.Blocked, ReportExploreStepStatus.Blocked], state.Steps.Select(step => step.Status));
        Assert.Equal(ReportExploreEnded.Finished, state.Ended);
    }

    [Fact]
    public async Task Keeps_the_issues_only_verdict_after_a_failed_charter_and_a_blocked_charter()
    {
        var state = new State("goal", Budgets);
        var outcome = ActOutcome.Fails(new AgentException("ACTION_FAILED", "Search could not complete"));
        var agent = new ScriptedExploreAgent(
            [PlanScript.Step("Search", "Search the catalog"), PlanScript.Step("Account", "Open the account"), PlanScript.Finish("Search was explored; account needs a login.")],
            [outcome, ActOutcome.Fails(Blocked("AUTH_CREDENTIAL_UNAVAILABLE", "No login configured"))]);
        await RunAsync(Options(state), agent);
        Assert.Equal([ReportExploreStepStatus.Failed, ReportExploreStepStatus.Blocked], state.Steps.Select(step => step.Status));
    }

    [Fact]
    public async Task Fails_for_a_reported_issue_even_when_every_charter_was_blocked()
    {
        var state = new State("goal", Budgets);
        var agent = new ScriptedExploreAgent(
            [PlanScript.Step("Account", "Open the account"), PlanScript.Finish("Account was blocked.")],
            [ActOutcome.Fails(Blocked("AUTH_CREDENTIAL_UNAVAILABLE", "No login configured"))]);
        state.AddFinding(new FindingInput(ReportExploreFindingKind.Issue, 4, "Total shows $0.00", "a total", "$0.00", ["open the cart"]));
        var error = await Assert.ThrowsAsync<AgentException>(() => RunAsync(Options(state), agent));
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.False(error.Blocked);
    }

    [Fact]
    public async Task Falls_back_to_a_survey_when_the_first_plan_is_not_in_the_grammar_and_stops_on_a_later_one()
    {
        var first = new State("goal", Budgets);
        await RunAsync(Options(first), new FailingPlanner(
            [Invalid, PlanScript.Finish("Surveyed.")],
            [ActOutcome.Passed("toured")]));
        Assert.Equal(["Survey the app"], first.Steps.Select(step => step.Title));
        Assert.Equal(ReportExploreEnded.Finished, first.Ended);

        // One mid-run failure is covered by a continuation charter; a second in a row ends the exploration.
        var later = new State("goal", Budgets);
        await RunAsync(Options(later), new FailingPlanner(
            [PlanScript.Step("Cart", "open the cart"), Invalid, Invalid],
            [ActOutcome.Passed("opened"), ActOutcome.Passed("opened")]));
        Assert.Equal(["Cart", "Continue exploring"], later.Steps.Select(step => step.Title));
        Assert.Equal(ReportExploreEnded.Aborted, later.Ended);
        Assert.Null(later.Summary);

        // A planner that recovers after one failure keeps going.
        var recovering = new State("goal", Budgets);
        await RunAsync(Options(recovering), new FailingPlanner(
            [PlanScript.Step("Cart", "open the cart"), Invalid, PlanScript.Step("Orders", "open orders"), PlanScript.Finish("Recovered and done.")],
            [ActOutcome.Passed("ok"), ActOutcome.Passed("ok"), ActOutcome.Passed("ok")]));
        Assert.Equal(["Cart", "Continue exploring", "Orders"], recovering.Steps.Select(step => step.Title));
        Assert.Equal(ReportExploreEnded.Finished, recovering.Ended);

        // Asked to finish, the model answers outside the grammar: the run ends for its own reason, without an assessment.
        var closing = new State("goal", new ExploreBudgets(1, TimeSpan.FromMilliseconds(600_000)));
        await RunAsync(Options(closing), new FailingPlanner(
            [PlanScript.Step("Only", "the one step"), Invalid],
            [ActOutcome.Passed("done")]));
        Assert.Single(closing.Steps);
        Assert.Equal(ReportExploreEnded.StepLimit, closing.Ended);
        Assert.Null(closing.Summary);
    }

    [Fact]
    public async Task Covers_a_planner_that_answers_not_found_like_one_outside_the_grammar()
    {
        var state = new State("goal", Budgets);
        await RunAsync(Options(state), new FailingPlanner(
            [NotFound, PlanScript.Finish("Surveyed.")],
            [ActOutcome.Passed("toured")]));
        Assert.Equal(["Survey the app"], state.Steps.Select(step => step.Title));
        Assert.Equal(ReportExploreEnded.Finished, state.Ended);
    }

    [Fact]
    public async Task Surfaces_a_cancelled_step_and_a_planner_failure_as_the_run_error()
    {
        var cancelled = new State("goal", Budgets);
        var error = await Assert.ThrowsAsync<AgentException>(() => RunAsync(
            Options(cancelled),
            new ScriptedExploreAgent([PlanScript.Step("Cart", "open the cart")], [ActOutcome.Fails(new AgentException("CANCELLED", "the step was cancelled"))])));
        Assert.Equal("CANCELLED", error.Code);
        Assert.Equal(ReportExploreStepStatus.Blocked, cancelled.Steps[0].Status);
        Assert.Equal(ReportExploreEnded.Aborted, cancelled.Ended);

        var broken = new State("goal", Budgets);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(Options(broken), new ScriptedExploreAgent([])));
        Assert.Matches("unscripted planner call", failure.Message);
        Assert.Equal(ReportExploreEnded.Aborted, broken.Ended);
    }

    [Fact]
    public async Task Records_the_open_step_as_blocked_when_the_caller_cancels()
    {
        var state = new State("goal", Budgets);
        using var cts = new CancellationTokenSource();
        var agent = new ScriptedExploreAgent([PlanScript.Step("Cart", "open the cart")], [ActOutcome.Passed("never")])
        {
            OnAct = () =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            },
        };
        await Assert.ThrowsAsync<OperationCanceledException>(() => Body.Create(Options(state))(new ExploreFixtures(agent, _ => Task.CompletedTask), cts.Token));
        var step = Assert.Single(state.Steps);
        Assert.Equal(ReportExploreStepStatus.Blocked, step.Status);
        Assert.Equal("CANCELLED", step.ErrorCode);
    }

    // Stands for an answer the model gave outside the grammar, and for one that says the screen shows no plan.
    private static readonly PlanAnswer Invalid = PlanScript.Step("", "");

    private static readonly PlanAnswer NotFound = PlanScript.Step("", "");

    /// <summary>A planner whose <see cref="Invalid"/> and <see cref="NotFound"/> answers throw the error the model's failure would.</summary>
    private sealed class FailingPlanner(IEnumerable<PlanAnswer> plans, IEnumerable<ActOutcome> acts) : IExploreAgent
    {
        private readonly Queue<PlanAnswer> _plans = new(plans);
        private readonly Queue<ActOutcome> _acts = new(acts);

        public Task<ActResult> ActAsync(string instruction, ActOptions? options, CancellationToken cancellationToken) =>
            Task.FromResult(new ActResult { Summary = _acts.Dequeue().Summary!, ModelCalls = 1, Actions = 1 });

        public Task<T> ExtractAsync<T>(string instruction, ExtractOptions? options, CancellationToken cancellationToken)
        {
            var answer = _plans.Dequeue();
            if (ReferenceEquals(answer, Invalid))
            {
                throw new AgentException("MODEL_OUTPUT_INVALID", "extracted data failed schema validation");
            }

            return ReferenceEquals(answer, NotFound)
                ? throw new AgentException("ASSERTION_INCONCLUSIVE", "nothing to extract: no plan is on screen")
                : Task.FromResult((T)(object)answer);
        }
    }

    /// <summary>A planner whose one call takes most of the clock.</summary>
    private sealed class SlowPlanner(FakeTimeProvider clock, PlanAnswer answer) : IExploreAgent
    {
        public List<string> ActCalls { get; } = [];

        public Task<ActResult> ActAsync(string instruction, ActOptions? options, CancellationToken cancellationToken)
        {
            ActCalls.Add(instruction);
            return Task.FromResult(new ActResult { Summary = "never", ModelCalls = 1, Actions = 1 });
        }

        public Task<T> ExtractAsync<T>(string instruction, ExtractOptions? options, CancellationToken cancellationToken)
        {
            clock.Advance(TimeSpan.FromMilliseconds(240_000));
            return Task.FromResult((T)(object)answer);
        }
    }
}
