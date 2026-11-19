// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Decision.Tests.Overlay.OverlayHelpers;

namespace E2E.Decision.Tests.Overlay;

/// <summary><c>describe('zoomAround')</c> of <c>overlay.test.ts</c>.</summary>
public sealed class ZoomAroundTests
{
    [Fact]
    public void Crops_a_box_around_the_point_kept_inside_the_image_and_enlarges_it()
    {
        var zoom = global::E2E.Decision.Overlay.ZoomAround(White(400, 300), new Point(390, 10), 100, 2);
        Assert.NotNull(zoom);
        Assert.Equal(new Point(300, 0), zoom.Origin);
        Assert.Equal(2, zoom.Factor);
        Assert.Equal((200, 200, 1.0), (zoom.Screenshot.Width, zoom.Screenshot.Height, zoom.Screenshot.Scale));
        Assert.Equal([255, 255, 255], Pixel(zoom.Screenshot.Data, 100, 100));
    }

    [Fact]
    public void Maps_a_scaled_screenshot_back_to_CSS_pixels()
    {
        var zoom = global::E2E.Decision.Overlay.ZoomAround(White(800, 600, 2), new Point(200, 150), 100, 2);
        Assert.NotNull(zoom);
        Assert.Equal(new Point(150, 100), zoom.Origin);
        Assert.Equal(200, zoom.Screenshot.Width);
    }

    [Fact]
    public void Returns_nothing_for_pixels_it_cannot_decode()
    {
        Assert.Null(
            global::E2E.Decision.Overlay.ZoomAround(new Screenshot([1], "image/png", 1, 1, 1), new Point(0, 0), 10, 2)
        );
    }

    [Fact]
    public void Returns_nothing_for_a_region_under_one_pixel_which_no_PNG_can_hold()
    {
        Assert.Null(global::E2E.Decision.Overlay.ZoomAround(White(1, 1, 4), new Point(0, 0), 10, 1));
    }
}
