// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.ClickScroll;

/// <summary>
/// <c>test.describe('page click scroll')</c> of upstream's <c>apps/testbed/tests/click-scroll.e2e.ts</c> (Playwright's
/// <c>page-click-scroll.spec.ts</c>). <c>TapAsync</c> stands for <c>page.click</c>, and a <c>text=</c> selector is a substring
/// <c>GetByText</c>. Not ported, as upstream: should not crash when force-clicking hidden input (no <c>force</c>); should not scroll the page
/// when scroll is "none", should click in-viewport element when scroll is "none", should not scroll nested container when scroll is "none",
/// should not scroll on hover when scroll is "none" (no <c>scroll</c> option); should scroll into view display:contents with position
/// (Playwright skips it on Chromium).
/// </summary>
public sealed class PageClickScrollTests : PlaywrightTest
{
    [Test]
    public async Task Should_not_hit_scroll_bar()
    {
        await Browser.GotoAsync(Content("""
            <style>
              .categories { width: 180px; display: flex; overflow-x: scroll; }
              button { flex: none; height: 28px; }
            </style>
            <div class="categories">
              <button>One</button>
              <button>Two</button>
              <button>Three</button>
              <button>Story</button>
              <button>More</button>
              <button>Items</button>
              <button>Here</button>
            </div>
            """));
        await Screen.GetByText("Story", exact: false).TapAsync(new ActionOptions { Timeout = TimeSpan.FromSeconds(2) });
    }

    [Test]
    public async Task Should_scroll_into_view_display_contents()
    {
        await Browser.GotoAsync(Content("""
            <div style="background:red;height:2000px">filler</div>
            <div>
              Example text, and button here:
              <button style="display: contents" onclick="window._clicked=true;">click me</button>
            </div>
            """));
        await Screen.GetByText("click me", exact: false).TapAsync(new ActionOptions { Timeout = TimeSpan.FromSeconds(5) });
        Assert.That(await Browser.EvaluateAsync<bool>("window._clicked"), Is.True);
    }

    [Test]
    public async Task Should_scroll_into_view_display_contents_with_a_child()
    {
        await Browser.GotoAsync(Content("""
            <div style="background:red;height:2000px">filler</div>
            Example text, and button here:
            <button style="display: contents" onclick="window._clicked=true;"><div>click me</div></button>
            """));
        await Screen.GetByText("click me", exact: false).TapAsync(new ActionOptions { Timeout = TimeSpan.FromSeconds(5) });
        Assert.That(await Browser.EvaluateAsync<bool>("window._clicked"), Is.True);
    }

    [Test]
    public async Task Should_scroll_into_view_span_element()
    {
        await Browser.GotoAsync(Content("""
            <div id=big style="height: 10000px;"></div>
            <span id=small>foo</span>
            """));
        await Browser.Locator("#small").ScrollIntoViewAsync();
        Assert.That(await Browser.EvaluateAsync<double>("() => window.scrollY"), Is.GreaterThan(9000));
    }

    [Test]
    public async Task Should_scroll_into_view_element_in_iframe()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.GotoAsync(Content($"""
            <div id=big style="height: 10000px;"></div>
            <iframe src='{CROSS}/input/button.html'></iframe>
            """));
        await Browser.FrameLocator("iframe").GetByRole("button").TapAsync(new ActionOptions { Timeout = TimeSpan.FromSeconds(5) });
    }

    [Test]
    public async Task Should_scroll_instantly_on_retry_when_scroll_behavior_is_smooth()
    {
        await Browser.GotoAsync(Content("""
            <style>
              html { scroll-behavior: smooth; }
              body { margin: 0; }
              .spacer { height: 2000px; }
              #header { position: fixed; top: 0; left: 0; right: 0; bottom: 50px; background: rgba(0, 0, 0, 0.1); }
              button { height: 30px; }
            </style>
            <div id="header"></div>
            <div class="spacer"></div>
            <button onclick="window.clicked = true">Target</button>
            <div class="spacer"></div>
            <script>
              window.scrolls = [];
              addEventListener('scroll', () => window.scrolls.push(window.scrollY));
            </script>
            """));
        await Browser.Locator("button").TapAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("window.clicked"), Is.True);
        var scrolls = await Browser.EvaluateAsync<double[]>("() => window['scrolls']");
        Assert.That(scrolls!.Length, Is.LessThanOrEqualTo(2));
    }
}
