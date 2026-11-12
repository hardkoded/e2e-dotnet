// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.LocatorConvenience;

/// <summary>
/// <c>test.describe('locator convenience')</c> of upstream's <c>apps/testbed/tests/locator-convenience.e2e.ts</c> (Playwright's
/// <c>locator-convenience.spec.ts</c>). The <c>page.&lt;read&gt;(selector)</c> twins of each locator read are locator reads, and a
/// <c>:text("...")</c> selector is <c>GetByText</c>. Not ported, as upstream: should have a nice preview, innerHTML should work,
/// innerText should work, innerText should throw, innerText should produce log, isEditable should work, allInnerTexts should work,
/// should return page, the five description tests, and the two toString() tests (the port has none of those reads); textContent should work
/// (<c>TextContentAsync</c> reads rendered text with whitespace normalized on purpose, where Playwright keeps the raw text).
/// </summary>
public sealed class LocatorConvenienceTests : PlaywrightTest
{
    [Test]
    public async Task GetAttribute_should_work()
    {
        await Browser.GotoAsync("/dom.html");
        var locator = Browser.Locator("#outer");
        Assert.That(await locator.GetAttributeAsync("name"), Is.EqualTo("value"));
        Assert.That(await locator.GetAttributeAsync("foo"), Is.Null);
    }

    [Test]
    public async Task InputValue_should_work()
    {
        await Browser.GotoAsync("/dom.html");

        await Browser.Locator("#select").SelectOptionAsync("foo");
        Equal("foo", await Browser.Locator("#select").InputValueAsync());

        await Browser.Locator("#textarea").FillAsync("text value");
        Equal("text value", await Browser.Locator("#textarea").InputValueAsync());

        await Browser.Locator("#input").FillAsync("input value");
        Equal("input value", await Browser.Locator("#input").InputValueAsync());

        Assert.That(await Assert.CatchAsync(() => Browser.Locator("#inner").InputValueAsync()), Is.Not.Null);
    }

    [Test]
    public async Task IsEnabled_and_isDisabled_should_work()
    {
        await Browser.GotoAsync(Content("""
            <button disabled>button1</button>
            <button>button2</button>
            <div>div</div>
            """));
        var div = Browser.Locator("div");
        Assert.That(await div.IsEnabledAsync(), Is.True);
        Assert.That(await div.IsDisabledAsync(), Is.False);
        var button1 = Screen.GetByText("button1", exact: false);
        Assert.That(await button1.IsEnabledAsync(), Is.False);
        Assert.That(await button1.IsDisabledAsync(), Is.True);
        var button2 = Screen.GetByText("button2", exact: false);
        Assert.That(await button2.IsEnabledAsync(), Is.True);
        Assert.That(await button2.IsDisabledAsync(), Is.False);
    }

    [Test]
    public async Task IsChecked_should_work()
    {
        await Browser.GotoAsync(Content("<input type='checkbox' checked><div>Not a checkbox</div>"));
        var element = Browser.Locator("input");
        Equal(true, await element.IsCheckedAsync());
        await Browser.EvaluateAsync<object>("() => { document.querySelector('input').checked = false; }");
        Equal(false, await element.IsCheckedAsync());
        Assert.That(await Assert.CatchAsync(() => Browser.Locator("div").IsCheckedAsync()), Is.Not.Null);
    }

    [Test]
    public async Task IsChecked_should_work_for_indeterminate_input()
    {
        await Browser.GotoAsync(Content("""<input type="checkbox" checked>"""));
        await Browser.EvaluateAsync<object>("() => { document.querySelector('input').indeterminate = true; }");

        Assert.That(await Browser.Locator("input").IsCheckedAsync(), Is.True);
        await Expect.That(Browser.Locator("input")).ToBeCheckedAsync();

        await Browser.Locator("input").UncheckAsync();

        Assert.That(await Browser.Locator("input").IsCheckedAsync(), Is.False);
        await Expect.That(Browser.Locator("input")).Not.ToBeCheckedAsync();
    }

    [Test]
    public async Task AllTextContents_should_work()
    {
        await Browser.GotoAsync(Content("<div>A</div><div>B</div><div>C</div>"));
        Assert.That(await Browser.Locator("div").AllTextContentsAsync(), Is.EqualTo(new[] { "A", "B", "C" }));
    }
}
