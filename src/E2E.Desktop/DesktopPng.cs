// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Desktop;

/// <summary>
/// Screenshot masking: a window capture decoded, cropped to the window, its secure regions painted black, encoded again. .NET-only holder name
/// (C# has no free functions): upstream's <c>windowImage</c>, <c>describeCapture</c> and <c>encodePng</c> in <c>png.ts</c>. Decoding and encoding
/// are <see cref="ImageOps"/> (upstream's <c>@napi-rs/image</c> calls).
/// </summary>
internal static class DesktopPng
{
    /// <summary>How far, in image pixels, the window in a capture may differ from the size its bounds give: points times scale, rounded.</summary>
    private const int SizeTolerance = 1;

    /// <summary>The widest outline macOS draws around a window in a capture, in image pixels on each side.</summary>
    private const int MaxOutline = 4;

    /// <summary>How far each mask reaches past its field, in image pixels, so a field off by the tolerance is still covered.</summary>
    private const int MaskMargin = 2;

    /// <summary>
    /// The window's own pixels out of a window capture, every rectangle (in the window's image pixels) painted opaque black. macOS draws the
    /// window's shadow into the capture, unevenly on each side, so the window is found as the box of fully opaque pixels, which the shadow never
    /// is, and cropped to. A capture within a pixel of <paramref name="expectedWidth"/> by <paramref name="expectedHeight"/> (the window's size in
    /// image pixels) is the window already; any other is cropped to its opaque box, which must then be within a pixel of it, or hold the window
    /// inside an outline of even width on either axis. An outline's width is not seen side by side, so its crop may sit off by up to half the
    /// extra on each axis: every mask then reaches that much further. Null otherwise: the rectangles would land somewhere else, and an image whose
    /// masking cannot be proven must not leave the engine. Upstream's <c>windowImage</c>.
    /// </summary>
    public static RgbaImage? WindowImage(byte[] bytes, int expectedWidth, int expectedHeight, IReadOnlyList<Rect> rects)
    {
        var image = ImageOps.DecodePng(bytes);
        bool Fits(int width, int height) =>
            Math.Abs(width - expectedWidth) <= SizeTolerance && Math.Abs(height - expectedHeight) <= SizeTolerance;
        var window = image;
        var reachX = MaskMargin;
        var reachY = MaskMargin;
        if (!Fits(image.Width, image.Height))
        {
            if (OpaqueBox(image) is not { } box)
            {
                return null;
            }

            var inner = Fits(box.Width, box.Height) ? box : InsideOutline(box, expectedWidth, expectedHeight);
            if (inner is not { } crop)
            {
                return null;
            }

            reachX += crop.X - box.X;
            reachY += crop.Y - box.Y;
            window = ImageOps.CropImage(image, crop);
        }

        foreach (var rect in rects)
        {
            var x0 = (int)Math.Max(0, Math.Floor(rect.X) - reachX);
            var y0 = (int)Math.Max(0, Math.Floor(rect.Y) - reachY);
            var x1 = (int)Math.Min(window.Width, Math.Ceiling(rect.X + rect.Width) + reachX);
            var y1 = (int)Math.Min(window.Height, Math.Ceiling(rect.Y + rect.Height) + reachY);
            for (var y = y0; y < y1; y++)
            {
                for (var x = x0; x < x1; x++)
                {
                    var at = ((y * window.Width) + x) * 4;
                    window.Data[at] = 0;
                    window.Data[at + 1] = 0;
                    window.Data[at + 2] = 0;
                    window.Data[at + 3] = 255;
                }
            }
        }

        return window;
    }

    /// <summary>What a capture holds, for an error that says why it was refused: its size, and the box of its fully opaque pixels. Upstream's <c>describeCapture</c>.</summary>
    public static string DescribeCapture(byte[] bytes)
    {
        var image = ImageOps.DecodePng(bytes);
        return $"{image.Width}x{image.Height}"
            + (
                OpaqueBox(image) is { } box
                    ? $", its opaque box {box.Width}x{box.Height} at ({box.X}, {box.Y})"
                    : ", none of it opaque"
            );
    }

    /// <summary>PNG bytes of an image. Upstream's <c>encodePng</c>.</summary>
    public static byte[] EncodePng(RgbaImage image) => ImageOps.EncodePng(image);

    /// <summary>
    /// The window inside the outline macOS can draw around it: equally wide on both sides of an axis, and on one axis or both. A box larger than
    /// the window by an even number of pixels on each axis, at most <see cref="MaxOutline"/> on each side, holds the window at its center. Null
    /// for a box of any other size; a one-sided outline would put the crop off by its width, so it is not taken.
    /// </summary>
    private static ImageBox? InsideOutline(ImageBox box, int expectedWidth, int expectedHeight)
    {
        var extraWidth = box.Width - expectedWidth;
        var extraHeight = box.Height - expectedHeight;
        static bool Outline(int extra) => extra >= 0 && extra % 2 == 0 && extra <= MaxOutline * 2;
        return Outline(extraWidth) && Outline(extraHeight)
            ? new ImageBox(box.X + (extraWidth / 2), box.Y + (extraHeight / 2), expectedWidth, expectedHeight)
            : null;
    }

    /// <summary>The smallest box holding every fully opaque pixel; null when there is none.</summary>
    private static ImageBox? OpaqueBox(RgbaImage image)
    {
        var left = image.Width;
        var top = image.Height;
        var right = -1;
        var bottom = -1;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (image.Data[(((y * image.Width) + x) * 4) + 3] != 255)
                {
                    continue;
                }

                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }

        return right < 0 ? null : new ImageBox(left, top, right - left + 1, bottom - top + 1);
    }
}
