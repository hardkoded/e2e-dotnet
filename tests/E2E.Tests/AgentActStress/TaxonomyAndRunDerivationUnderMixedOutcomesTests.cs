// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.AgentAct;
using static E2E.Tests.AgentAct.ExecutorKit;
using static E2E.Tests.CoreTests;

namespace E2E.Tests.AgentActStress;

/// <summary>
/// Not ported, because the port has no such feature: "reconstructs the executor per worker and passes in parallel" (the
/// "config-file executor across worker processes" describe). The port has no workers, and an executor is code, which its config
/// file cannot hold.
/// </summary>
public sealed class TaxonomyAndRunDerivationUnderMixedOutcomesTests
{
    /// <summary>
    /// Partial: the port has no table that names the category a blocked code stands for, so it checks which codes may carry a blocked
    /// verdict: the ones with an outside owner, and never a code that describes the app.
    /// </summary>
    [Fact]
    public void Every_blockable_code_names_a_category_others_name_none()
    {
        Assert.All(StepExecutors.BlockableCodes, code => Assert.Contains(code, StepExecutors.AgentCodes));
        Assert.DoesNotContain("CANCELLED", StepExecutors.BlockableCodes);
        Assert.DoesNotContain("ACTION_FAILED", StepExecutors.BlockableCodes);
        Assert.Contains("SEED_DATA_MISSING", StepExecutors.BlockableCodes);
        Assert.Contains("AUTH_CREDENTIAL_INVALID", StepExecutors.BlockableCodes);
    }

    /// <summary>
    /// Partial: the port has no run to derive a status for. Each test reports its own error: the blocked one is blocked, and the broken
    /// one is a failure of the app that no blocked result hides.
    /// </summary>
    [Fact]
    public async Task Blocks_the_run_on_a_new_code_but_one_real_failure_keeps_it_failed()
    {
        var executor = new TestExecutor("mixed-executor", (context, _) => Task.FromResult(
            context.Step.Instruction.Contains("seed", StringComparison.Ordinal)
                ? Blocked("no fixtures exist in this environment", "SEED_DATA_MISSING")
                : Failed("the button does nothing")));

        var blocked = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("needs seed data");
            },
            CounterPage.World(),
            executor: executor);
        var broken = await RunAsync(
            async ctx =>
            {
                await ctx.App.OpenAsync("/");
                await ctx.Agent.ActAsync("the app is broken");
            },
            CounterPage.World(),
            executor: executor);

        var stopped = Assert.IsType<AgentException>(blocked.Error);
        Assert.Equal("SEED_DATA_MISSING", stopped.Code);
        Assert.True(stopped.Blocked);
        var failure = Assert.IsType<AgentException>(broken.Error);
        Assert.Equal("ACTION_FAILED", failure.Code);
        Assert.False(failure.Blocked);
    }
}
