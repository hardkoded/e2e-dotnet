// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace WebBenchmark.E2ETests.Fixtures;

/// <summary>
/// Upstream's <c>apps/web-benchmark/tests/canvas.ts</c>: pixel reads for the canvas scenarios, whose state lives in paint only. A point in
/// canvas pixels mapped to the viewport through the element's box, the colour of one pixel, and how many pixels of a band hold one colour.
/// </summary>
public static class Canvas
{
    /// <summary>A rectangle in canvas pixels (upstream's <c>Band</c>).</summary>
    public readonly record struct Band(double X, double Y, double Width, double Height);

    /// <summary>A size in canvas pixels (upstream's <c>Size</c>).</summary>
    public readonly record struct Size(double Width, double Height);

    /// <summary>Maps a point in canvas pixels to the viewport through the canvas's current box.</summary>
    public static async Task<Point> OnCanvas(Browser browser, Size size, Point point)
    {
        var box = await browser.Locator("canvas").BoundingBoxAsync() ?? throw new InvalidOperationException("the canvas has no box");
        return new Point(box.X + point.X * box.Width / size.Width, box.Y + point.Y * box.Height / size.Height);
    }

    /// <summary>Reads one canvas pixel as <c>[r, g, b]</c>; the page keeps this state in paint only.</summary>
    public static async Task<int[]> PixelAt(Browser browser, Point point)
    {
        const string script = """
            (at) => {
              const context = document.querySelector('canvas')?.getContext('2d');
              if (!context) throw new Error('no canvas context');
              return Array.from(context.getImageData(at.x, at.y, 1, 1).data.slice(0, 3));
            }
            """;
        return await browser.EvaluateAsync<int[]>(script, new { x = point.X, y = point.Y }) ?? throw new InvalidOperationException("no pixel");
    }

    /// <summary>Counts the canvas pixels painted exactly <paramref name="color"/> inside a canvas-space band.</summary>
    public static async Task<int> CountColor(Browser browser, Band band, int[] color)
    {
        const string script = """
            (input) => {
              const context = document.querySelector('canvas')?.getContext('2d');
              if (!context) throw new Error('no canvas context');
              const { x, y, width, height } = input.band;
              const [r, g, b] = input.color;
              const { data } = context.getImageData(x, y, width, height);
              let count = 0;
              for (let i = 0; i < data.length; i += 4) {
                if (data[i] === r && data[i + 1] === g && data[i + 2] === b) count += 1;
              }
              return count;
            }
            """;
        return await browser.EvaluateAsync<int>(script, new { band = new { x = band.X, y = band.Y, width = band.Width, height = band.Height }, color });
    }
}
