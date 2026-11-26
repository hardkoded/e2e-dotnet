// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Playwright;
using Intersection = E2E.Engine.ViewportIntersection;

namespace E2E.Tests.ViewportIntersection;

/// <summary>
/// Upstream's <c>describe('viewport intersection on the web engine')</c> (<c>packages/web/tests/integration/viewport-intersection.test.ts</c>,
/// tester-army/e2e#2011): <c>ViewportIntersectionAsync</c> measures what Playwright's <c>toBeInViewport</c> does, the share of an element's box
/// inside the viewport once the viewport and every clipping ancestor, a scroll container or a frame included, cut it.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class ViewportIntersectionOnTheWebEngineTests : IAsyncLifetime
{
    private const string Box = "position: absolute; width: 100px; height: 100px";

    private static readonly string Document = """
        <style>body { margin: 0; height: 3000px }</style>
        <div id="inside" style="BOX; left: 10px; top: 10px"></div>
        <div id="half-off" style="BOX; left: -50px; top: 200px"></div>
        <div id="below" style="BOX; left: 10px; top: 2000px"></div>
        <div style="BOX; left: 300px; top: 10px; overflow: hidden">
          <div id="half-clipped" style="BOX; left: 50px"></div>
          <div id="clipped-out" style="BOX; top: 200px"></div>
        </div>
        <div style="BOX; left: 500px; top: 10px; overflow: auto">
          <div style="height: 1000px"></div>
          <div id="scrolled-away" style="height: 50px"></div>
        </div>
        <div id="empty" style="position: absolute; left: 10px; top: 400px; width: 0; height: 10px"></div>
        <div id="flat" style="position: absolute; left: 10px; top: 420px; width: 10px; height: 0"></div>
        <div id="no-box" style="display: none"></div>
        <input id="secret" type="password" value="hunter2" style="position: absolute; left: 10px; top: 500px">
        <iframe id="frame" style="BOX; left: 700px; top: 10px; border: 0"
          srcdoc="<div id='framed' style='BOX; left: 50px; top: 0'></div>"></iframe>
        """.Replace("BOX", Box, StringComparison.Ordinal);

    private static readonly OperationContext Operation = OperationContext.Test with
    {
        Timeout = TimeSpan.FromSeconds(5),
    };

    private IEngineSession _session = null!;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        _session = await new WebEngine(headless: true).StartAsync(new EngineStartOptions(), CancellationToken.None);
        await _session.OpenAsync("about:blank", CancellationToken.None);
        _page = WebEngine.SurfaceOf(_session)!.Page();
        await _page.SetContentAsync(Document);
        await _page
            .FrameLocator("#frame")
            .Locator("#framed")
            .WaitForAsync(new() { State = WaitForSelectorState.Attached });
    }

    public async Task DisposeAsync() => await _session.DisposeAsync();

    // The one element `selector` names, inside `frame` when given.
    private async Task<SemanticNode> LocateAsync(string selector, string? frame = null)
    {
        var source = new LocatorExpression { Kind = "selector", Selector = selector };
        var expression = frame is null
            ? source
            : new LocatorExpression
            {
                Kind = "frame",
                Selector = frame,
                Source = source,
            };
        return Assert.Single(
            await _session.LocateAsync(expression, TimeSpan.FromSeconds(5), Operation, CancellationToken.None)
        );
    }

    private async Task<Intersection> MeasureAsync(string selector, string? frame = null) =>
        await _session.ViewportIntersectionAsync(await LocateAsync(selector, frame), Operation, CancellationToken.None);

    [Fact]
    public async Task Measures_the_share_of_the_box_the_viewport_and_clipping_ancestors_leave()
    {
        Assert.Equal(new Intersection(true, 1), await MeasureAsync("#inside"));
        Assert.Equal(new Intersection(true, 0.5), await MeasureAsync("#half-off"));
        Assert.Equal(new Intersection(false, 0), await MeasureAsync("#below"));
        Assert.Equal(new Intersection(true, 0.5), await MeasureAsync("#half-clipped"));
        Assert.Equal(new Intersection(false, 0), await MeasureAsync("#clipped-out"));
        Assert.Equal(new Intersection(false, 0), await MeasureAsync("#scrolled-away"));
        Assert.Equal(new Intersection(true, 0.5), await MeasureAsync("#framed", "#frame"));
    }

    [Fact]
    public async Task Reads_an_element_with_no_box_or_an_empty_one_as_outside()
    {
        Assert.Equal(new Intersection(false, 0), await MeasureAsync("#empty"));
        Assert.Equal(new Intersection(false, 0), await MeasureAsync("#flat"));
        Assert.Equal(new Intersection(false, 0), await MeasureAsync("#no-box"));
    }

    [Fact]
    public async Task Measures_a_password_field_like_any_other_element()
    {
        Assert.Equal(new Intersection(true, 1), await MeasureAsync("#secret"));
    }

    [Fact]
    public async Task Follows_scrolling_and_a_removed_element_is_stale()
    {
        var below = await LocateAsync("#below");
        await _page.EvaluateAsync("window.scrollTo(0, 1950)");
        Assert.Equal(
            new Intersection(true, 1),
            await _session.ViewportIntersectionAsync(below, Operation, CancellationToken.None)
        );
        await _page.EvaluateAsync("window.scrollTo(0, 0)");

        await _page.Locator("#below").EvaluateAsync("element => element.remove()");
        var stale = await Assert.ThrowsAsync<EngineException>(() =>
            _session.ViewportIntersectionAsync(below, Operation, CancellationToken.None)
        );
        Assert.Equal(EngineErrorCodes.NodeStale, stale.Code);
    }

    [Fact]
    public async Task Reads_an_element_the_page_replaces_every_frame_as_stale_never_as_outside()
    {
        await _page.EvaluateAsync(
            """
            () => {
              const make = () => {
                const ticker = document.createElement('button');
                ticker.id = 'ticker';
                ticker.textContent = 'Ticker';
                ticker.style.cssText = 'position: fixed; left: 10px; top: 600px';
                return ticker;
              };
              document.body.append(make());
              const swap = () => {
                document.getElementById('ticker')?.replaceWith(make());
                requestAnimationFrame(swap);
              };
              requestAnimationFrame(swap);
            }
            """
        );
        for (var round = 0; round < 5; round++)
        {
            var ticker = await LocateAsync("#ticker");
            var stale = await Assert.ThrowsAsync<EngineException>(() =>
                _session.ViewportIntersectionAsync(ticker, Operation, CancellationToken.None)
            );
            Assert.Equal(EngineErrorCodes.NodeStale, stale.Code);
        }
    }
}
