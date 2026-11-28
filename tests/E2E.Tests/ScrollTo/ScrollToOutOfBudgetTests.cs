// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Playwright;
using static E2E.Tests.ScrollTo.ScrollToPage;

namespace E2E.Tests.ScrollTo;

/// <summary>Upstream's <c>describe('scrollTo out of budget')</c> (<c>packages/web/tests/integration/scroll-to.test.ts</c>): what a scroll reports when the budget or the page runs out under it.</summary>
[Collection(BrowserCollection.Name)]
public sealed class ScrollToOutOfBudgetTests(ChromiumPage chromium) : IClassFixture<ChromiumPage>
{
    [Fact]
    public Task Says_the_budget_ran_out_not_that_content_kept_loading_on_a_static_page_with_too_little_budget() =>
        WithPageAsync(chromium, async page =>
        {
            await page.SetContentAsync(Rows);
            var failure = await Assert.ThrowsAsync<EngineException>(() =>
                ScrollAsync(page, ScrollEdge.Bottom, null, TimeSpan.FromMilliseconds(40))
            );
            Assert.Equal(EngineErrorCodes.OperationTimeout, failure.Code);
            Assert.Equal("the budget ran out before the first jump toward the bottom edge", failure.Message);
            Assert.Equal(0, (await OffsetsAsync(page)).Top);
        });

    [Fact]
    public Task Jumps_nowhere_when_the_step_is_cancelled_while_the_page_is_busy() =>
        WithPageAsync(chromium, async page =>
        {
            // The page blocks its main thread, so the container lookup answers only after the cancel.
            await page.SetContentAsync(Rows);
            await page.EvaluateAsync(
                "() => void setTimeout(() => { const end = Date.now() + 400; while (Date.now() < end); }, 0)"
            );
            using var cancel = new CancellationTokenSource();
            cancel.CancelAfter(TimeSpan.FromMilliseconds(100));

            var failure = await Record.ExceptionAsync(() =>
                ScrollAsync(page, ScrollEdge.Bottom, null, TimeSpan.FromSeconds(2), cancel.Token)
            );

            Assert.NotNull(failure);
            Assert.Equal(0, (await OffsetsAsync(page)).Top);
        });

    [Fact]
    public Task Fails_a_node_that_leaves_the_page_as_stale_and_leaves_the_page_where_it_was() =>
        WithPageAsync(chromium, async page =>
        {
            // The pane replaces itself on its first scroll, so the next jump finds it gone.
            await page.SetContentAsync(
                $$"""
                <div id="pane" style="height: 300px; overflow-y: auto">{{Rows}}</div>{{Rows}}
                <script>
                  document.getElementById('pane').addEventListener('scroll', (event) => event.target.replaceWith(event.target.cloneNode(true)), { once: true });
                </script>
                """
            );
            var pane = await page.QuerySelectorAsync("#pane");

            var failure = await Assert.ThrowsAsync<EngineException>(() =>
                WebScrollTo.ScrollToEdgeAsync(
                    page,
                    new ElementTarget(pane!),
                    ScrollEdge.Bottom,
                    new OperationBudget(TimeSpan.FromSeconds(2), "scrollTo", CancellationToken.None)
                )
            );

            Assert.Equal(EngineErrorCodes.NodeStale, failure.Code);
            Assert.True(failure.Retryable);
            Assert.Equal(0, (await OffsetsAsync(page)).Top);
        });

    [Fact]
    public Task Says_the_budget_ran_out_not_stale_when_the_located_pane_left_and_the_page_stayed_busy_past_the_budget() =>
        WithPageAsync(chromium, async page =>
        {
            // The pane removes itself, and the located row with it, on its first scroll; then the page blocks past the
            // budget right after that jump reads, so the next jump finds the pane gone with no budget left to find it again.
            await page.SetContentAsync(
                $$"""
                <!doctype html><body style="margin: 0"><div id="pane" style="height: 100vh; overflow-y: auto"><p id="first">Row 0</p>{{Rows}}</div>
                <script>
                  document.getElementById('pane').addEventListener('scroll', (event) => {
                    event.target.remove();
                    requestAnimationFrame(() => setTimeout(() => { const end = Date.now() + 1_000; while (Date.now() < end); }, 0));
                  }, { once: true });
                </script></body>
                """
            );

            var failure = await Assert.ThrowsAsync<EngineException>(() =>
                ScrollAsync(page, ScrollEdge.Bottom, "#first", TimeSpan.FromMilliseconds(800))
            );

            Assert.Equal(EngineErrorCodes.OperationTimeout, failure.Code);
            Assert.Equal("the budget ran out after 1 jump, before a jump confirmed the bottom edge", failure.Message);
        });

    [Fact]
    public Task Fails_as_stale_when_the_located_node_is_gone_and_the_container_left_with_it() =>
        WithPageAsync(chromium, async page =>
        {
            await page.SetContentAsync(
                $$"""
                <div id="pane" style="height: 300px; overflow-y: auto">{{Rows}}</div>
                <script>
                  document.getElementById('pane').addEventListener('scroll', (event) => event.target.remove(), { once: true });
                </script>
                """
            );

            var failure = await Assert.ThrowsAsync<EngineException>(() =>
                ScrollAsync(page, ScrollEdge.Bottom, "#pane", TimeSpan.FromSeconds(1))
            );

            Assert.Equal(EngineErrorCodes.NodeStale, failure.Code);
            Assert.True(failure.Retryable);
        });
}
