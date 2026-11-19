// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Decision;

/// <summary>Asks the decision model one request, or nothing once the step budget is spent (upstream's <c>AskDecision</c>).</summary>
public delegate Task<IReadOnlyDictionary<string, Decision>?> AskDecision(DecisionRequest request);

/// <summary>What a point look is after: a control to tap, or a field to type into (upstream's <c>Aim</c>).</summary>
public enum Aim
{
    Control,
    Field,
}

/// <summary>Where a drawn control is, or why the looks could not say (upstream's <c>Located</c>): a point, or an uncertainty.</summary>
public sealed record Located
{
    private Located() { }

    public Point? Point { get; private init; }

    /// <summary>Why the looks could not say where the control is.</summary>
    public string? Uncertain { get; private init; }

    public static Located At(Point point) => new() { Point = point };

    public static Located Unsure(string why) => new() { Uncertain = why };
}

/// <summary>
/// Locates drawn controls on the screenshot and remembers the ones a tap found: three looks per control, the column and row on the plain
/// full screenshot, then again on two ever tighter zoomed crops with a grid drawn on them; a field takes the first look alone. A tap that
/// changed the pixels around it found its control; the same control on the same page is tapped there again with no new looks (upstream's
/// <c>PointLocator</c>).
/// </summary>
public sealed class PointLocator(StepExecutorContext ctx, AskDecision ask, Gates gates)
{
    /// <summary>
    /// CSS pixels of the regions the zoomed looks enlarge, in turn: the first wide enough to hold a first look one column off, the second for
    /// a target as small as a 32px map pin. Measured on Jev over 54 drawn targets at the runner's 768px capture, the second zoom lifts hits
    /// from 47-49 to 53-54.
    /// </summary>
    private static readonly double[] ZoomBoxes = [400, 200];

    /// <summary>
    /// The zoomed looks per aim. A field is wide enough for the full screenshot alone; a crop around it also holds its neighbors, and
    /// measured on Jev over a drawn form the zooms split their mass between two stacked fields and landed between them (about 4 in 5 hit),
    /// where the first look hit 72 of 72.
    /// </summary>
    private static readonly Dictionary<Aim, double[]> Looks = new() { [Aim.Control] = ZoomBoxes, [Aim.Field] = [] };

    /// <summary>How much each zoomed region is enlarged.</summary>
    private const int ZoomFactor = 2;

    /// <summary>Columns and rows of the grid drawn on a zoomed region.</summary>
    private const int ZoomCells = 8;

    /// <summary>Points that landed, by path and the control the text model named.</summary>
    private readonly Dictionary<string, Point> _landed = new(StringComparer.Ordinal);

    /// <summary>The last point tap, settled against the next observation.</summary>
    private (string Key, Point Point, string? Before)? _pending;

    /// <summary>A point already known for this control on this page.</summary>
    public Point? Known(string path, string wanted, Aim aim) =>
        _landed.TryGetValue(KeyOf(path, wanted, aim), out var point) ? point : null;

    /// <summary>Records a point tap, to be settled by the next observation.</summary>
    public void Tapped(string path, string wanted, Aim aim, Point point, Screenshot screenshot) =>
        _pending = (KeyOf(path, wanted, aim), point, Overlay.RegionHash(screenshot, point, ZoomBoxes[0]));

    /// <summary>Settles the last point tap against the newest pixels: changed means it landed.</summary>
    public void Settle(Screenshot? screenshot)
    {
        if (_pending is not { } pending)
        {
            return;
        }

        var after = Overlay.RegionHash(screenshot, pending.Point, ZoomBoxes[0]);
        if (after is not null && after != pending.Before)
        {
            _landed[pending.Key] = pending.Point;
        }
        else
        {
            _landed.Remove(pending.Key);
        }

        _pending = null;
    }

    /// <summary>The looks for the control or field, or null once the budget is spent.</summary>
    public async Task<Located?> LocateAsync(Screenshot screenshot, string path, string wanted, Aim aim)
    {
        ArgumentNullException.ThrowIfNull(screenshot);
        var grid = Overlay.GridFor(
            screenshot.Width / screenshot.Scale,
            screenshot.Height / screenshot.Scale,
            Overlay.CellTarget
        );
        var coarse = await ask(
                Questions.PointRequest(
                    ctx,
                    path,
                    screenshot,
                    grid,
                    "This image is the full screenshot; no grid is drawn on it.",
                    wanted
                )
            )
            .ConfigureAwait(false);
        if (coarse is null)
        {
            return null;
        }

        var first = Scored(coarse);
        if (first.Uncertain is not null)
        {
            return first;
        }

        var point = Overlay.PointOf(grid, first.Point!.Value.X, first.Point.Value.Y);
        foreach (var box in Looks[aim])
        {
            var closer = await CloserAsync(screenshot, path, wanted, point, box).ConfigureAwait(false);
            if (closer is null || closer.Uncertain is not null)
            {
                return closer;
            }

            point = closer.Point!.Value;
        }

        return Located.At(point);
    }

    /// <summary>One zoomed look: the <paramref name="box"/> CSS-pixel region around <paramref name="point"/>, enlarged with a grid drawn on it.</summary>
    private async Task<Located?> CloserAsync(Screenshot screenshot, string path, string wanted, Point point, double box)
    {
        var zoom = Overlay.ZoomAround(screenshot, point, box, ZoomFactor);
        if (zoom is null)
        {
            return Located.At(point);
        }

        double side = zoom.Screenshot.Width;
        var fine = new Grid(ZoomCells, ZoomCells, side / ZoomCells, side / ZoomCells);
        var region = (long)Overlay.RoundHalfUp(side / ZoomFactor);
        var framing =
            "This image is a "
            + ZoomFactor.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "x zoom of a "
            + region.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " by "
            + region.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " pixel region of the screen.";
        var answers = await ask(
                Questions.PointRequest(ctx, path, Overlay.WithGrid(zoom.Screenshot, fine), fine, framing, wanted)
            )
            .ConfigureAwait(false);
        if (answers is null)
        {
            return null;
        }

        var scored = Scored(answers);
        if (scored.Uncertain is not null)
        {
            return scored;
        }

        var inZoom = Overlay.PointOf(fine, scored.Point!.Value.X, scored.Point.Value.Y);
        return Located.At(
            new Point(
                Overlay.RoundHalfUp(zoom.Origin.X + (inZoom.X / zoom.Factor)),
                Overlay.RoundHalfUp(zoom.Origin.Y + (inZoom.Y / zoom.Factor))
            )
        );
    }

    /// <summary>The column and row scores of a point request, gated like any answer. The point it holds is the scores themselves, not yet a position.</summary>
    private Located Scored(IReadOnlyDictionary<string, Decision> answers)
    {
        var x = Picks.Need(answers.GetValueOrDefault("x"), "x");
        var y = Picks.Need(answers.GetValueOrDefault("y"), "y");
        if (x.Score is not { } columnScore || y.Score is not { } rowScore)
        {
            throw Picks.Invalid("The decision model answered a point question without a score.");
        }

        if (!Picks.Gated(x, gates) || !Picks.Gated(y, gates))
        {
            return Located.Unsure("x " + Picks.Describe(x) + "; y " + Picks.Describe(y));
        }

        return Located.At(new Point(columnScore, rowScore));
    }

    /// <summary>A landed point's key: a field and a control the text model named alike are located apart.</summary>
    private static string KeyOf(string path, string wanted, Aim aim) =>
        path + "\0" + (aim == Aim.Control ? "control" : "field") + "\0" + wanted;
}
