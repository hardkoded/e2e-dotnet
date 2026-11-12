// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.ScrollE2E;

/// <summary>
/// Upstream's <c>test.describe('scrolling')</c> (<c>apps/testbed/tests/scroll.e2e.ts</c>) against a real Chromium. The port has no
/// testbed, so each test opens a small page of its own with the same roles and names as the testbed's <c>/scroll</c> page: a
/// page of rows that render as the page scrolls, and a <c>Pane</c> list that does the same inside itself. The page counts
/// rows up to 100 and pane items up to 60, which stand in for the testbed's longer lists.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class ScrollingTests
{
    private const string ScrollPage = """
        <!DOCTYPE html>
        <html><body style="margin:0">
        <div style="position:fixed;right:0;bottom:0;width:240px;background:#fff">
          <div role="status" aria-label="Scroll position" id="pos">0</div>
          <div role="status" aria-label="Pane position" id="panepos">0</div>
          <div role="status" aria-label="Picked row" id="picked"></div>
        </div>
        <ul role="list" aria-label="Pane" id="pane" style="margin:100px 0 0 400px;padding:0;list-style:none;width:300px;height:200px;overflow:auto"></ul>
        <div id="rows"></div>
        <script>
          const pick = (label) => () => { document.getElementById("picked").textContent = "picked " + label; };
          const rows = document.getElementById("rows");
          const pane = document.getElementById("pane");
          let rowCount = 0;
          let itemCount = 0;
          function fill() {
            while (rowCount < 100 && rows.offsetTop + rows.offsetHeight < scrollY + innerHeight + 1500) {
              rowCount += 1;
              const button = document.createElement("button");
              button.type = "button";
              button.style.cssText = "display:block;height:40px;width:200px";
              button.textContent = "Row " + rowCount;
              button.onclick = pick("Row " + rowCount);
              rows.appendChild(button);
            }
            while (itemCount < 60 && pane.scrollHeight < pane.scrollTop + pane.clientHeight + 400) {
              itemCount += 1;
              const item = document.createElement("li");
              const button = document.createElement("button");
              button.type = "button";
              button.style.cssText = "display:block;height:40px;width:200px";
              button.textContent = "Item " + itemCount;
              button.onclick = pick("Item " + itemCount);
              item.appendChild(button);
              pane.appendChild(item);
            }
          }
          addEventListener("scroll", () => { document.getElementById("pos").textContent = String(Math.round(scrollY)); fill(); });
          pane.addEventListener("scroll", () => { document.getElementById("panepos").textContent = String(Math.round(pane.scrollTop)); fill(); });
          fill();
        </script>
        </body></html>
        """;

    [Fact]
    public async Task ScrollUntilVisible_pages_the_viewport_until_a_row_far_down_renders()
    {
        using var site = await TinySite.StartAsync(ScrollPage);
        var session = await WebEngineTests.StartAsync(site, new ScriptedModel(_ => ModelResponses.Done("passed", "unused")), cache: null);
        await WebEngineTests.RunAsync(session, async () =>
        {
            await session.App.OpenAsync("/");
            var screen = session.Screen;
            var row = screen.GetByRole("button", "Row 100");
            await Expect.That(row).ToHaveCountAsync(0);

            await screen.ScrollUntilVisibleAsync(row);
            await Expect.That(screen.GetByRole("status", "Scroll position")).Not.ToHaveTextAsync("0");
            await row.TapAsync();
            await Expect.That(screen.GetByRole("status", "Picked row")).ToHaveTextAsync("picked Row 100");
        });
    }

    [Fact]
    public async Task ScrollUntilVisible_on_a_node_pages_that_node_and_leaves_the_page_where_it_was()
    {
        using var site = await TinySite.StartAsync(ScrollPage);
        var session = await WebEngineTests.StartAsync(site, new ScriptedModel(_ => ModelResponses.Done("passed", "unused")), cache: null);
        await WebEngineTests.RunAsync(session, async () =>
        {
            await session.App.OpenAsync("/");
            var screen = session.Screen;
            var pane = screen.GetByRole("list", "Pane");
            var item = screen.GetByRole("button", "Item 40");
            await Expect.That(item).ToHaveCountAsync(0);

            await pane.ScrollUntilVisibleAsync(item);
            await Expect.That(screen.GetByRole("status", "Pane position")).Not.ToHaveTextAsync("0");
            await Expect.That(screen.GetByRole("status", "Scroll position")).ToHaveTextAsync("0");
            await item.TapAsync();
            await Expect.That(screen.GetByRole("status", "Picked row")).ToHaveTextAsync("picked Item 40");
        });
    }

    /// <summary>Partial: the port has no <c>screen.swipe</c> (COMPATIBILITY.md), so the viewport swipe is the engine's <c>SwipeAsync</c> and the wheel is Playwright's mouse wheel, and the position is read from the page.</summary>
    [Fact]
    public async Task Partial_A_viewport_swipe_and_a_wheel_gesture_both_move_the_page()
    {
        using var site = await TinySite.StartAsync(ScrollPage);
        await using var session = await new WebEngine(headless: true).StartAsync(new EngineStartOptions(), CancellationToken.None);
        await session.OpenAsync(site.Url, CancellationToken.None);
        var page = WebEngine.SurfaceOf(session)!.Page();
        async Task<int> PositionAsync() => int.Parse(await page.Locator("#pos").TextContentAsync() ?? "-1", System.Globalization.CultureInfo.InvariantCulture);
        async Task SettleAsync(Func<int, bool> reached)
        {
            for (var attempt = 0; attempt < 100 && !reached(await PositionAsync()); attempt++)
            {
                await Task.Delay(50);
            }
        }

        Assert.Equal(0, await PositionAsync());

        await session.ObserveAsync(CancellationToken.None);
        await session.SwipeAsync(ScrollDirection.Down, CancellationToken.None);
        await SettleAsync(position => position > 0);
        var afterSwipe = await PositionAsync();
        Assert.True(afterSwipe > 0);

        await page.Mouse.WheelAsync(0, 600);
        await SettleAsync(position => position == afterSwipe + 600);
        Assert.Equal(afterSwipe + 600, await PositionAsync());

        await session.SwipeAsync(ScrollDirection.Up, CancellationToken.None);
        await SettleAsync(position => position < afterSwipe + 600);
        Assert.True(await PositionAsync() < afterSwipe + 600);
    }

    [Fact]
    public async Task A_node_swipe_scrolls_only_inside_a_scrollable_node()
    {
        using var site = await TinySite.StartAsync(ScrollPage);
        var session = await WebEngineTests.StartAsync(site, new ScriptedModel(_ => ModelResponses.Done("passed", "unused")), cache: null);
        await WebEngineTests.RunAsync(session, async () =>
        {
            await session.App.OpenAsync("/");
            var screen = session.Screen;
            await screen.GetByRole("list", "Pane").SwipeAsync(new SwipeOptions { Direction = ScrollDirection.Down, Momentum = Momentum.Slow });
            await Expect.That(screen.GetByRole("status", "Pane position")).Not.ToHaveTextAsync("0");
            await Expect.That(screen.GetByRole("status", "Scroll position")).ToHaveTextAsync("0");
        });
    }
}
