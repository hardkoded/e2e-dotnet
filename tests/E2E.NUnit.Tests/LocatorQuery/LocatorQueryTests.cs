// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.LocatorQuery;

/// <summary>
/// <c>test.describe('locator query')</c> of upstream's <c>apps/testbed/tests/locator-query.e2e.ts</c> (Playwright's
/// <c>locator-query.spec.ts</c>). <c>locator(css, { hasText, has })</c> is a locator <c>Filter</c>, <c>div &gt;&gt; p</c> the CSS
/// <c>div p</c>, <c>toHaveClass</c> the browser matcher, and <c>evaluate(e =&gt; e.outerHTML)</c> a text read. JavaScript regex flags
/// <c>u</c> map to nothing in .NET, so those patterns carry <see cref="RegexOptions.IgnoreCase"/> only. Not ported, as upstream:
/// should throw on capture w/ nth(), should throw on due to strictness 2, should support locator.and, should support locator.locator
/// with and/or, should support locator.within, should keep the capture when removing a common frame prefix, and count() should not
/// throw during navigation.
/// </summary>
public sealed class LocatorQueryTests : PlaywrightTest
{
    private const string Sections = """
          <section>
            <div><p>A</p></div>
            <div><p>A</p><p>A</p></div>
            <div><p>A</p><p>A</p><p>A</p></div>
          </section>
        """;

    [Test]
    public async Task Should_respect_first_and_last_smoke()
    {
        await Browser.GotoAsync(Content(Sections));
        Assert.That(await Browser.Locator("div p").CountAsync(), Is.EqualTo(6));
        Assert.That(await Browser.Locator("div").First().GetByText("A").CountAsync(), Is.EqualTo(1));
        Assert.That(await Browser.Locator("div").Last().GetByText("A").CountAsync(), Is.EqualTo(3));
    }

    [Test]
    public async Task Should_respect_nth()
    {
        await Browser.GotoAsync(Content(Sections));
        Assert.That(await Browser.Locator("div p").Nth(0).CountAsync(), Is.EqualTo(1));
        Assert.That(await Browser.Locator("div").Nth(1).GetByText("A").CountAsync(), Is.EqualTo(2));
        Assert.That(await Browser.Locator("div").Nth(2).GetByText("A").CountAsync(), Is.EqualTo(3));
    }

    [Test]
    public async Task Should_throw_on_due_to_strictness()
    {
        await Browser.GotoAsync(Content("<div>A</div><div>B</div>"));
        Assert.That(await Assert.CatchAsync(() => Browser.Locator("div").IsVisibleAsync()), Is.Not.Null);
    }

    [Test]
    public async Task Should_filter_by_text()
    {
        await Browser.GotoAsync(Content("<div>Foobar</div><div>Bar</div>"));
        await Expect.That(Browser.Locator("div").Filter(hasText: "Foo")).ToHaveTextAsync("Foobar");
    }

    [Test]
    public async Task Should_filter_by_text_2()
    {
        await Browser.GotoAsync(Content("<div>foo <span>hello world</span> bar</div>"));
        await Expect.That(Browser.Locator("div").Filter(hasText: "hello world")).ToHaveTextAsync("foo hello world bar");
    }

    [Test]
    public async Task Should_filter_by_regex()
    {
        await Browser.GotoAsync(Content("<div>Foobar</div><div>Bar</div>"));
        await Expect.That(Browser.Locator("div").Filter(hasText: new Regex("Foo.*"))).ToHaveTextAsync("Foobar");
    }

    [Test]
    public async Task Should_filter_by_text_with_quotes()
    {
        await Browser.GotoAsync(Content("""<div>Hello "world"</div><div>Hello world</div>"""));
        await Expect.That(Browser.Locator("div").Filter(hasText: "Hello \"world\"")).ToHaveTextAsync("Hello \"world\"");
    }

    [Test]
    public async Task Should_filter_by_regex_with_quotes()
    {
        await Browser.GotoAsync(Content("""<div>Hello "world"</div><div>Hello world</div>"""));
        await Expect.That(Browser.Locator("div").Filter(hasText: new Regex("Hello \"world\""))).ToHaveTextAsync("Hello \"world\"");
    }

    // Known difference, hardkoded/e2e-dotnet#1073: getByText finds no text inside a button. Ignored, not run behind ExpectedToFailAsync,
    // because its Expect.Soft failures end the test, as upstream's expect.soft.
    [Test]
    [Ignore("hardkoded/e2e-dotnet#1073")]
    public async Task Should_filter_by_regex_with_a_single_quote()
    {
        await Browser.GotoAsync(Content("<button>let's let's<span>hello</span></button>"));
        foreach (var pattern in new[] { "let's", "let's", "'s", "'s", "let['abc]s", "let's let's", "let's let's" })
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            await Expect.Soft(Browser.Locator("button").Filter(hasText: regex).GetByText("hello")).ToHaveTextAsync("hello");
            await Expect.Soft(Screen.GetByRole("button", regex).GetByText("hello")).ToHaveTextAsync("hello");
        }

        var backslash = new Regex(@"let\\'s", RegexOptions.IgnoreCase);
        await Expect.Soft(Browser.Locator("button").Filter(hasText: backslash)).Not.ToBeVisibleAsync();
        await Expect.Soft(Screen.GetByRole("button", backslash)).Not.ToBeVisibleAsync();

        await Browser.GotoAsync(Content(@"<button>let\'s let\'s<span>hello</span></button>"));
        var quote = new Regex("let's", RegexOptions.IgnoreCase);
        await Expect.Soft(Browser.Locator("button").Filter(hasText: quote)).Not.ToBeVisibleAsync();
        await Expect.Soft(Screen.GetByRole("button", quote)).Not.ToBeVisibleAsync();
        foreach (var pattern in new[] { @"let\\'s", @"let\\'s", @"let\\'s let\\'s", @"let\\'s let\\'s" })
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            await Expect.Soft(Browser.Locator("button").Filter(hasText: regex).GetByText("hello")).ToHaveTextAsync("hello");
            await Expect.Soft(Screen.GetByRole("button", regex).GetByText("hello")).ToHaveTextAsync("hello");
        }

        await Browser.GotoAsync(Content("<button>let's hello</button>"));
        await Expect.Soft(Browser.Locator("button").Filter(hasText: quote)).ToHaveTextAsync("let's hello");
        await Expect.Soft(Screen.GetByRole("button", quote)).ToHaveTextAsync("let's hello");
    }

    [Test]
    public async Task Should_filter_by_regex_and_regexp_flags()
    {
        await Browser.GotoAsync(Content("""<div>Hello "world"</div><div>Hello world</div>"""));
        await Expect.That(Browser.Locator("div").Filter(hasText: new Regex("hElLo \"world\"", RegexOptions.IgnoreCase))).ToHaveTextAsync("Hello \"world\"");
    }

    [Test]
    public async Task Should_filter_by_case_insensitive_regex_in_a_child()
    {
        await Browser.GotoAsync(Content("""<div class="test"><h5>Title Text</h5></div>"""));
        await Expect.That(Browser.Locator("div").Filter(hasText: new Regex("^title text$", RegexOptions.IgnoreCase))).ToHaveTextAsync("Title Text");
    }

    [Test]
    public async Task Should_filter_by_case_insensitive_regex_in_multiple_children()
    {
        await Browser.GotoAsync(Content("""<div class="test"><h5>Title</h5> <h2><i>Text</i></h2></div>"""));
        await Expect.That(Browser).ToHaveClassAsync(Browser.Locator("div").Filter(hasText: new Regex("^title text$", RegexOptions.IgnoreCase)), "test");
    }

    // Known difference, hardkoded/e2e-dotnet#1074: the text of a div joins two adjacent block elements with a space.
    [Test]
    public async Task Should_filter_by_regex_with_special_symbols()
    {
        await ExpectedToFailAsync("expect.toHaveClass failed", async () =>
        {
            await Browser.GotoAsync(Content(@"<div class=""test""><h5>First/""and""</h5><h2><i>Second\</i></h2></div>"));
            var pattern = new Regex(@"^first/"".*""second\\$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            await Expect.That(Browser).ToHaveClassAsync(Browser.Locator("div").Filter(hasText: pattern), "test");
        });
    }

    [Test]
    public async Task Should_support_has_locator()
    {
        await Browser.GotoAsync(Content("<div><span>hello</span></div><div><span>world</span></div>"));
        await Expect.That(Browser.Locator("div").Filter(has: Screen.GetByText("world", exact: false))).ToHaveCountAsync(1);
        Assert.That(await Browser.Locator("div").Filter(has: Screen.GetByText("world", exact: false)).TextContentAsync(), Is.EqualTo("world"));
        await Expect.That(Browser.Locator("div").Filter(has: Screen.GetByText("hello"))).ToHaveCountAsync(1);
        Assert.That(await Browser.Locator("div").Filter(has: Screen.GetByText("hello")).TextContentAsync(), Is.EqualTo("hello"));
        await Expect.That(Browser.Locator("div").Filter(has: Browser.Locator("span"))).ToHaveCountAsync(2);
        await Expect.That(Browser.Locator("div").Filter(has: Browser.Locator("span").Filter(hasText: "wor"))).ToHaveCountAsync(1);
        Assert.That(await Browser.Locator("div").Filter(has: Browser.Locator("span").Filter(hasText: "wor")).TextContentAsync(), Is.EqualTo("world"));
        await Expect.That(Browser.Locator("div").Filter(has: Browser.Locator("span"), hasText: "wor")).ToHaveCountAsync(1);
    }

    [Test]
    public async Task Should_support_locator_filter()
    {
        await Browser.GotoAsync(Content("<section><div><span>hello</span></div><div><span>world</span></div></section>"));
        await Expect.That(Browser.Locator("div").Filter(hasText: "hello")).ToHaveCountAsync(1);
        await Expect.That(Browser.Locator("div").Filter(hasText: "hello").Filter(hasText: "hello")).ToHaveCountAsync(1);
        await Expect.That(Browser.Locator("div").Filter(hasText: "hello").Filter(hasText: "world")).ToHaveCountAsync(0);
        await Expect.That(Browser.Locator("section").Filter(hasText: "hello").Filter(hasText: "world")).ToHaveCountAsync(1);
        await Expect.That(Browser.Locator("div").Filter(hasText: "hello").GetByText("hello")).ToHaveCountAsync(1);
        await Expect.That(Browser.Locator("div").Filter(has: Browser.Locator("span").Filter(hasText: "world"))).ToHaveCountAsync(1);
        await Expect.That(Browser.Locator("div").Filter(has: Browser.Locator("span"))).ToHaveCountAsync(2);
        await Expect.That(Browser.Locator("div").Filter(has: Browser.Locator("span"), hasText: "world")).ToHaveCountAsync(1);
        await Expect.That(Browser.Locator("div").Filter(hasNot: Browser.Locator("span").Filter(hasText: "world"))).ToHaveCountAsync(1);
        await Expect.That(Browser.Locator("div").Filter(hasNot: Browser.Locator("section"))).ToHaveCountAsync(2);
        await Expect.That(Browser.Locator("div").Filter(hasNot: Browser.Locator("span"))).ToHaveCountAsync(0);
        await Expect.That(Browser.Locator("div").Filter(hasNotText: "hello")).ToHaveCountAsync(1);
        await Expect.That(Browser.Locator("div").Filter(hasNotText: "foo")).ToHaveCountAsync(2);
    }

    [Test]
    public async Task Should_support_locator_or()
    {
        await Browser.GotoAsync(Content("<div>hello</div><span>world</span>"));
        await Expect.That(Browser.Locator("div").Or(Browser.Locator("span"))).ToHaveCountAsync(2);
        await Expect.That(Browser.Locator("div").Or(Browser.Locator("span"))).ToHaveTextAsync(new TextMatch[] { "hello", "world" });
        await Expect.That(Browser.Locator("span").Or(Browser.Locator("article")).Or(Browser.Locator("div"))).ToHaveTextAsync(new TextMatch[] { "hello", "world" });
        await Expect.That(Browser.Locator("article").Or(Browser.Locator("something"))).ToHaveCountAsync(0);
        await Expect.That(Browser.Locator("article").Or(Browser.Locator("div"))).ToHaveTextAsync("hello");
        await Expect.That(Browser.Locator("article").Or(Browser.Locator("span"))).ToHaveTextAsync("world");
        await Expect.That(Browser.Locator("div").Or(Browser.Locator("article"))).ToHaveTextAsync("hello");
        await Expect.That(Browser.Locator("span").Or(Browser.Locator("article"))).ToHaveTextAsync("world");
    }

    [Test]
    public async Task Should_allow_some_but_not_all_nested_frameLocators()
    {
        await Browser.GotoAsync(Content("""<iframe srcdoc="<span id=target>world</span>"></iframe><span>hello</span>"""));
        var frame = Browser.FrameLocator("iframe");
        await Expect.That(frame.Locator("span").Or(frame.Locator("article"))).ToHaveTextAsync("world");
        await Expect.That(frame.Locator("article").Or(frame.Locator("span"))).ToHaveTextAsync("world");
        var error = await Assert.CatchAsync(() => Expect.That(frame.Locator("div").Or(Browser.FrameLocator("#iframe").Locator("span"))).ToHaveTextAsync("world"));
        Assert.That(error, Is.Not.Null);
    }

    [Test]
    public async Task Should_enforce_same_frame_for_has_leftOf_rightOf_above_below_near()
    {
        await Browser.GotoAsync("/frames/two-frames.html");
        var child = Browser.FrameLocator("iframe[name=uno]");
        var error = await Assert.CatchAsync(() => Browser.Locator("div").Filter(has: child.Locator("span")).CountAsync());
        Assert.That(error, Is.Not.Null);
    }

    [Test]
    public async Task Alias_methods_coverage()
    {
        await Browser.GotoAsync(Content("<div><button>Submit</button></div>"));
        await Expect.That(Browser.Locator("button")).ToHaveCountAsync(1);
        await Expect.That(Browser.Locator("div").GetByRole("button")).ToHaveCountAsync(1);
        await Expect.That(Screen.GetByRole("button")).ToHaveCountAsync(1);
    }
}
