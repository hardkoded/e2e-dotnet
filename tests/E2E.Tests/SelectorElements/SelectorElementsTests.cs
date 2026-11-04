// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.SelectorElements;

/// <summary>
/// .NET-only: upstream has no <c>it</c> for a <c>selector</c> that names an element the page script does not list. Upstream resolves
/// every selector with Playwright and reads each match through its <c>e2e-read</c> engine, so any element is a node; the port lists
/// those elements in a second observation, so they are nodes with refs of that observation.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class SelectorElementsTests
{
    [Theory]
    [InlineData("body", 1)]
    [InlineData("html", 1)]
    [InlineData(":root", 1)]
    [InlineData("head", 1)]
    [InlineData("title", 1)]
    [InlineData("#outer, .inner", 2)]
    [InlineData("#outer", 1)]
    [InlineData("section.card", 1)]
    [InlineData("a", 1)]
    [InlineData("main#empty", 1)]
    [InlineData("button i.glyph", 1)]
    [InlineData("#shadow-menu i.in-shadow", 1)]
    [InlineData("#absent", 0)]
    public async Task Counts_every_element_a_selector_names(string selector, int expected)
    {
        await RunAsync(async session => Assert.Equal(expected, await session.Browser.Locator(selector).CountAsync()));
    }

    [Fact]
    public async Task Asserts_the_visibility_of_body_and_of_a_wrapper()
    {
        await RunAsync(async session =>
        {
            await Expect.That(session.Browser.Locator("body")).ToBeVisibleAsync();
            await Expect.That(session.Browser.Locator("section.card")).ToBeVisibleAsync();
            await Expect.That(session.Browser.Locator("head")).ToBeVisibleAsync(visible: false);
        });
    }

    [Fact]
    public async Task Scopes_a_query_to_the_children_of_an_element_the_tree_did_not_list()
    {
        await RunAsync(async session =>
        {
            Assert.Equal(1, await session.Browser.Locator("#outer").GetByRole("link", "Open about").CountAsync());
            Assert.Equal(0, await session.Browser.Locator("main#empty").GetByRole("link").CountAsync());
            Assert.Equal(1, await session.Browser.Locator("div.inner").Filter(hasText: "Open about").CountAsync());
        });
    }

    [Fact]
    public async Task Reads_an_attribute_of_an_unlisted_element_and_acts_on_a_listed_one_after_it()
    {
        await RunAsync(async session =>
        {
            Assert.Equal("card", await session.Browser.Locator("section.card").GetAttributeAsync("class"));
            await session.Browser.Locator("a").TapAsync();
            await Expect.That(session.Browser).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/about$"));
        });
    }

    [Fact]
    public async Task Taps_an_unlisted_element()
    {
        await RunAsync(async session =>
        {
            await session.Browser.Locator("#pad").TapAsync();
            await Expect.That(session.Browser).ToHaveTitleAsync("Pad clicked");
        });
    }

    [Fact]
    public async Task Reaches_an_unlisted_element_inside_a_frame()
    {
        await RunAsync(async session =>
        {
            Assert.Equal(1, await session.Browser.FrameLocator("#box").Locator("#in-frame").CountAsync());
            Assert.Equal(1, await session.Browser.FrameLocator("#box").Locator("#in-frame").GetByRole("link", "Inside link").CountAsync());
            var wrapper = session.Browser.Locator("div.inner");
            await Expect.That(wrapper).ToBeVisibleAsync();
            await wrapper.TapAsync();
        });
    }

    private static async Task RunAsync(Func<E2ESession, Task> body)
    {
        using var site = await WebPlatform.FixtureApp.StartAsync();
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(headless: true),
            Model = new ScriptedModel(_ => ModelResponses.Done("passed", "unused")),
            BaseUrl = site.Url,
            TestTitle = "selector elements",
            ActionTimeout = TimeSpan.FromSeconds(5),
            AssertionTimeout = TimeSpan.FromSeconds(4),
            TestTimeout = TimeSpan.FromSeconds(30),
        });
        await session.App.OpenAsync("/wrappers");
        await Expect.That(session.Screen.GetByRole("link", "Open about")).ToBeVisibleAsync();
        await body(session);
    }
}
