// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.NUnit.Tests.Helpers;
using static E2E.NUnit.Tests.Screenshot.Png;

namespace E2E.NUnit.Tests.Screenshot;

/// <summary>
/// <c>test.describe('page screenshot')</c> of upstream's <c>apps/testbed/tests/screenshot.e2e.ts</c> (Playwright's
/// <c>page-screenshot.spec.ts</c>), without its <c>mask option</c> describe (<see cref="PageScreenshotMaskOptionTests"/>). Playwright's
/// golden images belong to its own renderer, so, as upstream, a golden comparison becomes a check of the pixels it shows. Not ported, as
/// upstream: the tests of the <c>clip</c>, <c>fullPage</c>, <c>omitBackground</c>, <c>path</c>, <c>type</c>, <c>quality</c>,
/// <c>caret</c>, <c>animations</c> and <c>style</c> options (the port has none), the jpeg and webp tests (screenshots are PNG only),
/// should work for webgl and should capture css transform (golden only), should run in parallel, and the mask option test with an
/// element handle.
/// </summary>
public sealed class PageScreenshotTests : ScreenshotTest
{
    [Test]
    public async Task Should_work()
    {
        await Browser.SetViewportAsync(500, 500);
        await Browser.GotoAsync("/grid.html");
        var screenshot = Read(await App.ScreenshotAsync());
        Assert.That((screenshot.Width, screenshot.Height), Is.EqualTo((500, 500)));
        Assert.That(Rgb(screenshot, 0, 0), Is.EqualTo((169, 169, 169)));
        Assert.That(Near(Rgb(screenshot, 3, 3), (255, 204, 204)), Is.True);
        Assert.That(Near(Rgb(screenshot, 453, 453), (255, 204, 207)), Is.True);
    }

    [Test]
    public async Task Should_not_capture_blinking_caret_by_default()
    {
        await Browser.GotoAsync(Content($$"""
            <link rel=stylesheet href="{{CROSS}}/injectedstyle.css">
            <style>
              div {
                caret-color: #000 !important;
              }
            </style>
            <div contenteditable="true"></div>
            """));
        var div = Browser.Locator("div");
        await div.PressSequentiallyAsync("foo bar");
        var box = (await div.BoundingBoxAsync())!;
        var screenshot = Crop(Read(await App.ScreenshotAsync()), box);
        for (var i = 0; i < 10; ++i)
        {
            await Task.Delay(150);
            var newScreenshot = Crop(Read(await App.ScreenshotAsync()), box);
            Assert.That(newScreenshot.AsSpan().SequenceEqual(screenshot), Is.True);
        }
    }

    [Test]
    public async Task Should_capture_blinking_caret_in_shadow_dom()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.EvaluateAsync<object>("""
            () => {
              class CustomElementContainer extends HTMLElement {
                #shadowRoot;
                constructor() {
                  super();
                  this.#shadowRoot = this.attachShadow({ mode: 'open' });
                  this.#shadowRoot.innerHTML = '<custom-element-input-wrapper><input type="text"/></custom-element-input-wrapper>';
                }
              }
              class CustomElementInputWrapper extends HTMLElement {
                #shadowRoot;
                constructor() {
                  super();
                  this.#shadowRoot = this.attachShadow({ mode: 'open' });
                  this.#shadowRoot.innerHTML = '<style>:host { all: initial; }</style><slot/>';
                }
              }
              customElements.define('custom-element-input-wrapper', CustomElementInputWrapper);
              customElements.define('custom-element-container', CustomElementContainer);

              const container = document.createElement('custom-element-container');
              document.body.appendChild(container);
            }
            """);

        var input = Browser.Locator("input");
        await input.FocusAsync();

        var box = (await input.BoundingBoxAsync())!;
        var screenshot = Crop(Read(await App.ScreenshotAsync()), box);
        var hasDifferentScreenshots = false;
        for (var i = 0; !hasDifferentScreenshots && i < 10; ++i)
        {
            await Task.Delay(150);
            var newScreenshot = Crop(Read(await App.ScreenshotAsync()), box);
            hasDifferentScreenshots = !newScreenshot.AsSpan().SequenceEqual(screenshot);
        }

        Assert.That(hasDifferentScreenshots, Is.False);
    }

    [Test]
    public async Task Should_work_for_canvas()
    {
        await Browser.SetViewportAsync(500, 500);
        await Browser.GotoAsync("/screenshots/canvas.html");
        var screenshot = Read(await App.ScreenshotAsync());
        Assert.That(Rgb(screenshot, 8 + 30, 8 + 30), Is.EqualTo((0, 0, 0)));
        Assert.That(Rgb(screenshot, 8 + 120, 8 + 120), Is.EqualTo((0, 0, 0)));
        Assert.That(Rgb(screenshot, 8 + 60, 8 + 60), Is.EqualTo((255, 255, 255)));
        Assert.That(Rgb(screenshot, 8 + 75, 8 + 75), Is.EqualTo((255, 255, 255)));
        Assert.That(Rgb(screenshot, 8 + 140, 8 + 140), Is.EqualTo((255, 255, 255)));
    }

    [Test]
    public async Task Should_capture_canvas_changes()
    {
        await Browser.GotoAsync(Content("<canvas></canvas>"));
        await Browser.EvaluateAsync<object>("""
            () => {
              const canvas = document.querySelector('canvas');
              canvas.width = 600;
              canvas.height = 600;
            }
            """);

        // Draws the horizontal line of step, 100 pixels below the one before.
        async Task AddLine(int step) =>
            await Browser.EvaluateAsync<object>(
                """
                (n) => {
                  const canvas = document.querySelector('canvas');
                  const ctx = canvas.getContext('2d');
                  ctx.lineWidth = 1;
                  ctx.beginPath();
                  ctx.moveTo(0, n * 100);
                  ctx.lineTo(300, n * 100);
                  ctx.stroke();
                }
                """,
                step);

        // Whether the line of step shows in screenshot: the canvas sits at the body margin, 8 pixels in.
        static bool HasLine(Internal.RgbaImage screenshot, int step)
        {
            var y = 8 + (step * 100);
            return new[] { y - 1, y }.Any(row => row >= 8 && Rgb(screenshot, 8 + 150, row).R < 200);
        }

        for (var i = 0; i < 3; i++)
        {
            await AddLine(i);
            await Task.Delay(100);
            var screenshot = Read(await App.ScreenshotAsync());
            for (var step = 0; step < 3; step++)
            {
                Assert.That(HasLine(screenshot, step), Is.EqualTo(step <= i));
            }
        }
    }

    [Test]
    public async Task Should_work_for_translateZ()
    {
        await Browser.SetViewportAsync(500, 500);
        await Browser.GotoAsync("/screenshots/translateZ.html");
        var screenshot = Read(await App.ScreenshotAsync());
        Assert.That(Rgb(screenshot, 120, 120), Is.EqualTo((255, 0, 0)));
        Assert.That(Rgb(screenshot, 175, 175), Is.EqualTo((0, 128, 0)));
        Assert.That(Rgb(screenshot, 230, 230), Is.EqualTo((0, 128, 0)));
        Assert.That(Rgb(screenshot, 300, 300), Is.EqualTo((255, 255, 255)));
    }

    [Test]
    [Ignore("App.ScreenshotAsync fails when the page navigates during the capture: #1095")]
    public async Task Should_work_while_navigating()
    {
        await Browser.SetViewportAsync(500, 500);
        await Browser.GotoAsync("/redirectloop1.html");
        for (var i = 0; i < 10; i++)
        {
            var screenshot = Read(await App.ScreenshotAsync());
            Assert.That((screenshot.Width, screenshot.Height), Is.EqualTo((500, 500)));
        }
    }

    [Test]
    public async Task Should_work_with_iframe_in_shadow()
    {
        await Browser.SetViewportAsync(500, 500);
        await Browser.GotoAsync("/grid-iframe-in-shadow.html");
        await Expect.That(Browser.FrameLocator("iframe").Locator("div").First()).ToBeVisibleAsync();
        var screenshot = Read(await App.ScreenshotAsync());
        Assert.That(Near(Rgb(screenshot, 3, 3), (255, 204, 204)), Is.True);
        Assert.That(Rgb(screenshot, 400, 400), Is.EqualTo((255, 255, 255)));
    }

    [Test]
    public async Task Should_not_issue_resize_event()
    {
        await Browser.GotoAsync("/grid.html");
        await Browser.EvaluateAsync<object>("() => { window.__resized = false; window.addEventListener('resize', () => { window.__resized = true; }); }");
        await App.ScreenshotAsync();
        Assert.That(await Browser.EvaluateAsync<bool>("window.__resized"), Is.False);
    }

    [Test]
    public async Task Should_work_with_Array_deleted()
    {
        await Browser.SetViewportAsync(500, 500);
        await Browser.GotoAsync("/grid.html");
        await Browser.EvaluateAsync<object>("() => { delete window.Array; }");
        var screenshot = Read(await App.ScreenshotAsync());
        Assert.That((screenshot.Width, screenshot.Height), Is.EqualTo((500, 500)));
        Assert.That(Near(Rgb(screenshot, 3, 3), (255, 204, 204)), Is.True);
    }
}
