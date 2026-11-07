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
    /// Skia cannot hand out 16-bit samples, so a 16-bit image is read from its chunks (<see cref="ReadSixteenBitPng"/>) and each sample is scaled to 8 bits with rounding, as upstream's <c>narrow</c> does in <c>internal/image.ts</c>.
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

        if (bytes[24] == 16)
        {
            return ReadSixteenBitPng(bytes, info.Width, info.Height);
        }

        return new RgbaImage(info.Width, info.Height, bitmap.GetPixelSpan().ToArray());
    }

    /// <summary>
    /// A 16-bit gray, gray-alpha, RGB, or RGBA PNG as 8-bit RGBA: inflate the image data, unfilter each scanline (Adam7 passes included), apply the
    /// transparent color (alpha 0 where the 16-bit samples equal it), and <see cref="Narrow"/> each sample. .NET-only: upstream's addon hands out the 16-bit pixels itself.
    /// The caller has already had Skia accept the file, so the chunks are well formed.
    /// </summary>
    private static RgbaImage ReadSixteenBitPng(byte[] bytes, int width, int height)
    {
        var colorType = bytes[25];
        var interlaced = bytes[28] == 1;
        var channels = colorType switch { 0 => 1, 2 => 3, 4 => 2, 6 => 4, _ => throw new InvalidDataException($"unsupported PNG color type {colorType}") };
        using var idat = new MemoryStream();
        byte[]? transparent = null;
        for (var at = 8; at + 12 <= bytes.Length;)
        {
            var declared = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(at));
            if (declared > bytes.Length - at - 12)
            {
                throw new InvalidDataException("the PNG is cut short");
            }

            var length = (int)declared;
            var type = bytes.AsSpan(at + 4, 4);
            if (type.SequenceEqual("IDAT"u8))
            {
                idat.Write(bytes, at + 8, length);
            }
            else if (type.SequenceEqual("tRNS"u8) && colorType is 0 or 2)
            {
                transparent = bytes.AsSpan(at + 8, length).ToArray();
            }

            at += 12 + length;
        }

        var passes = interlaced
            ? new (int X, int Y, int DX, int DY)[] { (0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2) }
            : [(0, 0, 1, 1)];
        var bytesPerPixel = channels * 2;
        var samples = new byte[(long)width * height * bytesPerPixel];
        idat.Position = 0;
        using var inflated = new System.IO.Compression.ZLibStream(idat, System.IO.Compression.CompressionMode.Decompress);
        foreach (var (x0, y0, dx, dy) in passes)
        {
            var passWidth = (width - x0 + dx - 1) / dx;
            var passHeight = (height - y0 + dy - 1) / dy;
            if (passWidth <= 0 || passHeight <= 0)
            {
                continue;
            }

            var stride = passWidth * bytesPerPixel;
            var previous = new byte[stride];
            var line = new byte[stride];
            for (var row = 0; row < passHeight; row++)
            {
                var filter = inflated.ReadByte();
                if (filter < 0)
                {
                    throw new InvalidDataException("the PNG is cut short");
                }

                try
                {
                    inflated.ReadExactly(line);
                }
                catch (EndOfStreamException)
                {
                    throw new InvalidDataException("the PNG is cut short");
                }

                Unfilter(filter, line, previous, bytesPerPixel);
                for (var column = 0; column < passWidth; column++)
                {
                    var to = ((((long)(y0 + (row * dy)) * width) + x0 + (column * dx)) * bytesPerPixel);
                    line.AsSpan(column * bytesPerPixel, bytesPerPixel).CopyTo(samples.AsSpan((int)to));
                }

                (previous, line) = (line, previous);
            }
        }

        var pixels = (long)width * height;
        var data = new byte[pixels * 4];
        var wide = new ushort[channels];
        var gray = channels <= 2;
        for (long pixel = 0, from = 0; pixel < pixels; pixel++, from += bytesPerPixel)
        {
            for (var channel = 0; channel < channels; channel++)
            {
                wide[channel] = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(samples.AsSpan((int)from + (channel * 2)));
            }

            var to = pixel * 4;
            data[to] = Narrow(wide[0]);
            data[to + 1] = Narrow(gray ? wide[0] : wide[1]);
            data[to + 2] = Narrow(gray ? wide[0] : wide[2]);
            data[to + 3] = channels is 2 or 4 ? Narrow(wide[channels - 1]) : (byte)255;
            if (transparent is not null && Matches(transparent, wide, channels))
            {
                data[to + 3] = 0;
            }
        }

        return new RgbaImage(width, height, data);
    }

    /// <summary>Whether a pixel's 16-bit samples (gray, or red, green, blue) are the tRNS chunk's transparent color.</summary>
    private static bool Matches(byte[] transparent, ushort[] pixel, int channels)
    {
        var count = channels == 1 ? 1 : 3;
        if (transparent.Length < count * 2)
        {
            return false;
        }

        for (var channel = 0; channel < count; channel++)
        {
            if (System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(transparent.AsSpan(channel * 2)) != pixel[channel])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Undoes a scanline's PNG filter in place, given the scanline above it (zeros for the first).</summary>
    private static void Unfilter(int filter, byte[] line, byte[] previous, int bytesPerPixel)
    {
        for (var at = 0; at < line.Length; at++)
        {
            int left = at >= bytesPerPixel ? line[at - bytesPerPixel] : 0;
            int up = previous[at];
            int upLeft = at >= bytesPerPixel ? previous[at - bytesPerPixel] : 0;
            var predictor = filter switch
            {
                0 => 0,
                1 => left,
                2 => up,
                3 => (left + up) / 2,
                4 => Paeth(left, up, upLeft),
                _ => throw new InvalidDataException($"unknown PNG filter {filter}"),
            };
            line[at] = (byte)(line[at] + predictor);
        }
    }

    private static int Paeth(int left, int up, int upLeft)
    {
        var estimate = left + up - upLeft;
        var toLeft = Math.Abs(estimate - left);
        var toUp = Math.Abs(estimate - up);
        var toUpLeft = Math.Abs(estimate - upLeft);
        return toLeft <= toUp && toLeft <= toUpLeft ? left : toUp <= toUpLeft ? up : upLeft;
    }

    /// <summary>A 16-bit sample as an 8-bit one, rounded: upstream's <c>narrow</c> (<c>Math.round(sample * 255 / 65535)</c>).</summary>
    private static byte Narrow(ushort sample) => (byte)Math.Round(sample * 255 / 65535d, MidpointRounding.AwayFromZero);

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
