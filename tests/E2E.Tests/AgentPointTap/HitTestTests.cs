// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentPointTap;

/// <summary>Upstream's <c>describe('hitTest')</c> (<c>agent-point-tap.test.ts</c>).</summary>
public sealed class HitTestTests
{
    [Fact]
    public void Resolves_a_point_inside_a_control_to_the_innermost_control()
    {
        var hit = PointTap.HitTest(PointTapScreen.Screen, new Point(50, 30));
        Assert.Equal("n2", hit.Control?.Ref);
        Assert.Equal("n2", hit.Under?.Ref);
    }

    [Fact]
    public void Reports_what_is_under_a_point_that_lands_on_no_control()
    {
        var hit = PointTap.HitTest(PointTapScreen.Screen, new Point(640, 120));
        Assert.Null(hit.Control);
        Assert.Equal("n5", hit.Under?.Ref);
    }

    [Fact]
    public void Skips_hidden_and_disabled_controls()
    {
        Assert.Null(PointTap.HitTest(PointTapScreen.Screen, new Point(250, 30)).Control);
        Assert.Equal("n1", PointTap.HitTest(PointTapScreen.Screen, new Point(250, 30)).Under?.Ref);
        Assert.Null(PointTap.HitTest(PointTapScreen.Screen, new Point(450, 30)).Control);
    }

    // Partial of upstream's "finds a control inside a nested document, whose box is in top-level viewport pixels too": the port's node has no
    // framePath, so the control is found like any other node by its box; the nested document itself is not built.
    [Fact]
    public void Partial_finds_a_control_whose_box_is_in_top_level_viewport_pixels()
    {
        var framed = PointTap.HitTest(PointTapScreen.Screen, new Point(50, 220));
        Assert.Equal("n6", framed.Control?.Ref);
        Assert.Equal("n6", framed.Under?.Ref);
    }

    [Fact]
    public void Treats_the_box_as_half_open_so_adjacent_controls_never_share_an_edge()
    {
        Assert.Null(PointTap.HitTest(PointTapScreen.Screen, new Point(110, 30)).Control);
        Assert.Equal("n2", PointTap.HitTest(PointTapScreen.Screen, new Point(109, 30)).Control?.Ref);
    }
}
