// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using SkiaSharp;

namespace E2E.Internal;

/// <summary>An RGBA image, 8 bits per channel, rows top to bottom.</summary>
internal sealed record RgbaImage(int Width, int Height, byte[] Data);

/// <summary>A box on whole image pixels.</summary>
internal readonly record struct ImageBox(int X, int Y, int Width, int Height);

/// <summary>How far two images may differ and still match.</summary>
/// <param name="Threshold">Per-pixel color distance, 0 to 1, below which two pixels are the same.</param>
/// <param name="MaxDiffPixels">How many pixels may differ, or null.</param>
/// <param name="MaxDiffPixelRatio">What share of the pixels may differ, or null.</param>
internal sealed record ImageTolerance(double Threshold, int? MaxDiffPixels, double? MaxDiffPixelRatio);

/// <summary>What comparing two images found.</summary>
internal abstract record ImageComparison
{
    public sealed record Match(int DiffPixels) : ImageComparison;

    public sealed record SizeMismatch(ImageBox Expected, ImageBox Actual) : ImageComparison;

    public sealed record Pixels(int DiffPixels, double Ratio, RgbaImage Diff) : ImageComparison;
}

/// <summary>RGBA images in memory: PNG decoding and encoding, resampling, cropping, painting boxes, and comparing two images.</summary>
internal static class ImageOps
{
    /// <summary>
    /// Decodes PNG bytes to 8-bit RGBA: any color type, at 1 to 16 bits or with a palette. Throws on anything that is not a PNG.
    /// Skia expands the sub-byte samples, the palette, and a gray or RGB image's transparent color (alpha 0, compared at the image's own bit depth) itself.
    /// A 16-bit sample is scaled to 8 bits by Skia, which can land one level from upstream's rounding (<c>narrow</c> in <c>internal/image.ts</c>).
    /// </summary>
    public static RgbaImage DecodePng(byte[] bytes)
    {
        using var codec = SKCodec.Create(new MemoryStream(bytes));
        if (codec is null || codec.EncodedFormat != SKEncodedImageFormat.Png)
        {
            throw new InvalidDataException("not a PNG");
        }

        if (!EndsWithIend(bytes))
        {
            throw new InvalidDataException("the PNG is cut short");
        }

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        if (codec.GetPixels(info, bitmap.GetPixels()) != SKCodecResult.Success)
        {
            throw new InvalidDataException("the PNG is cut short or could not be decoded");
        }

        return new RgbaImage(info.Width, info.Height, bitmap.GetPixelSpan().ToArray());
    }

    /// <summary>Whether the chunks, walked from the signature by their lengths, reach a whole IEND chunk: a PNG cut inside its last chunk does not.</summary>
    private static bool EndsWithIend(byte[] bytes)
    {
        var at = 8;
        while (at + 12 <= bytes.Length)
        {
            var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(at));
            if (length > bytes.Length - at - 12)
            {
                return false;
            }

            if (bytes.AsSpan(at + 4, 4).SequenceEqual("IEND"u8))
            {
                // A whole end chunk: no data, and the CRC every IEND carries.
                return length == 0 && bytes.AsSpan(at + 8, 4).SequenceEqual<byte>([0xae, 0x42, 0x60, 0x82]);
            }

            at += 12 + (int)length;
        }

        return false;
    }

    /// <summary>Encodes an image as PNG bytes. A pixel buffer that does not fill the image throws (upstream's guard, which keeps its native addon from aborting the process).</summary>
    public static byte[] EncodePng(RgbaImage image)
    {
        if (image.Data.Length != (long)image.Width * image.Height * 4)
        {
            throw new ArgumentException($"a {image.Width}x{image.Height} image is {(long)image.Width * image.Height * 4} bytes of RGBA, not {image.Data.Length}");
        }

        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var pixels = SKImage.FromPixelCopy(info, image.Data);
        using var data = pixels?.Encode(SKEncodedImageFormat.Png, 100) ?? throw new InvalidOperationException("the image could not be encoded as a PNG");
        return data.ToArray();
    }

    /// <summary>Resamples an image to <paramref name="width"/> by <paramref name="height"/> with a box filter; the input itself when it already has that size.</summary>
    public static RgbaImage ResampleImage(RgbaImage image, int width, int height)
    {
        if (image.Width == width && image.Height == height)
        {
            return image;
        }

        var data = new byte[width * height * 4];
        var xRatio = (double)image.Width / width;
        var yRatio = (double)image.Height / height;
        for (var ty = 0; ty < height; ty++)
        {
            var y0 = (int)Math.Floor(ty * yRatio);
            var y1 = Math.Min(image.Height, Math.Max(y0 + 1, (int)Math.Floor((ty + 1) * yRatio)));
            for (var tx = 0; tx < width; tx++)
            {
                var x0 = (int)Math.Floor(tx * xRatio);
                var x1 = Math.Min(image.Width, Math.Max(x0 + 1, (int)Math.Floor((tx + 1) * xRatio)));
                long r = 0, g = 0, b = 0, a = 0;
                var count = 0;
                for (var y = y0; y < y1; y++)
                {
                    var offset = (y * image.Width + x0) * 4;
                    for (var x = x0; x < x1; x++)
                    {
                        r += image.Data[offset];
                        g += image.Data[offset + 1];
                        b += image.Data[offset + 2];
                        a += image.Data[offset + 3];
                        offset += 4;
                        count++;
                    }
                }

                var at = (ty * width + tx) * 4;
                data[at] = Average(r, count);
                data[at + 1] = Average(g, count);
                data[at + 2] = Average(b, count);
                data[at + 3] = Average(a, count);
            }
        }

        return new RgbaImage(width, height, data);
    }

    /// <summary>The part of <paramref name="box"/> inside the image, on whole pixels; null when none of it is.</summary>
    public static ImageBox? ClipBox(int imageWidth, int imageHeight, BoundingBox box)
    {
        var x0 = Math.Max(0, RoundHalfUp(box.X));
        var y0 = Math.Max(0, RoundHalfUp(box.Y));
        var x1 = Math.Min(imageWidth, RoundHalfUp(box.X + box.Width));
        var y1 = Math.Min(imageHeight, RoundHalfUp(box.Y + box.Height));
        return x1 > x0 && y1 > y0 ? new ImageBox(x0, y0, x1 - x0, y1 - y0) : null;
    }

    /// <summary>The pixels of a box already inside the image (see <see cref="ClipBox"/>).</summary>
    public static RgbaImage CropImage(RgbaImage image, ImageBox box)
    {
        var data = new byte[box.Width * box.Height * 4];
        for (var row = 0; row < box.Height; row++)
        {
            var from = ((box.Y + row) * image.Width + box.X) * 4;
            Buffer.BlockCopy(image.Data, from, data, row * box.Width * 4, box.Width * 4);
        }

        return new RgbaImage(box.Width, box.Height, data);
    }

    /// <summary>A copy of the image with each box, clipped to it, painted opaque in <paramref name="color"/>.</summary>
    public static RgbaImage FillBoxes(RgbaImage image, IEnumerable<BoundingBox> boxes, (byte R, byte G, byte B) color)
    {
        var data = (byte[])image.Data.Clone();
        foreach (var box in boxes)
        {
            if (ClipBox(image.Width, image.Height, box) is not { } clipped)
            {
                continue;
            }

            for (var y = clipped.Y; y < clipped.Y + clipped.Height; y++)
            {
                for (var x = clipped.X; x < clipped.X + clipped.Width; x++)
                {
                    var at = (y * image.Width + x) * 4;
                    data[at] = color.R;
                    data[at + 1] = color.G;
                    data[at + 2] = color.B;
                    data[at + 3] = 255;
                }
            }
        }

        return new RgbaImage(image.Width, image.Height, data);
    }

    /// <summary>
    /// Compares <paramref name="actual"/> against <paramref name="expected"/>. They match when they have one size
    /// and at most the tolerated number of pixels differ: <c>MaxDiffPixels</c>, a <c>MaxDiffPixelRatio</c> share
    /// of the image, the smaller of the two when both are set, and none when neither is.
    /// </summary>
    public static ImageComparison CompareImages(RgbaImage expected, RgbaImage actual, ImageTolerance tolerance)
    {
        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            return new ImageComparison.SizeMismatch(new ImageBox(0, 0, expected.Width, expected.Height), new ImageBox(0, 0, actual.Width, actual.Height));
        }

        var diff = new byte[expected.Width * expected.Height * 4];
        var diffPixels = PixelMatch.Count(expected.Data, actual.Data, diff, expected.Width, expected.Height, tolerance.Threshold);
        var total = expected.Width * expected.Height;
        double? byRatio = tolerance.MaxDiffPixelRatio is { } ratio ? total * ratio : null;
        var allowed = tolerance.MaxDiffPixels is { } pixels && byRatio is { } share
            ? Math.Min(pixels, share)
            : tolerance.MaxDiffPixels ?? byRatio ?? 0;
        return diffPixels <= allowed
            ? new ImageComparison.Match(diffPixels)
            : new ImageComparison.Pixels(diffPixels, total == 0 ? 0 : (double)diffPixels / total, new RgbaImage(expected.Width, expected.Height, diff));
    }

    private static byte Average(long sum, int count) => (byte)RoundHalfUp((double)sum / count);

    // JavaScript's Math.round: halves go up.
    private static int RoundHalfUp(double value) => (int)Math.Floor(value + 0.5);
}
