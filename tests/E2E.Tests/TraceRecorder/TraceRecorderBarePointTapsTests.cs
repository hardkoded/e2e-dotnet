// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Encodings.Web;
using System.Text.Json;
using E2E.Engine;
using static E2E.Tests.TraceRecorder.TraceRecorderTests;

namespace E2E.Tests.TraceRecorder;

/// <summary>
/// <c>describe('TraceRecorder: bare-point taps')</c> of <c>trace-recorder.test.ts</c>. All four its are partial: the port keeps no summary,
/// so the <c>summary</c> each upstream <c>toEqual</c> checks is not checked, and its stored entry is a typed object, so a field of the wrong
/// JSON type (upstream's <c>point.x</c> as <c>'left'</c>) cannot be built and is not checked.
/// </summary>
public sealed class TraceRecorderBarePointTapsTests
{
    private static readonly JsonSerializerOptions Relaxed = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly TraceViewport Viewport = new(1280, 720);

    private static readonly SemanticNode Map = new() { Ref = "m1", Role = "img", Name = "Map", Rect = new BoundingBox(100, 200, 400, 200) };

    [Fact]
    public void Partial_Records_the_point_with_its_viewport_and_where_it_sat_inside_the_node_that_contained_it()
    {
        var recorder = MakeRecorder();
        recorder.Record("tapAt", new PlacedPoint(new Point(400, 260), Map), Viewport);

        var entry = TraceRecorderTests.Finalize(recorder, "/canvas");

        var action = Assert.Single(entry!.Actions);
        Assert.Equal("tapAt", action.Kind);
        Assert.Equal(new Point(400, 260), action.Point);
        Assert.Equal(Viewport, action.Viewport);
        Assert.Equal(("img", "Map", 0.75, 0.3), (action.Under?.Target?.Role, action.Under?.Target?.Name, action.Under?.Fx, action.Under?.Fy));
        Assert.Null(action.From);
        Assert.Null(action.To);
    }

    [Fact]
    public void Partial_Records_a_point_under_an_anonymous_container_by_the_viewport_alone_since_a_group_among_groups_cannot_be_re_found()
    {
        var recorder = MakeRecorder();
        var merged = new SemanticNode { Ref = "g1", Role = "group", Rect = new BoundingBox(0, 0, 1280, 720) };
        recorder.Record("tapAt", new PlacedPoint(new Point(400, 260), merged), Viewport);

        var action = Assert.Single(TraceRecorderTests.Finalize(recorder, "/canvas")!.Actions);

        Assert.Equal(("tapAt", new Point(400, 260), Viewport), (action.Kind, action.Point!.Value, action.Viewport));
        Assert.Null(action.Under);
    }

    [Fact]
    public void Partial_Records_a_point_on_nothing_listed_by_the_viewport_alone_and_reads_it_back()
    {
        var recorder = MakeRecorder();
        recorder.Record("tapAt", new PlacedPoint(new Point(300, 60)), Viewport);
        var entry = TraceRecorderTests.Finalize(recorder, "/canvas")!;

        var action = Assert.Single(entry.Actions);
        Assert.Equal(("tapAt", new Point(300, 60), Viewport), (action.Kind, action.Point!.Value, action.Viewport));
        Assert.Null(action.Under);
        Assert.Equal(JsonSerializer.Serialize(entry.Actions, Relaxed), JsonSerializer.Serialize(RoundTrip(entry).Actions, Relaxed));
    }

    [Fact]
    public void Partial_Rejects_a_stored_point_tap_whose_viewport_or_fraction_is_malformed()
    {
        var recorder = MakeRecorder();
        recorder.Record("tapAt", new PlacedPoint(new Point(300, 60)), Viewport);
        var stored = Assert.Single(RoundTrip(TraceRecorderTests.Finalize(recorder, "/canvas")!).Actions);
        Assert.True(stored.HasValidPoints());

        Assert.False(Broken(stored, action => action.Viewport = new TraceViewport(0, 720)).HasValidPoints());
        Assert.False(Broken(stored, action => action.Under = new RecordedPointPlace { Target = new RecordedTarget { Role = "img" }, Fx = 1.5, Fy = 0 }).HasValidPoints());
    }

    private static RecordedAction Broken(RecordedAction stored, Action<RecordedAction> patch)
    {
        var copy = stored.Copy();
        patch(copy);
        return copy;
    }
}
