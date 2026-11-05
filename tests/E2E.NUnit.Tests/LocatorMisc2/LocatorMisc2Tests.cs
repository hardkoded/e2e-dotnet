// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.LocatorMisc2;

/// <summary>
/// <c>test.describe('locator misc 2')</c> of upstream's <c>apps/testbed/tests/locator-misc-2.e2e.ts</c> (Playwright's
/// <c>locator-misc-2.spec.ts</c>). <c>scrollIntoViewIfNeeded</c> is <c>ScrollIntoViewAsync</c>, and an element's own <c>evaluate</c>
/// is a page <c>EvaluateAsync</c>. Not ported, as upstream: should select textarea (no selectText), should type (deprecated; its
/// replacement, pressSequentially, is ported), should take screenshot (Playwright's own golden image), should combine visible with
/// other selectors (<c>&gt;&gt; visible=true</c> chains), should support filter(visible), should support visible(), and
/// Locator.locator() and FrameLocator.locator() should accept locator (no <c>locator(locator)</c>).
/// </summary>
public sealed class LocatorMisc2Tests : PlaywrightTest
{
    [Test]
    public async Task Should_press_smoke()
    {
        await Browser.GotoAsync(Content("<input type='text' />"));
        await Browser.Locator("input").PressAsync("h");
        Assert.That(await Browser.EvaluateAsync<string>("() => document.querySelector('input').value"), Is.EqualTo("h"));
    }

    [Test]
    public async Task Should_scroll_into_view()
    {
        await Browser.GotoAsync("/offscreenbuttons.html");
        const string Overflow = "(id) => document.getElementById(id).getBoundingClientRect().right - window.innerWidth";
        for (var i = 0; i < 11; ++i)
        {
            var before = await Browser.EvaluateAsync<double>(Overflow, $"btn{i}");
            Assert.That(before, Is.EqualTo(10 * i));
            await Browser.Locator($"#btn{i}").ScrollIntoViewAsync();
            var after = await Browser.EvaluateAsync<double>(Overflow, $"btn{i}");
            Assert.That(after <= 0, Is.True);
            await Browser.EvaluateAsync<object>("() => window.scrollTo(0, 0)");
        }
    }

    [Test]
    public async Task Should_scroll_zero_sized_element_into_view()
    {
        await Browser.GotoAsync(Content("""
            <style>
              html,body { margin: 0; padding: 0; }
              ::-webkit-scrollbar { display: none; }
              * { scrollbar-width: none; }
            </style>
            <div style="height: 2000px; text-align: center; border: 10px solid blue;">
              <h1>SCROLL DOWN</h1>
            </div>
            <div id=lazyload style="font-size:75px; background-color: green;"></div>
            <script>
              const lazyLoadElement = document.querySelector('#lazyload');
              const observer = new IntersectionObserver((entries) => {
                if (entries.some(entry => entry.isIntersecting)) {
                  lazyLoadElement.textContent = 'LAZY LOADED CONTENT';
                  lazyLoadElement.style.height = '20px';
                  observer.disconnect();
                }
              });
              observer.observe(lazyLoadElement);
            </script>
            """));
        Assert.That(await Browser.Locator("#lazyload").BoundingBoxAsync(), Is.EqualTo(new BoundingBox(0, 2020, 1280, 0)));
        await Browser.Locator("#lazyload").ScrollIntoViewAsync();
        await Expect.That(Browser.Locator("#lazyload")).ToHaveTextAsync("LAZY LOADED CONTENT");
        Assert.That(await Browser.Locator("#lazyload").BoundingBoxAsync(), Is.EqualTo(new BoundingBox(0, 720, 1280, 20)));
    }

    [Test]
    public async Task Should_pressSequentially()
    {
        await Browser.GotoAsync(Content("<input type='text' />"));
        await Browser.Locator("input").PressSequentiallyAsync("hello");
        Assert.That(await Browser.EvaluateAsync<string>("() => document.querySelector('input').value"), Is.EqualTo("hello"));
    }

    [Test]
    public async Task Should_return_bounding_box()
    {
        await Browser.SetViewportAsync(500, 500);
        await Browser.GotoAsync("/grid.html");
        var element = Browser.Locator(".box:nth-of-type(13)");
        var box = await element.BoundingBoxAsync();
        Assert.That(box, Is.EqualTo(new BoundingBox(100, 50, 50, 50)));
    }

    [Test]
    public async Task Should_waitFor()
    {
        await Browser.GotoAsync(Content("<div></div>"));
        var locator = Browser.Locator("span");
        var promise = locator.WaitForAsync();
        await Browser.EvaluateAsync<object>("() => { document.querySelector('div').innerHTML = '<span>target</span>'; }");
        await promise;
        await Expect.That(locator).ToHaveTextAsync("target");
    }

    [Test]
    public async Task Should_waitFor_hidden()
    {
        await Browser.GotoAsync(Content("<div><span>target</span></div>"));
        var locator = Browser.Locator("span");
        var promise = locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForState.Hidden });
        await Browser.EvaluateAsync<object>("() => { document.querySelector('div').innerHTML = ''; }");
        await promise;
    }

    // Upstream: { fail: 'locators resolve in Playwright\'s main world, which a page that replaces `Map` breaks; Playwright resolves its own in a
    // utility world, but e2e\'s closed-shadow reach and locate reads need the main world' }. In the port, `EvaluateAsync` works on that page
    // and the locator fails with `EVALUATE_FAILED`.
    [Test]
    public async Task Locator_count_should_work_with_deleted_Map_in_main_world()
    {
        await ExpectedToFailAsync("the page deleted or replaced a JavaScript built-in", async () =>
        {
            await Browser.GotoAsync("/empty.html");
            await Browser.EvaluateAsync<object>("() => { window.Map = 1; }");
            await Browser.Locator("#searchResultTableDiv .x-grid3-row").CountAsync();
            await Expect.That(Browser.Locator("#searchResultTableDiv .x-grid3-row")).ToHaveCountAsync(0);
        });
    }

    [Test]
    public async Task Should_fill_programmatically_enabled_textarea()
    {
        await Browser.GotoAsync(Content("""
            <button>Enable</button>
            <form>
              <textarea id="text" disabled></textarea>
            </form>
            <script>
              document.querySelector('button').addEventListener('click', () => {
                document.querySelector('#text').disabled = false;
              });
            </script>
            """));
        await Browser.Locator("button").ClickAsync();
        await Browser.Locator("#text").FillAsync("Hello");
        await Expect.That(Browser.Locator("#text")).ToHaveValueAsync("Hello");
    }

    [Test]
    public async Task Press_should_throw_on_unknown_keys()
    {
        await Browser.GotoAsync(Content("<input type='text' value='hello' />"));
        var locator = Screen.GetByRole("textbox");
        foreach (var key in new[] { "NotARealKey", "ё", "😊" })
        {
            var error = await Assert.CatchAsync(() => locator.PressAsync(key));
            Assert.That(error!.Message, Does.Contain(key));
        }
    }
}
