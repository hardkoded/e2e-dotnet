// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.HungPage.HungPageSupport;

namespace E2E.Tests;

/// <summary>
/// Port-only hung-page cases: the reads, node actions, and input the port
/// bounds beyond upstream's locate and point tap.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class HungPageTests
{
    [Fact]
    public async Task Chromium_times_out_reads_and_node_actions_on_a_page_whose_renderer_is_stuck_in_a_script()
    {
        using var site = await TinySite.StartAsync(BusyPage);
        await using var session = await StartAsync();
        var next = await FreezeAsync(session, site);
        var browser = (IBrowserSession)session;

        var scroll = await BoundedAsync(() => session.SwipeAsync(ScrollDirection.Down, CancellationToken.None));
        Assert.Equal("OPERATION_TIMEOUT", scroll.Code);
        var evaluate = await BoundedAsync(() => browser.EvaluateAsync("1 + 1", null, hasArg: false, CancellationToken.None));
        Assert.Equal("OPERATION_TIMEOUT", evaluate.Code);
        var title = await BoundedAsync(() => browser.GetTitleAsync(CancellationToken.None));
        Assert.Equal("OPERATION_TIMEOUT", title.Code);
        var tap = await BoundedAsync(() => session.PerformAsync(next, new LocatorAction.Tap(), CancellationToken.None));
        Assert.Equal("OPERATION_TIMEOUT", tap.Code);
        var swipe = await BoundedAsync(() => session.PerformAsync(next, new LocatorAction.Swipe(ScrollDirection.Down), CancellationToken.None));
        Assert.Equal("OPERATION_TIMEOUT", swipe.Code);
    }

    [Fact]
    public async Task Chromium_times_out_keyboard_input_and_a_mouse_wheel_on_a_page_whose_renderer_is_stuck_in_a_script()
    {
        using var site = await TinySite.StartAsync(BusyPage);
        await using var session = await StartAsync();
        await FreezeAsync(session, site);
        var browser = (IBrowserSession)session;

        var press = await BoundedAsync(() => session.PressAsync("Enter", CancellationToken.None));
        Assert.Equal("ACTION_MAY_HAVE_COMMITTED", press.Code);
        var type = await BoundedAsync(() => browser.KeyboardTypeAsync("hello", CancellationToken.None));
        Assert.Equal("ACTION_MAY_HAVE_COMMITTED", type.Code);
        var wheel = await BoundedAsync(() => browser.MouseWheelAsync(0, 100, CancellationToken.None));
        Assert.Equal("ACTION_MAY_HAVE_COMMITTED", wheel.Code);
    }
}
