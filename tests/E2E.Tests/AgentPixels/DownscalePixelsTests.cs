// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.AgentPixels;

/// <summary>
/// Sizing a screenshot for the model: the cap, the scale arithmetic, and masks that stay black. Upstream's
/// <c>ObservationPixels</c> is the port's <see cref="EngineScreenshot"/>, which carries no width or height of its own:
/// the tests read the size from the PNG.
/// </summary>
public sealed class DownscalePixelsTests
{
    /// <summary>A width by height PNG: white, with a black box at the given rect.</summary>
    private static EngineScreenshot Picture(int width, int height, ImageBox box, double scale = 1)
    {
        var data = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var at = ((y * width) + x) * 4;
                var inBox = x >= box.X && x < box.X + box.Width && y >= box.Y && y < box.Y + box.Height;
                data[at] = data[at + 1] = data[at + 2] = (byte)(inBox ? 0 : 255);
                data[at + 3] = 255;
            }
        }

        return new EngineScreenshot(ImageOps.EncodePng(new RgbaImage(width, height, data)), scale);
    }

    private static byte[] PixelAt(EngineScreenshot pixels, int x, int y)
    {
        var image = ImageOps.DecodePng(pixels.Png);
        var at = ((y * image.Width) + x) * 4;
        return image.Data[at..(at + 3)];
    }

    private static (int Width, int Height) SizeOf(EngineScreenshot pixels) => Pixels.ReadPngSize(pixels.Png)!.Value;

    [Fact]
    public void Returns_the_input_untouched_when_the_long_side_already_fits()
    {
        var shot = Picture(640, 360, new ImageBox(0, 0, 10, 10));
        Assert.Same(shot, Pixels.DownscalePixels(shot, 640));
        Assert.Same(shot, Pixels.DownscalePixels(shot, 0));
    }

    [Fact]
    public void Caps_the_long_side_keeps_the_aspect_and_scales_the_image_to_viewport_ratio_with_it()
    {
        var shot = Picture(1280, 720, new ImageBox(0, 0, 10, 10));
        var small = Pixels.DownscalePixels(shot, 640);
        Assert.Equal((640, 360), SizeOf(small));
        Assert.Equal(0.5, small.Scale, 3);
        Assert.True(small.Png.Length < shot.Png.Length);

        // A device capture at 3x keeps its ratio meaning: image pixels per CSS pixel.
        var device = Picture(1170, 2532, new ImageBox(0, 0, 10, 10), scale: 3);
        var capped = Pixels.DownscalePixels(device, 1024);
        var (width, height) = SizeOf(capped);
        Assert.Equal(1024, height);
        Assert.Equal(3 * ((double)width / 1170), capped.Scale, 3);
    }

    [Fact]
    public void Keeps_a_masked_region_black_after_resampling_and_the_surroundings_white()
    {
        var shot = Picture(1280, 720, new ImageBox(200, 200, 400, 200));
        var small = Pixels.DownscalePixels(shot, 640);
        Assert.Equal(new byte[] { 0, 0, 0 }, PixelAt(small, 200, 150));
        Assert.Equal(new byte[] { 255, 255, 255 }, PixelAt(small, 50, 50));
    }
}
