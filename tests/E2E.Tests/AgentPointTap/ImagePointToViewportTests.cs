// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentPointTap;

/// <summary>Upstream's <c>describe('imagePointToViewport')</c> (<c>agent-point-tap.test.ts</c>).</summary>
public sealed class ImagePointToViewportTests
{
    private static ExecutorPixels Image(int width, int height, double scale) => new([], "image/png", width, height, scale, 0);

    [Fact]
    public void Divides_by_the_image_scale_and_rounds_a_3x_device_screenshot_maps_to_logical_points()
    {
        var point = PointTap.ImagePointToViewport(new Point(585, 1266), Image(1170, 2532, 3), new TraceViewport(390, 844));
        Assert.Equal(new Point(195, 422), point);
    }

    [Fact]
    public void Is_the_identity_for_a_CSS_scale_capture()
    {
        var point = PointTap.ImagePointToViewport(new Point(140.4, 220), Image(1280, 720, 1), new TraceViewport(1280, 720));
        Assert.Equal(new Point(140, 220), point);
    }

    [Fact]
    public void Clamps_a_point_the_model_placed_off_the_image_and_off_the_viewport()
    {
        var pixels = Image(1280, 720, 1);
        Assert.Equal(new Point(0, 719), PointTap.ImagePointToViewport(new Point(-5, 9_999), pixels, new TraceViewport(1280, 720)));
        Assert.Equal(new Point(999, 499), PointTap.ImagePointToViewport(new Point(1279, 719), pixels, new TraceViewport(1000, 500)));
    }
}
