// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.TraceReplay.TestHost;

namespace E2E.Tests.TraceReplay;

/// <summary><c>describe('replayTrace: bare-point drags')</c> of <c>trace-replay.test.ts</c>.</summary>
public sealed class ReplayTraceBarePointDragsTests
{
    private static void CollectDrags(List<object?> drags, string name, object? detail)
    {
        if (name == "dragAt")
        {
            drags.Add(detail);
        }
    }

    [Fact]
    public async Task Replays_a_bare_point_drag_with_both_points_as_recorded_on_the_recorded_viewport_only()
    {
        var shape = new RecordedAction
        {
            Kind = "dragAt",
            From = new RecordedPointEnd { Point = new Point(100, 100) },
            To = new RecordedPointEnd { Point = new Point(300, 100) },
            Viewport = Viewport,
        };
        var drags = new List<object?>();
        var host = MakeHost(onAction: (name, detail) => CollectDrags(drags, name, detail));

        var outcome = await Replay.ReplayTrace(host, [shape]);

        Assert.True(outcome.Completed);
        Assert.Equal(1, outcome.Executed);
        Assert.Equal(new object?[] { (100.0, 100.0, 300.0, 100.0) }, drags);
        var resized = MakeHost(viewport: new TraceViewport(390, 844));
        var changed = await Replay.ReplayTrace(resized, [shape]);
        Assert.False(changed.Completed);
        Assert.Equal(0, changed.Executed);
        Assert.Equal(ReplayHandOffReason.ViewportChanged, changed.StopReason);
        Assert.Empty(resized.Calls);
    }

    [Fact]
    public async Task Places_each_end_of_a_bare_point_drag_inside_the_live_box_of_the_node_it_sat_in_and_hands_off_when_one_is_gone()
    {
        var board = new SemanticNode { Ref = "b", Role = "img", Name = "Board", Rect = new BoundingBox(100, 200, 400, 200) };
        RecordedPointPlace Place(string name, double fx, double fy) => new() { Target = new RecordedTarget { Role = "img", Name = name }, Fx = fx, Fy = fy };
        RecordedAction Moved(RecordedPointPlace to) => new()
        {
            Kind = "dragAt",
            From = new RecordedPointEnd { Point = new Point(100, 50), Under = Place("Board", 0.25, 0.25) },
            To = new RecordedPointEnd { Point = new Point(300, 150), Under = to },
            Viewport = Viewport,
        };
        var drags = new List<object?>();
        var host = MakeHost([board, Email], (name, detail) => CollectDrags(drags, name, detail));

        var outcome = await Replay.ReplayTrace(host, [Moved(Place("Board", 0.75, 0.75))]);

        Assert.True(outcome.Completed);
        Assert.Equal(1, outcome.Executed);
        // The recording had the board at (0, 0); the live board moved to (100, 200), and both ends move with it.
        Assert.Equal(new object?[] { (200.0, 250.0, 400.0, 350.0) }, drags);
        var gone = MakeHost([board, Email]);
        var lost = await Replay.ReplayTrace(gone, [Moved(Place("Tray", 0.5, 0.5))]);
        Assert.False(lost.Completed);
        Assert.Equal(0, lost.Executed);
        Assert.Equal(ReplayHandOffReason.TargetNotFound, lost.StopReason);
        Assert.DoesNotContain("dragAt", gone.Calls);
    }
}
