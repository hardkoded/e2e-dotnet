// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine;
using static E2E.Tests.TraceRecorder.TraceRecorderTests;

namespace E2E.Tests.TraceRecorder;

/// <summary>
/// <c>describe('TraceRecorder: bare-point taps')</c> of <c>trace-recorder.test.ts</c>. The last <c>it</c> reads the broken entry
/// through <see cref="TraceRecorderTests.ReadsAfter"/>, the port's stand-in for upstream's <c>readTraceEntry</c>.
/// </summary>
public sealed class TraceRecorderBarePointTapsTests
{
    private static readonly JsonSerializerOptions Relaxed = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly TraceViewport Viewport = new(1280, 720);

    private static readonly SemanticNode Map = new() { Ref = "m1", Role = "img", Name = "Map", Rect = new BoundingBox(100, 200, 400, 200) };

    [Fact]
    public void Records_the_point_with_its_viewport_and_where_it_sat_inside_the_node_that_contained_it()
    {
        var recorder = MakeRecorder();
        recorder.Record("tapAt", new PlacedPoint(new Point(400, 260), Map), Viewport);

        var entry = TraceRecorderTests.Finalize(recorder, "/canvas");

        var action = Assert.Single(entry!.Actions);
        Assert.Equal("tapAt", action.Kind);
        Assert.Equal("tap the point (400, 260) on img \"Map\"", action.Summary);
        Assert.Equal(new Point(400, 260), action.Point);
        Assert.Equal(Viewport, action.Viewport);
        Assert.Equal(("img", "Map", 0.75, 0.3), (action.Under?.Target?.Role, action.Under?.Target?.Name, action.Under?.Fx, action.Under?.Fy));
        Assert.Null(action.From);
        Assert.Null(action.To);
        Assert.Null(entry.Truncated);
    }

    [Fact]
    public void Records_a_point_under_an_anonymous_container_by_the_viewport_alone_since_a_group_among_groups_cannot_be_re_found()
    {
        var recorder = MakeRecorder();
        var merged = new SemanticNode { Ref = "g1", Role = "group", Rect = new BoundingBox(0, 0, 1280, 720) };
        recorder.Record("tapAt", new PlacedPoint(new Point(400, 260), merged), Viewport);

        var entry = TraceRecorderTests.Finalize(recorder, "/canvas")!;
        var action = Assert.Single(entry.Actions);

        Assert.Equal(("tapAt", "tap the point (400, 260) on group", new Point(400, 260), Viewport), (action.Kind, action.Summary, action.Point!.Value, action.Viewport));
        Assert.Null(action.Under);
        Assert.Null(entry.Truncated);
    }

    [Fact]
    public void Records_a_point_on_nothing_listed_by_the_viewport_alone_and_reads_it_back()
    {
        var recorder = MakeRecorder();
        recorder.Record("tapAt", new PlacedPoint(new Point(300, 60)), Viewport);
        var entry = TraceRecorderTests.Finalize(recorder, "/canvas")!;

        var action = Assert.Single(entry.Actions);
        Assert.Equal(("tapAt", "tap the point (300, 60)", new Point(300, 60), Viewport), (action.Kind, action.Summary, action.Point!.Value, action.Viewport));
        Assert.Null(action.Under);
        Assert.Equal(action.Summary, Assert.Single(RoundTrip(entry).Actions).Summary);
        Assert.Equal(JsonSerializer.Serialize(entry.Actions, Relaxed), JsonSerializer.Serialize(RoundTrip(entry).Actions, Relaxed));
    }

    [Fact]
    public void Rejects_a_stored_point_tap_whose_viewport_or_fraction_is_malformed()
    {
        var recorder = MakeRecorder();
        recorder.Record("tapAt", new PlacedPoint(new Point(300, 60)), Viewport);
        var stored = Stored(TraceRecorderTests.Finalize(recorder, "/canvas")!);

        Assert.True(ReadsAfter(stored, 0, _ => { }));
        Assert.True(ReadsAfter(stored, 0, action => action["under"] = Place(0, 1)));
        Assert.False(ReadsAfter(stored, 0, action => action["viewport"] = new JsonObject { ["width"] = 0, ["height"] = 720 }));
        Assert.False(ReadsAfter(stored, 0, action => action["under"] = Place(1.5, 0)));
        Assert.False(ReadsAfter(stored, 0, action => action["under"] = Place(-0.1, 0)));
        Assert.False(ReadsAfter(stored, 0, action => action["point"] = new JsonObject { ["x"] = "left", ["y"] = 60 }));
    }

    private static JsonObject Place(double fx, double fy) => new() { ["target"] = new JsonObject { ["role"] = "img" }, ["fx"] = fx, ["fy"] = fy };
}
