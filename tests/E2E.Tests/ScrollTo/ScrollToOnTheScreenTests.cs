// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Playwright;
using static E2E.Tests.ScrollTo.ScrollToPage;

namespace E2E.Tests.ScrollTo;

/// <summary>Upstream's <c>describe('scrollTo on the screen')</c> (<c>packages/web/tests/integration/scroll-to.test.ts</c>): which container the screen scrolls.</summary>
[Collection(BrowserCollection.Name)]
public sealed class ScrollToOnTheScreenTests(ChromiumPage chromium) : IClassFixture<ChromiumPage>
{
    [Fact]
    public Task Scrolls_the_page_to_its_bottom_and_back_to_its_top() => WithPageAsync(chromium, async page =>
        {
            await page.SetContentAsync(Rows);
            await ScrollAsync(page, ScrollEdge.Bottom);
            var bottom = await OffsetsAsync(page);
            Assert.True(bottom.MaxTop > 0);
            Assert.Equal(bottom.MaxTop, bottom.Top);
            await ScrollAsync(page, ScrollEdge.Top);
            Assert.Equal(0, (await OffsetsAsync(page)).Top);
        });

    [Fact]
    public Task Scrolls_the_pane_under_the_viewport_center_when_the_page_itself_does_not_scroll() =>
        WithPageAsync(chromium, async page =>
        {
            await page.SetContentAsync(
                $$"""
                <body style="margin: 0; overflow: hidden">
                  <header style="height: 60px">Shell</header>
                  <main id="pane" style="height: 540px; overflow-y: auto">{{Rows}}</main>
                </body>
                """
            );
            await ScrollAsync(page, ScrollEdge.Bottom);
            var pane = await OffsetsAsync(page, "#pane");
            Assert.Equal(pane.MaxTop, pane.Top);
            Assert.Equal(0, (await OffsetsAsync(page)).Top);
        });

    [Fact]
    public Task Scrolls_the_page_not_a_pane_that_happens_to_sit_under_the_center() =>
        WithPageAsync(chromium, async page =>
        {
            await page.SetContentAsync(
                $"<div style=\"height: 250px\"></div><div id=\"pane\" style=\"height: 200px; overflow-y: auto\">{Rows}</div>{Rows}"
            );
            await ScrollAsync(page, ScrollEdge.Bottom);
            var root = await OffsetsAsync(page);
            Assert.Equal(root.MaxTop, root.Top);
            Assert.Equal(0, (await OffsetsAsync(page, "#pane")).Top);
        });

    [Fact]
    public Task Lands_at_once_under_scroll_behavior_smooth() => WithPageAsync(chromium, async page =>
        {
            await page.SetContentAsync($"<style>html {{ scroll-behavior: smooth }}</style>{Rows}");
            await ScrollAsync(page, ScrollEdge.Bottom);
            var root = await OffsetsAsync(page);
            Assert.Equal(root.MaxTop, root.Top);
        });

    [Theory]
    [InlineData("open")]
    [InlineData("closed")]
    public Task Reaches_a_pane_inside_a_x_shadow_root_at_the_center(string mode) =>
        WithPageAsync(chromium, async page =>
        {
            // A navigation, not setContent: init scripts, the closed-root registry among them, run on a new document only.
            await GotoHtmlAsync(page, "<body style=\"margin: 0; overflow: hidden\"><div id=\"host\"></div></body>");
            await page.EvaluateAsync(
                """
                ([rows, shadowMode]) => {
                  const root = document.getElementById("host").attachShadow({ mode: shadowMode });
                  root.innerHTML = `<div id="pane" style="height: 600px; overflow-y: auto">${rows}</div>`;
                  window.pane = root.getElementById("pane");
                }
                """,
                new[] { Rows, mode }
            );
            await ScrollAsync(page, ScrollEdge.Bottom);
            var pane = await page.EvaluateAsync<ScrollOffsets>(
                "() => ({ top: window.pane.scrollTop, maxTop: window.pane.scrollHeight - window.pane.clientHeight })"
            );
            Assert.True(pane.MaxTop > 0);
            Assert.Equal(pane.MaxTop, pane.Top);
        });

    [Fact]
    public Task Resolves_with_nothing_scrolled_when_the_page_is_shorter_than_the_viewport() =>
        WithPageAsync(chromium, async page =>
        {
            await page.SetContentAsync("<p>Short</p>");
            await ScrollAsync(page, ScrollEdge.Bottom);
            Assert.Equal(0, (await OffsetsAsync(page)).Top);
        });

    [Fact]
    public Task Leaves_a_page_locked_with_overflow_hidden_alone() => WithPageAsync(chromium, async page =>
        {
            await page.SetContentAsync($"<body style=\"overflow: hidden\">{Rows}</body>");
            await ScrollAsync(page, ScrollEdge.Bottom);
            Assert.Equal(0, (await OffsetsAsync(page)).Top);
        });

    [Fact]
    public Task Follows_content_that_grows_at_the_bottom_until_it_stops_growing() =>
        WithPageAsync(chromium, async page =>
        {
            await page.SetContentAsync(
                $$"""
                <div id="feed">{{Rows}}</div>
                <script>
                  let batches = 0;
                  addEventListener('scroll', () => {
                    if (batches < 3 && innerHeight + scrollY >= document.body.scrollHeight - 1) {
                      batches += 1;
                      document.getElementById('feed').insertAdjacentHTML('beforeend', '<p style="height: 400px">Batch ' + batches + '</p>');
                    }
                  });
                </script>
                """
            );
            await ScrollAsync(page, ScrollEdge.Bottom);
            var bottom = await OffsetsAsync(page);
            Assert.Equal(1, await page.Locator("text=Batch 3").CountAsync());
            Assert.Equal(bottom.MaxTop, bottom.Top);
        });

    [Fact]
    public Task Gives_up_with_OPERATION_TIMEOUT_when_the_content_never_stops_growing() =>
        WithPageAsync(chromium, async page =>
        {
            await page.SetContentAsync(
                $$"""
                {{Rows}}
                <script>
                  addEventListener('scroll', () => document.body.insertAdjacentHTML('beforeend', '<p style="height: 400px">More</p>'));
                </script>
                """
            );
            var failure = await Assert.ThrowsAsync<EngineException>(() =>
                ScrollAsync(page, ScrollEdge.Bottom, null, TimeSpan.FromMilliseconds(400))
            );
            Assert.Equal(EngineErrorCodes.OperationTimeout, failure.Code);
            Assert.Contains("more content kept loading at the bottom edge", failure.Message);
        });

    [Fact]
    public Task Reports_the_budget_not_a_Playwright_timeout_when_the_page_is_busy_as_the_budget_ends() =>
        WithPageAsync(chromium, async page =>
        {
            // Each scroll adds content, then blocks the page for 300 ms right after the jump reads (a frame
            // callback queued after the jump's own), so the next jump waits on the main thread as the budget ends.
            await page.SetContentAsync(
                $$"""
                {{Rows}}
                <script>
                  addEventListener('scroll', () => {
                    document.body.insertAdjacentHTML('beforeend', '<p style="height: 400px">More</p>');
                    requestAnimationFrame(() => setTimeout(() => { const end = Date.now() + 300; while (Date.now() < end); }, 0));
                  });
                </script>
                """
            );
            var failure = await Assert.ThrowsAsync<EngineException>(() =>
                ScrollAsync(page, ScrollEdge.Bottom, null, TimeSpan.FromMilliseconds(400))
            );
            Assert.Equal(EngineErrorCodes.OperationTimeout, failure.Code);
            Assert.Contains("more content kept loading at the bottom edge", failure.Message);
        });

    [Fact]
    public Task Finishes_while_the_page_clock_is_paused_whose_timers_never_fire() =>
        WithPageAsync(chromium, async page =>
        {
            await page.Clock.InstallAsync();
            await page.SetContentAsync(Rows);
            await page.Clock.PauseAtAsync(DateTime.UtcNow.AddSeconds(60));
            await ScrollAsync(page, ScrollEdge.Bottom);
            var bottom = await OffsetsAsync(page);
            Assert.Equal(bottom.MaxTop, bottom.Top);
        });

    [Fact]
    public Task Follows_a_feed_that_loads_from_an_IntersectionObserver_on_a_sentinel_row() =>
        WithPageAsync(chromium, async page =>
        {
            // The observer runs after the frame a jump waits for, so only a jump that moves nothing may end the scroll.
            await page.SetContentAsync(
                $$"""
                <!doctype html><body style="margin: 0"><div id="feed">{{Rows}}</div><div id="sentinel" style="height: 1px"></div>
                <script>
                  let batches = 0;
                  const sentinel = document.getElementById('sentinel');
                  const observer = new IntersectionObserver((entries) => {
                    if (batches < 3 && entries.some((entry) => entry.isIntersecting)) {
                      batches += 1;
                      document.getElementById('feed').insertAdjacentHTML('beforeend', '<p style="height: 400px; margin: 0">Batch ' + batches + '</p>');
                      // Observe again, as feed libraries do, so the next visit reports afresh.
                      observer.unobserve(sentinel);
                      observer.observe(sentinel);
                    }
                  });
                  observer.observe(sentinel);
                </script></body>
                """
            );
            await ScrollAsync(page, ScrollEdge.Bottom);
            var bottom = await OffsetsAsync(page);
            Assert.Equal(1, await page.Locator("text=Batch 3").CountAsync());
            Assert.Equal(bottom.MaxTop, bottom.Top);
        });
}
