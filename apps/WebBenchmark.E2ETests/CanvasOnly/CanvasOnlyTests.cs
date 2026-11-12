// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using NUnit.Framework;
using WebBenchmark.E2ETests.Fixtures;
using static WebBenchmark.E2ETests.Fixtures.Canvas;

namespace WebBenchmark.E2ETests.CanvasOnly;

/// <summary>Ported from <c>describe('canvas only')</c> in upstream's <c>apps/web-benchmark/tests/canvas-only.e2e.ts</c>.</summary>
public sealed class CanvasOnlyTests : BenchmarkTest
{
    private static readonly Size CanvasSize = new(480, 360);
    private static readonly Point Square = new(90, 210);
    private static readonly Point Circle = new(240, 210);
    private static readonly Point Diamond = new(390, 210);
    private static readonly Point FirstDot = new(210, 100);
    private static readonly Point SecondDot = new(240, 100);
    private static readonly Point ThirdDot = new(270, 100);
    private static readonly Band StatusBand = new(0, 296, 480, 36);
    private static readonly int[] Green = [0, 170, 0];
    private static readonly int[] Red = [204, 0, 0];
    private static readonly int[] Grey = [204, 204, 204];

    [SetUp]
    public async Task OpenTheCanvasOnly() => await App.OpenAsync("/e/canvas-only");

    [Test]
    public async Task Taps_square_circle_then_diamond_at_their_pixel_coordinates()
    {
        await Expect.Poll(() => PixelAt(Browser, FirstDot)).ToMatchAsync(Is.EqualTo(Grey));
        Assert.That(await PixelAt(Browser, SecondDot), Is.EqualTo(Grey));
        Assert.That(await PixelAt(Browser, ThirdDot), Is.EqualTo(Grey));

        await Screen.TapAtAsync(await OnCanvas(Browser, CanvasSize, Square));
        await Expect.Poll(() => PixelAt(Browser, FirstDot)).ToMatchAsync(Is.EqualTo(Green));
        await Screen.TapAtAsync(await OnCanvas(Browser, CanvasSize, Circle));
        await Expect.Poll(() => PixelAt(Browser, SecondDot)).ToMatchAsync(Is.EqualTo(Green));
        await Screen.TapAtAsync(await OnCanvas(Browser, CanvasSize, Diamond));
        await Expect.Poll(() => PixelAt(Browser, ThirdDot)).ToMatchAsync(Is.EqualTo(Green));
        await Expect.Poll(() => CountColor(Browser, StatusBand, Green)).ToMatchAsync(Is.GreaterThan(0));
    }

    [Test]
    public async Task A_shape_out_of_order_paints_the_error_and_resets_progress()
    {
        await Screen.TapAtAsync(await OnCanvas(Browser, CanvasSize, Square));
        await Expect.Poll(() => PixelAt(Browser, FirstDot)).ToMatchAsync(Is.EqualTo(Green));

        await Screen.TapAtAsync(await OnCanvas(Browser, CanvasSize, Diamond));
        await Expect.Poll(() => CountColor(Browser, StatusBand, Red)).ToMatchAsync(Is.GreaterThan(0));
        await Expect.Poll(() => PixelAt(Browser, FirstDot)).ToMatchAsync(Is.EqualTo(Grey));
    }
}
