// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests.ScrollTo;

/// <summary>
/// The helpers of upstream's <c>packages/web/tests/integration/scroll-to.test.ts</c>: the sixty rows every page scrolls, <c>scroll</c> (a scroll
/// of the screen, or of the node a selector names, within a timeout) and <c>offsets</c>.
/// </summary>
internal static class ScrollToPage
{
    /// <summary>Sixty 40px rows.</summary>
    public static readonly string Rows = string.Concat(
        Enumerable.Range(1, 60).Select(index => $"<p style=\"height: 40px; margin: 0\">Row {index}</p>")
    );

    /// <summary>The offsets of a scroll container and how far it can scroll.</summary>
    public sealed class ScrollOffsets
    {
        public double Top { get; set; }

        public double Left { get; set; }

        public double MaxTop { get; set; }

        public double MaxLeft { get; set; }
    }

    /// <summary>A fresh page of the class's browser, closed when <paramref name="test"/> ends (upstream's <c>beforeEach</c>).</summary>
    public static async Task WithPageAsync(ChromiumPage chromium, Func<IPage, Task> test)
    {
        var page = await chromium.NewPageAsync();
        try
        {
            await page.SetViewportSizeAsync(800, 600);
            await test(page);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>Scrolls the screen, or the node <paramref name="selector"/> names, to <paramref name="edge"/> within <paramref name="timeout"/>.</summary>
    public static Task ScrollAsync(
        IPage page,
        ScrollEdge edge,
        string? selector = null,
        TimeSpan? timeout = null,
        CancellationToken signal = default
    ) =>
        WebScrollTo.ScrollToEdgeAsync(
            page,
            selector is null ? null : new LocatorTarget(page.Locator(selector)),
            edge,
            new OperationBudget(timeout ?? TimeSpan.FromSeconds(2), "scrollTo", signal)
        );

    /// <summary>The offsets of the page, or of the element <paramref name="selector"/> names.</summary>
    public static Task<ScrollOffsets> OffsetsAsync(IPage page, string? selector = null) =>
        page.EvaluateAsync<ScrollOffsets>(
            """
            (query) => {
              const element = query === null ? document.scrollingElement : document.querySelector(query);
              return {
                top: element.scrollTop,
                left: element.scrollLeft,
                maxTop: element.scrollHeight - element.clientHeight,
                maxLeft: element.scrollWidth - element.clientWidth,
              };
            }
            """,
            selector
        );

    /// <summary>A page navigated to, so the init scripts (the closed-root registry among them) run on its new document.</summary>
    public static Task GotoHtmlAsync(IPage page, string html) =>
        page.GotoAsync("data:text/html," + Uri.EscapeDataString(html));

    /// <summary><paramref name="html"/> escaped for an attribute (<c>ROWS.replaceAll('"', '&quot;')</c>).</summary>
    public static string ForAttribute(string html) => html.Replace("\"", "&quot;", StringComparison.Ordinal);
}
