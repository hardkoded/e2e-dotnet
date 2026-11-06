// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.LoopGuards;

/// <summary>
/// Upstream's "failure streak" describe (loop-guards.test.ts). Only the streak `it` is ported: the others read tool result text
/// (`isFailedResult`, `extractToolResults`), which the port does not parse (COMPATIBILITY, Ported).
/// </summary>
public sealed class FailureStreakTests
{
    private static (bool Stop, string? Reason) Streak(params bool[] failed) =>
        E2E.LoopGuards.CheckFailureStreak([.. failed.Select(item => new GuardToolResult(item))]);

    [Fact]
    public void Warns_at_three_failures_in_a_row_stops_at_five_and_starts_over_after_a_success()
    {
        Assert.Equal((false, null), Streak(true, true));
        Assert.Equal((false, "the last 3 actions failed in a row"), Streak(false, true, true, true));
        Assert.True(Streak(true, true, true, true, true).Stop);
        Assert.Equal((false, null), Streak(true, true, true, true, false, true, true));
    }
}
