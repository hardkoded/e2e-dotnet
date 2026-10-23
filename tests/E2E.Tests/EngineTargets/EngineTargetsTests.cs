// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;
using E2E.Tests.AgentAct;
using E2E.Tests.ReportJson;
using static E2E.Tests.AgentAct.ExecutorKit;

namespace E2E.Tests.EngineTargets;

/// <summary>
/// An agent step over a toy engine and a hand-rolled executor, with no model and no browser. Not ported, because the port has no such
/// feature: every other test of upstream's file needs the <c>defineEngine</c> hooks (init, state, isolation, fixtures,
/// <c>requires</c>), the runner (workers, the report, notices), or the screen tier over <c>engine.locate</c>. The two that exercise
/// <c>prepare</c> and <c>finish</c> are ported through <c>Standalone.OpenAttemptAsync</c>, the port's one host, and are partial: there is
/// no run to end, no run-level error event, and no report, so each says what it checks instead.
/// </summary>
public sealed partial class EngineTargetsTests
{
    /// <summary>
    /// Partial: an engine of the port has no name besides its platform, so the target's engine name in the report is the platform
    /// <c>ios</c>, not upstream's <c>toy-device</c>. The verbs are the ones the engine's capabilities allow: the port's engines declare capabilities, not action kinds.
    /// </summary>
    [Fact]
    public async Task Partial_Runs_an_agent_step_over_the_adapter_with_lifecycle_and_honest_provenance()
    {
        var toy = new ToyEngine();
        var executor = new TestExecutor("toy-tapper", async (context, _) =>
        {
            // The executor is told which grammar the surface honors: no scroll and no back, since the toy declares neither.
            var verbs = string.Join(",", context.Target.Verbs.Select(verb => verb.ToString()).Order(StringComparer.Ordinal));
            if (context.Target.Platform != "ios" || verbs != "Check,Clear,DoubleTap,Navigate,Press,Select,Tap,Type,TypeSecret")
            {
                return Failed("unexpected target " + context.Target.Platform + " " + verbs);
            }

            for (var round = 0; round < 5; round++)
            {
                var observation = await context.ObserveAsync();
                var match = CounterReading().Match(observation.Text);
                if (match.Success && match.Groups["value"].Value == "2")
                {
                    return Passed("counter reached 2 after " + round + " taps");
                }

                await context.Actions.TapAsync(new ExecutorTarget("increment"));
            }

            return Failed("counter never reached 2");
        })
        {
            Version = "1",
        };
        using var report = new ReportDirectory();
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            ResultsDirectory = report.Results,
            TargetName = "toy-sim",
            Engine = toy,
            AgentExecutor = executor,
            MaxModelCalls = 10,
            CacheEnabled = false,
            TestTitle = "agent drives the toy device",
        });

        var result = await session.Agent.ActAsync("increment the counter to 2");
        session.Complete();

        Assert.Equal(2, toy.Count);
        Assert.Contains("counter reached 2", result.Summary, StringComparison.Ordinal);
        Assert.Equal("ios", toy.Platform);
        Assert.Equal(["init"], toy.Lifecycle);
        await session.DisposeAsync();
        Assert.Equal(["init", "dispose"], toy.Lifecycle);
        Assert.Equal(0, (int)report.Run()["exitCode"]!);
        var reportTarget = report.Run()["targets"]!.AsArray().Single(entry => (string)entry!["id"]! == "toy-sim")!;
        Assert.Equal("ios", (string)reportTarget["engine"]!["name"]!);
        Assert.Equal("ios", (string)reportTarget["platform"]!);
    }

    /// <summary>
    /// Partial: the attempt does not open, with the failure of prepare, and the engine is finished. The port has no run, so upstream's
    /// outcome (<c>status: 'error'</c>, no results, one <c>run-error</c> event in the <c>launch</c> phase) has nothing to check.
    /// </summary>
    [Fact]
    public async Task Partial_Ends_the_run_before_any_worker_starts_when_prepare_fails()
    {
        var toy = new ToyEngine(ToyPrepare.Fail);
        using var report = new ReportDirectory();
        var failure = await Assert.ThrowsAsync<EngineException>(() => Standalone.OpenAttemptAsync(new E2ESessionOptions
        {
            ResultsDirectory = report.Results,
            TargetName = "toy-sim",
            Engine = toy,
            CacheEnabled = false,
            TestTitle = "screen and expect drive the toy device over locate",
        }));

        Assert.Contains("toolchain missing", failure.Message, StringComparison.Ordinal);

        // No worker ever booted: nothing to init, nothing to dispose. What the failed prepare may have acquired is still released.
        Assert.Equal(["prepare", "finish"], toy.Lifecycle);
    }

    /// <summary>
    /// Partial: the attempt's close reports the abandoned finish as <c>CLEANUP_TIMEOUT</c> instead of hanging. The port has no run to
    /// report it on, so upstream's passed result and its <c>run-error</c> event in the <c>cleanup</c> phase have nothing to check.
    /// </summary>
    [Fact]
    public async Task Partial_Abandons_a_finish_hook_that_outlives_the_cleanup_budget_as_a_cleanup_error_instead_of_hanging_the_run()
    {
        var toy = new ToyEngine(ToyPrepare.HangFinish);
        using var report = new ReportDirectory();
        var attempt = await Standalone.OpenAttemptAsync(new E2ESessionOptions
        {
            ResultsDirectory = report.Results,
            TargetName = "toy-sim",
            Engine = toy,
            CacheEnabled = false,
            CleanupTimeout = TimeSpan.FromMilliseconds(300),
            TestTitle = "screen and expect drive the toy device over locate",
        });
        attempt.Session.Complete();

        var errors = await attempt.CloseAsync().WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(["prepare", "init", "dispose", "finish"], toy.Lifecycle);
        var timeout = Assert.IsAssignableFrom<E2EException>(Assert.Single(errors));
        Assert.Equal("CLEANUP_TIMEOUT", timeout.Code);
    }

    [GeneratedRegex("status \"count\" text=\"(?<value>\\d+)\"")]
    private static partial Regex CounterReading();
}
