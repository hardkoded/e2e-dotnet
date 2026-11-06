// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Mobile.Tests.PngCodec;

namespace E2E.Mobile.Tests.Png;

public sealed class ScreenshotMaskingTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Masks_and_clips_RGBA_screenshots_encoded_with_filter(int filter)
    {
        var masked = Decode(ScreenshotMasking.MaskPng(Encode(Solid(10, 10, 4, 255), filter), [new Rect(2, 2, 3, 2), new Rect(8, 8, 50, 50)]));
        byte[] At(int x, int y) => masked.Pixels[(((y * 10) + x) * 4)..(((y * 10) + x) * 4 + 4)];

        Assert.Equal([0, 0, 0, 255], At(2, 2));
        Assert.Equal([0, 0, 0, 255], At(4, 3));
        Assert.Equal([255, 255, 255, 200], At(5, 3));
        Assert.Equal([255, 255, 255, 200], At(1, 2));
        Assert.Equal([0, 0, 0, 255], At(9, 9));
        Assert.Equal([255, 255, 255, 200], At(7, 7));
    }

    /// <summary>A 4 by 4 screenshot whose every pixel has the same <paramref name="samples"/>, as the color type and bit depth pack them.</summary>
    private static byte[] Filled(int depth, int colorType, params byte[] samples) =>
        RawPng(4, 4, depth, colorType, Enumerable.Range(0, 4).Select(_ => Enumerable.Repeat(samples, 4).SelectMany(pixel => pixel).ToArray()).ToArray());

    public static TheoryData<string, byte[], byte[]> ColorTypes() => new()
    {
        { "gray", Filled(8, 0, 200), [200, 200, 200, 255] },
        { "gray with alpha", Filled(8, 4, 200, 128), [200, 200, 200, 128] },
        { "RGB", Filled(8, 2, 10, 20, 30), [10, 20, 30, 255] },
        { "16-bit RGB", Filled(16, 2, 255, 255, 128, 0, 1, 1), [255, 128, 1, 255] },
        { "16-bit RGBA", Filled(16, 6, 0, 0, 255, 255, 0, 0, 128, 0), [0, 255, 0, 128] },
    };

    [Theory]
    [MemberData(nameof(ColorTypes))]
    public void Masks_a_screenshot_and_writes_the_rest_of_it_as_RGBA(string label, byte[] bytes, byte[] color)
    {
        var masked = Decode(ScreenshotMasking.MaskPng(bytes, [new Rect(0, 0, 2, 2)]));
        byte[] At(int x, int y) => masked.Pixels[(((y * 4) + x) * 4)..(((y * 4) + x) * 4 + 4)];

        byte[] black = [0, 0, 0, 255];
        Assert.True(black.AsSpan().SequenceEqual(At(0, 0)) && black.AsSpan().SequenceEqual(At(1, 1)), $"{label}: the rectangle is not black");
        Assert.True(color.AsSpan().SequenceEqual(At(2, 0)) && color.AsSpan().SequenceEqual(At(3, 3)), $"{label}: the rest is not {Convert.ToHexString(color)}");
    }

    [Fact]
    public void Returns_the_bytes_untouched_when_there_is_nothing_to_mask_and_refuses_other_formats()
    {
        var bytes = Encode(Solid(2, 2, 3, 9));
        Assert.Same(bytes, ScreenshotMasking.MaskPng(bytes, []));
        Rect[] rects = [new Rect(0, 0, 1, 1)];
        Assert.ThrowsAny<Exception>(() => ScreenshotMasking.MaskPng([1, 2, 3], rects));
        Assert.ThrowsAny<Exception>(() => ScreenshotMasking.MaskPng(bytes[..^10], rects));
        var corrupt = (byte[])bytes.Clone();
        corrupt[20] = 255;
        Assert.ThrowsAny<Exception>(() => ScreenshotMasking.MaskPng(corrupt, rects));
        using var bitmap = new SkiaSharp.SKBitmap(2, 2);
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var jpeg = image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 90);
        Assert.Contains("not a PNG", Assert.ThrowsAny<Exception>(() => ScreenshotMasking.MaskPng(jpeg.ToArray(), rects)).Message);
        var masked = Decode(ScreenshotMasking.MaskPng(bytes, rects));
        Assert.Equal([0, 0, 0, 255], masked.Pixels[..4]);
        Assert.Equal([9, 9, 9, 255], masked.Pixels[4..8]);
    }
}
