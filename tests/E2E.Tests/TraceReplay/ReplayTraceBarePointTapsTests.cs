// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.TraceReplay.ReplayTraceTests;
using static E2E.Tests.TraceReplay.TestHost;

namespace E2E.Tests.TraceReplay;

/// <summary>
/// <c>describe('replayTrace: bare-point taps')</c> of <c>trace-replay.test.ts</c>. A recorded point names its place by
/// <see cref="RecordedAction.Under"/> where upstream's reads <c>within</c>.
/// </summary>
public sealed class ReplayTraceBarePointTapsTests
{
    private static RecordedAction Pin => new() { Kind = "tapAt", Point = new Point(300, 60), Viewport = Viewport };

    private static RecordedPointPlace Within(string role, string? name, double fx, double fy) =>
        new() { Target = new RecordedTarget { Role = role, Name = name }, Fx = fx, Fy = fy };

    private static SemanticNode Box(string reference, string role, string? name, double x, double y, double width, double height, bool hidden = false) =>
        new() { Ref = reference, Role = role, Name = name, Rect = new BoundingBox(x, y, width, height), States = new NodeStates { Hidden = hidden } };

    private static void CollectPoints(List<object?> points, string name, object? detail)
    {
        if (name == "tapAt")
        {
            points.Add(detail);
        }
    }

    [Fact]
    public async Task Replays_the_recorded_point_as_given_when_the_viewport_is_the_recorded_size()
    {
        var points = new List<object?>();
        var host = MakeHost(onAction: (name, detail) => CollectPoints(points, name, detail));

        var outcome = await Replay.ReplayTrace(host, [Pin]);

        Assert.True(outcome.Completed);
        Assert.Equal(1, outcome.Executed);
        Assert.Equal(new object?[] { (300.0, 60.0) }, points);
    }

    [Fact]
    public async Task Hands_off_with_viewport_changed_when_the_viewport_is_another_size_without_tapping()
    {
        var host = MakeHost(viewport: new TraceViewport(390, 844));

        var outcome = await Replay.ReplayTrace(host, [Pin]);

        Assert.False(outcome.Completed);
        Assert.Equal(0, outcome.Executed);
        Assert.Equal(ReplayHandOffReason.ViewportChanged, outcome.StopReason);
        Assert.Empty(host.Calls);
    }

    [Fact]
    public async Task Follows_the_node_the_point_was_placed_in_the_same_place_inside_its_live_box()
    {
        var map = Box("m", "img", "Map", 100, 200, 400, 200);
        var points = new List<object?>();
        var host = MakeHost([map, Email], (name, detail) => CollectPoints(points, name, detail));
        var action = Pin;
        action.Under = Within("img", "Map", 0.75, 0.3);

        var outcome = await Replay.ReplayTrace(host, [action]);

        Assert.True(outcome.Completed);
        Assert.Equal(1, outcome.Executed);
        // The recording had the map at (0, 0) with the pin at (300, 60); the live map moved to (100, 200).
        Assert.Equal(new object?[] { (400.0, 260.0) }, points);
    }

    [Fact]
    public async Task Hands_off_when_the_node_the_point_was_placed_in_is_gone_or_has_no_box_never_tapping_the_bare_point()
    {
        var action = Pin;
        action.Under = Within("img", "Map", 0.5, 0.5);
        var host = MakeHost([Email]);

        var outcome = await Replay.ReplayTrace(host, [action]);

        Assert.False(outcome.Completed);
        Assert.Equal(0, outcome.Executed);
        Assert.Equal(ReplayHandOffReason.TargetNotFound, outcome.StopReason);
        Assert.Empty(host.Calls);
        var flat = MakeHost([new SemanticNode { Ref = "m", Role = "img", Name = "Map" }]);
        var boxless = await Replay.ReplayTrace(flat, [action]);
        Assert.False(boxless.Completed);
        Assert.Equal(ReplayHandOffReason.TargetNotFound, boxless.StopReason);
        Assert.Empty(flat.Calls);
    }

    [Fact]
    public async Task Lets_the_recorded_point_pick_among_look_alikes_when_it_lies_inside_exactly_one_of_them()
    {
        var points = new List<object?>();
        var host = MakeHost([Box("a", "img", "Map", 0, 0, 200, 200), Box("b", "img", "Map", 200, 0, 200, 200)], (name, detail) => CollectPoints(points, name, detail));
        var action = Pin;
        action.Under = Within("img", "Map", 0.5, 0.5);

        var outcome = await Replay.ReplayTrace(host, [action]);

        Assert.True(outcome.Completed);
        Assert.Equal(1, outcome.Executed);
        // (300, 60) lies in the right-hand map; the tap lands at its recorded place inside that box.
        Assert.Equal(new object?[] { (300.0, 100.0) }, points);
    }

    [Fact]
    public async Task Ignores_a_hidden_look_alike_that_keeps_its_box_as_the_hit_test_that_recorded_the_point_did()
    {
        var points = new List<object?>();
        var host = MakeHost([Box("a", "img", "Map", 200, 0, 200, 200), Box("h", "img", "Map", 200, 0, 200, 200, hidden: true)], (name, detail) => CollectPoints(points, name, detail));
        var action = Pin;
        action.Under = Within("img", "Map", 0.5, 0.5);

        var outcome = await Replay.ReplayTrace(host, [action]);

        Assert.True(outcome.Completed);
        Assert.Equal(1, outcome.Executed);
        Assert.Equal(new object?[] { (300.0, 100.0) }, points);
    }

    [Fact]
    public async Task Taps_the_recorded_point_when_several_nested_look_alikes_hold_it_as_a_host_view_and_the_view_inside_it_do()
    {
        var points = new List<object?>();
        var nested = MakeHost([Box("h", "group", "Form", 0, 0, 400, 200), Box("i", "group", "Form", 10, 10, 380, 180)], (name, detail) => CollectPoints(points, name, detail));
        var action = Pin;
        action.Under = Within("group", "Form", 0.75, 0.3);

        var outcome = await Replay.ReplayTrace(nested, [action]);

        Assert.True(outcome.Completed);
        Assert.Equal(1, outcome.Executed);
        Assert.Equal(new object?[] { (300.0, 60.0) }, points);
        // An anonymous container among its kind, the merged tree of a screen built for pixels, is the same case.
        var groups = new[] { Box("a", "group", null, 0, 0, 400, 400), Box("b", "group", null, 0, 0, 400, 100), Box("c", "group", null, 0, 300, 400, 100) };
        var anonymousAction = Pin;
        anonymousAction.Under = Within("group", null, 0.75, 0.6);
        var anonymous = await Replay.ReplayTrace(MakeHost(groups, (name, detail) => CollectPoints(points, name, detail)), [anonymousAction]);
        Assert.True(anonymous.Completed);
        Assert.Equal(1, anonymous.Executed);
        Assert.Equal(new object?[] { (300.0, 60.0), (300.0, 60.0) }, points);
    }

    [Fact]
    public async Task Hands_off_when_the_look_alikes_holding_the_point_merely_overlap_instead_of_nesting_since_the_one_on_top_may_have_changed()
    {
        var host = MakeHost([Box("a", "img", "Map", 0, 0, 320, 200), Box("b", "img", "Map", 280, 0, 200, 200)]);
        var action = Pin;
        action.Under = Within("img", "Map", 0.5, 0.5);

        var outcome = await Replay.ReplayTrace(host, [action]);

        Assert.False(outcome.Completed);
        Assert.Equal(0, outcome.Executed);
        Assert.Equal(ReplayHandOffReason.TargetAmbiguous, outcome.StopReason);
        Assert.DoesNotContain("tapAt", host.Calls);
    }

    [Fact]
    public async Task Hands_off_among_look_alikes_when_the_point_settles_nothing_or_the_viewport_changed()
    {
        SemanticNode[] maps = [Box("a", "img", "Map", 0, 0, 200, 200), Box("b", "img", "Map", 200, 0, 200, 200)];
        var off = Pin;
        off.Point = new Point(600, 60);
        off.Under = Within("img", "Map", 0.5, 0.5);
        var missed = await Replay.ReplayTrace(MakeHost(maps), [off]);
        Assert.False(missed.Completed);
        Assert.Equal(0, missed.Executed);
        Assert.Equal(ReplayHandOffReason.TargetAmbiguous, missed.StopReason);

        var resizedAction = Pin;
        resizedAction.Under = Within("img", "Map", 0.5, 0.5);
        var resized = await Replay.ReplayTrace(MakeHost(maps, viewport: new TraceViewport(390, 844)), [resizedAction]);
        Assert.False(resized.Completed);
        Assert.Equal(0, resized.Executed);
        Assert.Equal(ReplayHandOffReason.TargetAmbiguous, resized.StopReason);
    }

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
        scroll.Summary = "scroll down x3";

        var outcome = await Replay.ReplayTrace(host, [scroll]);

        Assert.False(outcome.Completed);
        Assert.Equal(1, outcome.Executed);
        Assert.Equal(ReplayHandOffReason.TargetNotFound, outcome.StopReason);
        Assert.Equal(["scroll down x3 (1 of 3 repeats)"], outcome.Summaries);
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
