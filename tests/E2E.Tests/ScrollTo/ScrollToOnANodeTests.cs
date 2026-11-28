// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Playwright;
using static E2E.Tests.ScrollTo.ScrollToPage;

namespace E2E.Tests.ScrollTo;

/// <summary>Upstream's <c>describe('scrollTo on a node')</c> (<c>packages/web/tests/integration/scroll-to.test.ts</c>): which container a node scrolls, on both axes and in either writing direction.</summary>
[Collection(BrowserCollection.Name)]
public sealed class ScrollToOnANodeTests(ChromiumPage chromium) : IClassFixture<ChromiumPage>
{
    private static readonly string Pane =
        $"<div id=\"pane\" style=\"height: 300px; overflow-y: auto\">{Rows}</div>{Rows}";

    [Fact]
    public Task Scrolls_the_node_itself_not_the_page_around_it() => WithPageAsync(chromium, async page =>
        {
            await page.SetContentAsync(Pane);
            await ScrollAsync(page, ScrollEdge.Bottom, "#pane");
            var pane = await OffsetsAsync(page, "#pane");
            Assert.Equal(pane.MaxTop, pane.Top);
            Assert.Equal(0, (await OffsetsAsync(page)).Top);
        });

    [Fact]
    public Task Scrolls_the_nearest_ancestor_that_scrolls_when_the_node_does_not() =>
        WithPageAsync(chromium, async page =>
        {
            await page.SetContentAsync(Pane);
            await ScrollAsync(page, ScrollEdge.Bottom, "#pane p:first-child");
            var pane = await OffsetsAsync(page, "#pane");
            Assert.Equal(pane.MaxTop, pane.Top);
        });

    [Fact]
    public Task Scrolls_the_page_from_a_node_when_the_bodys_overflow_belongs_to_the_viewport_not_the_body_itself() =>
        WithPageAsync(chromium, async page =>
        {
            // Standards mode, where the root is the scrolling element: the body reports overflow-y auto and room to scroll, yet does not scroll.
            await page.SetContentAsync(
                $"<!doctype html><style>html, body {{ height: 100% }} body {{ overflow-x: hidden }}</style><p id=\"first\">Row 0</p>{Rows}"
            );
            await ScrollAsync(page, ScrollEdge.Bottom, "#first");
            var root = await OffsetsAsync(page);
            Assert.True(root.MaxTop > 0);
            Assert.Equal(root.MaxTop, root.Top);
        });

    [Theory]
    [InlineData("ltr", ScrollEdge.Right)]
    [InlineData("ltr", ScrollEdge.Left)]
    [InlineData("rtl", ScrollEdge.Right)]
    [InlineData("rtl", ScrollEdge.Left)]
    public Task Reaches_the_x_carousels_y_edge(string dir, ScrollEdge edge) => WithPageAsync(chromium, async page =>
        {
            var cards = string.Concat(
                Enumerable
                    .Range(1, 20)
                    .Select(index => $"<span style=\"display: inline-block; width: 200px\">Card {index}</span>")
            );
            await page.SetContentAsync(
                $"<div id=\"carousel\" dir=\"{dir}\" style=\"width: 400px; overflow-x: auto; white-space: nowrap\">{cards}</div>"
            );
            // Start in the middle so either edge is a move.
            await page.Locator("#carousel")
                .EvaluateAsync(
                    "(element) => { element.scrollLeft = (element.scrollWidth - element.clientWidth) / 2 * (getComputedStyle(element).direction === 'rtl' ? -1 : 1); }"
                );
            await ScrollAsync(page, edge, "#carousel");
            var carousel = await OffsetsAsync(page, "#carousel");
            // In right-to-left, the start is offset 0 at the right and the offsets run negative to the left.
            var expected =
                dir == "ltr" ? (edge == ScrollEdge.Left ? 0 : carousel.MaxLeft)
                : edge == ScrollEdge.Right ? 0
                : -carousel.MaxLeft;
            Assert.True(Math.Abs(carousel.Left - expected) <= 1, $"left {carousel.Left}, expected {expected}");
        });

    [Fact]
    public Task Scrolls_the_frames_own_page_from_a_node_inside_an_iframe_and_leaves_the_outer_page_alone() =>
        WithPageAsync(chromium, async page =>
        {
            await page.SetContentAsync(
                $"<iframe id=\"frame\" style=\"height: 300px; width: 400px\" srcdoc=\"{ForAttribute(Rows)}\"></iframe>{Rows}"
            );
            var inner = page.FrameLocator("#frame").Locator("p").First;
            await inner.WaitForAsync();

            await WebScrollTo.ScrollToEdgeAsync(
                page,
                new LocatorTarget(inner),
                ScrollEdge.Bottom,
                new OperationBudget(TimeSpan.FromSeconds(2), "scrollTo", CancellationToken.None)
            );

            var frame = await page.FrameLocator("#frame")
                .Locator(":root")
                .EvaluateAsync<ScrollOffsets>(
                    "(root) => ({ top: root.scrollTop, maxTop: root.scrollHeight - root.clientHeight })"
                );
            Assert.True(frame.MaxTop > 0);
            Assert.Equal(frame.MaxTop, frame.Top);
            Assert.Equal(0, (await OffsetsAsync(page)).Top);
        });

    [Theory]
    [InlineData("open")]
    [InlineData("closed")]
    public Task Walks_from_a_slotted_node_up_through_its_slot_to_the_pane_in_a_x_shadow_root(string mode) =>
        WithPageAsync(chromium, async page =>
        {
            // A navigation, not setContent: init scripts, the closed-root registry among them, run on a new document only.
            await GotoHtmlAsync(page, $"<div id=\"host\">{Rows}</div>");
            await page.EvaluateAsync(
                """
                (shadowMode) => {
                  const root = document.getElementById("host").attachShadow({ mode: shadowMode });
                  root.innerHTML = '<div id="pane" style="height: 300px; overflow-y: auto"><slot></slot></div>';
                  window.pane = root.getElementById("pane");
                }
                """,
                mode
            );

            await ScrollAsync(page, ScrollEdge.Bottom, "#host p:first-child");

            var pane = await page.EvaluateAsync<ScrollOffsets>(
                "() => ({ top: window.pane.scrollTop, maxTop: window.pane.scrollHeight - window.pane.clientHeight })"
            );
            Assert.True(pane.MaxTop > 0);
            Assert.Equal(pane.MaxTop, pane.Top);
            Assert.Equal(0, (await OffsetsAsync(page)).Top);
        });

    [Fact]
    public Task Keeps_the_container_when_the_located_row_unmounts_while_the_list_grows() =>
        WithPageAsync(chromium, async page =>
        {
            await page.SetContentAsync(
                $$"""
                <div id="pane" style="height: 300px; overflow-y: auto"><p id="row" style="height: 40px; margin: 0">Row</p>{{Rows}}</div>
                <script>
                  let batches = 0;
                  const pane = document.getElementById('pane');
                  pane.addEventListener('scroll', () => {
                    if (batches < 3 && pane.scrollTop + pane.clientHeight >= pane.scrollHeight - 1) {
                      batches += 1;
                      document.getElementById('row')?.remove();
                      pane.insertAdjacentHTML('beforeend', '<p style="height: 400px; margin: 0">Batch ' + batches + '</p>');
                    }
                  });
                </script>
                """
            );

            await ScrollAsync(page, ScrollEdge.Bottom, "#row");

            var pane = await OffsetsAsync(page, "#pane");
            Assert.Equal(1, await page.Locator("text=Batch 3").CountAsync());
            Assert.Equal(pane.MaxTop, pane.Top);
        });

    [Theory]
    [InlineData("body", "contain: paint")]
    [InlineData("body", "contain: layout")]
    [InlineData("body", "contain: size")]
    [InlineData("body", "contain: inline-size")]
    [InlineData("body", "contain: style")]
    [InlineData("body", "container-type: inline-size")]
    [InlineData("body", "container-type: size")]
    [InlineData("body", "container-type: anchored")]
    [InlineData("html", "container-type: anchored")]
    [InlineData("body", "container-type: scroll-state anchored")]
    [InlineData("html", "contain: layout")]
    public Task Scrolls_a_body_whose_overflow_stays_its_own_under_x_y_from_a_node_and_from_the_screen(
        string on,
        string style
    ) => WithPageAsync(chromium, async page =>
        {
            string Content(string form) =>
                $"<!doctype html><html style=\"{(on == "html" ? style : "")}\">"
                + $"<body style=\"margin: 0; height: 100vh; overflow: auto; {(on == "body" ? style : "")}\"><p id=\"first\">Row 0 {form}</p>{Rows}</body></html>";
            foreach (var form in new[] { "node", "screen" })
            {
                await page.SetContentAsync(Content(form));
                await ScrollAsync(page, ScrollEdge.Bottom, form == "node" ? "#first" : null);
                var body = await OffsetsAsync(page, "body");
                Assert.True(body.MaxTop > 0, form);
                Assert.True(body.Top == body.MaxTop, form);
                Assert.True((await OffsetsAsync(page)).Top == 0, form);
            }
        });

    [Theory]
    [InlineData("html")]
    [InlineData("body")]
    public Task Scrolls_the_page_not_the_body_under_x_container_type_scroll_state_which_applies_no_containment(
        string on
    ) => WithPageAsync(chromium, async page =>
        {
            const string style = "container-type: scroll-state";
            string Content(string form) =>
                $"<!doctype html><html style=\"height: 100%; {(on == "html" ? style : "")}\">"
                + $"<body style=\"margin: 0; height: 100%; overflow-y: auto; {(on == "body" ? style : "")}\"><p id=\"first\">Row 0 {form}</p>{Rows}</body></html>";
            foreach (var form in new[] { "node", "screen" })
            {
                await page.SetContentAsync(Content(form));
                await ScrollAsync(page, ScrollEdge.Bottom, form == "node" ? "#first" : null);
                var root = await OffsetsAsync(page);
                Assert.True(root.MaxTop > 0, form);
                Assert.True(root.Top == root.MaxTop, form);
            }
        });

    [Fact]
    public Task Walks_out_of_a_shadow_root_to_its_host_when_the_page_replaced_the_global_ShadowRoot() =>
        WithPageAsync(chromium, async page =>
        {
            await page.SetContentAsync(
                $$"""
                <div id="pane" style="height: 300px; overflow-y: auto"><div id="host"></div>{{Rows}}</div>{{Rows}}
                <script>
                  document.getElementById('host').attachShadow({ mode: 'open' }).innerHTML = '<p id="start">Start</p>';
                  window.ShadowRoot = function ShadowRoot() {};
                </script>
                """
            );

            await ScrollAsync(page, ScrollEdge.Bottom, "#start");

            var pane = await OffsetsAsync(page, "#pane");
            Assert.Equal(pane.MaxTop, pane.Top);
            Assert.Equal(0, (await OffsetsAsync(page)).Top);
        });

    [Fact]
    public Task Finishes_for_a_node_in_a_cross_origin_iframe_out_of_view_which_renders_no_frames() =>
        WithPageAsync(chromium, async page =>
        {
            var inner = $"<div id=\"pane\" style=\"height: 300px; overflow-y: auto\">{Rows}</div>";
            await page.SetContentAsync(
                $"<div style=\"height: 2000px\"></div><iframe id=\"frame\" style=\"height: 400px; width: 400px\" src=\"data:text/html,{Uri.EscapeDataString(inner)}\"></iframe>"
            );
            var pane = page.FrameLocator("#frame").Locator("#pane");
            await pane.WaitForAsync();

            var done = WebScrollTo.ScrollToEdgeAsync(
                page,
                new LocatorTarget(pane),
                ScrollEdge.Bottom,
                new OperationBudget(TimeSpan.FromSeconds(2), "scrollTo", CancellationToken.None)
            );
            var finished = await Task.WhenAny(done, Task.Delay(TimeSpan.FromMilliseconds(1_500)));

            Assert.Same(done, finished);
            await done;
            var inFrame = await pane.EvaluateAsync<ScrollOffsets>(
                "(element) => ({ top: element.scrollTop, maxTop: element.scrollHeight - element.clientHeight })"
            );
            Assert.Equal(inFrame.MaxTop, inFrame.Top);
        });

    [Fact]
    public Task Waits_for_a_node_that_is_not_visible_yet_then_scrolls_it() => WithPageAsync(chromium, async page =>
        {
            await page.SetContentAsync(
                $$"""
                <div id="pane" style="display: none; height: 300px; overflow-y: auto">{{Rows}}</div>
                <script>setTimeout(() => { document.getElementById('pane').style.display = 'block'; }, 200);</script>
                """
            );

            await ScrollAsync(page, ScrollEdge.Bottom, "#pane");

            var pane = await OffsetsAsync(page, "#pane");
            Assert.True(pane.MaxTop > 0);
            Assert.Equal(pane.MaxTop, pane.Top);
        });
}
