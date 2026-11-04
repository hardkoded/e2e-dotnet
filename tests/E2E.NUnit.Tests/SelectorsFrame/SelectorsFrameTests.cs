// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.SelectorsFrame;

/// <summary>
/// <c>test.describe('selectors frame')</c> of upstream's <c>apps/testbed/tests/selectors-frame.e2e.ts</c> (Playwright's
/// <c>selectors-frame.spec.ts</c>). <c>iframe &gt;&gt; internal:control=enter-frame &gt;&gt; x</c> is
/// <c>FrameLocator("iframe").Locator(x)</c>. Not ported, as upstream: the (handle) tests (element handles), $eval should throw for
/// missing frame (no <c>$eval</c>), should not allow dangling/leading enter-frame, should not allow capturing before enter-frame,
/// should capture after the enter-frame (Playwright selector syntax), should non work for non-frame, and the nine <c>any-frame</c>
/// tests (no counterpart).
/// </summary>
public sealed class SelectorsFrameTests : PlaywrightTest
{
    private static readonly string Iframe2 = Content("<html><button tag=\"iframe2\">Hello nested iframe</button></html>");

    private static readonly string Iframe = Content($"""
        <html>
          <div>
            <button>Hello iframe</button>
            <iframe src="{Iframe2}"></iframe>
          </div>
          <span>1</span>
          <span>2</span>
        </html>
        """);

    private static readonly string Routed = Content($"""<iframe src="{Iframe}"></iframe>""");

    [Test]
    public async Task Should_work_for_iframe()
    {
        await Browser.GotoAsync(Routed);
        var button = Browser.FrameLocator("iframe").Locator("button");
        await button.WaitForAsync();
        Assert.That(await button.TextContentAsync(), Is.EqualTo("Hello iframe"));
        await Expect.That(button).ToHaveTextAsync("Hello iframe");
        await button.TapAsync();
    }

    [Test]
    public async Task Should_work_for_nested_iframe()
    {
        await Browser.GotoAsync(Routed);
        var button = Browser.FrameLocator("iframe").FrameLocator("iframe").Locator("button");
        await button.WaitForAsync();
        Assert.That(await button.TextContentAsync(), Is.EqualTo("Hello nested iframe"));
        await Expect.That(button).ToHaveTextAsync("Hello nested iframe");
        await button.TapAsync();
    }

    [Test]
    public async Task Should_work_for_Dollar_and_DollarDollar()
    {
        await Browser.GotoAsync(Routed);
        var element = Browser.FrameLocator("iframe").Locator("button");
        await element.WaitForAsync(new LocatorWaitForOptions { State = WaitForState.Attached });
        Assert.That(await element.TextContentAsync(), Is.EqualTo("Hello iframe"));
        await Browser.FrameLocator("iframe").Locator("span").Nth(1).WaitForAsync(new LocatorWaitForOptions { State = WaitForState.Attached });
        Assert.That(await Browser.FrameLocator("iframe").Locator("span").CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task Dollar_should_not_wait_for_frame()
    {
        await Browser.GotoAsync("/empty.html");
        Assert.That(await Browser.FrameLocator("iframe").Locator("canvas").CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task DollarDollar_should_not_wait_for_frame()
    {
        await Browser.GotoAsync("/empty.html");
        Assert.That(await Browser.FrameLocator("iframe").Locator("canvas").AllAsync(), Has.Count.EqualTo(0));
    }

    [Test]
    public async Task DollarDollarEval_should_not_throw_for_missing_frame()
    {
        await Browser.GotoAsync("/empty.html");
        Assert.That(await Browser.FrameLocator("iframe").Locator("canvas").AllTextContentsAsync(), Has.Count.EqualTo(0));
    }

    // Known difference, hardkoded/e2e-dotnet#1066: a frame-scoped getByRole also matches the nested frame's button.
    [Test]
    public async Task Should_work_for_DollarEval()
    {
        await ExpectedToFailAsync("observed count 2", async () =>
        {
            await Browser.GotoAsync(Routed);
            await Expect.That(Browser.FrameLocator("iframe").GetByRole("button")).ToHaveCountAsync(1);
            await Expect.That(Browser.FrameLocator("iframe").Locator("button")).ToHaveCountAsync(1);
        });
    }

    [Test]
    public async Task Should_work_for_DollarDollarEval()
    {
        await Browser.GotoAsync(Routed);
        await Expect.That(Browser.FrameLocator("iframe").Locator("span")).ToHaveCountAsync(2);
        Assert.That(await Browser.FrameLocator("iframe").Locator("span").AllTextContentsAsync(), Is.EqualTo(new[] { "1", "2" }));
    }

    [Test]
    public async Task Should_click_in_lazy_iframe()
    {
        var lazy = Content("<html><button>Hello iframe</button></html>");
        await Browser.GotoAsync("/empty.html");
        var attached = After(500, async () =>
        {
            await Browser.EvaluateAsync<object>("() => { document.body.appendChild(document.createElement('iframe')); }");
            await Task.Delay(500);
            await Browser.EvaluateAsync<object>("(src) => { document.querySelector('iframe').src = src; }", lazy);
        });
        var button = Browser.FrameLocator("iframe").Locator("button");
        var tap = button.TapAsync();
        var text = button.TextContentAsync();
        var hasText = Expect.That(button).ToHaveTextAsync("Hello iframe");
        await Task.WhenAll(tap, text, hasText, attached);
        Assert.That(await text, Is.EqualTo("Hello iframe"));
    }

    [Test]
    public async Task WaitFor_should_survive_frame_reattach()
    {
        await Browser.GotoAsync(Routed);
        var button = Browser.FrameLocator("iframe").Locator("button").Filter(hasText: "Hello nested iframe");
        var promise = button.WaitForAsync();
        await Browser.EvaluateAsync<object>("() => { document.querySelector('iframe').remove(); }");
        await Browser.EvaluateAsync<object>("(src) => { const iframe = document.createElement('iframe'); iframe.src = src; document.body.appendChild(iframe); }", Iframe2);
        await promise;
    }

    [Test]
    public async Task Click_should_survive_frame_reattach()
    {
        await Browser.GotoAsync(Routed);
        var button = Browser.FrameLocator("iframe").Locator("button").Filter(hasText: "Hello nested iframe");
        var promise = button.TapAsync();
        await Browser.EvaluateAsync<object>("() => { document.querySelector('iframe').remove(); }");
        await Browser.EvaluateAsync<object>("(src) => { const iframe = document.createElement('iframe'); iframe.src = src; document.body.appendChild(iframe); }", Iframe2);
        await promise;
    }

    [Test]
    public async Task Click_should_survive_iframe_navigation()
    {
        await Browser.GotoAsync(Routed);
        var button = Browser.FrameLocator("iframe").Locator("button").Filter(hasText: "Hello nested iframe");
        var promise = button.TapAsync();
        var navigated = Browser.EvaluateAsync<object>("(src) => { document.querySelector('iframe').src = src; }", Iframe2);
        await promise;
        await navigated;
    }

    [Test]
    public async Task Click_should_survive_navigation()
    {
        await Browser.GotoAsync(Iframe);
        var promise = Browser.Locator("button").Filter(hasText: "Hello nested iframe").TapAsync();
        await Task.Delay(100);
        await Browser.GotoAsync(Iframe2);
        await promise;
    }
}
