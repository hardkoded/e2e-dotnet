// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.Overlay.OverlayHelpers;

namespace E2E.Decision.Tests.Overlay;

/// <summary><c>describe('withGrid')</c> of <c>overlay.test.ts</c>.</summary>
public sealed class WithGridTests
{
    [Fact]
    public void Draws_cell_borders_on_the_screenshot_and_nothing_else()
    {
        var output = global::E2E.Decision.Overlay.WithGrid(White(200, 80), new Grid(2, 1, 100, 80));
        Assert.Equal(200, output.Width);
        Assert.Equal([255, 0, 255], Pixel(output.Data, 50, 0));
        Assert.Equal([255, 0, 255], Pixel(output.Data, 100, 40));
        Assert.Equal([255, 255, 255], Pixel(output.Data, 2, 2));
        Assert.Equal([255, 255, 255], Pixel(output.Data, 150, 40));
    }

    [Fact]
    public void Scales_borders_with_the_image_scale()
    {
        var output = global::E2E.Decision.Overlay.WithGrid(White(400, 160, 2), new Grid(2, 1, 100, 80));
        Assert.Equal([255, 0, 255], Pixel(output.Data, 199, 100));
        Assert.Equal([255, 255, 255], Pixel(output.Data, 300, 100));
    }

    [Fact]
    public void Returns_pixels_it_cannot_decode_untouched()
    {
        var screenshot = new Screenshot([1, 2, 3], "image/png", 1, 1, 1);
        Assert.Same(screenshot, global::E2E.Decision.Overlay.WithGrid(screenshot, new Grid(1, 1, 1, 1)));
    }
}
