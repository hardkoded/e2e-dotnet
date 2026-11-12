// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.ReportRepeats;

/// <summary>Upstream's <c>describe('repeat tally')</c> (<c>tests/unit/report-repeats.test.ts</c>).</summary>
public sealed class RepeatTallyTests
{
    private static (string Key, string Label, RepeatRun Run) Run(string key, int repeat, string status, string? code = null) => (key, key, new RepeatRun(repeat, status, code));

    [Fact]
    public void Groups_the_runs_of_each_test_in_repeat_order_and_leaves_out_a_test_that_ran_once_or_skipped_every_run()
    {
        var groups = Repeats.RepeatGroups(
            new[]
            {
                Run("pays", 1, "failed", "ASSERTION_FAILED"),
                Run("pays", 0, "passed"),
                Run("pays", 2, "flaky", "STEP_TIMEOUT"),
                Run("once", 0, "failed", "ACTION_FAILED"),
                Run("skipped", 0, "skipped"),
                Run("skipped", 1, "skipped"),
                Run("stable", 0, "passed"),
                Run("stable", 1, "passed"),
                Run("stable", 2, "passed"),
            },
            entry => entry);

        Assert.Equal(
            [("pays", 1, true, "0,1,2"), ("stable", 3, false, "0,1,2")],
            groups.Select(group => (group.Label, group.Passed, group.Unstable, string.Join(",", group.Runs.Select(entry => entry.Repeat)))));
        Assert.Equal("1 of 2 tests passed all 3 runs", Repeats.RepeatSummary(groups));
        Assert.Equal("1/3 passed · repeat 1 ASSERTION_FAILED · repeat 2 flaky (STEP_TIMEOUT)", Repeats.RepeatLine(groups[0]));
    }

    [Fact]
    public void Counts_a_run_the_run_interrupted_or_never_started_as_a_miss_not_a_flake()
    {
        var groups = Repeats.RepeatGroups(
            new[] { Run("cut", 0, "passed"), Run("cut", 1, "interrupted", "INTERRUPTED"), Run("cut", 2, "skipped"), Run("flake", 0, "passed"), Run("flake", 1, "failed", "ASSERTION_FAILED") },
            entry => entry);

        Assert.Equal([("cut", 1, false), ("flake", 1, true)], groups.Select(group => (group.Label, group.Passed, group.Unstable)));
        Assert.Equal("1/3 passed · repeat 1 interrupted · repeat 2 skipped", Repeats.RepeatLine(groups[0]));
    }
}
