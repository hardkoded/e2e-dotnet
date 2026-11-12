// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.SelectorsGetBy;

/// <summary>
/// <c>test.describe('selectors get by')</c> of upstream's <c>apps/testbed/tests/selectors-get-by.e2e.ts</c> (Playwright's
/// <c>selectors-get-by.spec.ts</c>). A string query is exact by default, so Playwright's default substring match is
/// <c>exact: false</c> and its <c>exact: true</c> is the default; <c>evaluate(e =&gt; e.outerHTML)</c> reads become text and attribute
/// reads of the match. .NET regexes have no <c>g</c>, <c>y</c>, or <c>v</c> flag, so the tests that vary those flags use a plain
/// pattern. Not ported, as upstream: getByTestId with custom testId should work, getByTestId with comma-separated testIdAttributes
/// should match any, getByAltText should work, getByTitle should work, and the four getByRole with description tests.
/// </summary>
public sealed class SelectorsGetByTests : PlaywrightTest
{
    [Test]
    public async Task GetByTestId_should_work()
    {
        await Browser.GotoAsync(Content("""<div><div data-testid="Hello">Hello world</div></div>"""));
        await Expect.That(Screen.GetByTestId("Hello")).ToHaveTextAsync("Hello world");
        await Expect.That(Browser.Locator("div").GetByTestId("Hello")).ToHaveTextAsync("Hello world");
    }

    [Test]
    public async Task GetByTestId_should_escape_id()
    {
        await Browser.GotoAsync(Content("""<div><div data-testid='He"llo'>Hello world</div></div>"""));
        await Expect.That(Screen.GetByTestId("He\"llo")).ToHaveTextAsync("Hello world");
    }

    [Test]
    public async Task GetByTestId_should_work_for_regex()
    {
        await Browser.GotoAsync(Content("""<div><div data-testid="Hello">Hello world</div></div>"""));
        await Expect.That(Screen.GetByTestId(new Regex("He[l]*o"))).ToHaveTextAsync("Hello world");
        await Expect.That(Browser.Locator("div").GetByTestId("Hello")).ToHaveTextAsync("Hello world");
    }

    [Test]
    public async Task GetByText_should_work()
    {
        await Browser.GotoAsync(Content("<div>yo</div><div>ya</div><div>\nye  </div>"));
        Assert.That(await Screen.GetByText("ye", exact: false).TextContentAsync(), Is.EqualTo("ye"));
        Assert.That(await Screen.GetByText(new Regex("ye")).TextContentAsync(), Is.EqualTo("ye"));
        Assert.That(await Screen.GetByText(new Regex("e")).TextContentAsync(), Is.EqualTo("ye"));

        await Browser.GotoAsync(Content("<div> ye </div><div>ye</div>"));
        Assert.That(await Screen.GetByText("ye").First().BoundingBoxAsync(), Is.EqualTo(await Browser.Locator("div").First().BoundingBoxAsync()));

        await Browser.GotoAsync(Content("<div>Hello world</div><div>Hello</div>"));
        Assert.That(await Screen.GetByText("Hello").TextContentAsync(), Is.EqualTo("Hello"));
    }

    [Test]
    public async Task GetByLabel_should_work()
    {
        await Browser.GotoAsync(Content("<div><label for=target>Name</label><input id=target type=text></div>"));
        Equal("target", await Screen.GetByText("Name", exact: false).GetAttributeAsync("for"));
        Equal("target", await Screen.GetByLabel("Name", exact: false).GetAttributeAsync("id"));
        Equal("target", await Browser.Locator("div").GetByLabel("Name", exact: false).GetAttributeAsync("id"));
    }

    [Test]
    public async Task GetByLabel_should_work_with_nested_elements()
    {
        await Browser.GotoAsync(Content("<label for=target>Last <span>Name</span></label><input id=target type=text>"));

        await Expect.That(Screen.GetByLabel("last name", exact: false)).ToHaveAttributeAsync("id", "target");
        await Expect.That(Screen.GetByLabel("st na", exact: false)).ToHaveAttributeAsync("id", "target");
        await Expect.That(Screen.GetByLabel("Name", exact: false)).ToHaveAttributeAsync("id", "target");
        await Expect.That(Screen.GetByLabel("Last Name")).ToHaveAttributeAsync("id", "target");
        await Expect.That(Screen.GetByLabel(new Regex(@"Last\s+name", RegexOptions.IgnoreCase))).ToHaveAttributeAsync("id", "target");

        Assert.That(await Screen.GetByLabel("Last").CountAsync(), Is.EqualTo(0));
        Assert.That(await Screen.GetByLabel("last name").CountAsync(), Is.EqualTo(0));
        Assert.That(await Screen.GetByLabel("Name").CountAsync(), Is.EqualTo(0));
        Assert.That(await Screen.GetByLabel("what?", exact: false).CountAsync(), Is.EqualTo(0));
        Assert.That(await Screen.GetByLabel(new Regex("last name")).CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task GetByLabel_should_work_with_multiply_labelled_input()
    {
        await Browser.GotoAsync(Content("<label for=target>Name</label><input id=target type=text><label for=target>First or Last</label>"));
        Assert.That(await Screen.GetByLabel("Name", exact: false).GetAttributeAsync("id"), Is.EqualTo("target"));
        Assert.That(await Screen.GetByLabel("First or Last", exact: false).GetAttributeAsync("id"), Is.EqualTo("target"));
    }

    [Test]
    public async Task GetByLabel_should_work_with_ancestor_label_and_multiple_controls()
    {
        await Browser.GotoAsync(Content("<label>Name<button id=target>Click me</button><input type=text></label>"));
        Assert.That(await Screen.GetByLabel("Name", exact: false).GetAttributeAsync("id"), Is.EqualTo("target"));
    }

    [Test]
    public async Task GetByLabel_should_work_with_ancestor_label_and_for()
    {
        await Browser.GotoAsync(Content("""
            <label for=target>Name<input type=text id=nontarget></label>
            <input type=text id=target>
            """));
        Assert.That(await Screen.GetByLabel("Name", exact: false).GetAttributeAsync("id"), Is.EqualTo("target"));
    }

    [Test]
    public async Task GetByLabel_should_work_with_aria_labelledby()
    {
        await Browser.GotoAsync(Content("<label id=name-label>Name</label><button aria-labelledby=name-label>Click me</button>"));
        Assert.That(await Screen.GetByLabel("Name", exact: false).TextContentAsync(), Is.EqualTo("Click me"));
    }

    [Test]
    public async Task GetByLabel_should_prioritize_aria_labelledby_over_native_label()
    {
        await Browser.GotoAsync(Content("""
            <label id=name-label>Name</label>
            <label>Wrong<button aria-labelledby=name-label>Click me</button></label>
            """));
        Assert.That(await Screen.GetByLabel("Name", exact: false).TextContentAsync(), Is.EqualTo("Click me"));
    }

    [Test]
    public async Task GetByLabel_should_work_with_aria_label()
    {
        await Browser.GotoAsync(Content("""<input id=target aria-label="Name">"""));
        Assert.That(await Screen.GetByLabel("Name", exact: false).GetAttributeAsync("id"), Is.EqualTo("target"));
    }

    [Test]
    public async Task GetByLabel_should_ignore_empty_aria_label()
    {
        await Browser.GotoAsync(Content("<label for=target>Last Name</label><input id=target type=text aria-label>"));
        Assert.That(await Screen.GetByLabel("Last Name", exact: false).GetAttributeAsync("id"), Is.EqualTo("target"));
    }

    [Test]
    public async Task GetByLabel_should_prioritize_aria_labelledby_over_aria_label()
    {
        await Browser.GotoAsync(Content("""<label id=other-label>Other</label><input id=target aria-label="Name" aria-labelledby=other-label>"""));
        Assert.That(await Screen.GetByLabel("Other", exact: false).GetAttributeAsync("id"), Is.EqualTo("target"));
    }

    [Test]
    public async Task GetByPlaceholder_should_work()
    {
        await Browser.GotoAsync(Content("""
            <div>
              <input placeholder='Hello'>
              <input placeholder='Hello World'>
            </div>
            """));
        await Expect.That(Screen.GetByPlaceholder("hello", exact: false)).ToHaveCountAsync(2);
        await Expect.That(Screen.GetByPlaceholder("Hello")).ToHaveCountAsync(1);
        await Expect.That(Screen.GetByPlaceholder(new Regex("wor", RegexOptions.IgnoreCase))).ToHaveCountAsync(1);

        await Expect.That(Browser.Locator("div").GetByPlaceholder("hello", exact: false)).ToHaveCountAsync(2);
    }

    [Test]
    public async Task GetBy_escaping()
    {
        const string SetAttributes = "(value) => { const input = document.querySelector('input'); for (const name of ['placeholder', 'title', 'alt']) input.setAttribute(name, value); }";

        await Browser.GotoAsync(Content("<label id=label for=control>Hello my\nwo\"rld</label><input id=control />"));
        await Browser.EvaluateAsync<object>(SetAttributes, "hello my\nwo\"rld");
        await Expect.Soft(Screen.GetByText("hello my\nwo\"rld", exact: false)).ToHaveAttributeAsync("id", "label");
        await Expect.Soft(Screen.GetByText("hello       my     wo\"rld", exact: false)).ToHaveAttributeAsync("id", "label");
        await Expect.Soft(Screen.GetByLabel("hello my\nwo\"rld", exact: false)).ToHaveAttributeAsync("id", "control");
        await Expect.Soft(Screen.GetByPlaceholder("hello my\nwo\"rld", exact: false)).ToHaveAttributeAsync("id", "control");

        await Browser.GotoAsync(Content("<label id=label for=control>Hello my\nworld</label><input id=control />"));
        await Browser.EvaluateAsync<object>(SetAttributes, "hello my\nworld");
        await Expect.Soft(Screen.GetByText("hello my\nworld", exact: false)).ToHaveAttributeAsync("id", "label");
        await Expect.Soft(Screen.GetByText("hello        my    world", exact: false)).ToHaveAttributeAsync("id", "label");
        await Expect.Soft(Screen.GetByLabel("hello my\nworld", exact: false)).ToHaveAttributeAsync("id", "control");
        await Expect.Soft(Screen.GetByPlaceholder("hello my\nworld", exact: false)).ToHaveAttributeAsync("id", "control");

        await Browser.GotoAsync(Content("<label for=target>foo &gt;&gt; bar</label><input id=target>"));
        await Browser.EvaluateAsync<object>(SetAttributes, "foo >> bar");
        Assert.That(await Screen.GetByText("foo >> bar", exact: false).TextContentAsync(), Is.EqualTo("foo >> bar"));
        await Expect.Soft(Browser.Locator("label")).ToHaveTextAsync("foo >> bar");
        await Expect.Soft(Screen.GetByText("foo >> bar", exact: false)).ToHaveTextAsync("foo >> bar");
        Assert.That(await Screen.GetByText(new Regex("foo >> bar")).TextContentAsync(), Is.EqualTo("foo >> bar"));
        await Expect.Soft(Screen.GetByLabel("foo >> bar", exact: false)).ToHaveAttributeAsync("id", "target");
        await Expect.Soft(Screen.GetByLabel(new Regex("foo >> bar"))).ToHaveAttributeAsync("id", "target");
        await Expect.Soft(Screen.GetByPlaceholder("foo >> bar", exact: false)).ToHaveAttributeAsync("id", "target");
        await Expect.Soft(Screen.GetByPlaceholder(new Regex("foo >> bar"))).ToHaveAttributeAsync("id", "target");
    }

    [Test]
    public async Task GetByRole_escaping()
    {
        await Browser.GotoAsync(Content("""
            <a href="https://playwright.dev">issues 123</a>
            <a href="https://playwright.dev">he llo 56</a>
            <button>Click me</button>
            """));
        Assert.That(await Screen.GetByRole("button").AllTextContentsAsync(), Is.EqualTo(new[] { "Click me" }));
        Assert.That(await Screen.GetByRole("link").AllTextContentsAsync(), Is.EqualTo(new[] { "issues 123", "he llo 56" }));

        Assert.That(await Screen.GetByRole("link", "issues 123", exact: false).AllTextContentsAsync(), Is.EqualTo(new[] { "issues 123" }));
        Assert.That(await Screen.GetByRole("link", "sues", exact: false).AllTextContentsAsync(), Is.EqualTo(new[] { "issues 123" }));
        Assert.That(await Screen.GetByRole("link", "  he    \n  llo ", exact: false).AllTextContentsAsync(), Is.EqualTo(new[] { "he llo 56" }));
        Assert.That(await Screen.GetByRole("button", "issues", exact: false).AllTextContentsAsync(), Is.Empty);

        Assert.That(await Screen.GetByRole("link", "sues").AllTextContentsAsync(), Is.Empty);
        Assert.That(await Screen.GetByRole("link", "   he \n llo 56 ").AllTextContentsAsync(), Is.EqualTo(new[] { "he llo 56" }));

        Assert.That(await Screen.GetByRole("button", "Click me").AllTextContentsAsync(), Is.EqualTo(new[] { "Click me" }));
        Assert.That(await Screen.GetByRole("button", @"Click \me").AllTextContentsAsync(), Is.Empty);
        Assert.That(await Screen.GetByRole("button", @"Click \\me").AllTextContentsAsync(), Is.Empty);
    }

    [Test]
    public async Task GetByRole_should_accept_regexp_with_v_flag()
    {
        await Browser.GotoAsync(Content("<button>Click me</button><button>Submit</button>"));
        await Expect.That(Screen.GetByRole("button", new Regex("Click me"))).ToHaveCountAsync(1);
        await Expect.That(Screen.GetByRole("button", new Regex("click me", RegexOptions.IgnoreCase))).ToHaveCountAsync(1);
        await Expect.That(Screen.GetByRole("button", new Regex("Missing"))).ToHaveCountAsync(0, TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Locators_should_not_be_affected_by_global_and_sticky_regex_flags()
    {
        await Browser.GotoAsync(Content("""
            <div>foo</div><div>foo</div><div>foo</div>
            <label>foo<input></label><label>foo<input></label><label>foo<input></label>
            <button>foo</button><button>foo</button><button>foo</button>
            <input placeholder=foo><input placeholder=foo><input placeholder=foo>
            """));
        var foo = new Regex("foo");
        await Expect.That(Screen.GetByText(foo)).ToHaveCountAsync(9);
        await Expect.That(Screen.GetByText(foo)).ToHaveCountAsync(9);
        await Expect.That(Screen.GetByText(foo)).ToHaveCountAsync(9);
        await Expect.That(Screen.GetByLabel(foo)).ToHaveCountAsync(3);
        await Expect.That(Screen.GetByLabel(foo)).ToHaveCountAsync(3);
        await Expect.That(Screen.GetByLabel(foo)).ToHaveCountAsync(3);
        await Expect.That(Screen.GetByRole("button", foo)).ToHaveCountAsync(3);
        await Expect.That(Screen.GetByRole("button", foo)).ToHaveCountAsync(3);
        await Expect.That(Screen.GetByRole("button", foo)).ToHaveCountAsync(3);
        await Expect.That(Screen.GetByPlaceholder(foo)).ToHaveCountAsync(3);
        await Expect.That(Screen.GetByPlaceholder(foo)).ToHaveCountAsync(3);
        await Expect.That(Screen.GetByPlaceholder(foo)).ToHaveCountAsync(3);
    }
}
