// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace E2E.WinApp.Tests.Helpers;

/// <summary>Reads the PNGs the engine writes: 8-bit RGBA, filter 0 rows, one or more IDAT chunks.</summary>
public static class PngReader
{
    public static (int Width, int Height, byte[] Rgba) Read(byte[] png)
    {
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }, png[..8]);
        var at = 8;
        int width = 0, height = 0;
        using var data = new MemoryStream();
        while (at < png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(at));
            var type = Encoding.ASCII.GetString(png, at + 4, 4);
            var body = png.AsSpan(at + 8, length);
            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(body);
                    height = BinaryPrimitives.ReadInt32BigEndian(body[4..]);
                    Assert.Equal(8, body[8]);
                    Assert.Equal(6, body[9]);
                    break;
                case "IDAT":
                    data.Write(body);
                    break;
            }

            at += 12 + length;
            if (type == "IEND")
            {
                break;
            }
        }

        data.Position = 0;
        using var inflated = new MemoryStream();
        using (var zlib = new ZLibStream(data, CompressionMode.Decompress))
        {
            zlib.CopyTo(inflated);
        }

        var raw = inflated.ToArray();
        var stride = width * 4;
        var rgba = new byte[stride * height];
        for (var row = 0; row < height; row++)
        {
            Assert.Equal(0, raw[row * (stride + 1)]);
            raw.AsSpan((row * (stride + 1)) + 1, stride).CopyTo(rgba.AsSpan(row * stride));
        }

        return (width, height, rgba);
    }

    public static (byte R, byte G, byte B, byte A) Pixel((int Width, int Height, byte[] Rgba) image, int x, int y)
    {
        var at = ((y * image.Width) + x) * 4;
        return (image.Rgba[at], image.Rgba[at + 1], image.Rgba[at + 2], image.Rgba[at + 3]);
    }
}
