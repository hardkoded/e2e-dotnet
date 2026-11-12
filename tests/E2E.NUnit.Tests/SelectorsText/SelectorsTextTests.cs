// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.SelectorsText;

/// <summary>
/// <c>test.describe('selectors text')</c> of upstream's <c>apps/testbed/tests/selectors-text.e2e.ts</c> (Playwright's
/// <c>selectors-text.spec.ts</c>). <c>locator(css, { hasText })</c> is a locator <c>Filter(hasText)</c>. The 20 other tests are not
/// ported, as upstream: they need Playwright's <c>text=</c> selectors, <c>:text</c> pseudo-classes, <c>&gt;&gt;</c> chains, or element handles.
/// </summary>
public sealed partial class SelectorsTextTests : PlaywrightTest
{
    [Test]
    public async Task Text_is_should_ignore_comments()
    {
        await Browser.GotoAsync(Content("""
            <div id=me>hel<!-- comment -->lo
                <!-- comment -->
                world</div>
            """));
        Assert.That(await Browser.Locator("div").Filter(hasText: "hello world").GetAttributeAsync("id"), Is.EqualTo("me"));
        Assert.That(await Screen.GetByText("hello world").GetAttributeAsync("id"), Is.EqualTo("me"));
    }

    [Test]
    public async Task HasText_and_internal_text_should_match_full_node_text_in_strict_mode()
    {
        await Browser.GotoAsync(Content("""
            <div id=div1>hello<span>world</span></div>
            <div id=div2>hello</div>
            """));
        await Expect.That(Screen.GetByText("helloworld")).ToHaveAttributeAsync("id", "div1");
        await Expect.That(Screen.GetByText("hello")).ToHaveAttributeAsync("id", "div2");
        await Expect.That(Browser.Locator("div").Filter(hasText: HelloWorld())).ToHaveAttributeAsync("id", "div1");
        await Expect.That(Browser.Locator("div").Filter(hasText: Hello())).ToHaveAttributeAsync("id", "div2");

        await Browser.GotoAsync(Content("""
            <div id=div1><span id=span1>hello</span>world</div>
            <div id=div2><span id=span2>hello</span></div>
            """));
        await Expect.That(Screen.GetByText("helloworld")).ToHaveAttributeAsync("id", "div1");
        Assert.That(await Screen.GetByText("hello").Nth(0).GetAttributeAsync("id"), Is.EqualTo("span1"));
        Assert.That(await Screen.GetByText("hello").Nth(1).GetAttributeAsync("id"), Is.EqualTo("span2"));
        await Expect.That(Screen.GetByText("hello")).ToHaveCountAsync(2);
        await Expect.That(Browser.Locator("div").Filter(hasText: HelloWorld())).ToHaveAttributeAsync("id", "div1");
        await Expect.That(Browser.Locator("div").Filter(hasText: Hello())).ToHaveAttributeAsync("id", "div2");
    }

    [GeneratedRegex(@"^helloworld$")]
    private static partial Regex HelloWorld();

    [GeneratedRegex(@"^hello$")]
    private static partial Regex Hello();
}
