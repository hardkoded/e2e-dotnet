// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests.NestedFrames;

/// <summary>
/// Upstream's <c>describe('nested frame locators')</c> of <c>packages/web/tests/integration/nested-frames.test.ts</c>: a <c>frameLocator</c>
/// chain resolves each frame inside the one before it, and a located node's box is in the top-level viewport. The last three tests are
/// .NET-only.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class NestedFrameLocatorsTests
{
    private const string DeepDocument = "<button>Deep button</button>";
    private const string TwinFrame = "<iframe class=\"twin\" title=\"twin\" srcdoc=\"<p>twin</p>\"></iframe>";

    private static readonly string InnerDocument =
        "<button onclick=\"this.textContent = 'Inner clicked'\">Inner button</button>" +
        $"<iframe id=\"deep\" title=\"deep\" srcdoc=\"{Attribute(DeepDocument)}\"></iframe>";

    private static readonly string OuterDocument =
        $"<h2>Outer</h2><iframe id=\"inner\" title=\"inner\" style=\"border:7px solid;padding:5px\" srcdoc=\"{Attribute(InnerDocument)}\"></iframe>" +
        TwinFrame +
        TwinFrame;

    private static readonly string HostDocument =
        "<h1>Host</h1><iframe id=\"aside\" title=\"aside\" srcdoc=\"<p>aside</p>\"></iframe>" +
        TwinFrame +
        $"<iframe id=\"outer\" title=\"outer\" srcdoc=\"{Attribute(OuterDocument)}\"></iframe>";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static readonly LocatorExpression InnerButton = ButtonNamed("Inner button");

    private static readonly LocatorExpression DeepButton = ButtonNamed("Deep button");

    [Fact]
    public async Task Resolves_a_two_level_chain_and_taps_inside_the_innermost_frame()
    {
        await using var host = await HostAsync();

        var matches = await host.Session.LocateAsync(WithinFrames(["#outer", "#inner"], InnerButton), Timeout, CancellationToken.None);
        Assert.Equal(["Inner button"], matches.Select(node => node.Name));
        await host.Session.PerformAsync(matches[0], new LocatorAction.Tap(), CancellationToken.None);

        Assert.Equal("Inner clicked", await host.Page.FrameLocator("#outer").FrameLocator("#inner").GetByRole(AriaRole.Button).InnerTextAsync());
    }

    [Fact]
    public async Task Reports_a_located_nodes_box_in_the_top_level_viewport_past_each_frames_border_and_padding()
    {
        await using var host = await HostAsync();

        // The inner document's one button, whatever an earlier test renamed it to.
        var button = new LocatorExpression { Kind = "role", Role = "button" };
        var node = (await host.Session.LocateAsync(WithinFrames(["#outer", "#inner"], button), Timeout, CancellationToken.None))[0];

        // Playwright's own box for the same element, measured through both frames' borders and padding.
        var expected = (await host.Page.FrameLocator("#outer").FrameLocator("#inner").GetByRole(AriaRole.Button).BoundingBoxAsync())!;
        Assert.Equal(expected.X, node.Rect!.X, 3);
        Assert.Equal(expected.Y, node.Rect.Y, 3);
        Assert.Equal(expected.Width, node.Rect.Width, 3);
        Assert.Equal(expected.Height, node.Rect.Height, 3);
    }

    [Fact]
    public async Task Resolves_a_three_level_chain_counting_the_last_frame_inside_the_second()
    {
        await using var host = await HostAsync();

        var matches = await host.Session.LocateAsync(WithinFrames(["#outer", "#inner", "#deep"], DeepButton), Timeout, CancellationToken.None);
        Assert.Equal(["Deep button"], matches.Select(node => node.Name));

        var missing = await Assert.ThrowsAsync<EngineException>(() => host.Session.LocateAsync(WithinFrames(["#outer", "#deep"], DeepButton), Timeout, CancellationToken.None));
        Assert.Equal("FRAME_NOT_FOUND", missing.Code);
        Assert.Equal("no frame matches #deep", missing.Message);
    }

    [Fact]
    public async Task Names_the_inner_selector_when_the_outer_frame_holds_no_such_frame_even_though_the_page_does()
    {
        await using var host = await HostAsync();

        var error = await Assert.ThrowsAsync<EngineException>(() => host.Session.LocateAsync(WithinFrames(["#outer", "#aside"], InnerButton), Timeout, CancellationToken.None));

        Assert.Equal("FRAME_NOT_FOUND", error.Code);
        Assert.Equal("no frame matches #aside", error.Message);
        Assert.True(error.Retryable);
    }

    [Fact]
    public async Task Reports_two_matching_frames_inside_the_outer_document_as_ambiguous_though_the_page_holds_one()
    {
        await using var host = await HostAsync();

        Assert.Equal(1, await host.Page.Locator(".twin").CountAsync());
        var error = await Assert.ThrowsAsync<EngineException>(() => host.Session.LocateAsync(WithinFrames(["#outer", ".twin"], InnerButton), Timeout, CancellationToken.None));

        Assert.Equal("FRAME_AMBIGUOUS", error.Code);
        Assert.Equal("2 frames match .twin", error.Message);
        Assert.False(error.Retryable);
    }

    // .NET-only: upstream's locator-frame and selectors-frame testbed tests cover this page; the port has no testbed here.
    [Fact]
    public async Task A_frame_scoped_role_query_stops_at_a_nested_frame()
    {
        await using var session = await WebSemanticsTests.OpenAsync("about:blank", new WebEngineOptions { Headless = true });
        var page = WebEngine.SurfaceOf(session)!.Page();
        var nested = "<button>Hello nested iframe</button>";
        var outer = $"<button>Hello iframe</button><iframe srcdoc=\"{Attribute(nested)}\"></iframe>";
        await page.SetContentAsync($"<iframe srcdoc=\"{Attribute(outer)}\"></iframe>");
        await page.FrameLocator("iframe").FrameLocator("iframe").GetByRole(AriaRole.Button).WaitForAsync();

        var inFrame = new LocatorExpression { Kind = "frame", Selector = "iframe", Source = new LocatorExpression { Kind = "role", Role = "button" } };
        var nodes = await session.LocateAsync(inFrame, TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.Equal(["Hello iframe"], nodes.Select(node => node.Name));
    }

    // .NET-only, as the test above: upstream's text engine and hasText filter read one document, so the frame's text is not the box's.
    [Fact]
    public async Task A_text_query_keeps_an_outer_match_that_a_nested_frame_repeats()
    {
        await using var session = await WebSemanticsTests.OpenAsync("about:blank", new WebEngineOptions { Headless = true });
        var page = WebEngine.SurfaceOf(session)!.Page();
        await page.SetContentAsync($"<div>Greeting<iframe srcdoc=\"{Attribute("<p>Greeting</p>")}\"></iframe></div>");
        await page.FrameLocator("iframe").GetByText("Greeting").WaitForAsync();

        var nodes = await session.LocateAsync(new LocatorExpression { Kind = "text", Value = "Greeting" }, TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.Equal(2, nodes.Count);
    }

    [Fact]
    public async Task A_has_text_filter_does_not_read_the_text_of_a_nested_frame()
    {
        await using var session = await WebSemanticsTests.OpenAsync("about:blank", new WebEngineOptions { Headless = true });
        var page = WebEngine.SurfaceOf(session)!.Page();
        await page.SetContentAsync($"<div data-testid=\"box\"><iframe srcdoc=\"{Attribute("<p>Nested only</p>")}\"></iframe></div>");
        await page.FrameLocator("iframe").GetByText("Nested only").WaitForAsync();

        var box = new LocatorExpression { Kind = "testid", Value = "box" };
        var withText = await session.LocateAsync(new LocatorExpression { Kind = "filter", Source = box, HasText = "Nested only" }, TimeSpan.FromSeconds(10), CancellationToken.None);
        var withoutText = await session.LocateAsync(new LocatorExpression { Kind = "filter", Source = box, HasNotText = "Nested only" }, TimeSpan.FromSeconds(10), CancellationToken.None);

        Assert.Empty(withText);
        Assert.Single(withoutText);
    }

    private sealed class Host(IEngineSession session, IPage page) : IAsyncDisposable
    {
        public IEngineSession Session { get; } = session;

        public IPage Page { get; } = page;

        public ValueTask DisposeAsync() => Session.DisposeAsync();
    }

    private static async Task<Host> HostAsync()
    {
        var session = await WebSemanticsTests.OpenAsync("about:blank", new WebEngineOptions { Headless = true });
        try
        {
            var page = WebEngine.SurfaceOf(session)!.Page();
            await page.SetContentAsync(HostDocument);
            await page.FrameLocator("#outer").FrameLocator("#inner").FrameLocator("#deep").GetByRole(AriaRole.Button, new() { Name = "Deep button" }).WaitForAsync();
            return new Host(session, page);
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    /// <summary>Wraps <paramref name="source"/> in <c>frame</c> scopes, outermost first, as a <c>browser.frameLocator</c> chain does.</summary>
    private static LocatorExpression WithinFrames(IReadOnlyList<string> selectors, LocatorExpression source)
    {
        for (var i = selectors.Count - 1; i >= 0; i--)
        {
            source = new LocatorExpression { Kind = "frame", Selector = selectors[i], Source = source };
        }

        return source;
    }

    /// <summary>A role query for the one button named <paramref name="name"/>.</summary>
    private static LocatorExpression ButtonNamed(string name) => new() { Kind = "role", Role = "button", Value = name };

    /// <summary>Escapes HTML for a double-quoted attribute value, so a document can carry a nested <c>srcdoc</c>.</summary>
    private static string Attribute(string html) => html.Replace("&", "&amp;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal);
}
