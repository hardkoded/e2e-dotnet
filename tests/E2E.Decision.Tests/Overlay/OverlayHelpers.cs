// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Tests.Overlay;

/// <summary>The helpers <c>overlay.test.ts</c> declares at its top.</summary>
internal static class OverlayHelpers
{
    /// <summary>A white PNG of the given size as the executor's screenshot.</summary>
    public static Screenshot White(int width, int height, double scale = 1) =>
        new(PngHelpers.WhitePng(width, height), "image/png", width, height, scale);

    public static int[] Pixel(byte[] data, int x, int y)
    {
        var png = PngHelpers.RgbaOf(data);
        var offset = ((y * png.Width) + x) * 4;
        return [png.Data[offset], png.Data[offset + 1], png.Data[offset + 2]];
    }

    /// <summary>A 4 by 4 screenshot whose every pixel has the same <paramref name="samples"/>, written as the color type and bit depth say; an indexed one names its <paramref name="palette"/>.</summary>
    public static Screenshot Filled(
        int depth,
        int colorType,
        int[] samples,
        IReadOnlyList<IReadOnlyList<int>>? palette = null
    )
    {
        IReadOnlyList<int> row = [.. Enumerable.Range(0, 4).SelectMany(_ => samples)];
        IReadOnlyList<IReadOnlyList<int>> rows = [.. Enumerable.Range(0, 4).Select(_ => row)];
        return new Screenshot(PngHelpers.RawPng(4, 4, depth, colorType, rows, palette), "image/png", 4, 4, 1);
    }
}
