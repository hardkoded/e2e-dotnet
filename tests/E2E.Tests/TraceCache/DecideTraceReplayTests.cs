// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cache;

namespace E2E.Tests.TraceCache;

/// <summary>
/// Upstream's <c>decideTraceReplay</c> describe of <c>trace-cache.test.ts</c>. "never replays a truncated trace" and "skips
/// leading gaps when checking for the navigate opener" are not ported: the port records no truncated entry, and reads the
/// first action as it is. The undefined current path is the null one.
/// </summary>
public sealed class DecideTraceReplayTests
{
    [Fact]
    public void Enforces_the_start_path_precondition_when_the_trace_does_not_open_with_navigate()
    {
        var entry = Entry("/settings", Tap());
        Assert.Equal("replay", Decide.DecideTraceReplay(entry, "/settings").Action);
        Assert.Equal("replay", Decide.DecideTraceReplay(Entry("/orders/42", Tap()), "/orders/43917#top").Action);
        foreach (var currentPath in new string?[] { "/other", null })
        {
            Assert.Equal(TraceReplayDecision.Miss("wrong-context"), Decide.DecideTraceReplay(entry, currentPath));
        }
    }

    /// <summary>Partial: upstream "compares the start path by pathname so query strings never cold-miss" also replays across a query (<c>?utm_source=mail</c>, <c>?tab=2</c>). The port keeps a query value that is not minted as part of the screen (<c>CacheRoute</c>).</summary>
    [Fact]
    public void Compares_the_start_path_by_pathname_whatever_the_fragment()
    {
        var entry = Entry("/settings", Tap());
        Assert.Equal("replay", Decide.DecideTraceReplay(entry, "/settings#top").Action);
        Assert.Equal(TraceReplayDecision.Miss("wrong-context"), Decide.DecideTraceReplay(entry, "/settings/billing"));
    }

    [Fact]
    public void Replays_a_navigate_opening_trace_from_anywhere()
    {
        var entry = Entry("/settings", Navigate(), Tap());
        Assert.Equal("replay", Decide.DecideTraceReplay(entry, "/other").Action);
        Assert.Equal("replay", Decide.DecideTraceReplay(entry, null).Action);
    }

    [Fact]
    public void Requires_a_recorded_start_path_for_a_non_navigating_trace()
    {
        Assert.Equal(TraceReplayDecision.Miss("wrong-context"), Decide.DecideTraceReplay(Entry(null, Tap()), "/settings"));
    }

    private static RecordedAction Tap() => new() { Kind = "tap", Role = "button", Name = "Save" };

    private static RecordedAction Navigate() => new() { Kind = "navigate", Url = "/companies" };

    private static CacheEntry Entry(string? startPath, params RecordedAction[] actions) => new() { Route = startPath, Actions = [.. actions] };
}
