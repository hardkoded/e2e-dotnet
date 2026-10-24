// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.WinApp;

/// <summary>Screenshot policy: the captured BGRA pixels become an RGBA PNG with every secure field painted over first.</summary>
public static class WindowScreenshots
{
    /// <summary>
    /// Paints each rectangle (in the image's own pixels) opaque black, rounding outward and clipping to the image, then encodes.
    /// Answers the PNG and how many rectangles it painted.
    /// </summary>
    public static (byte[] Png, int Masked) Render(RawScreenshot shot, IReadOnlyList<Rect> masks)
    {
        ArgumentNullException.ThrowIfNull(shot);
        ArgumentNullException.ThrowIfNull(masks);
        if (shot.Bgra.Length != shot.Width * shot.Height * 4)
        {
            throw new InvalidDataException($"the capture has {shot.Bgra.Length} bytes for {shot.Width}x{shot.Height}; expected four per pixel");
        }

        var rgba = ToRgba(shot);
        foreach (var rect in masks)
        {
            // Clamp in double before the cast, so a huge or non-finite rect cannot wrap around to an empty box.
            var x0 = (int)Math.Clamp(Math.Floor(rect.X), 0, shot.Width);
            var y0 = (int)Math.Clamp(Math.Floor(rect.Y), 0, shot.Height);
            var x1 = (int)Math.Clamp(Math.Ceiling(rect.X + rect.Width), 0, shot.Width);
            var y1 = (int)Math.Clamp(Math.Ceiling(rect.Y + rect.Height), 0, shot.Height);
            for (var y = y0; y < y1; y++)
            {
                for (var x = x0; x < x1; x++)
                {
                    var at = ((y * shot.Width) + x) * 4;
                    rgba[at] = 0;
                    rgba[at + 1] = 0;
                    rgba[at + 2] = 0;
                    rgba[at + 3] = 255;
                }
            }
        }

        return (PngEncoder.Encode(rgba, shot.Width, shot.Height), masks.Count);
    }

    /// <summary>The capture's pixels in RGBA order, opaque: a window capture carries no alpha worth keeping.</summary>
    public static byte[] ToRgba(RawScreenshot shot)
    {
        ArgumentNullException.ThrowIfNull(shot);
        var source = shot.Bgra;
        var rgba = new byte[source.Length];
        for (var at = 0; at + 3 < source.Length; at += 4)
        {
            rgba[at] = source[at + 2];
            rgba[at + 1] = source[at + 1];
            rgba[at + 2] = source[at];
            rgba[at + 3] = 255;
        }

        return rgba;
    }
}
