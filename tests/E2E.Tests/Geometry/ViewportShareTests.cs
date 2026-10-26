// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.Geometry;

/// <summary>
/// <c>describe('viewportShare')</c> of <c>geometry.test.ts</c>. <c>describe('overlapShare')</c> is not ported: the port has no
/// <c>overlapShare</c>, which only upstream's live <c>samePlace</c> relocation of a scroll target uses.
/// </summary>
public sealed class ViewportShareTests
{
    private static readonly TraceViewport Viewport = new(400, 800);

    [Fact]
    public void Counts_only_the_part_of_the_box_inside_the_viewport()
    {
        Assert.Equal(1, Internal.Geometry.ViewportShare(new BoundingBox(0, 0, 400, 800), Viewport));
        Assert.Equal(0.125, Internal.Geometry.ViewportShare(new BoundingBox(0, 700, 400, 500), Viewport), 5);
        Assert.Equal(0.5, Internal.Geometry.ViewportShare(new BoundingBox(-200, 0, 400, 800), Viewport));
        Assert.Equal(0, Internal.Geometry.ViewportShare(new BoundingBox(0, 900, 400, 500), Viewport));
        Assert.Equal(0, Internal.Geometry.ViewportShare(new BoundingBox(0, 0, 400, 800), new TraceViewport(0, 800)));
    }
}
