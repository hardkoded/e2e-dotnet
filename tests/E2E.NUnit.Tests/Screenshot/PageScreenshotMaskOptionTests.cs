// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.Screenshot;

/// <summary>
/// <c>test.describe('mask option')</c>, inside <c>page screenshot</c>, of upstream's <c>apps/testbed/tests/screenshot.e2e.ts</c>
/// (Playwright's <c>page-screenshot.spec.ts</c>). A <c>mask</c> is the <c>ToHaveScreenshotAsync</c> option, compared against the same
/// viewport with boxes of the mask color painted over the masked elements. Not ported, as upstream: mask option should work with
/// elementhandle (element handles), and should hide and remove elements based on attr (no <c>style</c> option).
/// All nine tests are ignored: <c>ToHaveScreenshotAsync</c> cannot resolve the CSS-selector and frame locators of <c>Browser</c> as masks (#1109).
/// </summary>
[Ignore("ToHaveScreenshotAsync cannot mask a CSS-selector or frame locator of Browser: #1109")]
public sealed class PageScreenshotMaskOptionTests : ScreenshotTest
{
    [Test]
    public async Task Should_work()
    {
        await Browser.SetViewportAsync(500, 500);
        await Browser.GotoAsync("/grid.html");
        await StoreMaskedReferenceAsync("mask-should-work", [new MaskTarget("div", 5)]);
        await Expect.That(Screen).ToHaveScreenshotAsync("mask-should-work", new ScreenshotOptions { Mask = [Browser.Locator("div").Nth(5)] });
    }

    [Test]
    public async Task Should_work_with_locator()
    {
        await Browser.SetViewportAsync(500, 500);
        await Browser.GotoAsync("/grid.html");
        await StoreMaskedReferenceAsync("mask-should-work-with-locator", [new MaskTarget("div", 5)]);
        var bodyLocator = Browser.Locator("body");
        await Expect.That(bodyLocator).ToHaveScreenshotAsync("mask-should-work-with-locator", new ScreenshotOptions { Mask = [Browser.Locator("div").Nth(5)] });
    }

    [Test]
    public async Task Should_mask_multiple_elements()
    {
        await Browser.SetViewportAsync(500, 500);
        await Browser.GotoAsync("/grid.html");
        await StoreMaskedReferenceAsync("should-mask-multiple-elements", [new MaskTarget("div", 5), new MaskTarget("div", 12)]);
        await Expect.That(Screen).ToHaveScreenshotAsync("should-mask-multiple-elements", new ScreenshotOptions
        {
            Mask = [Browser.Locator("div").Nth(5), Browser.Locator("div").Nth(12)],
        });
    }

    [Test]
    public async Task Should_mask_inside_iframe()
    {
        await Browser.SetViewportAsync(500, 500);
        await Browser.GotoAsync("/grid.html");
        await AttachFrameAsync("frame1", $"{PREFIX}/grid.html");
        await AddStyleTagAsync("iframe { border: none; }");
        await StoreMaskedReferenceAsync("should-mask-inside-iframe", [new MaskTarget("div", 5), new MaskTarget("div", 12, Frame: "#frame1")]);
        await Expect.That(Screen).ToHaveScreenshotAsync("should-mask-inside-iframe", new ScreenshotOptions
        {
            Mask = [Browser.Locator("div").Nth(5), Browser.FrameLocator("#frame1").Locator("div").Nth(12)],
        });
    }

    [Test]
    public async Task Should_mask_in_parallel()
    {
        await Browser.SetViewportAsync(500, 500);
        await Browser.GotoAsync("/empty.html");
        await AttachFrameAsync("frame1", $"{PREFIX}/grid.html");
        await AttachFrameAsync("frame2", $"{PREFIX}/grid.html");
        await AddStyleTagAsync("iframe { border: none; }");
        await StoreMaskedReferenceAsync("should-mask-in-parallel-1", [new MaskTarget("div", 1, Frame: "#frame1")]);
        await StoreMaskedReferenceAsync("should-mask-in-parallel-2", [new MaskTarget("div", 3, Frame: "#frame2")]);
        var results = new[]
        {
            Record(() => Expect.That(Screen).ToHaveScreenshotAsync("should-mask-in-parallel-1", new ScreenshotOptions
            {
                Mask = [Browser.FrameLocator("#frame1").Locator("div").Nth(1)],
            })),
            Record(() => Expect.That(Screen).ToHaveScreenshotAsync("should-mask-in-parallel-2", new ScreenshotOptions
            {
                Mask = [Browser.FrameLocator("#frame2").Locator("div").Nth(3)],
            })),
        };
        foreach (var result in await Task.WhenAll(results))
        {
            if (result is not null)
            {
                throw result;
            }
        }
    }

    [Test]
    public async Task Should_remove_mask_after_screenshot()
    {
        await Browser.SetViewportAsync(500, 500);
        await Browser.GotoAsync("/grid.html");
        var screenshot1 = await File.ReadAllBytesAsync(await App.ScreenshotAsync());
        await StoreMaskedReferenceAsync("should-remove-mask-after-screenshot", [new MaskTarget("div", 1)]);
        await Expect.That(Screen).ToHaveScreenshotAsync("should-remove-mask-after-screenshot", new ScreenshotOptions { Mask = [Browser.Locator("div").Nth(1)] });
        var screenshot2 = await File.ReadAllBytesAsync(await App.ScreenshotAsync());
        Assert.That(screenshot1.AsSpan().SequenceEqual(screenshot2), Is.True);
    }

    [Test]
    public async Task Should_work_when_subframe_has_stalled_navigation()
    {
        var routed = Deferred();
        var released = Deferred();
        await Browser.RouteAsync("**/subframe.html", async route =>
        {
            routed.TrySetResult();
            await released.Task;
            await route.FulfillAsync(new RouteFulfillResponse { Body = "" });
        });

        await Browser.GotoAsync(Content("<iframe></iframe>"));
        await StoreReferenceAsync("mask-stalled-subframe");
        await Browser.GotoAsync(Content("<iframe src='/subframe.html'></iframe>"), new GotoOptions { WaitUntil = GotoWaitUntil.DomContentLoaded });
        await routed.Task;

        await Expect.That(Screen).ToHaveScreenshotAsync("mask-stalled-subframe", new ScreenshotOptions { Mask = [Browser.Locator("non-existent")] });
        released.TrySetResult();
        await Expect.That(Browser.FrameLocator("iframe").Locator("body")).ToBeAttachedAsync();
    }

    [Test]
    public async Task Should_work_when_subframe_used_document_open_after_a_weird_url()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.EvaluateAsync<object>("""
            () => {
              const iframe = document.createElement('iframe');
              iframe.src = 'javascript:hi';
              document.body.appendChild(iframe);
              iframe.contentDocument.open();
              iframe.contentDocument.write('Hello');
              iframe.contentDocument.close();
            }
            """);
        await StoreReferenceAsync("mask-document-open");
        await Expect.That(Screen).ToHaveScreenshotAsync("mask-document-open", new ScreenshotOptions { Mask = [Browser.Locator("non-existent")] });
    }

    [Test]
    public async Task Should_work_when_mask_color_is_not_pink_F0F()
    {
        await Browser.SetViewportAsync(500, 500);
        await Browser.GotoAsync("/grid.html");
        await StoreMaskedReferenceAsync("mask-color-should-work", [new MaskTarget("div", 5)], "#00ff00");
        await Expect.That(Screen).ToHaveScreenshotAsync("mask-color-should-work", new ScreenshotOptions
        {
            Mask = [Browser.Locator("div").Nth(5)],
            MaskColor = "#00FF00",
        });
    }
}
