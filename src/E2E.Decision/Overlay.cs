// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Decision;

/// <summary>Pixels as the decision model receives them: the runner's capture without its masking count (upstream's <c>Screenshot</c>, <c>Omit&lt;ExecutorPixels, 'maskedRegionCount'&gt;</c>).</summary>
/// <param name="Data">The encoded image.</param>
/// <param name="MediaType">The media type of <paramref name="Data"/>.</param>
/// <param name="Width">Width in image pixels.</param>
/// <param name="Height">Height in image pixels.</param>
/// <param name="Scale">Image pixels per viewport (CSS) pixel.</param>
public sealed record Screenshot(byte[] Data, string MediaType, int Width, int Height, double Scale);

/// <summary>
/// A grid over an image: at most ten columns and rows, one score level per column and row (upstream's <c>Grid</c>). Sizes are in the CSS
/// pixels of the image's coordinates.
/// </summary>
public sealed record Grid(int Columns, int Rows, double CellWidth, double CellHeight);

/// <summary>A zoomed crop: the image, and where its top-left corner sits in the screenshot's CSS pixels (upstream's <c>Zoom</c>).</summary>
public sealed record Zoom(Screenshot Screenshot, Point Origin, double Factor);

/// <summary>Upstream's <c>overlay.ts</c>. A viewport point is an <see cref="Point"/>, in CSS pixels.</summary>
public static class Overlay
{
    /// <summary>
    /// Target cell size of a first look in CSS pixels (upstream's <c>CELL_TARGET</c>). A 1280 by 720 viewport gets 10 columns of 128px and
    /// 9 rows of 80px.
    /// </summary>
    public const double CellTarget = 80;

    /// <summary>The Decisions API scores at most ten levels per question (upstream's <c>MAX_LEVELS</c>).</summary>
    private const int MaxLevels = 10;

    private static readonly (byte R, byte G, byte B) GridColor = (255, 0, 255);

    /// <summary>Upstream's <c>gridFor</c>: the grid for an image, as close to <paramref name="cellTarget"/> CSS pixels per cell as the level cap allows.</summary>
    public static Grid GridFor(double width, double height, double cellTarget)
    {
        var columns = (int)Math.Min(MaxLevels, Math.Max(1, RoundHalfUp(width / cellTarget)));
        var rows = (int)Math.Min(MaxLevels, Math.Max(1, RoundHalfUp(height / cellTarget)));
        return new Grid(columns, rows, width / columns, height / rows);
    }

    /// <summary>Upstream's <c>pointOf</c>: the point a column and row score name on a grid: the center of the scored position, inside the image.</summary>
    public static Point PointOf(Grid grid, double column, double row)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return new Point(
            Clamp((column + 0.5) * grid.CellWidth, grid.Columns * grid.CellWidth),
            Clamp((row + 0.5) * grid.CellHeight, grid.Rows * grid.CellHeight)
        );

        static double Clamp(double value, double max) => Math.Min(max - 1, Math.Max(0, RoundHalfUp(value)));
    }

    /// <summary>
    /// Upstream's <c>zoomAround</c>: the <paramref name="box"/> by <paramref name="box"/> CSS-pixel region around <paramref name="center"/>, kept inside the
    /// screenshot and enlarged <paramref name="factor"/> times (nearest neighbor), for a finer closer look at a drawn control. Null when
    /// the pixels cannot be decoded or the region comes out under one pixel, which no PNG can hold.
    /// </summary>
    public static Zoom? ZoomAround(Screenshot screenshot, Point center, double box, double factor)
    {
        ArgumentNullException.ThrowIfNull(screenshot);
        if (Decode(screenshot) is not { } png)
        {
            return null;
        }

        var scale = screenshot.Scale;
        var cssWidth = png.Width / scale;
        var cssHeight = png.Height / scale;
        var side = Math.Min(box, Math.Min(cssWidth, cssHeight));
        var origin = new Point(
            RoundHalfUp(Math.Max(0, Math.Min(cssWidth - side, center.X - (side / 2)))),
            RoundHalfUp(Math.Max(0, Math.Min(cssHeight - side, center.Y - (side / 2))))
        );
        var sizeValue = RoundHalfUp(side * factor);
        if (double.IsNaN(sizeValue) || double.IsInfinity(sizeValue) || sizeValue < 1 || sizeValue > 16384)
        {
            return null;
        }

        var size = (int)sizeValue;
        var data = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var sx = Math.Min(png.Width - 1, (int)Math.Floor((origin.X + (x / factor)) * scale));
                var sy = Math.Min(png.Height - 1, (int)Math.Floor((origin.Y + (y / factor)) * scale));
                var from = ((sy * png.Width) + sx) * 4;
                var to = ((y * size) + x) * 4;
                data[to] = png.Data[from];
                data[to + 1] = png.Data[from + 1];
                data[to + 2] = png.Data[from + 2];
                data[to + 3] = 255;
            }
        }

        var zoomed = new Screenshot(Png.EncodeRgba(new RgbaImage(size, size, data)), "image/png", size, size, 1);
        return new Zoom(zoomed, origin, factor);
    }

    /// <summary>
    /// Upstream's <c>regionHash</c>: a hash of the <paramref name="box"/> by <paramref name="box"/> CSS-pixel region around <paramref name="center"/>: what a tap
    /// there changed, read without the rest of the page. Null without pixels or when they cannot be decoded.
    /// </summary>
    public static string? RegionHash(Screenshot? screenshot, Point center, double box)
    {
        if (screenshot is null)
        {
            return null;
        }

        return ZoomAround(screenshot, center, box, 1) is { } zoom ? Hash.Fnv1a([zoom.Screenshot.Data]) : null;
    }

    /// <summary>Upstream's <c>withGrid</c>: the screenshot with the grid's cell borders drawn on it. Returns the input untouched when it cannot be decoded.</summary>
    public static Screenshot WithGrid(Screenshot screenshot, Grid grid)
    {
        ArgumentNullException.ThrowIfNull(screenshot);
        ArgumentNullException.ThrowIfNull(grid);
        if (Decode(screenshot) is not { } png)
        {
            return screenshot;
        }

        var scale = screenshot.Scale;
        for (var row = 0; row < grid.Rows; row++)
        {
            for (var column = 0; column < grid.Columns; column++)
            {
                var x0 = (int)RoundHalfUp(column * grid.CellWidth * scale);
                var y0 = (int)RoundHalfUp(row * grid.CellHeight * scale);
                var x1 = (int)Math.Min(png.Width, RoundHalfUp((column + 1) * grid.CellWidth * scale));
                var y1 = (int)Math.Min(png.Height, RoundHalfUp((row + 1) * grid.CellHeight * scale));
                for (var x = x0; x < x1; x++)
                {
                    Paint(png, x, y0);
                    Paint(png, x, y1 - 1);
                }

                for (var y = y0; y < y1; y++)
                {
                    Paint(png, x0, y);
                    Paint(png, x1 - 1, y);
                }
            }
        }

        return screenshot with
        {
            Data = Png.EncodeRgba(png),
        };
    }

    /// <summary>JavaScript's <c>Math.round</c>: halves round up.</summary>
    internal static double RoundHalfUp(double value) => Math.Floor(value + 0.5);

    private static RgbaImage? Decode(Screenshot screenshot)
    {
        try
        {
            return Png.DecodeRgba(screenshot.Data);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return null;
        }
    }

    private static void Paint(RgbaImage png, int x, int y)
    {
        if (x < 0 || y < 0 || x >= png.Width || y >= png.Height)
        {
            return;
        }

        var offset = ((y * png.Width) + x) * 4;
        png.Data[offset] = GridColor.R;
        png.Data[offset + 1] = GridColor.G;
        png.Data[offset + 2] = GridColor.B;
        png.Data[offset + 3] = 255;
    }
}
