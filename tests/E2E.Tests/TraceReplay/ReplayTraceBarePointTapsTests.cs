// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.TraceReplay.ReplayTraceTests;
using static E2E.Tests.TraceReplay.TestHost;

namespace E2E.Tests.TraceReplay;

/// <summary>
/// <c>describe('replayTrace: bare-point taps')</c> of <c>trace-replay.test.ts</c>. The port records no bare point
/// (<c>tapAt</c>, <c>hoverAt</c>), so these <c>it</c>s are not ported: "replays the recorded point as given when the viewport is
/// the recorded size", "hands off with viewport-changed when the viewport is another size, without tapping", "follows the node
/// the point was placed in: the same place inside its live box", "hands off when the node the point was placed in is gone or
/// has no box, never tapping the bare point", "lets the recorded point pick among look-alikes when it lies inside exactly one of
/// them", "ignores a hidden look-alike that keeps its box, as the hit test that recorded the point did", "taps the recorded
/// point when several nested look-alikes hold it, as a host view and the view inside it do", "hands off when the look-alikes
/// holding the point merely overlap instead of nesting, since the one on top may have changed", and "hands off among
/// look-alikes when the point settles nothing or the viewport changed".
/// </summary>
public sealed class ReplayTraceBarePointTapsTests
{
    [Fact]
    public async Task Pages_to_a_text_on_the_viewport_on_the_re_found_list_on_the_viewport_for_a_lost_list_that_filled_the_screen_and_hands_off_for_a_lost_small_one()
    {
        var calls = new List<object?>();
        var list = new SemanticNode { Ref = "l", Role = "list", Name = "Ledger", Rect = new BoundingBox(0, 0, 1280, 200) };
        var host = MakeHost([list], (name, detail) =>
        {
            if (name == "scrollUntil")
            {
                calls.Add(detail);
            }
        });

        var viewport = await Replay.ReplayTrace(host, [new RecordedAction { Kind = "scrollUntil", Text = "Row 333", Direction = "down" }]);
        Assert.True(viewport.Completed);
        Assert.Equal(1, viewport.Executed);
        var onList = await Replay.ReplayTrace(host, [new RecordedAction { Kind = "scrollUntil", Text = "Row 333", Direction = "down", Role = "list", Name = "Ledger", Spans = 0.3 }]);
        Assert.True(onList.Completed);
        Assert.Equal(1, onList.Executed);
        Assert.Equal(new object?[] { ("Row 333", ScrollDirection.Down, (string?)null), ("Row 333", ScrollDirection.Down, "l") }, calls);

        var lostFull = await Replay.ReplayTrace(MakeHost([]), [new RecordedAction { Kind = "scrollUntil", Text = "Row 333", Direction = "down", Role = "list", Name = "Ledger", Spans = 0.9 }]);
        Assert.True(lostFull.Completed);
        Assert.Equal(1, lostFull.Executed);
        var lostSmall = await Replay.ReplayTrace(MakeHost([]), [new RecordedAction { Kind = "scrollUntil", Text = "Row 333", Direction = "down", Role = "list", Name = "Ledger", Spans = 0.3 }]);
        Assert.False(lostSmall.Completed);
        Assert.Equal(0, lostSmall.Executed);
        Assert.Equal(ReplayHandOffReason.TargetNotFound, lostSmall.StopReason);
    }

    [Fact]
    public async Task Repeats_a_folded_viewport_scroll_as_many_times_as_recorded_with_a_settled_look_between_repeats()
    {
        var host = MakeHost();

        var outcome = await Replay.ReplayTrace(host, [Scroll(null, null, "down", 4)]);

        Assert.True(outcome.Completed);
        Assert.Equal(1, outcome.Executed);
        Assert.Equal(["scroll", "scroll", "scroll", "scroll"], host.Calls);
        // Nothing to relocate, so the first repeat takes no look; each later one holds still first.
        Assert.Equal([SettleMode.HeldStill, SettleMode.HeldStill, SettleMode.HeldStill], host.Looks);
    }

    [Fact]
    public async Task Scrolls_the_viewport_when_a_list_that_filled_the_screen_cannot_be_re_found()
    {
        var targets = new List<object?>();
        var host = MakeHost([Email], (name, detail) =>
        {
            if (name == "scroll")
            {
                targets.Add(detail);
            }
        });
        var scroll = Scroll("group", "Rows 1 to 12", "down", 2);
        scroll.Spans = 0.92;

        var outcome = await Replay.ReplayTrace(host, [scroll]);

        Assert.True(outcome.Completed);
        Assert.Equal(1, outcome.Executed);
        Assert.Equal(new object?[] { (ScrollDirection.Down, (string?)null), (ScrollDirection.Down, (string?)null) }, targets);
    }

    /// <summary>
    /// Partial: the port relocates on its own cadence (a look every 20 ms until the replay timeout) where upstream backs off over 14 s,
    /// so the looks and the time of the one wait are the port's.
    /// </summary>
    [Fact]
    public async Task Partial_waits_for_a_lost_list_that_filled_the_screen_once_per_folded_scroll_not_once_per_repeat()
    {
        var targets = new List<object?>();
        var host = MakeHost([Email], (name, detail) =>
        {
            if (name == "scroll")
            {
                targets.Add(detail);
            }
        });
        host.Time = new AutoAdvanceClock();
        var started = host.Time.GetUtcNow();
        var scroll = Scroll("group", "Rows 1 to 12", "down", 4);
        scroll.Spans = 0.92;

        var outcome = await Replay.ReplayTrace(host, [scroll]);

        Assert.True(outcome.Completed);
        Assert.Equal(1, outcome.Executed);
        Assert.Equal(Enumerable.Repeat<object?>((ScrollDirection.Down, (string?)null), 4), targets);
        // One relocation wait (the first look and a raw one each 20 ms until the replay timeout), then one settled look before each later repeat.
        Assert.Equal(host.ReplayTimeout, host.Time.GetUtcNow() - started);
        Assert.Equal([SettleMode.HeldStill, .. Enumerable.Repeat(SettleMode.Raw, 5), .. Enumerable.Repeat(SettleMode.HeldStill, 3)], host.Looks);
    }

    [Fact]
    public async Task Counts_the_repeats_of_a_folded_scroll_that_ran_before_a_later_one_lost_the_list()
    {
        var list = new SemanticNode { Ref = "g1", Role = "group", Name = "Rows 1 to 12" };
        var host = MakeHost([list, Email]);
        host.Capture = () =>
        {
            host.Observations++;
            // The list leaves the tree after the first scroll.
            return Screen(host.Calls.Contains("scroll") ? new[] { Email } : new[] { list, Email });
        };
        var scroll = Scroll("group", "Rows 1 to 12", "down", 3);
        scroll.Spans = 0.3;

        var outcome = await Replay.ReplayTrace(host, [scroll]);

        Assert.False(outcome.Completed);
        Assert.Equal(1, outcome.Executed);
        Assert.Equal(ReplayHandOffReason.TargetNotFound, outcome.StopReason);
        Assert.Equal([DefaultAgent.Describe(scroll) + " (1 of 3 repeats)"], outcome.Summaries);
        Assert.Equal(["scroll"], host.Calls);
    }

    [Fact]
    public async Task Hands_off_when_a_smaller_scrolled_region_cannot_be_re_found_never_scrolling_the_viewport_for_it()
    {
        var host = MakeHost([Email]);

        var carousel = Scroll("group", "Recommended", "right", null);
        carousel.Spans = 0.18;

        var outcome = await Replay.ReplayTrace(host, [carousel]);

        Assert.False(outcome.Completed);
        Assert.Equal(0, outcome.Executed);
        Assert.Equal(ReplayHandOffReason.TargetNotFound, outcome.StopReason);
        Assert.Empty(host.Calls);
        var unknown = await Replay.ReplayTrace(MakeHost([Email]), [Scroll("group", "Recommended", "right", null)]);
        Assert.False(unknown.Completed);
        Assert.Equal(0, unknown.Executed);
        Assert.Equal(ReplayHandOffReason.TargetNotFound, unknown.StopReason);
    }
}
