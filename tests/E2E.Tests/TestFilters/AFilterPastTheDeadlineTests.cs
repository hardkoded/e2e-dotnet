// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.TestFilters.TestFiltersKit;
using Filters = E2E.TestFilters;

namespace E2E.Tests.TestFilters;

/// <summary>Upstream's <c>describe('a filter past the deadline')</c> (<c>tests/unit/test-filters.test.ts</c>).</summary>
public sealed class AFilterPastTheDeadlineTests
{
    /// <summary>The deadline of the tests below, in real time. It must outlast the start of every later filter on a loaded runner: the later filter runs under the same deadline, so a late start reads as a timeout.</summary>
    private const int Deadline = 300;

    /// <summary>Partial: the deadline is real time (300 ms), where upstream drives fake timers to the millisecond, and the signal carries no <c>TimeoutError</c> reason to inspect.</summary>
    [Fact]
    public async Task Partial_is_abandoned_at_its_deadline_leaves_nothing_out_and_the_next_filter_judges_every_test()
    {
        var candidates = CandidatesOf("a", "b");
        var (slow, seen) = Hanging("slow");
        IReadOnlyList<TestFilterCandidate>? laterGot = null;
        var later = FilterOf.Of("later", tests =>
        {
            laterGot = tests;
            return Excluding((tests[0].Id, "unaffected"));
        });
        var ended = new List<string>();
        var (scope, notices, steps) = Scope(
            timeout: TimeSpan.FromMilliseconds(Deadline),
            step: async (name, work, recorded) =>
            {
                recorded.Add(name);
                var call = await work();
                ended.Add($"{name}: {(call.Outcome is null ? "answered" : call.Outcome == FilterOutcome.TimedOut ? "timed-out" : "failed")}");
                return call;
            });
        var applied = await Filters.ApplyTestFilters([slow, later], candidates, scope);
        Assert.True(seen[0].Signal.IsCancellationRequested);
        Assert.Equal(candidates, laterGot);
        Assert.Equal(["slow", "later"], steps);
        Assert.Equal(["slow: timed-out", "later: answered"], ended);
        Assert.Equal(["warning: slow did not finish within 300ms, so it leaves nothing out"], notices);
        Assert.Equal(new FilterDecision("slow", 2, "did not finish within 300ms, so it leaves nothing out", FilterOutcome.TimedOut), applied.Decisions[0]);
        Assert.Equal([IdOf(candidates, "a")], applied.Excluded.Keys);
    }

    [Fact]
    public async Task Reads_a_filter_that_rejects_with_its_own_deadline_as_timed_out_never_failed()
    {
        var candidates = CandidatesOf("a");
        var obedient = new FilterOf("jev", async (_, context) =>
        {
            await Task.Delay(Timeout.Infinite, context.Signal);
            return Excluding();
        });
        var (scope, notices, _) = Scope(timeout: TimeSpan.FromMilliseconds(20));
        var applied = await Filters.ApplyTestFilters([obedient], candidates, scope);
        Assert.Equal([new FilterDecision("jev", 1, "did not finish within 20ms, so it leaves nothing out", FilterOutcome.TimedOut)], applied.Decisions);
        Assert.Equal(["warning: jev did not finish within 20ms, so it leaves nothing out"], notices);
    }

    /// <summary>Partial: the port records no work left behind (upstream's <c>leaveWorkBehind</c>), and a result is always typed, so only the warning at the end of the call, before the next filter is called, is checked.</summary>
    [Fact]
    public async Task Partial_warns_at_once_before_the_next_filter_is_called()
    {
        var candidates = CandidatesOf("a", "b");
        var (scope, notices, _) = Scope(timeout: TimeSpan.FromMilliseconds(Deadline));
        var (slow, _) = Hanging("slow");
        List<string>? seenByNext = null;
        var next = FilterOf.Of("next", _ =>
        {
            seenByNext = [.. notices];
            return Excluding();
        });
        await Filters.ApplyTestFilters([slow, next], candidates, scope);
        const string Warning = "warning: slow did not finish within 300ms, so it leaves nothing out";
        Assert.Equal([Warning], seenByNext);
        Assert.Equal([Warning], notices);
    }
}
