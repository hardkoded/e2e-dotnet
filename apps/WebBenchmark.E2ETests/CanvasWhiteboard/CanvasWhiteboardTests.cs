// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E;
using E2E.Engine;
using NUnit.Framework;
using WebBenchmark.E2ETests.Fixtures;
using static WebBenchmark.E2ETests.Fixtures.Canvas;

namespace WebBenchmark.E2ETests.CanvasWhiteboard;

/// <summary>
/// Ported from <c>describe('canvas whiteboard')</c> in upstream's <c>apps/web-benchmark/tests/canvas-whiteboard.e2e.ts</c>.
/// The whiteboard is one aria-hidden canvas: shapes are dragged by pointer path at canvas coordinates and every outcome is read back from
/// the pixels. The page squeezes the 640px canvas into a 592px column, so x and y scale apart.
/// </summary>
public sealed class CanvasWhiteboardTests : BenchmarkTest
{
    private static readonly Size CanvasSize = new(640, 440);
    private static readonly Point RectHome = new(105, 165);
    private static readonly Point RectSlot = new(475, 165);
    private static readonly Point CircleHome = new(105, 320);
    private static readonly Point CircleSlot = new(475, 320);
    private static readonly Point SaveButton = new(570, 28);
    private static readonly Point SaveButtonEdge = new(525, 28);
    private static readonly Band BannerBand = new(0, 400, 640, 36);
    private static readonly int[] Blue = [37, 99, 235];
    private static readonly int[] Orange = [234, 88, 12];
    private static readonly int[] Green = [0, 170, 0];
    private static readonly int[] Red = [204, 0, 0];
    private static readonly int[] White = [255, 255, 255];
    private static readonly int[] Inactive = [221, 221, 221];

    [SetUp]
    public async Task OpenTheCanvasWhiteboard() => await App.OpenAsync("/e/canvas-whiteboard");

    [Test]
    public async Task Drags_both_shapes_into_their_slots_and_saves()
    {
        await Expect.Poll(() => PixelAt(Browser, RectHome)).ToMatchAsync(Is.EqualTo(Blue));
        Assert.That(await PixelAt(Browser, RectSlot), Is.EqualTo(White));
        Assert.That(await PixelAt(Browser, CircleSlot), Is.EqualTo(White));

        await Screen.SwipeAsync(new SwipePathOptions
        {
            From = await OnCanvas(Browser, CanvasSize, RectHome),
            To = await OnCanvas(Browser, CanvasSize, RectSlot),
        });
        await Expect.Poll(() => PixelAt(Browser, RectSlot)).ToMatchAsync(Is.EqualTo(Blue));

        await Screen.SwipeAsync(new SwipePathOptions
        {
            From = await OnCanvas(Browser, CanvasSize, CircleHome),
            To = await OnCanvas(Browser, CanvasSize, CircleSlot),
        });
        await Expect.Poll(() => PixelAt(Browser, CircleSlot)).ToMatchAsync(Is.EqualTo(Orange));
        await Expect.Poll(() => PixelAt(Browser, SaveButtonEdge)).ToMatchAsync(Is.EqualTo(Blue));

        await Screen.TapAtAsync(await OnCanvas(Browser, CanvasSize, SaveButton));
        await Expect.Poll(() => CountColor(Browser, BannerBand, Green)).ToMatchAsync(Is.GreaterThan(0));
        await Expect.Poll(() => PixelAt(Browser, SaveButtonEdge)).ToMatchAsync(Is.EqualTo(Inactive));
    }

    [Test]
    public async Task Bounces_a_shape_off_the_wrong_slot_and_refuses_to_save_early()
    {
        await Screen.SwipeAsync(new SwipePathOptions
        {
            From = await OnCanvas(Browser, CanvasSize, RectHome),
            To = await OnCanvas(Browser, CanvasSize, CircleSlot),
        });
        await Expect.Poll(() => CountColor(Browser, BannerBand, Red)).ToMatchAsync(Is.GreaterThan(0));
        Assert.That(await PixelAt(Browser, RectHome), Is.EqualTo(Blue));
        Assert.That(await PixelAt(Browser, CircleSlot), Is.EqualTo(White));

        await Screen.TapAtAsync(await OnCanvas(Browser, CanvasSize, SaveButton));
        await Expect.Poll(() => PixelAt(Browser, SaveButtonEdge)).ToMatchAsync(Is.EqualTo(Inactive));
        await Expect.Poll(() => CountColor(Browser, BannerBand, Red)).ToMatchAsync(Is.GreaterThan(0));
    }
}
