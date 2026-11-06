// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E;
using E2E.Engine;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.Pointer;

/// <summary>
/// The tests of upstream's <c>apps/testbed/tests/pointer.e2e.ts</c> that sit outside a <c>describe</c>: a tap at a viewport point, a tap at a
/// position of a node, and a swipe along a path, against the playground's <c>/pointer</c> pad and <c>/board</c>.
/// </summary>
public sealed class PointerTests : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => E2E.Playground.Testbed.Url;

    [Test]
    public async Task Screen_tapAt_taps_a_viewport_point_with_no_node_behind_it()
    {
        await App.OpenAsync("/pointer");

        var box = await BoxOf(Screen.GetByRole("image", "Pointer pad"));
        await Screen.TapAtAsync(new Point(box.X + 40, box.Y + 30));

        await Expect.That(Screen.GetByLabel("Pad state")).ToHaveTextAsync("tapped at 40,30");
    }

    [Test]
    public async Task Locator_tap_position_taps_at_an_offset_of_the_node()
    {
        await App.OpenAsync("/pointer");
        var pad = Screen.GetByRole("image", "Pointer pad");

        await pad.TapAsync(new TapOptions { Position = new Point(300, 10) });
        await Expect.That(Screen.GetByLabel("Pad state")).ToHaveTextAsync("tapped at 300,10");

        await pad.ClickAsync(new TapOptions { Position = new Point(0, 0) });
        await Expect.That(Screen.GetByLabel("Pad state")).ToHaveTextAsync("tapped at 0,0");
    }

    [Test]
    public async Task A_position_tap_on_a_node_the_engine_cannot_bring_into_view_fails_as_the_engine_reports()
    {
        await App.OpenAsync("/pointer");

        var error = await Failure(() => Screen.GetByLabel("Hidden pad").TapAsync(new TapOptions { Position = new Point(1, 1), Timeout = TimeSpan.FromSeconds(1) }));

        Assert.That(error, Is.InstanceOf<E2EException>());
        Assert.That(((E2EException)error!).Code, Is.EqualTo("ACTION_FAILED"));
    }

    [Test]
    public async Task Screen_swipe_from_to_moves_the_pointer_along_the_path()
    {
        await App.OpenAsync("/pointer");

        var box = await BoxOf(Screen.GetByRole("image", "Pointer pad"));
        await Screen.SwipeAsync(new SwipePathOptions { From = new Point(box.X + 10, box.Y + 20), To = new Point(box.X + 250, box.Y + 20) });

        await Expect.That(Screen.GetByLabel("Pad state")).ToHaveTextAsync("swiped from 10,20 to 250,20");
    }

    [Test]
    public async Task A_path_swipe_drags_and_drops_on_the_board()
    {
        await App.OpenAsync("/board");

        var card = CenterOf(await BoxOf(Screen.GetByText("Design review", exact: true)));
        var done = CenterOf(await BoxOf(Screen.GetByLabel("Done column")));
        await Screen.SwipeAsync(new SwipePathOptions { From = card, To = done });

        await Expect.That(Screen.GetByLabel("Board state")).ToHaveTextAsync("Design review is done");
    }

    /// <summary>Upstream's <c>boxOf</c>: the box a node paints, for pointer maths; a node without one is a test bug.</summary>
    private static async Task<BoundingBox> BoxOf(Locator locator) =>
        await locator.BoundingBoxAsync() ?? throw new InvalidOperationException("node has no box");

    /// <summary>Upstream's <c>centerOf</c>: the center of a box, the point a finger would aim for.</summary>
    private static Point CenterOf(BoundingBox box) => new(box.X + (box.Width / 2), box.Y + (box.Height / 2));
}
