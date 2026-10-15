// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using E2E.Engine;
using E2E.Internal;

namespace E2E;

/// <summary>
/// The screenshot a model receives, sized for it. Engines capture at CSS or device scale; a 1280 by 720 web viewport
/// or a 1170 by 2532 phone is more image than a model needs to place a tap, and every image pixel is paid for on every
/// turn it stays in the conversation. The long side is capped and the image resampled, and the scale (image pixels per
/// CSS pixel) is reduced with it, so every coordinate a model reads off the image still maps onto the viewport through
/// the same arithmetic. Masked regions stay masked: a box filter over black stays black.
/// </summary>
internal static class Pixels
{
    /// <summary>
    /// Longest side a model-bound screenshot keeps, in image pixels. Measured upstream on gpt-5.6 over a ten-key drawn
    /// keypad and an eight-screen drawn wizard, four runs per size: a 1280 by 720 capture costs about 1390 input tokens
    /// per turn, 1024 about 960, 768 about 680, 640 about 580, 512 about 500. Every size down to 640 passed every run;
    /// at 512 the model misread the keypad's display, needed retries, and once accepted a wrong code. 768 keeps a full
    /// step of margin above that cliff for 35% less per run than full size.
    /// </summary>
    public const int MaxScreenshotLongSide = 768;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Sizes a captured screenshot for the model; the input is returned as is when it already fits.</summary>
    public static EngineScreenshot SizeForModel(EngineScreenshot pixels) => DownscalePixels(pixels, MaxScreenshotLongSide);

    /// <summary>
    /// Resamples the image so its longer side is at most <paramref name="maxLongSide"/>, with a box filter over the
    /// source pixels each target pixel covers. Returns the input untouched when it already fits or cannot be decoded;
    /// whatever else the caller's pixels carry (a masked-region count) rides along unchanged.
    /// </summary>
    public static EngineScreenshot DownscalePixels(EngineScreenshot pixels, int maxLongSide)
    {
        if (ReadPngSize(pixels.Png) is not { } size)
        {
            return pixels;
        }

        var longSide = Math.Max(size.Width, size.Height);
        if (longSide <= maxLongSide || maxLongSide <= 0)
        {
            return pixels;
        }

        var factor = (double)maxLongSide / longSide;
        var width = Math.Max(1, (int)Math.Round(size.Width * factor, MidpointRounding.AwayFromZero));
        var height = Math.Max(1, (int)Math.Round(size.Height * factor, MidpointRounding.AwayFromZero));
        try
        {
            var resampled = ImageOps.ResampleImage(ImageOps.DecodePng(pixels.Png), width, height);
            return pixels with { Png = ImageOps.EncodePng(resampled), Scale = pixels.Scale * ((double)width / size.Width) };
        }
        catch (Exception cause) when (cause is InvalidDataException or InvalidOperationException)
        {
            return pixels;
        }
    }

    /// <summary>Reads the dimensions out of a PNG's IHDR chunk, which comes first, so no decoder is needed; null for anything else.</summary>
    public static (int Width, int Height)? ReadPngSize(ReadOnlySpan<byte> data)
    {
        if (data.Length < 24 || !data[..PngSignature.Length].SequenceEqual(PngSignature))
        {
            return null;
        }

        return (BinaryPrimitives.ReadInt32BigEndian(data[16..]), BinaryPrimitives.ReadInt32BigEndian(data[20..]));
    }
}
