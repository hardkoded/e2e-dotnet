// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using E2E.Engine;
using E2E.Internal;
using Microsoft.Playwright;

namespace E2E.Tests.SemanticFallback;

/// <summary>
/// Upstream's <c>describe('masked fallback after a stalled semantic reader')</c> (<c>packages/web/tests/integration/semantic-fallback.test.ts</c>,
/// tester-army/e2e at <c>f185ca8</c>). Upstream stalls the page's <c>evaluateHandle</c> with a spy; the port replaces
/// <see cref="WebEngine.ReadFrame"/>, the call the reader runs in each frame.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class MaskedFallbackAfterAStalledSemanticReaderTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Reserves_capture_time_and_takes_fresh_masked_pixels()
    {
        var operation = TimeSpan.FromSeconds(2);
        var stalled = false;
        using var site = await TinySite.StartAsync("<!doctype html><title>Fallback</title>");
        var engine = new WebEngine(new WebEngineOptions { Headless = true, Viewport = new WebViewport(320, 200) })
        {
            ReadFrame = (frame, arguments) => stalled ? new TaskCompletionSource<IJSHandle>().Task : frame.EvaluateHandleAsync(PageScript.Collect, arguments),
        };
        await using var session = await engine.StartAsync(new EngineStartOptions { ActionTimeout = operation }, CancellationToken.None);
        await session.OpenAsync(site.Url, CancellationToken.None);
        var page = WebEngine.SurfaceOf(session)!.Page();
        await page.SetContentAsync("<style>input{position:absolute;left:20px;top:20px;width:100px;height:30px;border:0;padding:0;background:red}</style><input data-testid=\"password\" type=\"password\" value=\"private-value\">");
        await session.ObserveAsync(CancellationToken.None);
        var password = Assert.Single(await session.LocateAsync(LocatorExpression.For("testid", "password", new TextMatchOptions { Exact = true }, "testId"), operation, CancellationToken.None));
        await page.EvaluateAsync("() => { setTimeout(() => { document.body.style.background = 'blue'; }, 1000); }");
        stalled = true;

        var started = Stopwatch.StartNew();
        var fallback = await session.ObserveAsync(new ObserveOptions { Pixels = true, PixelFallback = true }, operation, CancellationToken.None);

        Assert.True(started.Elapsed > TimeSpan.FromMilliseconds(1250));
        Assert.True(started.Elapsed < operation);
        Assert.True(fallback.TreeUnavailable);
        Assert.Empty(fallback.Roots);
        Assert.True(fallback.Pixels!.MaskedRegionCount >= 1);
        var image = ImageOps.DecodePng(fallback.Pixels.Png);
        Assert.Equal([0, 0, 0, 255], PixelAt(image, 40, 30));
        Assert.Equal([0, 0, 255, 255], PixelAt(image, 200, 100));
        var stale = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(password, new LocatorAction.Tap(), CancellationToken.None));
        Assert.Equal(EngineErrorCodes.NodeStale, stale.Code);
    }

    [Fact]
    public async Task Keeps_abandoned_reader_IDs_distinct_from_new_nodes_frames_and_located_refs()
    {
        var delayed = new TaskCompletionSource();
        var spy = false;
        using var site = await TinySite.StartAsync("<!doctype html><title>Ids</title>");
        var engine = new WebEngine(headless: true)
        {
            // The real reader stamped B already; only its protocol reply is delayed.
            ReadFrame = async (frame, arguments) =>
            {
                var captured = await frame.EvaluateHandleAsync(PageScript.Collect, arguments);
                if (spy)
                {
                    await delayed.Task;
                }

                return captured;
            },
        };
        await using var session = await engine.StartAsync(new EngineStartOptions { ActionTimeout = Budget }, CancellationToken.None);
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
        spy = true;
        try
        {
            var fallback = await session.ObserveAsync(new ObserveOptions { PixelFallback = true }, TimeSpan.FromSeconds(2), CancellationToken.None);
            Assert.True(fallback.TreeUnavailable);
        }
        finally
        {
            spy = false;
            delayed.SetResult();
        }

        var located = Assert.Single(await session.LocateAsync(LocatorExpression.For("testid", "a", new TextMatchOptions { Exact = true }, "testId"), Budget, CancellationToken.None));
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
        Assert.DoesNotContain(located.Ref, ids);
        foreach (var name in new[] { "Button B", "Button C", "Button D" })
        {
            var target = Assert.Single(observed, node => node.Name == name);
            await page.Locator("body").EvaluateAsync("body => { delete body.dataset.clicked; }");
            await page.FrameLocator("iframe").Locator("body").EvaluateAsync("body => { delete body.dataset.clicked; }");
            await session.PerformAsync(target, new LocatorAction.Tap(), CancellationToken.None);
            var clicked = name == "Button D"
                ? await page.FrameLocator("iframe").Locator("body").GetAttributeAsync("data-clicked")
                : await page.Locator("body").GetAttributeAsync("data-clicked");
            Assert.Equal(name[^1].ToString(), clicked);
        }
    }

    private static byte[] PixelAt(RgbaImage image, int x, int y)
    {
        var at = ((y * image.Width) + x) * 4;
        return image.Data[at..(at + 4)];
    }

    private static IEnumerable<SemanticNode> Flatten(IEnumerable<SemanticNode> nodes) => nodes.SelectMany(node => (IEnumerable<SemanticNode>)[node, .. Flatten(node.Children)]);
}
