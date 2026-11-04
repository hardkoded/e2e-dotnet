// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.PageScriptGlobals;

/// <summary>
/// .NET-only: the page script reads nothing from the page's own globals, so a page that removes one still observes and acts. Upstream's
/// testbed test ('should click the button if window.Node is removed' in <c>click.e2e.ts</c>) checks it through a tap.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class PageScriptGlobalsTests
{
    [Fact]
    public async Task NET_only_observes_a_page_that_deleted_window_Node()
    {
        await using var session = await new WebEngine(headless: true).StartAsync(new EngineStartOptions(), CancellationToken.None);
        await session.OpenAsync("about:blank", CancellationToken.None);
        var page = WebEngine.SurfaceOf(session)!.Page();
        await page.SetContentAsync("<button onclick=\"document.body.dataset.clicked = 'yes'\">Press</button><p>Some text</p>");
        await page.EvaluateAsync("() => { delete window.Node; }");

        var observation = await session.ObserveAsync(CancellationToken.None);

        var button = Flatten(observation.Roots).First(node => node.Role == "button");
        await session.PerformAsync(button, new LocatorAction.Tap(), CancellationToken.None);
        Assert.Equal("yes", await page.EvaluateAsync<string>("() => document.body.dataset.clicked"));
        Assert.Contains(Flatten(observation.Roots), node => node.Text == "Some text");
    }

    [Fact]
    public async Task NET_only_lists_the_text_of_a_label_whose_control_has_no_box()
    {
        await using var session = await new WebEngine(headless: true).StartAsync(new EngineStartOptions(), CancellationToken.None);
        await session.OpenAsync("about:blank", CancellationToken.None);
        var page = WebEngine.SurfaceOf(session)!.Page();
        await page.SetContentAsync("<label>Click me <input onclick=\"window.__clicked=true\" style=\"width:0;height:0;padding:0;margin:0;border:0;\"></label>");

        var observation = await session.ObserveAsync(CancellationToken.None);

        var text = Assert.Single(Flatten(observation.Roots), node => node.Text == "Click me" && node.Role is null);
        Assert.True(text.Rect is { Width: > 0, Height: > 0 });
    }

    private static IEnumerable<SemanticNode> Flatten(IEnumerable<SemanticNode> nodes) => nodes.SelectMany(node => (IEnumerable<SemanticNode>)[node, .. Flatten(node.Children)]);
}
