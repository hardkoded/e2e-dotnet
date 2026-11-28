// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop;
using E2E.Internal;

namespace E2E.Desktop.Tests.Png;

/// <summary>Ported from <c>describe('windowImage')</c> in upstream's <c>packages/desktop/tests/unit/png.test.ts</c>.</summary>
public sealed class WindowImageTests
{
    /// <summary>A capture as macOS takes it: the opaque window inside a half-transparent shadow.</summary>
    private static byte[] Capture(int width, int height, (int X, int Y, int Width, int Height) window)
    {
        var data = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var inside =
                    x >= window.X && x < window.X + window.Width && y >= window.Y && y < window.Y + window.Height;
                var at = ((y * width) + x) * 4;
                (inside ? new byte[] { 200, 200, 200, 255 } : [0, 0, 0, 90]).CopyTo(data, at);
            }
        }

        return ImageOps.EncodePng(new RgbaImage(width, height, data));
    }

    private static byte[] Pixel(RgbaImage image, int x, int y)
    {
        var at = ((y * image.Width) + x) * 4;
        return image.Data[at..(at + 4)];
    }

    private static void AssertSize(RgbaImage? image, int width, int height)
    {
        Assert.NotNull(image);
        Assert.Equal(width, image.Width);
        Assert.Equal(height, image.Height);
    }

    [Fact]
    public void Crops_the_shadow_off_and_masks_in_window_coordinates()
    {
        var image = DesktopPng.WindowImage(Capture(30, 24, (6, 3, 16, 10)), 16, 10, [new Rect(2, 2, 4, 3)]);
        AssertSize(image, 16, 10);
        Assert.Equal(new byte[] { 0, 0, 0, 255 }, Pixel(image!, 3, 3));
        Assert.Equal(new byte[] { 200, 200, 200, 255 }, Pixel(image!, 10, 8));
        Assert.Equal(new byte[] { 0x50, 0x4e, 0x47 }, DesktopPng.EncodePng(image!)[1..4]);
    }

    [Fact]
    public void Takes_a_capture_with_no_shadow_as_the_window()
    {
        var image = DesktopPng.WindowImage(Capture(16, 10, (0, 0, 16, 10)), 16, 10, []);
        AssertSize(image, 16, 10);
    }

    [Fact]
    public void Masks_a_secure_field_wherever_an_outline_of_even_width_puts_the_window_on_one_side_only_too()
    {
        // A 108x100 opaque capture of a 100x100 window that starts 1 px in, not at the 4 px a centered outline would give.
        const int width = 108;
        const int height = 100;
        var data = new byte[width * height * 4];
        for (var at = 0; at < data.Length; at += 4)
        {
            new byte[] { 200, 200, 200, 255 }.CopyTo(data, at);
        }

        var field = new Rect(10, 20, 4, 3);
        for (var y = (int)field.Y; y < field.Y + field.Height; y++)
        {
            for (var x = (int)field.X + 1; x < field.X + 1 + field.Width; x++)
            {
                new byte[] { 255, 0, 0, 255 }.CopyTo(data, ((y * width) + x) * 4);
            }
        }

        var png = ImageOps.EncodePng(new RgbaImage(width, height, data));
        var image = DesktopPng.WindowImage(png, 100, 100, [field]);
        AssertSize(image, 100, 100);
        for (var at = 0; at < image!.Data.Length; at += 4)
        {
            Assert.NotEqual(new byte[] { 255, 0, 0, 255 }, image.Data[at..(at + 4)]);
        }
    }

    [Fact]
    public void Withholds_a_capture_with_a_border_of_odd_width_rather_than_mask_it_in_the_wrong_place()
    {
        var bordered = Capture(13, 10, (0, 0, 13, 10));
        Assert.Null(DesktopPng.WindowImage(bordered, 10, 10, [new Rect(7, 2, 3, 3)]));
    }

    [Fact]
    public void Covers_a_field_off_by_a_pixel_of_rounding()
    {
        var image = DesktopPng.WindowImage(Capture(16, 10, (0, 0, 16, 10)), 16, 10, [new Rect(4, 4, 2, 2)]);
        Assert.Equal(new byte[] { 0, 0, 0, 255 }, Pixel(image!, 3, 3));
        Assert.Equal(new byte[] { 0, 0, 0, 255 }, Pixel(image!, 6, 6));
    }

    [Fact]
    public void Crops_a_window_out_of_the_outline_macOS_draws_around_it_and_masks_in_window_coordinates()
    {
        // An 844x708 capture: a 1-px transparent margin, a 1-px opaque outline, and the 840x704 window inside.
        const int width = 844;
        const int height = 708;
        var data = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var at = ((y * width) + x) * 4;
                var margin = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                var outline = !margin && (x == 1 || y == 1 || x == width - 2 || y == height - 2);
                (
                    margin ? new byte[] { 0, 0, 0, 0 }
                    : outline ? [90, 90, 90, 255]
                    : [200, 200, 200, 255]
                ).CopyTo(data, at);
            }
        }

        var png = ImageOps.EncodePng(new RgbaImage(width, height, data));
        var image = DesktopPng.WindowImage(png, 840, 704, [new Rect(100, 100, 50, 20)]);
        AssertSize(image, 840, 704);
        // The window's own corner, not the outline.
        Assert.Equal(new byte[] { 200, 200, 200, 255 }, Pixel(image!, 0, 0));
        Assert.Equal(new byte[] { 200, 200, 200, 255 }, Pixel(image!, 839, 703));
        // The mask, in window coordinates, with its 2-px margin.
        Assert.Equal(new byte[] { 0, 0, 0, 255 }, Pixel(image!, 98, 98));
        Assert.Equal(new byte[] { 0, 0, 0, 255 }, Pixel(image!, 151, 121));
        Assert.Equal(new byte[] { 200, 200, 200, 255 }, Pixel(image!, 153, 123));
    }

    [Fact]
    public void Withholds_a_capture_whose_opaque_box_is_larger_by_an_odd_amount_or_by_more_than_an_outline_and_crops_the_center_of_an_even_one()
    {
        // Odd: 3 px wider.
        Assert.Null(DesktopPng.WindowImage(Capture(19, 12, (0, 0, 19, 12)), 16, 10, []));
        // Wider than any outline: 10 px on each side.
        Assert.Null(DesktopPng.WindowImage(Capture(36, 10, (0, 0, 36, 10)), 16, 10, []));
        // Even on each axis, within an outline: the center, even where the axes differ.
        AssertSize(DesktopPng.WindowImage(Capture(18, 14, (0, 0, 18, 14)), 16, 10, []), 16, 10);
    }

    [Fact]
    public void Withholds_a_capture_whose_window_is_not_the_size_expected()
    {
        Assert.Null(DesktopPng.WindowImage(Capture(30, 24, (6, 3, 16, 10)), 40, 10, []));
    }
}
