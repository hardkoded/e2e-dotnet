// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests.FramePath;

/// <summary>
/// .NET-only: no upstream <c>it</c> reads <c>framePath</c> off an observed web node. Upstream's <c>toSemanticNode</c> stamps the chain of
/// enclosing iframe selectors on every node a nested document reads (packages/web/src/observation.ts), and the port's web engine does the same
/// (<c>WebEngine.CollectAsync</c>). This pins the chain, outermost first, and that the main document carries none.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class FramePathTests : IAsyncLifetime
{
    private const string Document = """
        <button>Top</button>
        <iframe name="ad-slot" srcdoc="<button>Inside</button><iframe id='inner' srcdoc='<button>Nested</button>'></iframe>"></iframe>
        """;

    private IEngineSession _session = null!;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        _session = await new WebEngine(headless: true).StartAsync(new EngineStartOptions(), CancellationToken.None);
        await _session.OpenAsync("about:blank", CancellationToken.None);
        _page = WebEngine.SurfaceOf(_session)!.Page();
        await _page.SetContentAsync(Document);
        await _page.FrameLocator("[name=ad-slot]").FrameLocator("#inner").GetByRole(AriaRole.Button).WaitForAsync(new() { State = WaitForSelectorState.Attached });
    }

    public async Task DisposeAsync() => await _session.DisposeAsync();

    [Fact]
    public async Task Every_node_of_a_nested_document_carries_the_iframe_selectors_enclosing_it_outermost_first()
    {
        var observation = await _session.ObserveAsync(CancellationToken.None);
        var nodes = Flatten(observation.Roots).ToList();

        var inside = nodes.Single(node => node.Role == "button" && node.Name == "Inside");
        Assert.Equal(["[name=\"ad-slot\"]"], inside.FramePath);

        var nested = nodes.Single(node => node.Role == "button" && node.Name == "Nested");
        Assert.NotNull(nested.FramePath);
        Assert.Equal(2, nested.FramePath.Count);
        Assert.Equal("[name=\"ad-slot\"]", nested.FramePath[0]);
        Assert.Contains("iframe:nth-child(", nested.FramePath[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_main_document_carries_no_frame_path()
    {
        var observation = await _session.ObserveAsync(CancellationToken.None);
        var top = Flatten(observation.Roots).Single(node => node.Role == "button" && node.Name == "Top");

        Assert.Null(top.FramePath);
    }

    private static IEnumerable<SemanticNode> Flatten(IEnumerable<SemanticNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var descendant in Flatten(node.Children))
            {
                yield return descendant;
            }
        }
    }
}
