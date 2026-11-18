// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Decision.Tests.Overlay.OverlayHelpers;

namespace E2E.Decision.Tests.Overlay;

/// <summary><c>describe('every PNG color type')</c> of <c>overlay.test.ts</c>.</summary>
public sealed class EveryPngColorTypeTests
{
    public static TheoryData<string> Labels =>
        ["RGB", "palette", "gray", "gray with alpha", "16-bit RGB", "16-bit RGBA"];

    private static (Screenshot Shot, int[] Rgb) Shot(string label) =>
        label switch
        {
            "RGB" => (Filled(8, 2, [10, 20, 30]), [10, 20, 30]),
            "palette" => (
                Filled(
                    8,
                    3,
                    [1],
                    [
                        [255, 0, 0],
                        [10, 20, 30],
                    ]
                ),
                [10, 20, 30]
            ),
            "gray" => (Filled(8, 0, [200]), [200, 200, 200]),
            "gray with alpha" => (Filled(8, 4, [200, 128]), [200, 200, 200]),
            "16-bit RGB" => (Filled(16, 2, [255, 255, 128, 0, 1, 1]), [255, 128, 1]),
            _ => (Filled(16, 6, [0, 0, 255, 255, 0, 0, 255, 255]), [0, 255, 0]),
        };

    [Theory]
    [MemberData(nameof(Labels))]
    public void Reads_a_screenshot_into_the_grid_and_the_zoom(string label)
    {
        var (shot, rgb) = Shot(label);
        var grid = global::E2E.Decision.Overlay.WithGrid(shot, new Grid(1, 1, 4, 4));
        Assert.Equal([255, 0, 255], Pixel(grid.Data, 0, 0));
        Assert.Equal(rgb, Pixel(grid.Data, 1, 1));
        var zoom = global::E2E.Decision.Overlay.ZoomAround(shot, new Point(2, 2), 4, 1);
        Assert.NotNull(zoom);
        Assert.Equal((4, 4), (zoom.Screenshot.Width, zoom.Screenshot.Height));
        Assert.Equal(rgb, Pixel(zoom.Screenshot.Data, 1, 1));
    }
}
