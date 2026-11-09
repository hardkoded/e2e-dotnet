// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AgentAct;
using E2E.Tests.AgentTraceCache;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentActBeforeAction;

/// <summary>
/// Upstream's <c>describe('agent.act onBeforeAction')</c> of <c>agent-act-before-action.test.ts</c>: the hook runs before each action on
/// the screen it starts on, for model-chosen and replayed actions alike, through the built-in loop; a throw from it fails the step before
/// the action runs; and a verification inside it never confirms the step it runs in. Upstream's hook takes a screenshot with
/// <c>app.screenshot</c>, which the document engine (the counter page here, where upstream runs a real browser on its fixture app) keeps
/// none of, so each hook records the label it would have given the screenshot in a list. The tests that use the built-in loop script
/// its model. Two tests are <c>Partial_</c>: the first does not check that each hook step holds one screenshot artifact, and the last
/// makes its refused dispatch from a hand-rolled executor, because the port's loop turns a target that names no control away before the
/// dispatcher takes an action slot.
/// </summary>
public sealed class AgentActOnBeforeActionTests
{
    private const string Title = "captures before incrementing";

    private static string Label(BeforeActionEvent e) => $"before {e.Index} {e.Source.ToString().ToLowerInvariant()}";

    private static Task<E2ESession> StartAsync(string directory, IAgentModel? model, IStepExecutor? executor = null, CacheMode mode = CacheMode.ReadWrite) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(CounterPage.World()),
            Model = model,
            AgentExecutor = executor,
            BaseUrl = "https://counter.test",
            Cache = new FileStepCache(directory),
            CacheEnabled = true,
            CacheMode = mode,
            TestTitle = Title,
            ActionTimeout = TimeSpan.FromMilliseconds(300),
            ReplayTimeout = TimeSpan.FromMilliseconds(300),
        });

    // Upstream's SUITE: the starting screen shows the count before this action, and onAction the count after it.
    private static async Task RunSuiteAsync(E2ESession session, List<string> labels, Func<BeforeActionEvent, Task>? extra = null, bool confirm = true)
    {
        await session.App.OpenAsync("/");
        await session.Agent.ActAsync(
            TwoTapCounter.Instruction,
            new ActOptions
            {
                OnBeforeAction = async e =>
                {
                    await Expect.That(session.Screen.GetByRole("status")).ToHaveTextAsync((e.Index - 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    labels.Add(Label(e));
                    if (extra is not null)
                    {
                        await extra(e);
                    }
                },
                OnAction = e => Expect.That(session.Screen.GetByRole("status")).ToHaveTextAsync(e.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            });
        if (confirm)
        {
            await Expect.That(session.Screen.GetByRole("status")).ToHaveTextAsync("2");
        }
    }

    [Fact]
    public async Task Partial_Captures_the_starting_screens_in_the_built_in_loop_and_verified_replay_with_no_after_images()
    {
        var directory = TwoTapCounter.NewDirectory();
        var model = TwoTapCounter.Model();
        var recorded = new List<string>();
        await using (var session = await StartAsync(directory, model))
        {
            await RunSuiteAsync(session, recorded);
            session.Complete();
        }

        Assert.Equal(["before 1 model", "before 2 model"], recorded);
        Assert.Single(BillingSession.Entries(directory));

        var calls = model.CallCount;
        var replayed = new List<string>();
        await using (var session = await StartAsync(directory, model))
        {
            await RunSuiteAsync(session, replayed);
            session.Complete();
        }

        Assert.Equal(["before 1 replay", "before 2 replay"], replayed);
        Assert.Equal(calls, model.CallCount);
    }

    [Fact]
    public async Task Stops_before_mutation_when_the_observer_throws()
    {
        await using var session = await StartAsync(TwoTapCounter.NewDirectory(), TwoTapCounter.Model());
        await session.App.OpenAsync("/");
        AgentException? failure = null;
        try
        {
            await session.Agent.ActAsync(
                TwoTapCounter.Instruction,
                new ActOptions { OnBeforeAction = _ => throw new InvalidOperationException("before broke") });
        }
        catch (AgentException error)
        {
            failure = error;
        }

        await Expect.That(session.Screen.GetByRole("status")).ToHaveTextAsync("0");
        Assert.NotNull(failure);
        session.Complete(failure);

        Assert.Equal("ACTION_FAILED", failure.Code);
        Assert.Contains("onBeforeAction failed: before broke", failure.Message, StringComparison.Ordinal);
        Assert.Contains(session.StepRecords, step => step.Api == "expect.toHaveText" && step.Status == StepStatus.Passed);
        Assert.Equal(1, session.StepRecords.Single(step => step.Api == "agent.act").Metrics?.ActionSteps);
    }

    [Fact]
    public async Task Preserves_a_verified_recording_when_a_replay_observer_fails_before_dispatch()
    {
        var directory = TwoTapCounter.NewDirectory();
        var model = TwoTapCounter.Model();
        await using (var session = await StartAsync(directory, model))
        {
            await RunSuiteAsync(session, []);
            session.Complete();
        }

        var files = TwoTapCounter.FileState(directory);
        var calls = model.CallCount;
        await using (var session = await StartAsync(directory, model))
        {
            var error = await Assert.ThrowsAsync<AgentException>(() => RunSuiteAsync(
                session,
                [],
                e => e.Source == ActionSource.Replay ? throw new InvalidOperationException("replay observer broke") : Task.CompletedTask));
            session.Complete(error);
            Assert.Contains("onBeforeAction failed: replay observer broke", error.Message, StringComparison.Ordinal);
        }

        Assert.Equal(calls, model.CallCount);
        Assert.Equal(files, TwoTapCounter.FileState(directory));
    }

    [Fact]
    public async Task Does_not_confirm_a_recording_through_checks_inside_the_observer()
    {
        var directory = TwoTapCounter.NewDirectory();
        await using (var session = await StartAsync(directory, TwoTapCounter.Model()))
        {
            await RunSuiteAsync(session, [], confirm: false);
            session.Complete();
        }

        Assert.Empty(BillingSession.Entries(directory));
    }

    [Fact]
    public async Task Observes_mutating_project_tools_not_read_only_tools()
    {
        var executor = new TestExecutor("tool-fixture", async (context, _) =>
        {
            await context.Budgets.RunToolAsync("read", false, () => Task.FromResult(true));
            await context.Budgets.RunToolAsync("write", true, () => Task.FromResult(true));
            return Passed("ran tools");
        });
        var labels = new List<string>();
        await using var session = await StartAsync(TwoTapCounter.NewDirectory(), null, executor);
        await session.App.OpenAsync("/");
        await session.Agent.ActAsync(
            "run tools",
            new ActOptions
            {
                OnBeforeAction = e =>
                {
                    labels.Add(Label(e));
                    return Task.CompletedTask;
                },
            });
        session.Complete();

        Assert.Equal(["before 1 model"], labels);
    }

    [Fact]
    public async Task Partial_Records_a_failed_dispatch_attempt_without_a_completion_notification()
    {
        // Upstream's scripted model taps node n99999 through the built-in loop. The port's loop turns a target that names no control away
        // before the dispatcher takes a slot, so a hand-rolled executor makes the same dispatcher call, and the refusal comes from the dispatcher.
        var executor = new TestExecutor("missing-target-executor", async (context, token) =>
        {
            var refused = await Assert.ThrowsAsync<TestException>(() => context.Actions.TapAsync(new ExecutorTarget("n99999"), token));
            Assert.Equal("LOCATOR_NOT_FOUND", refused.Code);
            return Passed("observed the refused attempt");
        });
        var labels = new List<string>();
        await using var session = await StartAsync(TwoTapCounter.NewDirectory(), null, executor);
        await session.App.OpenAsync("/");
        await session.Agent.ActAsync(
            "attempt a missing target",
            new ActOptions
            {
                OnBeforeAction = e =>
                {
                    labels.Add($"before {e.Index}");
                    return Task.CompletedTask;
                },
                OnAction = _ => throw new InvalidOperationException("must not complete"),
            });
        await Expect.That(session.Screen.GetByRole("status")).ToHaveTextAsync("0");
        session.Complete();

        Assert.Equal(["before 1"], labels);
    }
}
