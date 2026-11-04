// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using E2E.Reports;
using static E2E.Tests.Explore.ExploreHarness;
using Explorer = E2E.Explore;

namespace E2E.Tests.Explore;

/// <summary>
/// .NET-only: an exploration writes its progress to the process's run events as <see cref="ExploreEvent"/>, beside the <c>test-started</c> and
/// <c>test-finished</c> of its one test, as upstream's run does. Upstream's list reporter tests feed the reporter such a stream by hand (see
/// <c>ExploreRunsTests</c> in <c>E2E.Cli.Tests</c>); no upstream <c>it</c> covers the emitter, since upstream's runner owns it.
/// </summary>
public sealed class ExploreRunEventsTests
{
    [Fact]
    public async Task Emits_the_explore_moments_between_the_started_and_finished_events_of_the_exploration_test()
    {
        const string goal = "Explore the home page for the run events test";
        var facts = new ConcurrentQueue<RunEventFact>();
        using var listening = E2E.RunEvents.Subscribe(new RunEventSubscriber("test", runEvent => facts.Enqueue(runEvent.Fact)));
        var model = new ExploreModel(_ => ExploreModel.Finish("Looked."), _ => ExploreModel.Done("passed", "unused"));

        await Explorer.ExploreAsync(Options(TempRoot(), model.Model, [], goal: goal));

        // Other explorations in the process write to the same events: this one is the one with this goal.
        var started = facts.OfType<TestStartedEvent>().Single(fact => fact.Title == goal);
        Assert.Equal(Explorer.ExploreFile, started.File);
        var moments = facts.OfType<ExploreEvent>().Select(fact => fact.Progress).SkipWhile(progress => progress is not ExploreProgress.Started { Goal: goal }).ToList();
        Assert.IsType<ExploreProgress.Started>(moments[0]);
        Assert.IsType<ExploreProgress.Finished>(moments.First(progress => progress is ExploreProgress.Finished));
        Assert.Contains(facts.OfType<TestFinishedEvent>(), fact => fact.Result.Test.File == Explorer.ExploreFile && fact.Result.Test.TitlePath[0] == goal);
    }

    [Fact]
    public async Task Announces_the_run_and_its_plan_of_one_test_before_the_test_starts()
    {
        const string goal = "Explore the home page for the run start test";
        var facts = new ConcurrentQueue<RunEventFact>();
        using var listening = E2E.RunEvents.Subscribe(new RunEventSubscriber("test", runEvent => facts.Enqueue(runEvent.Fact)));
        var model = new ExploreModel(_ => ExploreModel.Finish("Looked."), _ => ExploreModel.Done("passed", "unused"));

        await Explorer.ExploreAsync(Options(TempRoot(), model.Model, [], goal: goal));

        // Other explorations in the process write to the same events: the last start before this goal's test is this exploration's.
        var all = facts.ToList();
        var testStarted = all.FindIndex(fact => fact is TestStartedEvent started && started.Title == goal);
        var plan = all.FindLastIndex(testStarted, fact => fact is PlanEvent);
        var runStarted = all.FindLastIndex(plan, fact => fact is RunStartedEvent);
        Assert.True(0 <= runStarted && runStarted < plan && plan < testStarted);
        var planned = Assert.IsType<PlanEvent>(all[plan]);
        Assert.Equal(1, planned.Total);
        Assert.Equal(Explorer.ExploreFile, Assert.Single(planned.Files).File);
    }

    // .NET-only: upstream redacts at its report boundary; the port's boundary is the explore state, which the list reporter reads through the run events.
    [Fact]
    public void Redacts_the_text_of_steps_paths_and_the_assessment_before_the_progress_event()
    {
        const string value = "step-Secret-9Lm2Qa";
        var moments = new List<ExploreProgress>();
        var state = new E2E.ExploreState("Explore", new ExploreBudgets(4, TimeSpan.FromMinutes(5)), redactor: E2E.Internal.Redactor.For([Secret.Create("probe", value)]));
        state.Subscribe(moments.Add);

        state.BeginStep("Sign in " + value, "Type " + value);
        state.AddFinding(new FindingInput(ReportExploreFindingKind.Warning, 1, "t", "e", "a", [], "/p/" + value));
        state.EndStep(ReportExploreStepStatus.Passed, "Saw " + value);
        state.End(ReportExploreEnded.Finished, "Assessment " + value);

        var text = string.Join('\n', moments.Select(moment => moment.ToString()));
        Assert.DoesNotContain(value, text, StringComparison.Ordinal);
        Assert.Contains("<secret:probe>", text, StringComparison.Ordinal);
    }
}
