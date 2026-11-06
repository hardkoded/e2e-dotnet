// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.Image;

/// <summary>
/// The in-memory image operations <c>ToHaveScreenshotAsync</c> is built on: clipping, cropping, painting, the PNG round trip, and the comparison's tolerance.
/// </summary>
public sealed class ImageTests
{
    private static readonly ImageTolerance Exact = new(0, null, null);

    /// <summary>A width by height image of one gray level.</summary>
    private static RgbaImage Gray(int width, int height, byte level)
    {
        var data = new byte[width * height * 4];
        for (var at = 0; at < data.Length; at += 4)
        {
            data[at] = level;
            data[at + 1] = level;
            data[at + 2] = level;
            data[at + 3] = 255;
        }

        return new RgbaImage(width, height, data);
    }

    /// <summary>One PNG chunk: its length, type, data, and the CRC of type and data.</summary>
    private static void Chunk(Stream output, string type, byte[] data)
    {
        var length = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        var body = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        output.Write(body);
        var crc = uint.MaxValue;
        foreach (var value in body)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
        }

        var checksum = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(checksum, ~crc);
        output.Write(checksum);
    }

    /// <summary>Samples as 16-bit big-endian bytes, the order a PNG stores them in.</summary>
    private static byte[] Sixteen(params int[] samples) => samples.SelectMany(sample => new[] { (byte)(sample >> 8), (byte)(sample & 255) }).ToArray();

    private static byte[] Bytes(params int[] values) => values.Select(value => (byte)value).ToArray();

    /// <summary>
    /// A PNG written straight from the format: <paramref name="rows"/> hold each scanline's sample bytes, packed as the color type and
    /// <paramref name="depth"/> say, with no filter. It does not go through the library under test, so it can say what an encoder never would.
    /// </summary>
    private static byte[] Png(int width, int height, int depth, int colorType, byte[][] rows, byte[][]? palette = null, byte[]? transparency = null)
    {
        using var output = new MemoryStream();
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header, width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = (byte)depth;
        header[9] = (byte)colorType;
        Chunk(output, "IHDR", header);
        if (palette is not null)
        {
            Chunk(output, "PLTE", palette.SelectMany(entry => entry).ToArray());
        }

        if (transparency is not null)
        {
            Chunk(output, "tRNS", transparency);
        }

        using var compressed = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(compressed, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
        {
            foreach (var row in rows)
            {
                zlib.WriteByte(0);
                zlib.Write(row, 0, row.Length);
            }
        }

        Chunk(output, "IDAT", compressed.ToArray());
        Chunk(output, "IEND", []);
        return output.ToArray();
    }

    /// <summary>Red, green, and blue: the palette the indexed cases share.</summary>
    private static readonly byte[][] Palette = [[255, 0, 0], [0, 255, 0], [0, 0, 255]];

    public static TheoryData<string, byte[], byte[]> DecodeCases() => new()
    {
        { "gray", Png(3, 2, 8, 0, [Bytes(0, 90, 180), Bytes(255, 30, 60)]), Bytes(0, 0, 0, 255, 90, 90, 90, 255, 180, 180, 180, 255, 255, 255, 255, 255, 30, 30, 30, 255, 60, 60, 60, 255) },
        { "gray with alpha", Png(3, 2, 8, 4, [Bytes(0, 255, 90, 128, 180, 0), Bytes(255, 255, 30, 10, 60, 200)]), Bytes(0, 0, 0, 255, 90, 90, 90, 128, 180, 180, 180, 0, 255, 255, 255, 255, 30, 30, 30, 10, 60, 60, 60, 200) },
        { "RGB", Png(3, 2, 8, 2, [Bytes(255, 0, 0, 0, 255, 0, 0, 0, 255), Bytes(10, 20, 30, 40, 50, 60, 70, 80, 90)]), Bytes(255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 10, 20, 30, 255, 40, 50, 60, 255, 70, 80, 90, 255) },
        { "RGBA", Png(3, 2, 8, 6, [Bytes(255, 0, 0, 255, 0, 255, 0, 128, 0, 0, 255, 0), Bytes(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12)]), Bytes(255, 0, 0, 255, 0, 255, 0, 128, 0, 0, 255, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12) },
        { "16-bit gray", Png(3, 2, 16, 0, [Sixteen(0, 32768, 65535), Sixteen(1, 257, 60000)]), Bytes(0, 0, 0, 255, 128, 128, 128, 255, 255, 255, 255, 255, 0, 0, 0, 255, 1, 1, 1, 255, 233, 233, 233, 255) },
        { "16-bit RGBA", Png(3, 2, 16, 6, [Sixteen(65535, 0, 0, 65535, 0, 32768, 0, 65535, 0, 0, 65535, 0), Sixteen(4096, 8192, 12288, 65535, 1, 2, 3, 4, 60000, 50000, 40000, 30000)]), Bytes(255, 0, 0, 255, 0, 128, 0, 255, 0, 0, 255, 0, 16, 32, 48, 255, 0, 0, 0, 0, 233, 195, 156, 117) },
        { "a palette", Png(3, 2, 8, 3, [Bytes(0, 1, 2), Bytes(1, 0, 2)], Palette), Bytes(255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 0, 255, 0, 255, 255, 0, 0, 255, 0, 0, 255, 255) },
        { "a palette with alpha", Png(3, 2, 8, 3, [Bytes(0, 1, 2), Bytes(1, 0, 2)], Palette, Bytes(255, 128, 0)), Bytes(255, 0, 0, 255, 0, 255, 0, 128, 0, 0, 255, 0, 0, 255, 0, 128, 255, 0, 0, 255, 0, 0, 255, 0) },
        { "a 2-bit palette", Png(3, 2, 2, 3, [Bytes(0b00_01_10_00), Bytes(0b01_00_10_00)], Palette), Bytes(255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 0, 255, 0, 255, 255, 0, 0, 255, 0, 0, 255, 255) },
        { "1-bit gray", Png(3, 2, 1, 0, [Bytes(0b010_00000), Bytes(0b101_00000)]), Bytes(0, 0, 0, 255, 255, 255, 255, 255, 0, 0, 0, 255, 255, 255, 255, 255, 0, 0, 0, 255, 255, 255, 255, 255) },
        { "2-bit gray", Png(3, 2, 2, 0, [Bytes(0b00_01_10_00), Bytes(0b11_10_01_00)]), Bytes(0, 0, 0, 255, 85, 85, 85, 255, 170, 170, 170, 255, 255, 255, 255, 255, 170, 170, 170, 255, 85, 85, 85, 255) },
        { "4-bit gray", Png(3, 2, 4, 0, [Bytes(0x0f, 0x80), Bytes(0x37, 0x00)]), Bytes(0, 0, 0, 255, 255, 255, 255, 255, 136, 136, 136, 255, 51, 51, 51, 255, 119, 119, 119, 255, 0, 0, 0, 255) },
        { "gray with a transparent color", Png(3, 2, 8, 0, [Bytes(0, 90, 180), Bytes(255, 90, 60)], null, Bytes(0, 90)), Bytes(0, 0, 0, 255, 90, 90, 90, 0, 180, 180, 180, 255, 255, 255, 255, 255, 90, 90, 90, 0, 60, 60, 60, 255) },
        { "RGB with a transparent color", Png(3, 2, 8, 2, [Bytes(255, 0, 0, 0, 255, 0, 0, 0, 255), Bytes(10, 20, 30, 0, 255, 0, 70, 80, 90)], null, Bytes(0, 0, 0, 255, 0, 0)), Bytes(255, 0, 0, 255, 0, 255, 0, 0, 0, 0, 255, 255, 10, 20, 30, 255, 0, 255, 0, 0, 70, 80, 90, 255) },
        // Samples chosen where scaling 16 bits to 8 rounds both ways: 128 and 255 down, 129 and 32768 up.
        { "16-bit RGB", Png(3, 2, 16, 2, [Sixteen(0, 65535, 32768, 257, 128, 129, 1000, 40000, 65000), Sixteen(65535, 0, 0, 12345, 54321, 22222, 255, 256, 65534)]), Bytes(0, 255, 128, 255, 1, 0, 1, 255, 4, 156, 253, 255, 255, 0, 0, 255, 48, 211, 86, 255, 1, 1, 255, 255) },
        { "16-bit gray with alpha", Png(3, 2, 16, 4, [Sixteen(0, 65535, 32768, 32768, 65535, 0), Sixteen(1000, 129, 257, 128, 40000, 65000)]), Bytes(0, 0, 0, 255, 128, 128, 128, 128, 255, 255, 255, 0, 4, 4, 4, 1, 1, 1, 1, 0, 156, 156, 156, 253) },
        // Every sample here scales to 0 at 8 bits, so only the 16-bit comparison tells (1, 2, 3) from the colors beside it.
        { "16-bit RGB with a transparent color", Png(3, 2, 16, 2, [Sixteen(1, 2, 3, 4, 5, 6, 7, 8, 9), Sixteen(1, 2, 3, 1, 2, 4, 9, 9, 9)], null, Sixteen(1, 2, 3)), Bytes(0, 0, 0, 0, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 0, 0, 0, 0, 255, 0, 0, 0, 255) },
    };

    [Fact]
    public void Round_trips_through_PNG()
    {
        var image = Gray(3, 2, 120);
        var decoded = ImageOps.DecodePng(ImageOps.EncodePng(image));
        Assert.Equal(image.Width, decoded.Width);
        Assert.Equal(image.Height, decoded.Height);
        Assert.Equal(image.Data, decoded.Data);
    }

    private static bool Wide(string label) => label.StartsWith("16-bit", StringComparison.Ordinal);

    public static TheoryData<string, byte[], byte[]> ExactDecodeCases() => Cases(label => !Wide(label));

    public static TheoryData<string, byte[], byte[]> SixteenBitDecodeCases() => Cases(Wide);

    private static TheoryData<string, byte[], byte[]> Cases(Func<string, bool> pick)
    {
        var data = new TheoryData<string, byte[], byte[]>();
        foreach (var row in DecodeCases().Where(row => pick((string)row[0]!)))
        {
            data.Add((string)row[0]!, (byte[])row[1]!, (byte[])row[2]!);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ExactDecodeCases))]
    public void Decodes_to_8_bit_RGBA(string label, byte[] bytes, byte[] rgba)
    {
        var decoded = ImageOps.DecodePng(bytes);
        Assert.Equal([3, 2], [decoded.Width, decoded.Height]);
        Assert.True(rgba.AsSpan().SequenceEqual(decoded.Data), $"{label}: expected {Convert.ToHexString(rgba)}, got {Convert.ToHexString(decoded.Data)}");
    }

    [Theory]
    [MemberData(nameof(SixteenBitDecodeCases))]
    public void Partial_Decodes_16_bit_to_8_bit_RGBA(string label, byte[] bytes, byte[] rgba)
    {
        var decoded = ImageOps.DecodePng(bytes);
        Assert.Equal([3, 2], [decoded.Width, decoded.Height]);

        // Partial: Skia scales a 16-bit sample to 8 bits itself, which can land one level from upstream's rounding, so a sample is
        // checked within one level. An expected 0 or 255 is exact (a transparent color gives alpha 0 or 255 only by the 16-bit comparison).
        Assert.True(rgba.Length == decoded.Data.Length, label);
        for (var at = 0; at < rgba.Length; at++)
        {
            var tolerance = rgba[at] is 0 or 255 ? 0 : 1;
            Assert.True(Math.Abs(decoded.Data[at] - rgba[at]) <= tolerance, $"{label}: sample {at} expected {rgba[at]}, got {decoded.Data[at]}");
        }
    }

    [Fact]
    public void Keeps_every_sample_of_an_odd_sized_8_bit_RGB_image_when_it_adds_the_alpha_channel()
    {
        const int width = 37;
        const int height = 11;
        var samples = Enumerable.Range(0, width * height * 3).Select(at => (byte)((uint)(at * 2654435761L) >> 24)).ToArray();
        var rows = Enumerable.Range(0, height).Select(y => samples.AsSpan(y * width * 3, width * 3).ToArray()).ToArray();
        var expected = Enumerable.Range(0, width * height).SelectMany(pixel => new[] { samples[pixel * 3], samples[(pixel * 3) + 1], samples[(pixel * 3) + 2], (byte)255 }).ToArray();
        var decoded = ImageOps.DecodePng(Png(width, height, 8, 2, rows));
        Assert.Equal([width, height], [decoded.Width, decoded.Height]);
        Assert.Equal(expected, decoded.Data);
    }

    [Fact]
    public void Encodes_8_bit_RGBA_PNG_and_returns_decoded_pixels_a_painted_copy_does_not_share()
    {
        var encoded = ImageOps.EncodePng(Gray(4, 4, 0));
        Assert.Equal([8, 6], [encoded[24], encoded[25]]);
        var decoded = ImageOps.DecodePng(encoded);
        var painted = ImageOps.FillBoxes(decoded, [new BoundingBox(0, 0, 2, 2)], (255, 0, 0));
        Assert.Equal([255, 0, 0, 255], painted.Data.AsSpan(0, 4).ToArray());
        Assert.Equal([0, 0, 0, 255], decoded.Data.AsSpan(0, 4).ToArray());
    }

    [Fact]
    public void Throws_on_bytes_that_are_not_a_PNG_a_PNG_cut_short_or_one_with_a_corrupt_chunk()
    {
        var valid = Png(2, 2, 8, 2, [Bytes(9, 9, 9, 9, 9, 9), Bytes(9, 9, 9, 9, 9, 9)]);
        var corrupt = (byte[])valid.Clone();
        corrupt[20] = 255;
        using var bitmap = new SkiaSharp.SKBitmap(2, 2);
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var jpegData = image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 90);
        var jpeg = jpegData.ToArray();
        foreach (var bytes in new[] { Array.Empty<byte>(), new byte[] { 1, 2, 3 }, valid[..^10], valid[..40], corrupt, jpeg })
        {
            Assert.ThrowsAny<Exception>(() => ImageOps.DecodePng(bytes));
        }

        Assert.Contains("not a PNG", Assert.ThrowsAny<Exception>(() => ImageOps.DecodePng(jpeg)).Message);

        // Cut inside the end chunk, or with an end chunk that is not whole: the pixels are all there, so only the chunk walk refuses them.
        var badCrc = (byte[])valid.Clone();
        badCrc[^1] ^= 0xff;
        foreach (var cut in new[] { valid[..^4], valid[..^1], badCrc })
        {
            Assert.Contains("cut short", Assert.ThrowsAny<Exception>(() => ImageOps.DecodePng(cut)).Message);
        }
    }

    [Fact]
    public void Refuses_pixels_that_do_not_fill_the_image_where_the_addon_would_abort_the_process()
    {
        Assert.Contains("not 17", Assert.Throws<ArgumentException>(() => ImageOps.EncodePng(new RgbaImage(2, 2, new byte[17]))).Message);
        Assert.Contains("not 15", Assert.Throws<ArgumentException>(() => ImageOps.EncodePng(new RgbaImage(2, 2, new byte[15]))).Message);
    }

    [Fact]
    public void Clips_a_box_to_the_image_on_whole_pixels_and_has_none_for_a_box_outside_it()
    {
        Assert.Equal(new ImageBox(0, 4, 3, 6), ImageOps.ClipBox(10, 10, new BoundingBox(-2.4, 3.6, 5, 20)));
        Assert.Null(ImageOps.ClipBox(10, 10, new BoundingBox(12, 0, 5, 5)));
    }

    [Fact]
    public void Crops_the_pixels_of_a_box_and_paints_boxes_clipped_to_the_image()
    {
        var blank = Gray(4, 4, 0);
        var image = ImageOps.FillBoxes(blank, [new BoundingBox(2, 2, 10, 10)], (255, 0, 0));
        Assert.Equal(Gray(4, 4, 0).Data, blank.Data);
        Assert.Equal(
            new byte[] { 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 0, 0, 255 },
            ImageOps.CropImage(image, new ImageBox(1, 1, 2, 2)).Data);
    }

    [Fact]
    public void Averages_the_pixels_each_target_pixel_covers_when_it_resamples()
    {
        var image = new RgbaImage(2, 1, [0, 0, 0, 255, 200, 200, 200, 255]);
        Assert.Equal(new byte[] { 100, 100, 100, 255 }, ImageOps.ResampleImage(image, 1, 1).Data);
        Assert.Same(image, ImageOps.ResampleImage(image, 2, 1));
    }

    [Fact]
    public void Reports_a_size_mismatch_and_counts_differing_pixels_against_maxDiffPixels_and_maxDiffPixelRatio_the_smaller_one_when_both_are_set()
    {
        Assert.IsType<ImageComparison.SizeMismatch>(ImageOps.CompareImages(Gray(2, 2, 0), Gray(2, 3, 0), Exact));
        var expected = Gray(10, 10, 255);
        var actual = ImageOps.FillBoxes(Gray(10, 10, 255), [new BoundingBox(0, 0, 5, 1)], (0, 0, 0));
        var pixels = Assert.IsType<ImageComparison.Pixels>(ImageOps.CompareImages(expected, actual, Exact));
        Assert.Equal(5, pixels.DiffPixels);
        Assert.Equal(0.05, pixels.Ratio);
        Assert.IsType<ImageComparison.Match>(ImageOps.CompareImages(expected, actual, Exact with { MaxDiffPixels = 5 }));
        Assert.IsType<ImageComparison.Match>(ImageOps.CompareImages(expected, actual, Exact with { MaxDiffPixelRatio = 0.05 }));
        Assert.IsType<ImageComparison.Pixels>(ImageOps.CompareImages(expected, actual, Exact with { MaxDiffPixels = 10, MaxDiffPixelRatio = 0.04 }));
    }

    [Fact]
    public void Counts_a_pixel_as_the_same_when_its_color_is_within_the_threshold()
    {
        Assert.IsType<ImageComparison.Match>(ImageOps.CompareImages(Gray(4, 4, 200), Gray(4, 4, 204), Exact with { Threshold = 0.2 }));
        var pixels = Assert.IsType<ImageComparison.Pixels>(ImageOps.CompareImages(Gray(4, 4, 200), Gray(4, 4, 204), Exact));
        Assert.Equal(16, pixels.DiffPixels);
    }
}
