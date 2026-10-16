// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using static E2E.Tests.AgentAct.ExecutorKit;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.AgentAct;

/// <summary>
/// The raw <see cref="IStepExecutor"/> socket, driven by a hand-rolled executor with no model. Not ported, because the port has
/// no such feature: "emits a schema-valid report for executor-driven steps" (the port has no report).
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
    /// Partial: the port has no step record, so it reads the step's accounting from its <see cref="ActResult"/>: the action it
    /// took, the model call the executor reported, and the verdict's summary. Upstream also reads the step's events, which
    /// the port does not record.
    /// </summary>
    [Fact]
    public async Task Records_the_act_step_with_harness_owned_accounting()
    {
        ActResult? act = null;
        var result = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                act = await ctx.Agent.ActAsync(Instruction);
            },
            CounterPage.World(),
            executor: ScriptedExecutor());

        Assert.Null(result.Error);
        Assert.Equal(1, act!.Actions);
        Assert.Equal(1, act.ModelCalls);
        Assert.Contains("the counter shows 1", act.Summary, StringComparison.Ordinal);
        Assert.Equal(1, result.ModelCalls);
    }

    [GeneratedRegex("status \"Counter\" text=\"1\"")]
    private static partial Regex CounterShowsOne();
}
