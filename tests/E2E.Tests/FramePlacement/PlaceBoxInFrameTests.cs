// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.FramePlacement;

/// <summary>Upstream's <c>describe('placeBoxInFrame')</c> of <c>packages/web/tests/unit/frame-placement.test.ts</c>.</summary>
public sealed class PlaceBoxInFrameTests
{
    private static readonly BoundingBox Frame = new(40, 30, 300, 150);

    [Fact]
    public void Shifts_a_box_inside_the_frame_with_no_origin_when_its_corner_is_its_own()
    {
        var placed = E2E.Engine.FramePlacement.PlaceBoxInFrame(new BoundingBox(10, 20, 50, 40), null, Frame);

        Assert.Equal((new BoundingBox(50, 50, 50, 40), (Point?)null), placed);
    }

    [Fact]
    public void Shifts_a_bordered_node_origin_with_its_box()
    {
        var placed = E2E.Engine.FramePlacement.PlaceBoxInFrame(new BoundingBox(10, 20, 50, 40), new Point(18, 28), Frame);

        Assert.Equal((new BoundingBox(50, 50, 50, 40), (Point?)new Point(58, 58)), placed);
    }

    [Fact]
    public void Keeps_the_unclipped_corner_as_the_origin_of_a_box_the_frame_clips()
    {
        var placed = E2E.Engine.FramePlacement.PlaceBoxInFrame(new BoundingBox(0, -125, 200, 400), null, Frame);

        Assert.Equal((new BoundingBox(40, 30, 200, 150), (Point?)new Point(40, -95)), placed);
    }

    [Fact]
    public void Keeps_a_bordered_origin_unclipped_when_the_frame_clips_the_box()
    {
        var placed = E2E.Engine.FramePlacement.PlaceBoxInFrame(new BoundingBox(0, -125, 200, 400), new Point(1, -124), Frame);

        Assert.Equal((new BoundingBox(40, 30, 200, 150), (Point?)new Point(41, -94)), placed);
    }

    [Fact]
    public void Drops_the_box_and_origin_of_a_node_outside_the_frame()
    {
        var placed = E2E.Engine.FramePlacement.PlaceBoxInFrame(new BoundingBox(0, 400, 20, 20), new Point(1, 401), Frame);

        Assert.Equal(((BoundingBox?)null, (Point?)null), placed);
    }
}
