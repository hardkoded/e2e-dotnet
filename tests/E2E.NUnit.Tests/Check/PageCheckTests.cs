// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.Check;

/// <summary>
/// <c>test.describe('page check')</c> of upstream's <c>apps/testbed/tests/check.e2e.ts</c> (Playwright's <c>page-check.spec.ts</c>).
/// Not ported, as upstream: should check the label with position (no <c>position</c>), trial run should not check and trial run
/// should not uncheck (no <c>trial</c>), should check the box using setChecked (no <c>setChecked</c>).
/// </summary>
public sealed class PageCheckTests : PlaywrightTest
{
    private static readonly string[] AriaRoles = ["checkbox", "menuitemcheckbox", "option", "radio", "switch", "menuitemradio", "treeitem"];

    [Test]
    public async Task Should_check_the_box()
    {
        await Browser.GotoAsync(Content("<input id='checkbox' type='checkbox'></input>"));
        await Browser.Locator("input").CheckAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("() => window.checkbox.checked"), Is.True);
    }

    [Test]
    public async Task Should_not_check_the_checked_box()
    {
        await Browser.GotoAsync(Content("<input id='checkbox' type='checkbox' checked></input>"));
        await Browser.Locator("input").CheckAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("() => window.checkbox.checked"), Is.True);
    }

    [Test]
    public async Task Should_uncheck_the_box()
    {
        await Browser.GotoAsync(Content("<input id='checkbox' type='checkbox' checked></input>"));
        await Browser.Locator("input").UncheckAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("() => window.checkbox.checked"), Is.False);
    }

    [Test]
    public async Task Should_not_uncheck_the_unchecked_box()
    {
        await Browser.GotoAsync(Content("<input id='checkbox' type='checkbox'></input>"));
        await Browser.Locator("input").UncheckAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("() => window.checkbox.checked"), Is.False);
    }

    [Test]
    public async Task Should_check_radio()
    {
        await Browser.GotoAsync(Content("""
            <input type='radio'>one</input>
            <input id='two' type='radio'>two</input>
            <input type='radio'>three</input>
            """));
        await Browser.Locator("#two").CheckAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("() => window.two.checked"), Is.True);
    }

    [Test]
    public async Task Should_check_radio_by_aria_role()
    {
        await Browser.GotoAsync(Content("""
            <div role='radio' id='checkbox'>CHECKBOX</div>
            <script>
              checkbox.addEventListener('click', () => checkbox.setAttribute('aria-checked', 'true'));
            </script>
            """));
        await Browser.Locator("div").CheckAsync();
        Assert.That(await Browser.EvaluateAsync<string>("() => window.checkbox.getAttribute('aria-checked')"), Is.EqualTo("true"));
    }

    [Test]
    public async Task Should_uncheck_radio_by_aria_role()
    {
        await Browser.GotoAsync(Content("""
            <div role='radio' id='checkbox' aria-checked="true">CHECKBOX</div>
            <script>
              checkbox.addEventListener('click', () => checkbox.setAttribute('aria-checked', 'false'));
            </script>
            """));
        await Browser.Locator("div").UncheckAsync();
        Equal("false", await Browser.EvaluateAsync<string>("() => window.checkbox.getAttribute('aria-checked')"));
    }

    [Test]
    public async Task Should_check_the_box_by_aria_role()
    {
        foreach (var role in AriaRoles)
        {
            await Browser.GotoAsync(Content($$"""
                <div role='{{role}}' id='checkbox'>CHECKBOX</div>
                <script>
                  checkbox.addEventListener('click', () => checkbox.setAttribute('aria-checked', 'true'));
                </script>
                """));
            await Browser.Locator("div").CheckAsync();
            Assert.That(await Browser.EvaluateAsync<string>("() => window.checkbox.getAttribute('aria-checked')"), Is.EqualTo("true"));
        }
    }

    [Test]
    public async Task Should_uncheck_the_box_by_aria_role()
    {
        foreach (var role in AriaRoles)
        {
            await Browser.GotoAsync(Content($$"""
                <div role='{{role}}' id='checkbox' aria-checked="true">CHECKBOX</div>
                <script>
                  checkbox.addEventListener('click', () => checkbox.setAttribute('aria-checked', 'false'));
                </script>
                """));
            await Browser.Locator("div").UncheckAsync();
            Equal("false", await Browser.EvaluateAsync<string>("() => window.checkbox.getAttribute('aria-checked')"));
        }
    }

    [Test]
    public async Task Should_throw_when_not_a_checkbox()
    {
        await Browser.GotoAsync(Content("<div>Check me</div>"));
        var error = await Assert.CatchAsync(() => Browser.Locator("div").CheckAsync());
        Assert.That(error!.Message, Does.Contain("Not a checkbox or radio button"));
    }

    [Test]
    public async Task Should_throw_when_not_a_checkbox_2()
    {
        await Browser.GotoAsync(Content("<div role=button>Check me</div>"));
        var error = await Assert.CatchAsync(() => Browser.Locator("div").CheckAsync());
        Assert.That(error!.Message, Does.Contain("Not a checkbox or radio button"));
    }

    [Test]
    public async Task Should_check_the_box_inside_a_button()
    {
        await Browser.GotoAsync(Content("<div role='button'><input type='checkbox'></div>"));
        await Browser.Locator("input").CheckAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("() => document.querySelector('input').checked"), Is.True);
        Assert.That(await Browser.Locator("input").IsCheckedAsync(), Is.True);
    }

    [Test]
    public async Task Should_throw_when_trying_to_uncheck_radio_button()
    {
        await Browser.GotoAsync(Content("<input type='radio' name='test' checked id='radio'>"));
        var error = await Assert.CatchAsync(() => Browser.Locator("#radio").UncheckAsync());
        Assert.That(error!.Message, Does.Contain("uncheck cannot clear a radio button"));
    }
}
