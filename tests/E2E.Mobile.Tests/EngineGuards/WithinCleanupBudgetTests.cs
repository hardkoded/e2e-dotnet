// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace E2E.Mobile.Tests.EngineGuards;

/// <summary>
/// <c>describe('withinCleanupBudget')</c> of upstream <c>engine-guards.test.ts</c>: <c>withinCleanupBudget</c> never outlives its
/// budget. The port's counterpart is <c>CleanupBudget.WithinAsync</c>, which takes the work as a function of the budget's token.
/// </summary>
public sealed class WithinCleanupBudgetTests
{
    [Fact]
    public async Task Resolves_when_the_work_fails_without_rethrowing()
    {
        await CleanupBudget.WithinAsync(_ => Task.FromException(new InvalidOperationException("close failed")), TimeSpan.FromSeconds(1), CancellationToken.None);
    }

    [Fact]
    public async Task Resolves_at_abort_while_the_work_still_hangs()
    {
        using var cancel = new CancellationTokenSource();
        var waiting = CleanupBudget.WithinAsync(_ => new TaskCompletionSource().Task, TimeSpan.FromSeconds(60), cancel.Token);
        await cancel.CancelAsync();
        await waiting.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Resolves_once_the_budget_elapses_while_the_work_still_hangs_and_not_before()
    {
        var budget = TimeSpan.FromMilliseconds(1500);
        var clock = Stopwatch.StartNew();
        var waiting = CleanupBudget.WithinAsync(_ => new TaskCompletionSource().Task, budget, CancellationToken.None);

        await Task.Delay(budget / 10);
        Assert.False(waiting.IsCompleted);
        await waiting.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(clock.Elapsed >= budget - TimeSpan.FromMilliseconds(50));
    }
}
