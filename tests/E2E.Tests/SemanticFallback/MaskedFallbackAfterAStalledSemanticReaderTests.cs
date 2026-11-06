// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests.SemanticFallback;

/// <summary>
/// Upstream's <c>describe('masked fallback after a stalled semantic reader')</c> (<c>packages/web/tests/integration/semantic-fallback.test.ts</c>,
/// tester-army/e2e at <c>f185ca8</c>). The web engine has no pixel fallback and no stalled-reader seam (upstream's tests stall the
/// page's <c>evaluateHandle</c> with a spy), so <c>reserves capture time and takes fresh masked pixels</c> is not ported (see
/// COMPATIBILITY.md) and the other <c>it</c> is partial.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class MaskedFallbackAfterAStalledSemanticReaderTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Partial: upstream abandons a reader that already stamped Button B, then falls back to pixels, and checks the refs of the next
    /// observation. The port has no abandoned reader and no pixel fallback on the web engine, so this reads the page between observations
    /// instead and checks the ids of the nodes added since, a frame's, and the located one: the ids are distinct and every added node acts.
    /// </summary>
    [Fact]
    public async Task Partial_Keeps_abandoned_reader_IDs_distinct_from_new_nodes_frames_and_located_refs()
    {
        using var site = await TinySite.StartAsync("<!doctype html><title>Ids</title>");
        await using var session = await new WebEngine(headless: true).StartAsync(new EngineStartOptions(), CancellationToken.None);
        await session.OpenAsync(site.Url, CancellationToken.None);
        var page = WebEngine.SurfaceOf(session)!.Page();
        await page.SetContentAsync("<button data-testid=\"a\" onclick=\"document.body.dataset.clicked='A'\">Button A</button>");
        await session.ObserveAsync(CancellationToken.None);
        await page.EvaluateAsync("""
            () => {
              const button = document.createElement('button');
              button.textContent = 'Button B';
              button.addEventListener('click', () => { document.body.dataset.clicked = 'B'; });
              document.body.append(button);
            }
            """);
        await session.ObserveAsync(CancellationToken.None);
        var located = Assert.Single(await session.LocateAsync(LocatorExpression.ForRole("button", "Button A", new RoleOptions()), Budget, CancellationToken.None));
        await page.EvaluateAsync("""
            () => {
              const button = document.createElement('button');
              button.textContent = 'Button C';
              button.addEventListener('click', () => { document.body.dataset.clicked = 'C'; });
              document.body.prepend(button);
              const frame = document.createElement('iframe');
              frame.srcdoc = '<button onclick="document.body.dataset.clicked=\'D\'">Button D</button>';
              document.body.append(frame);
            }
            """);
        await page.FrameLocator("iframe").GetByRole(AriaRole.Button, new() { Name = "Button D" }).WaitForAsync();

        var fresh = await session.ObserveAsync(CancellationToken.None);

        var observed = Flatten(fresh.Roots).ToList();
        var ids = observed.Select(node => node.Ref).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.NotNull(located);
        foreach (var name in new[] { "Button B", "Button C", "Button D" })
        {
            var target = Assert.Single(observed, node => node.Name == name);
            await session.PerformAsync(target, new LocatorAction.Tap(), CancellationToken.None);
            var clicked = name == "Button D"
                ? await page.FrameLocator("iframe").Locator("body").GetAttributeAsync("data-clicked")
                : await page.Locator("body").GetAttributeAsync("data-clicked");
            Assert.Equal(name[^1].ToString(), clicked);
        }
    }

    private static IEnumerable<SemanticNode> Flatten(IEnumerable<SemanticNode> nodes) => nodes.SelectMany(node => (IEnumerable<SemanticNode>)[node, .. Flatten(node.Children)]);
}
