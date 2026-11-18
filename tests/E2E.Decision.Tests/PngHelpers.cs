// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.IO.Compression;
using SkiaSharp;

namespace E2E.Decision.Tests;

/// <summary>Upstream's <c>tests/png-helpers.ts</c>.</summary>
internal static class PngHelpers
{
    /// <summary>Upstream's <c>pngOf</c>: PNG bytes of 8-bit RGBA pixels.</summary>
    public static byte[] PngOf(int width, int height, byte[] rgba)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var image = SKImage.FromPixelCopy(info, rgba);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>Upstream's <c>whitePng</c>: a white, opaque RGBA PNG.</summary>
    public static byte[] WhitePng(int width, int height) =>
        PngOf(width, height, Enumerable.Repeat((byte)255, width * height * 4).ToArray());

    /// <summary>Upstream's <c>rgbaOf</c>: the size and RGBA pixels of a PNG the package wrote, which is always 8-bit RGBA.</summary>
    public static (int Width, int Height, byte[] Data) RgbaOf(byte[] bytes)
    {
        var image = Internal.ImageOps.DecodePng(bytes);
        return (image.Width, image.Height, image.Data);
    }

    /// <summary>
    /// Upstream's <c>rawPng</c>: a PNG written straight from the format, so a test can hand the package a color type no encoder of its own
    /// would write: <paramref name="rows"/> hold each scanline's sample bytes as the color type and <paramref name="depth"/> pack them, with
    /// no filter; an indexed one names its <paramref name="palette"/>.
    /// </summary>
    public static byte[] RawPng(
        int width,
        int height,
        int depth,
        int colorType,
        IReadOnlyList<IReadOnlyList<int>> rows,
        IReadOnlyList<IReadOnlyList<int>>? palette = null
    )
    {
        var header = new byte[13];
        WriteUInt32(header, 0, (uint)width);
        WriteUInt32(header, 4, (uint)height);
        header[8] = (byte)depth;
        header[9] = (byte)colorType;
        using var output = new MemoryStream();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        Chunk(output, "IHDR", header);
        if (palette is not null)
        {
            Chunk(output, "PLTE", [.. palette.SelectMany(entry => entry).Select(value => (byte)value)]);
        }

        var scanlines = new MemoryStream();
        foreach (var row in rows)
        {
            scanlines.WriteByte(0);
            scanlines.Write([.. row.Select(value => (byte)value)]);
        }

        using var deflated = new MemoryStream();
        using (var zlib = new ZLibStream(deflated, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(scanlines.ToArray());
        }

        Chunk(output, "IDAT", deflated.ToArray());
        Chunk(output, "IEND", []);
        return output.ToArray();
    }

    private static void Chunk(Stream output, string type, byte[] data)
    {
        var body = new byte[4 + data.Length];
        System.Text.Encoding.Latin1.GetBytes(type, 0, 4, body, 0);
        data.CopyTo(body, 4);
        var length = new byte[4];
        WriteUInt32(length, 0, (uint)data.Length);
        output.Write(length);
        output.Write(body);
        var crc = new byte[4];
        WriteUInt32(crc, 0, Crc32(body));
        output.Write(crc);
    }

    private static void WriteUInt32(byte[] target, int at, uint value)
    {
        target[at] = (byte)(value >> 24);
        target[at + 1] = (byte)(value >> 16);
        target[at + 2] = (byte)(value >> 8);
        target[at + 3] = (byte)value;
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }
}
