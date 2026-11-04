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
}
