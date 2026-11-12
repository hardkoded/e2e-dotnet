// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.Geometry;

/// <summary><c>describe('overlapShare')</c> of <c>geometry.test.ts</c>.</summary>
public sealed class OverlapShareTests
{
    [Fact]
    public void Is_the_shared_area_over_the_union_1_for_the_same_box_and_0_for_boxes_apart()
    {
        var box = new BoundingBox(0, 96, 402, 778);
        Assert.Equal(1, Internal.Geometry.OverlapShare(box, box));
        Assert.Equal(160.0 / 778, Internal.Geometry.OverlapShare(box, new BoundingBox(0, 300, 402, 160)), 5);
        Assert.Equal(0, Internal.Geometry.OverlapShare(box, new BoundingBox(0, 900, 402, 100)));
    }
}
