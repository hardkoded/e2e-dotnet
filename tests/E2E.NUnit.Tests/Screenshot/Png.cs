// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.NUnit.Tests.Screenshot;

/// <summary>
/// The pixel helpers of upstream's <c>apps/testbed/tests/screenshot.e2e.ts</c>: <c>readPng</c>, <c>rgb</c>, <c>near</c> and <c>crop</c>.
/// <c>Rgb</c> returns a tuple where upstream returns an array.
/// </summary>
internal static class Png
{
    /// <summary>The PNG at <paramref name="file"/>, decoded.</summary>
    public static RgbaImage Read(string file) => ImageOps.DecodePng(File.ReadAllBytes(file));

    /// <summary>The red, green, and blue of the pixel at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public static (int R, int G, int B) Rgb(RgbaImage png, int x, int y)
    {
        var offset = ((y * png.Width) + x) * 4;
        return (png.Data[offset], png.Data[offset + 1], png.Data[offset + 2]);
    }

    /// <summary>Whether two colors are the same within <paramref name="slack"/> per channel.</summary>
    public static bool Near((int R, int G, int B) actual, (int R, int G, int B) expected, int slack = 3) =>
        Math.Abs(actual.R - expected.R) <= slack && Math.Abs(actual.G - expected.G) <= slack && Math.Abs(actual.B - expected.B) <= slack;

    /// <summary>The pixels of <paramref name="box"/> cut out of <paramref name="png"/>, rounded out to whole pixels.</summary>
    public static byte[] Crop(RgbaImage png, BoundingBox box)
    {
        var left = (int)Math.Floor(box.X);
        var top = (int)Math.Floor(box.Y);
        var right = (int)Math.Ceiling(box.X + box.Width);
        var bottom = (int)Math.Ceiling(box.Y + box.Height);
        var rows = new List<byte>();
        for (var y = top; y < bottom; y++)
        {
            rows.AddRange(png.Data.AsSpan((((y * png.Width) + left) * 4)..(((y * png.Width) + right) * 4)).ToArray());
        }

        return [.. rows];
    }
}
