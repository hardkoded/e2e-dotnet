// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.LocatorIsVisible;

/// <summary>
/// <c>test.describe('locator is visible')</c> of upstream's <c>apps/testbed/tests/locator-is-visible.e2e.ts</c> (Playwright's
/// <c>locator-is-visible.spec.ts</c>). The <c>page.isVisible(selector)</c> twins of each locator read are locator reads, and
/// <c>locator(css, { hasText })</c> is a locator <c>Filter(hasText)</c>. Not ported, as upstream: isVisible with invalid selector
/// should throw (Playwright selector engine names).
/// </summary>
public sealed class LocatorIsVisibleTests : PlaywrightTest
{
    [Test]
    public async Task IsVisible_and_isHidden_should_work()
    {
        await Browser.GotoAsync(Content("<div>Hi</div><span></span>"));

        var div = Browser.Locator("div");
        Equal(true, await div.IsVisibleAsync());
        Equal(false, await div.IsHiddenAsync());

        var span = Browser.Locator("span");
        Equal(false, await span.IsVisibleAsync());
        Equal(true, await span.IsHiddenAsync());

        Equal(false, await Browser.Locator("no-such-element").IsVisibleAsync());
        Equal(true, await Browser.Locator("no-such-element").IsHiddenAsync());
    }

    [Test]
    public async Task IsVisible_should_be_true_for_opacity_0()
    {
        await Browser.GotoAsync(Content("""<div style="opacity:0">Hi</div>"""));
        await Expect.That(Browser.Locator("div")).ToBeVisibleAsync();
    }

    [Test]
    public async Task IsVisible_should_be_true_for_element_outside_view()
    {
        await Browser.GotoAsync(Content("""<div style="position: absolute; left: -1000px">Hi</div>"""));
        await Expect.That(Browser.Locator("div")).ToBeVisibleAsync();
    }

    [Test]
    public async Task IsVisible_and_isHidden_should_work_with_details()
    {
        await Browser.GotoAsync(Content("""
            <details>
              <summary>click to open</summary>
                <ul>
                  <li>hidden item 1</li>
                  <li>hidden item 2</li>
                  <li>hidden item 3</li>
                </ul
            </details>
            """));

        await Expect.That(Browser.Locator("ul")).ToBeHiddenAsync();
    }

    [Test]
    public async Task IsVisible_and_isHidden_should_work_with_nested_details()
    {
        await Browser.GotoAsync(Content("""
            <details>
              <summary>outer</summary>
              <details open>
                <summary>inner</summary>
                <button>hidden button</button>
              </details>
            </details>
            """));

        await Expect.That(Browser.Locator("summary").Filter(hasText: "outer")).ToBeVisibleAsync();
        await Expect.That(Browser.Locator("summary").Filter(hasText: "inner")).ToBeHiddenAsync();
        await Expect.That(Browser.Locator("button")).ToBeHiddenAsync();
        await Expect.That(Screen.GetByRole("button")).ToHaveCountAsync(0);
    }

    [Test]
    public async Task IsVisible_and_isHidden_should_work_with_unslotted_content()
    {
        await Browser.GotoAsync(Content("""
            <div id=unslotted><span>hidden 1</span><div><span>hidden 2</span></div></div>
            <div id=slotted><span>visible</span></div>
            <script>
              unslotted.attachShadow({ mode: 'open' }).innerHTML = 'nothing to see here';
              slotted.attachShadow({ mode: 'open' }).innerHTML = '<slot></slot>';
            </script>
            """));

        await Expect.That(Browser.Locator("span").Filter(hasText: "hidden 1")).ToBeHiddenAsync();
        await Expect.That(Browser.Locator("span").Filter(hasText: "hidden 2")).ToBeHiddenAsync();
        await Expect.That(Browser.Locator("span").Filter(hasText: "visible")).ToBeVisibleAsync();
    }

    [Test]
    public async Task IsVisible_and_isHidden_should_work_with_list_box_options()
    {
        await Browser.GotoAsync(Content("""
            <select multiple>
              <option>visible 1</option>
              <optgroup label="group"><option>visible 2</option></optgroup>
              <option hidden>hidden 1</option>
              <optgroup label="hidden group" style="display:none"><option>hidden 2</option></optgroup>
            </select>
            <select size=3><option>visible 3</option></select>
            <select multiple style="display:none"><option>hidden 3</option></select>
            """));

        foreach (var name in new[] { "visible 1", "visible 2", "visible 3" })
        {
            await Expect.That(Screen.GetByRole("option", name)).ToBeVisibleAsync();
        }

        foreach (var text in new[] { "hidden 1", "hidden 2", "hidden 3" })
        {
            await Expect.That(Browser.Locator("option").Filter(hasText: text)).ToBeHiddenAsync();
        }
    }

    [Test]
    public async Task IsVisible_inside_a_button()
    {
        await Browser.GotoAsync(Content("<button><span></span>a button</button>"));
        var span = Browser.Locator("span");
        Equal(false, await span.IsVisibleAsync());
        Equal(true, await span.IsHiddenAsync());
        await Expect.That(span).Not.ToBeVisibleAsync();
        await Expect.That(span).ToBeHiddenAsync();
        await span.WaitForAsync(new LocatorWaitForOptions { State = WaitForState.Hidden });
        await Browser.Locator("button").WaitForAsync(new LocatorWaitForOptions { State = WaitForState.Visible });
    }

    [Test]
    public async Task IsVisible_inside_a_role_button()
    {
        await Browser.GotoAsync(Content("<div role=button><span></span>a button</div>"));
        var span = Browser.Locator("span");
        Equal(false, await span.IsVisibleAsync());
        Equal(true, await span.IsHiddenAsync());
        await Expect.That(span).Not.ToBeVisibleAsync();
        await Expect.That(span).ToBeHiddenAsync();
        await span.WaitForAsync(new LocatorWaitForOptions { State = WaitForState.Hidden });
        await Browser.Locator("[role=button]").WaitForAsync(new LocatorWaitForOptions { State = WaitForState.Visible });
    }

    [Test]
    public async Task IsVisible_during_navigation_should_not_throw()
    {
        for (var i = 0; i < 20; i++)
        {
            try
            {
                await Browser.GotoAsync(Content("""
                    <script>
                      setTimeout(() => {
                        window.location.href = '/empty.html';
                      }, Math.random(50));
                    </script>
                    """));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Upstream ignores a goto that the navigation interrupts.
            }

            Assert.That(await Browser.Locator("div").IsVisibleAsync(), Is.False);
        }
    }
}
