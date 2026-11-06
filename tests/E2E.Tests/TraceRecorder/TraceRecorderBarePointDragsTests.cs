// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Encodings.Web;
using System.Text.Json;
using E2E.Engine;
using static E2E.Tests.TraceRecorder.TraceRecorderTests;

namespace E2E.Tests.TraceRecorder;

/// <summary>
/// <c>describe('TraceRecorder: bare-point drags')</c> of <c>trace-recorder.test.ts</c>. All four its are partial: the port keeps no summary,
/// so the <c>summary</c> upstream's <c>toEqual</c> checks is not checked; it records no twin <c>position</c>, so the place among twins is
/// the container key only; and its stored entry is a typed object, so a field of the wrong JSON type (upstream's <c>from</c> as a bare
/// <c>{ x, y }</c>) cannot be built and is not checked.
/// </summary>
public sealed class TraceRecorderBarePointDragsTests
{
    private static readonly JsonSerializerOptions Relaxed = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly TraceViewport Viewport = new(1280, 720);

    private static readonly SemanticNode Board = new() { Ref = "b1", Role = "img", Name = "Board", Rect = new BoundingBox(100, 200, 400, 200) };

    [Fact]
    public void Partial_Records_both_points_with_the_viewport_and_where_each_sat_inside_the_node_that_contained_it()
    {
        var recorder = MakeRecorder();
        recorder.Record(new PlacedPoint(new Point(200, 250), Board), new PlacedPoint(new Point(400, 350), Board), Viewport);

        var action = Assert.Single(TraceRecorderTests.Finalize(recorder, "/canvas")!.Actions);

        Assert.Equal("dragAt", action.Kind);
        Assert.Equal(Viewport, action.Viewport);
        Assert.Equal((new Point(200, 250), "img", "Board", 0.25, 0.25), (action.From!.Point, action.From.Under?.Target?.Role, action.From.Under?.Target?.Name, action.From.Under?.Fx, action.From.Under?.Fy));
        Assert.Equal((new Point(400, 350), "img", "Board", 0.75, 0.75), (action.To!.Point, action.To.Under?.Target?.Role, action.To.Under?.Target?.Name, action.To.Under?.Fx, action.To.Under?.Fy));
        Assert.Null(action.Point);
        Assert.Null(action.Under);
    }

    [Fact]
    public void Partial_Keeps_the_container_and_place_among_twins_of_the_node_at_each_end()
    {
        var recorder = MakeRecorder();
        recorder.Record(new PlacedPoint(new Point(200, 250), Board, "Sprint 1"), new PlacedPoint(new Point(400, 350), Board, "Sprint 2"), Viewport);

        var action = Assert.Single(TraceRecorderTests.Finalize(recorder, "/canvas")!.Actions);

        Assert.Equal(("img", "Board", "Sprint 1"), (action.From!.Under!.Target!.Role, action.From.Under.Target.Name, action.From.Under.Target.Within));
        Assert.Equal(("img", "Board", "Sprint 2"), (action.To!.Under!.Target!.Role, action.To.Under.Target.Name, action.To.Under.Target.Within));
    }

    [Fact]
    public void Partial_Records_an_end_on_nothing_listed_by_the_viewport_alone_and_reads_the_drag_back()
    {
        var recorder = MakeRecorder();
        recorder.Record(new PlacedPoint(new Point(200, 250), Board), new PlacedPoint(new Point(700, 600)), Viewport);
        var entry = TraceRecorderTests.Finalize(recorder, "/canvas")!;

        var action = Assert.Single(entry.Actions);
        Assert.Equal((new Point(200, 250), 0.25, 0.25), (action.From!.Point, action.From.Under?.Fx, action.From.Under?.Fy));
        Assert.Equal(new Point(700, 600), action.To!.Point);
        Assert.Null(action.To.Under);
        Assert.Equal(Viewport, action.Viewport);
        Assert.Equal(JsonSerializer.Serialize(entry.Actions, Relaxed), JsonSerializer.Serialize(RoundTrip(entry).Actions, Relaxed));
    }

    [Fact]
    public void Partial_Rejects_a_stored_drag_whose_point_viewport_or_fraction_is_malformed()
    {
        var recorder = MakeRecorder();
        recorder.Record(new PlacedPoint(new Point(200, 250)), new PlacedPoint(new Point(700, 600)), Viewport);
        var stored = Assert.Single(RoundTrip(TraceRecorderTests.Finalize(recorder, "/canvas")!).Actions);
        Assert.True(stored.HasValidPoints());

        Assert.False(Broken(stored, action => action.To = null).HasValidPoints());
        Assert.False(Broken(stored, action => action.Viewport = new TraceViewport(1280, -1)).HasValidPoints());
        Assert.False(Broken(stored, action => action.To = new RecordedPointEnd { Point = new Point(700, 600), Under = new RecordedPointPlace { Target = new RecordedTarget { Role = "img" }, Fx = 0, Fy = 2 } }).HasValidPoints());
    }

    private static RecordedAction Broken(RecordedAction stored, Action<RecordedAction> patch)
    {
        var copy = stored.Copy();
        patch(copy);
        return copy;
    }
}
