// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Tests.ReportJson;
using static E2E.Tests.AgentAct.ExecutorKit;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.AgentAct;

/// <summary>
/// The raw <see cref="IStepExecutor"/> socket, driven by a hand-rolled executor with no model. Upstream no longer has an <c>it</c> for
/// "emits a schema-valid report for executor-driven steps": every report a test reads here is checked against the schema.
/// </summary>
public sealed partial class AgentActWithAHandRolledStepExecutorTests
{
    private const string Instruction = "increment the counter once and verify it shows 1";

    /// <summary>Taps the Increment button, verifies the counter, and passes. No model.</summary>
    private static TestExecutor ScriptedExecutor() => new("scripted-executor", async (context, _) =>
    {
        var observation = await context.ObserveAsync();
        await context.Actions.TapAsync(TargetFor(observation.Text, "button \"Increment\""));
        // Like a tool-using loop: the turn is reported after its tool ran.
        context.Budgets.RecordModelCall(new ExecutorModelCall { StartedAt = DateTimeOffset.UtcNow, DurationMs = 5 });
        observation = await context.ObserveAsync();
        return CounterShowsOne().IsMatch(observation.Text)
            ? Passed("tapped Increment; the counter shows 1")
            : Failed("the counter did not show 1 after one tap");
    })
    {
        Version = "test",
    };

    [Fact]
    public async Task Passes_the_test_through_the_socket_without_any_AI_SDK_involvement()
    {
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync(Instruction);
                await Expect.That(ctx.Screen.GetByRole("status")).ToHaveTextAsync("1");
            },
            CounterPage.World(),
            executor: ScriptedExecutor());

        Assert.Null(result.Error);
    }

    /// <summary>
    /// Partial: the port records no engine event for an action an executor takes through its context, so the engine event and the
    /// order of the model event against it are not checked.
    /// </summary>
    [Fact]
    public async Task Partial_Records_the_act_step_with_harness_owned_accounting()
    {
        using var report = new ReportDirectory();
        ActResult? act = null;
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                act = await ctx.Agent.ActAsync(Instruction);
            },
            CounterPage.World(),
            executor: ScriptedExecutor(),
            report: report);

        Assert.Null(result.Error);
        Assert.Equal((1, 1), (act!.Actions, act.ModelCalls));
        var step = report.Step("agent.act");
        Assert.Equal("agent", (string)step["kind"]!);
        Assert.Equal("passed", (string)step["status"]!);
        Assert.Equal(1, (int)step["metrics"]!["actionSteps"]!);
        Assert.Contains("the counter shows 1", (string)step["explanation"]!, StringComparison.Ordinal);
        var modelEvents = step["events"]!.AsArray().Where(each => (string)each!["kind"]! == "model").ToList();
        Assert.Single(modelEvents);
    }

    [GeneratedRegex("status \"Counter\" text=\"1\"")]
    private static partial Regex CounterShowsOne();
}
