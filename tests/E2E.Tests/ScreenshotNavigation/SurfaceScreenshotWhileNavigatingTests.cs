// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Web;
using E2E.Engine;
using E2E.Internal;
using Microsoft.Playwright;

namespace E2E.Tests.ScreenshotNavigation;

/// <summary>
/// An artifact screenshot taken while the page navigates captures the document showing, masked, instead of failing because the
/// document its masks were resolved in went away (upstream <c>screenshot-navigation.test.ts</c>). Upstream spies on
/// <c>page.screenshot</c>; the port hands the engine's <c>CapturePage</c> seam the same stand-in.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class SurfaceScreenshotWhileNavigatingTests
{
    /// <summary>The race Playwright reports when the document a capture's masks were resolved in is replaced.</summary>
    private const string Race = "page.screenshot: Execution context was destroyed, most likely because of a navigation";

    /// <summary><c>/hop?n=&lt;n&gt;</c> holds a white password field on a white page and moves on to the next hop a moment after it loads, up to 40.</summary>
    private static Task<TinySite> StartHoppingSiteAsync() => TinySite.StartAsync(context =>
    {
        var hop = int.Parse(HttpUtility.ParseQueryString(context.Request.Url!.Query)["n"] ?? "0", CultureInfo.InvariantCulture);
        return TinySite.RespondAsync(context, $$"""
            <!doctype html><title>Hop {{hop}}</title>
            <style>body { margin: 0; background: #fff; } input { position: absolute; left: 20px; top: 20px; width: 120px; height: 40px; border: 0; background: #fff; color: #fff; }</style>
            <input type="password" value="hunter22">
            <script>if ({{hop}} < 40) setTimeout(() => { location.href = '/hop?n={{hop + 1}}'; }, 1);</script>
            """);
    });

    [Fact]
    public Task Captures_each_screenshot_masked_while_the_page_hops_between_documents() => WithSessionAsync(null, TimeSpan.FromSeconds(10), async (session, page, site) =>
    {
        await page.GotoAsync(site.Url + "hop?n=0");
        for (var shot = 0; shot < 10; shot++)
        {
            await ExpectMaskedShotAsync(session, "hop-" + shot);
        }
    });

    [Fact]
    public async Task Retries_the_capture_when_the_navigation_event_arrives_after_the_screenshot_error()
    {
        var captures = 0;
        await WithSessionAsync(async (page, site, options) =>
        {
            if (Interlocked.Increment(ref captures) > 1)
            {
                return await page.ScreenshotAsync(options);
            }

            // Upstream emits framenavigated on a timer, after the rejection. Playwright for .NET cannot emit its events, so the
            // page commits a real navigation a moment after the rejection instead, which delivers the event late the same way.
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                await page.GotoAsync(site.Url + "hop?n=40");
            });
            throw new PlaywrightException(Race);
        }, TimeSpan.FromSeconds(10), async (session, page, site) =>
        {
            await page.GotoAsync(site.Url + "hop?n=40");
            await ExpectMaskedShotAsync(session, "late-event");
        });
        Assert.Equal(2, captures);
    }

    [Fact]
    public async Task Takes_the_capture_again_masked_of_the_document_that_replaced_the_one_its_masks_were_resolved_in()
    {
        var captures = 0;
        await WithSessionAsync(async (page, site, options) =>
        {
            if (Interlocked.Increment(ref captures) > 1)
            {
                return await page.ScreenshotAsync(options);
            }

            await page.GotoAsync(site.Url + "hop?n=40");
            throw new PlaywrightException(Race);
        }, TimeSpan.FromSeconds(10), async (session, page, site) =>
        {
            await page.GotoAsync(site.Url + "hop?n=40");
            await ExpectMaskedShotAsync(session, "replaced");
        });
        Assert.Equal(2, captures);
    }

    [Fact]
    public async Task Fails_with_the_race_itself_when_no_document_was_replaced_tried_once()
    {
        var captures = 0;
        await WithSessionAsync((page, site, options) =>
        {
            Interlocked.Increment(ref captures);
            return Task.FromException<byte[]>(new PlaywrightException(Race));
        }, TimeSpan.FromSeconds(10), async (session, page, site) =>
        {
            await page.GotoAsync(site.Url + "hop?n=40");
            var failure = await Assert.ThrowsAnyAsync<E2EException>(() => session.App.ScreenshotAsync("unreplaced"));
            Assert.Equal("ENGINE_FAILURE", failure.Code);
        });
        Assert.Equal(1, captures);
    }

    [Fact]
    public Task Times_out_when_the_page_replaces_its_document_during_every_capture_until_the_step_deadline() => WithSessionAsync(async (page, site, options) =>
    {
        await page.GotoAsync(site.Url + "hop?n=40");
        throw new PlaywrightException(Race);
    }, TimeSpan.FromSeconds(1), async (session, page, site) =>
    {
        await page.GotoAsync(site.Url + "hop?n=40");
        // Upstream calls the surface; the port goes through App, whose step maps the engine error: it is ACTION_FAILED, the engine's error its cause.
        var mapped = await Assert.ThrowsAsync<TestException>(() => session.App.ScreenshotAsync("restless"));
        Assert.Equal("ACTION_FAILED", mapped.Code);
        var failure = Assert.IsType<EngineException>(mapped.InnerException);
        Assert.Equal("OPERATION_TIMEOUT", failure.Code);
        Assert.False(failure.Retryable);
    });

    [Fact]
    public async Task Keeps_any_other_capture_failure_an_engine_failure_tried_once()
    {
        var captures = 0;
        await WithSessionAsync((page, site, options) =>
        {
            Interlocked.Increment(ref captures);
            return Task.FromException<byte[]>(new PlaywrightException("Target page, context or browser has been closed"));
        }, TimeSpan.FromSeconds(10), async (session, page, site) =>
        {
            await page.GotoAsync(site.Url + "hop?n=40");
            var failure = await Assert.ThrowsAnyAsync<E2EException>(() => session.App.ScreenshotAsync("closed"));
            Assert.Equal("ENGINE_FAILURE", failure.Code);
        });
        Assert.Equal(1, captures);
    }

    /// <summary>Takes a screenshot and asserts the password field is covered by the opaque mask, not left white.</summary>
    private static async Task ExpectMaskedShotAsync(E2ESession session, string label)
    {
        var image = ImageOps.DecodePng(await File.ReadAllBytesAsync(await session.App.ScreenshotAsync(label)));
        Assert.Equal([0, 0, 0, 255], PixelAt(image, 80, 40));
        Assert.Equal([255, 255, 255, 255], PixelAt(image, 200, 150));
    }

    private static byte[] PixelAt(RgbaImage image, int x, int y)
    {
        var at = ((y * image.Width) + x) * 4;
        return image.Data[at..(at + 4)];
    }

    private static async Task WithSessionAsync(
        Func<IPage, TinySite, PageScreenshotOptions, Task<byte[]>>? capture,
        TimeSpan actionTimeout,
        Func<E2ESession, IPage, TinySite, Task> body)
    {
        using var site = await StartHoppingSiteAsync();
        var projectRoot = Directory.CreateTempSubdirectory("e2e-screenshot-navigation-").FullName;
        try
        {
            var session = await E2ESession.StartAsync(new E2ESessionOptions
            {
                Engine = new WebEngine(new WebEngineOptions { Headless = true, Viewport = new WebViewport(320, 200) })
                {
                    CapturePage = (page, options) => capture is null ? page.ScreenshotAsync(options) : capture(page, site, options),
                },
                BaseUrl = site.Url,
                ProjectRoot = projectRoot,
                TestTitle = "surface.screenshot while navigating",
                ActionTimeout = actionTimeout,
                TestTimeout = TimeSpan.FromSeconds(30),
            });
            await WebEngineTests.RunAsync(session, async () =>
            {
                await session.App.OpenAsync("/hop?n=40");
                await body(session, WebEngine.SurfaceOf(session.Engine)!.Page(), site);
            });
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
        }
    }
}
