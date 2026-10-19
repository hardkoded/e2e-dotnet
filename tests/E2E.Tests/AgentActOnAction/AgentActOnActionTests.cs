// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using E2E.Engine;
using E2E.Tests.AgentAct;
using E2E.Tests.AgentTraceCache;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.AgentActOnAction;

/// <summary>
/// Upstream's <c>describe('agent.act onAction')</c> of <c>agent-act-on-action.test.ts</c>: the hook runs after each action on the
/// settled screen, for model-chosen and replayed actions alike; a throw from it fails the step without touching the step's
/// recording; and a verification inside it never confirms the step it runs in. Upstream's hook takes a screenshot with
/// <c>app.screenshot</c>, which the port does not have, so each hook here records its event, and the text it reads, in a list.
/// </summary>
public sealed class AgentActOnActionTests
{
    private const string Title = "screenshots every action";

    // Taps Increment until it has tapped twice, resuming after any replayed prefix.
    private static TestExecutor TwoTapExecutor(Action? onRun = null) =>
        new("two-tap-executor", async (context, token) =>
        {
            onRun?.Invoke();
            for (var tap = context.ReplayedPrefix?.ReplayedActions.Count ?? 0; tap < 2; tap++)
            {
                var observation = await context.ObserveAsync();
                await context.Actions.TapAsync(TargetFor(observation.Text, "button \"Increment\""), token);
            }

            return Passed("tapped Increment twice");
        })
        {
            Version = "test",
        };

    private static Task<E2ESession> StartAsync(string directory, IStepExecutor executor, DocumentWorld? world = null) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new DocumentEngine(world ?? CounterPage.World()),
            AgentExecutor = executor,
            BaseUrl = "https://counter.test",
            Cache = new FileStepCache(directory),
            CacheEnabled = true,
            CacheMode = CacheMode.ReadWrite,
            TestTitle = Title,
            ActionTimeout = TimeSpan.FromMilliseconds(300),
            ReplayTimeout = TimeSpan.FromMilliseconds(300),
        });

    private static async Task RunAsync(E2ESession session, ActionHook onAction)
    {
        await session.App.OpenAsync("/");
        await session.Agent.ActAsync(TwoTapCounter.Instruction, new ActOptions { OnAction = onAction });
    }

    private static string Line(ActionEvent e) => $"{e.Index} {e.Source.ToString().ToLowerInvariant()} {e.Action}: {e.Summary}";

    [Fact]
    public async Task Calls_the_hook_for_each_model_action_then_for_each_replayed_one_with_its_source()
    {
        var directory = TwoTapCounter.NewDirectory();
        var recorded = new List<string>();
        await using (var session = await StartAsync(directory, TwoTapExecutor()))
        {
            await RunAsync(session, async e => recorded.Add(Line(e) + " | " + await session.Screen.GetByRole("status").TextContentAsync()));
            await Expect.That(session.Screen.GetByRole("status")).ToHaveTextAsync("2");
            session.Complete();
        }

        Assert.Equal(2, recorded.Count);
        Assert.Matches(@"^1 model tap: .* \| 1$", recorded[0]);
        Assert.Matches(@"^2 model tap: .* \| 2$", recorded[1]);

        var calls = 0;
        var replayed = new List<string>();
        await using (var session = await StartAsync(directory, TwoTapExecutor(() => calls++)))
        {
            await RunAsync(session, e =>
            {
                replayed.Add(Line(e));
                return Task.CompletedTask;
            });
            await Expect.That(session.Screen.GetByRole("status")).ToHaveTextAsync("2");
            session.Complete();
        }

        Assert.Equal(0, calls);
        Assert.Equal(2, replayed.Count);
        Assert.Matches(@"^1 replay tap: ", replayed[0]);
        Assert.Matches(@"^2 replay tap: ", replayed[1]);
    }

    [Fact]
    public async Task Never_confirms_the_step_it_runs_in_with_a_check_of_its_own()
    {
        var directory = TwoTapCounter.NewDirectory();
        var calls = 0;
        for (var run = 0; run < 2; run++)
        {
            await using var session = await StartAsync(directory, TwoTapExecutor(() => calls++));
            await RunAsync(session, e => Expect.That(session.Screen.GetByRole("status")).ToHaveTextAsync(e.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            session.Complete();
        }

        Assert.Equal(2, calls);
        Assert.Empty(BillingSession.Entries(directory));
    }

    [Fact]
    public async Task Keeps_the_recording_when_it_throws_after_a_replay_handed_the_step_over()
    {
        var directory = TwoTapCounter.NewDirectory();
        await using (var session = await StartAsync(directory, TwoTapExecutor()))
        {
            await RunAsync(session, _ => Task.CompletedTask);
            await Expect.That(session.Screen.GetByRole("status")).ToHaveTextAsync("2");
            session.Complete();
        }

        // The second recorded tap no longer finds its button: the replay taps once and hands the step to the executor, whose tap
        // the hook rejects.
        var cache = new FileStepCache(directory);
        var key = Path.GetFileNameWithoutExtension(Directory.GetFiles(directory).Single());
        var entry = cache.Read(key).Entry!;
        Assert.Equal(["tap", "tap"], entry.Actions.Select(action => action.Kind));
        entry.Actions[1].Name = "No Such Button";
        cache.Write(key, entry);
        var before = TwoTapCounter.FileState(directory);

        var replayed = false;
        await using (var session = await StartAsync(directory, TwoTapExecutor()))
        {
            var error = await Assert.ThrowsAsync<AgentException>(() => RunAsync(session, e =>
            {
                if (e.Source == ActionSource.Replay)
                {
                    replayed = true;
                }
                else if (replayed)
                {
                    throw new InvalidOperationException("hook broke after the hand-off");
                }

                return Task.CompletedTask;
            }));
            Assert.Contains("agent.act onAction failed: hook broke after the hand-off", error.Message, StringComparison.Ordinal);
            session.Complete(error);
        }

        Assert.Equal(before, TwoTapCounter.FileState(directory));
    }

    [Fact]
    public async Task Waits_for_the_screen_the_action_left_before_calling_the_hook()
    {
        // The second view lands after the tap, as a screen that renders late does.
        var world = new DocumentWorld().Map("/", page =>
        {
            var second = page.Heading("Second view");
            second.Hidden = true;
            page.Button("Continue", () => _ = Task.Run(async () =>
            {
                await Task.Delay(300);
                second.Hidden = false;
            }));
        });
        var executor = new TestExecutor("continue-executor", async (context, token) =>
        {
            var observation = await context.ObserveAsync();
            await context.Actions.TapAsync(TargetFor(observation.Text, "button \"Continue\""), token);
            return Passed("tapped Continue");
        });
        var seen = new ConcurrentQueue<string>();
        await using var session = await StartAsync(TwoTapCounter.NewDirectory(), executor, world);
        await session.App.OpenAsync("/");
        await session.Agent.ActAsync("continue to the second view", new ActOptions
        {
            OnAction = async _ => seen.Enqueue(await session.Screen.GetByRole("heading", "Second view").IsVisibleAsync() ? "arrived" : "not yet"),
        });
        session.Complete();

        Assert.Equal(["arrived"], seen);
    }

    [Fact]
    public async Task Runs_after_a_mutating_project_tool()
    {
        var executor = new TestExecutor("tool-executor", async (context, _) =>
        {
            await context.Budgets.RunToolAsync("seed", true, () => Task.FromResult(true));
            return Passed("seeded");
        });
        var seen = new List<string>();
        await using var session = await StartAsync(TwoTapCounter.NewDirectory(), executor);
        await session.App.OpenAsync("/");
        await session.Agent.ActAsync("seed the data", new ActOptions
        {
            OnAction = e =>
            {
                seen.Add($"{e.Index} {e.Action}: {e.Summary}");
                return Task.CompletedTask;
            },
        });
        session.Complete();

        Assert.Equal(["1 tool:seed: ran seed"], seen);
    }

    [Fact]
    public async Task Fails_the_step_with_the_hook_error_after_the_first_action()
    {
        await using var session = await StartAsync(TwoTapCounter.NewDirectory(), TwoTapExecutor());
        var error = await Assert.ThrowsAsync<AgentException>(() => RunAsync(session, _ => throw new InvalidOperationException("hook broke")));
        session.Complete(error);

        Assert.Equal("ACTION_FAILED", error.Code);
        Assert.Contains("agent.act onAction failed: hook broke", error.Message, StringComparison.Ordinal);
        var step = session.Steps.Single(candidate => candidate.Api == "agent.act");
        Assert.Equal(StepStatus.Failed, step.Status);
        Assert.Equal(1, step.Metrics?.ActionSteps);
    }
}
