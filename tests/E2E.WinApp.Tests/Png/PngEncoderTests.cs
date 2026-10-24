// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.WinApp;
using E2E.WinApp.Tests.Helpers;

namespace E2E.WinApp.Tests.Png;

/// <summary>.NET-only: the engine's own PNG writer, since the <c>e2e</c> tool ships without the core's native image library.</summary>
public sealed class PngEncoderTests
{
    [Fact]
    public void Writes_a_png_that_reads_back_pixel_for_pixel()
    {
        var rgba = new byte[3 * 2 * 4];
        for (var at = 0; at < rgba.Length; at++)
        {
            rgba[at] = (byte)(at * 7);
        }

        var png = PngEncoder.Encode(rgba, 3, 2);
        var image = PngReader.Read(png);
        Assert.Equal(3, image.Width);
        Assert.Equal(2, image.Height);
        Assert.Equal(rgba, image.Rgba);
    }

    [Fact]
    public void Refuses_a_buffer_that_does_not_match_the_size()
    {
        Assert.Throws<ArgumentException>(() => PngEncoder.Encode(new byte[5], 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PngEncoder.Encode([], 0, 1));
    }
}
