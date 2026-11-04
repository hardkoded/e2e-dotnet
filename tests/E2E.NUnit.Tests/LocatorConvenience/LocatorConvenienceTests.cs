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
/// should return page, the five description tests, and the two toString() tests (the port has none of those reads).
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

    // Upstream: { fail: '`inputValue()` on an element that is no input reads "" instead of throwing' }.
    [Test]
    public async Task InputValue_should_work()
    {
        await ExpectedToFailAsync("no input", async () =>
        {
            await Browser.GotoAsync("/dom.html");

            await Browser.Locator("#select").SelectOptionAsync("foo");
            Equal("foo", await Browser.Locator("#select").InputValueAsync());

            await Browser.Locator("#textarea").FillAsync("text value");
            Equal("text value", await Browser.Locator("#textarea").InputValueAsync());

            await Browser.Locator("#input").FillAsync("input value");
            Equal("input value", await Browser.Locator("#input").InputValueAsync());

            await ExpectErrorAsync(() => Browser.Locator("#inner").InputValueAsync(), "no input");
        });
    }

    // Upstream: { fail: '`textContent()` normalizes whitespace, where Playwright keeps the raw text' }.
    [Test]
    public async Task TextContent_should_work()
    {
        await ExpectedToFailAsync("more text", async () =>
        {
            await Browser.GotoAsync("/dom.html");
            var locator = Browser.Locator("#inner");
            Equal("Text,\nmore text", await locator.TextContentAsync());
        });
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

    // Upstream: { fail: '`isChecked()` on an element that is no checkbox reads false instead of throwing' }.
    [Test]
    public async Task IsChecked_should_work()
    {
        await ExpectedToFailAsync("no checkbox", async () =>
        {
            await Browser.GotoAsync(Content("<input type='checkbox' checked><div>Not a checkbox</div>"));
            var element = Browser.Locator("input");
            Equal(true, await element.IsCheckedAsync());
            await Browser.EvaluateAsync<object>("() => { document.querySelector('input').checked = false; }");
            Equal(false, await element.IsCheckedAsync());
            await ExpectErrorAsync(() => Browser.Locator("div").IsCheckedAsync(), "no checkbox");
        });
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

    // Upstream's `catch((e) => e)` then `toEqual(expect.any(Error))`. Throws, so a body run by ExpectedToFailAsync can fail with it.
    private static async Task ExpectErrorAsync(Func<Task> action, string what)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected an error for an element that is {what}.");
    }
}
