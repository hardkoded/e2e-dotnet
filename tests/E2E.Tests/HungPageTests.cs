// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using E2E;
using E2E.Engine;

namespace E2E.Tests;

/// <summary>
/// Every operation ends at its budget, whatever the page does: a server that
/// never answers a navigation, which Playwright's own timeout ends, and a
/// renderer stuck in a script a button started. Playwright cannot answer an
/// evaluate there at all, so the engine's own deadline ends the call:
/// <c>OPERATION_TIMEOUT</c>, or <c>ACTION_MAY_HAVE_COMMITTED</c> for input,
/// which may have reached the page.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class HungPageTests
{
    private const string BusyPage = """
        <!DOCTYPE html><title>Busy</title>
        <button onclick="while (true) {}">Freeze</button>
        <button>Next</button>
        """;

    private const string CoveredPage = """
        <!DOCTYPE html><title>Covered</title>
        <button>Pay</button>
        <div id="overlay" style="position: fixed; inset: 0; background: rgba(0, 0, 0, 0.3)"></div>
        """;

    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(1_000);

    // How long past its budget a call may still be pending before the test calls it unbounded.
    private static readonly TimeSpan Slack = TimeSpan.FromMilliseconds(1_000);

    // How much sooner than the budget a call may fail.
    private static readonly TimeSpan Early = TimeSpan.FromMilliseconds(300);

    [Fact]
    public async Task Chromium_times_out_a_navigation_to_a_server_that_never_answers()
    {
        using var site = await TinySite.StartAsync(_ => Task.Delay(Timeout.Infinite));
        await using var session = await StartAsync();

        var open = await BoundedAsync(() => session.OpenAsync(site.Url + "never", CancellationToken.None));

        Assert.Equal("OPERATION_TIMEOUT", open.Code);
    }

    [Fact]
    public async Task Chromium_times_out_an_observe_a_scroll_and_an_evaluate_on_a_page_whose_renderer_is_stuck_in_a_script()
    {
        using var site = await TinySite.StartAsync(BusyPage);
        await using var session = await StartAsync();
        await FreezeAsync(session, site);

        var observe = await BoundedAsync(() => session.ObserveAsync(CancellationToken.None));
        Assert.Equal("OPERATION_TIMEOUT", observe.Code);
        var scroll = await BoundedAsync(() => session.SwipeAsync(ScrollDirection.Down, CancellationToken.None));
        Assert.Equal("OPERATION_TIMEOUT", scroll.Code);
        var browser = (IBrowserSession)session;
        var evaluate = await BoundedAsync(() => browser.EvaluateAsync("1 + 1", null, hasArg: false, CancellationToken.None));
        Assert.Equal("OPERATION_TIMEOUT", evaluate.Code);
        var title = await BoundedAsync(() => browser.GetTitleAsync(CancellationToken.None));
        Assert.Equal("OPERATION_TIMEOUT", title.Code);
    }

    [Fact]
    public async Task Chromium_times_out_a_tap_and_a_swipe_on_a_page_whose_renderer_is_stuck_in_a_script()
    {
        using var site = await TinySite.StartAsync(BusyPage);
        await using var session = await StartAsync();
        var next = await FreezeAsync(session, site);

        var tap = await BoundedAsync(() => session.PerformAsync(next, new LocatorAction.Tap(), CancellationToken.None));
        Assert.Equal("OPERATION_TIMEOUT", tap.Code);
        var swipe = await BoundedAsync(() => session.PerformAsync(next, new LocatorAction.Swipe(ScrollDirection.Down), CancellationToken.None));
        Assert.Equal("OPERATION_TIMEOUT", swipe.Code);
    }

    [Fact]
    public async Task Chromium_times_out_keyboard_and_mouse_input_on_a_page_whose_renderer_is_stuck_in_a_script()
    {
        using var site = await TinySite.StartAsync(BusyPage);
        await using var session = await StartAsync();
        await FreezeAsync(session, site);
        var browser = (IBrowserSession)session;

        var press = await BoundedAsync(() => session.PressAsync("Enter", CancellationToken.None));
        Assert.Equal("ACTION_MAY_HAVE_COMMITTED", press.Code);
        var type = await BoundedAsync(() => browser.KeyboardTypeAsync("hello", CancellationToken.None));
        Assert.Equal("ACTION_MAY_HAVE_COMMITTED", type.Code);
        var move = await BoundedAsync(() => browser.MouseMoveAsync(10, 10, CancellationToken.None));
        Assert.Equal("ACTION_MAY_HAVE_COMMITTED", move.Code);
        var down = await BoundedAsync(() => browser.MouseDownAsync(CancellationToken.None));
        Assert.Equal("ACTION_MAY_HAVE_COMMITTED", down.Code);
        var up = await BoundedAsync(() => browser.MouseUpAsync(CancellationToken.None));
        Assert.Equal("ACTION_MAY_HAVE_COMMITTED", up.Code);
        var wheel = await BoundedAsync(() => browser.MouseWheelAsync(0, 100, CancellationToken.None));
        Assert.Equal("ACTION_MAY_HAVE_COMMITTED", wheel.Code);
    }

    [Fact]
    public async Task Chromium_keeps_the_reason_playwright_gives_for_a_tap_that_cannot_land_ahead_of_the_deadline()
    {
        using var site = await TinySite.StartAsync(CoveredPage);
        await using var session = await StartAsync();
        await session.OpenAsync(site.Url, CancellationToken.None);
        var pay = Button(await session.ObserveAsync(CancellationToken.None), "Pay");

        var tap = await BoundedAsync(() => session.PerformAsync(pay, new LocatorAction.Tap(), CancellationToken.None));

        Assert.Equal("NOT_ACTIONABLE", tap.Code);
        Assert.Contains("intercepts pointer events", tap.InnerException?.Message, StringComparison.Ordinal);
    }

    private static Task<IEngineSession> StartAsync()
    {
        return new WebEngine(headless: true).StartAsync(new EngineStartOptions { ActionTimeout = Budget }, CancellationToken.None);
    }

    /// <summary>Opens the busy page, observes it, and taps the button that starts the endless script. Returns the other button.</summary>
    private static async Task<SemanticNode> FreezeAsync(IEngineSession session, TinySite site)
    {
        await session.OpenAsync(site.Url, CancellationToken.None);
        var observation = await session.ObserveAsync(CancellationToken.None);
        var freeze = Button(observation, "Freeze");
        var tap = await BoundedAsync(() => session.PerformAsync(freeze, new LocatorAction.Tap(), CancellationToken.None));
        Assert.Equal("ACTION_MAY_HAVE_COMMITTED", tap.Code);
        return Button(observation, "Next");
    }

    /// <summary>
    /// Settles one call, or gives up on it a while after its budget: a call
    /// still pending then fails the test at once instead of holding it. A call
    /// that failed well before its budget fails the test too: the budget must
    /// bound it, not cut it short.
    /// </summary>
    private static async Task<EngineException> BoundedAsync(Func<Task> call)
    {
        var started = Stopwatch.StartNew();
        var running = call();
        var settled = await Task.WhenAny(running, Task.Delay(Budget + Slack));
        Assert.True(settled == running, $"still pending {(Budget + Slack).TotalMilliseconds}ms into a {Budget.TotalMilliseconds}ms budget");
        Assert.True(started.Elapsed >= Budget - Early, $"failed after {started.ElapsedMilliseconds}ms of a {Budget.TotalMilliseconds}ms budget");
        return await Assert.ThrowsAsync<EngineException>(() => running);
    }

    private static SemanticNode Button(Observation observation, string name)
    {
        return Flatten(observation.Roots).First(node => node.Role == "button" && node.Name == name);
    }

    private static IEnumerable<SemanticNode> Flatten(IEnumerable<SemanticNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children))
            {
                yield return child;
            }
        }
    }
}
