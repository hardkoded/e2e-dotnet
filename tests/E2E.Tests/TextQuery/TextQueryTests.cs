// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E;
using E2E.Engine;

namespace E2E.Tests.TextQuery;

/// <summary>
/// .NET-only. Upstream resolves <c>getByText</c>, <c>getByLabel</c>, and <c>hasText</c> through Playwright on the real DOM; the port
/// resolves them over its tree, so these tests pin the DOM text, the labels, and the inline text nodes the tree carries for them.
/// Upstream's own tests of this behavior are <c>selectors-get-by</c> and <c>locator-query</c>.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class TextQueryTests
{
    [Fact]
    public Task A_text_query_answers_with_the_label_not_the_control_it_names() => RunAsync(
        """<div><label for="target">Name</label><input id="target" type="text"></div>""",
        async screen =>
        {
            Assert.Equal("target", await screen.GetByText("Name").GetAttributeAsync("for"));
            Assert.Equal("target", await screen.GetByLabel("Name").GetAttributeAsync("id"));
        });

    [Fact]
    public Task A_text_query_does_not_match_a_placeholder_a_title_or_an_aria_label() => RunAsync(
        """<input placeholder="foo"><input title="foo"><button aria-label="foo">x</button><div>foo</div>""",
        async screen => Assert.Equal(1, await screen.GetByText("foo").CountAsync()));

    [Fact]
    public Task A_text_query_matches_the_value_of_a_button_input() => RunAsync(
        """<input type="submit" value="Send"><input type="text" value="Send">""",
        async screen => Assert.Equal(1, await screen.GetByText("Send").CountAsync()));

    [Fact]
    public Task A_label_query_finds_a_control_by_its_label_not_by_its_content() => RunAsync(
        """<button>foo</button><label>foo<input></label><input placeholder="foo">""",
        async screen => Assert.Equal(1, await screen.GetByLabel("foo").CountAsync()));

    [Fact]
    public Task A_text_query_finds_the_text_of_an_inline_element_inside_a_button() => RunAsync(
        "<button>let's let's<span>hello</span></button>",
        async screen =>
        {
            Assert.Equal(1, await screen.GetByText("hello").CountAsync());
            Assert.Equal(1, await screen.GetByRole("button").GetByText("hello").CountAsync());
        });

    [Fact]
    public Task Has_text_reads_two_adjacent_blocks_with_no_space_between_them() => RunAsync(
        """<div data-testid="box"><h5>First/"and"</h5><h2><i>Second\</i></h2></div>""",
        async screen =>
        {
            var box = screen.GetByTestId("box");
            Assert.Equal(1, await box.Filter(new Regex(@"^First/"".*""Second\\$")).CountAsync());
            Assert.Equal(1, await box.Filter("First/\"and\"Second\\").CountAsync());
        });

    private static async Task RunAsync(string body, Func<Screen, Task> test)
    {
        using var site = await TinySite.StartAsync($"<!DOCTYPE html><html><body>{body}</body></html>");
        await using var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(headless: true),
            Model = new ScriptedModel(_ => throw new InvalidOperationException("no model call expected")),
            BaseUrl = site.Url,
            TestTitle = "text query",
            ProjectRoot = Directory.CreateTempSubdirectory("e2e-text-query-").FullName,
            ActionTimeout = TimeSpan.FromSeconds(5),
            AssertionTimeout = TimeSpan.FromSeconds(5),
        });
        await session.App.OpenAsync("/");
        await test(session.Screen);
        session.Complete(null);
    }
}
