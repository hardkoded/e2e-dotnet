// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentTraceCache;

/// <summary>
/// Upstream's <c>describe('trace cache: a replayed typed value is the flow\'s data on an app that keeps state')</c>, run in sessions over
/// one cache directory through the real web engine. Upstream's recorded action names <c>type</c> and <c>tap</c> are the port's
/// <c>fill</c> and <c>tap</c> kinds, and a <c>tool</c> gap keeps its text <c>type (run-time value)</c> where upstream's summary
/// reads <c>tool type (run-time value)</c>.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class TraceCacheAReplayedTypedValueIsTheFlowsDataOnAnAppThatKeepsStateTests
{
    // Types a todo it composes itself, "Buy milk", then taps Add: the shape of a model that made up the flow's data. The value is in
    // neither the instruction nor the params, so only the screen could make it look derived. No model.
    private static TestExecutor TodoExecutor(Action onRun) =>
        new("todo-executor", async (context, token) =>
        {
            onRun();
            var observation = await context.ObserveAsync(cancellationToken: token);
            await context.Actions.TypeAsync(TargetFor(observation.Text, "textbox \"New todo\""), "Buy milk", token);
            await context.Actions.TapAsync(TargetFor(observation.Text, "button \"Add\""), token);
            observation = await context.ObserveAsync(cancellationToken: token);
            return Regex.IsMatch(observation.Text, "status \"Result\"[^\n]*text=\"added\"")
                ? Passed("the list shows Buy milk")
                : Failed("the todo was not added");
        })
        {
            Version = "test",
        };

    private static async Task<(ActResult Result, StepRecord Step)> RunAsync(string directory, string appUrl, Action onRun)
    {
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(headless: true),
            AgentExecutor = TodoExecutor(onRun),
            BaseUrl = appUrl,
            AppIdentity = "fixture-app",
            Cache = new FileStepCache(directory),
            CacheEnabled = true,
            CacheMode = CacheMode.ReadWrite,
            TestTitle = "adds a todo",
            ActionTimeout = TimeSpan.FromSeconds(10),
            StepTimeout = TimeSpan.FromSeconds(30),
            TestTimeout = TimeSpan.FromSeconds(60),
        });
        await session.App.OpenAsync("/todos");
        var result = await session.Agent.ActAsync("add a todo for groceries");
        await Expect.That(session.Screen.GetByRole("list", "Todos")).ToContainTextAsync("Buy milk");
        session.Complete();
        return (result, session.StepRecords.Single(candidate => candidate.Api == "agent.act"));
    }

    [Fact]
    public async Task Replays_the_typed_value_on_the_second_and_third_run_although_the_first_run_left_it_on_screen()
    {
        var directory = TwoTapCounter.NewDirectory();
        using var pages = await TraceCacheStatePages.StartAsync();
        var runs = 0;
        await RunAsync(directory, pages.Url, () => runs++);

        var recorded = TwoTapCounter.OnlyEntry(directory).Actions;
        Assert.Equal(["fill", "tap"], recorded.Select(action => action.Kind));
        Assert.Equal("Buy milk", recorded[0].Value);

        // The list now shows "Buy milk" before the step starts: the start-path probe puts it among the texts the screen showed, where a
        // live type of the same value would count as read off the screen.
        foreach (var run in new[] { 2, 3 })
        {
            runs = 0;
            var (result, step) = await RunAsync(directory, pages.Url, () => runs++);

            Assert.True(runs == 0, $"run {run}");
            Assert.True(result.Cache is { Mode: "self-finalized", ReplayedActions: 2, TotalActions: 2 }, $"run {run}: {result.Cache?.Mode} {result.Cache?.Reason}");
            Assert.Equal(0, step.Metrics!.ModelCalls);
            var restaged = TwoTapCounter.OnlyEntry(directory).Actions;
            Assert.True(
                Serialized(recorded) == Serialized(restaged),
                $"run {run}: the entry was rewritten");
        }
    }

    [Fact]
    public async Task Still_records_a_gap_when_the_executor_itself_types_a_value_the_screen_shows()
    {
        using var pages = await TraceCacheStatePages.StartAsync();

        // Upstream's first `it` leaves "Buy milk" on the app's list; this one needs it on screen on its own.
        await pages.SeedTodoAsync("Buy milk");

        // A fresh cache directory against the same app: no entry, so the executor runs live and types "Buy milk" while the list already
        // shows it.
        var directory = TwoTapCounter.NewDirectory();
        var runs = 0;
        await RunAsync(directory, pages.Url, () => runs++);

        Assert.Equal(1, runs);
        var actions = TwoTapCounter.OnlyEntry(directory).Actions;
        Assert.Equal(["tool", "tap"], actions.Select(action => action.Kind));
        Assert.Equal(("type (run-time value)", DerivedReason.WholeNode), (actions[0].Text, actions[0].Derived));

        runs = 0;
        var (second, _) = await RunAsync(directory, pages.Url, () => runs++);

        Assert.Equal(1, runs);
        Assert.Equal(("missed", "gap", 2), (second.Cache?.Mode, second.Cache?.Reason, second.Cache?.TotalActions));
    }

    private static string Serialized(List<RecordedAction> actions) => System.Text.Json.JsonSerializer.Serialize(actions);
}
