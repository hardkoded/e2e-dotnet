// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.LocatorFrame;

/// <summary>
/// <c>test.describe('locator frame')</c> of upstream's <c>apps/testbed/tests/locator-frame.e2e.ts</c> (Playwright's
/// <c>locator-frame.spec.ts</c>). Not ported, as upstream: should wait for frame (no <c>locator.frameLocator</c>), should non work
/// for non-frame (Playwright's error text), locator.frameLocator should work for iframe, locator.frameLocator should throw on
/// ambiguity, locator.frameLocator should not throw on first/last/nth, should work with COEP/COOP/CORP isolated iframe,
/// locator.contentFrame should work, frameLocator.owner should work. The getBy coverage test keeps the role, text, test id, label and
/// placeholder queries (no <c>getByAltText</c> or <c>getByTitle</c>).
/// </summary>
public sealed class LocatorFrameTests : PlaywrightTest
{

    private static readonly string Nested = Content("<html><button>Hello nested iframe</button></html>");

    private static readonly string Iframe = Content($"""
        <html>
          <div>
            <button data-testid="buttonId">Hello iframe</button>
            <iframe src="{Nested}"></iframe>
          </div>
          <span>1</span>
          <span>2</span>
          <label for=target>Name</label><input id=target type=text placeholder=Placeholder title=Title alt=Alternative>
        </html>
        """);

    private static readonly string Routed = Content($"""<iframe src="{Iframe}" name="frame1"></iframe>""");

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
        var locator = Browser.FrameLocator("iframe").Locator("button");
        await Expect.That(locator).ToHaveTextAsync("Hello iframe");
        var spans = Browser.FrameLocator("iframe").Locator("span");
        await Expect.That(spans).ToHaveCountAsync(2);
    }

    [Test]
    public async Task Should_wait_for_frame_2()
    {
        await Browser.GotoAsync("about:blank");
        var navigated = After(300, () => Browser.GotoAsync(Routed));
        await Browser.FrameLocator("iframe").Locator("button").TapAsync();
        await navigated;
    }

    [Test]
    public async Task Should_wait_for_frame_to_go()
    {
        await Browser.GotoAsync(Routed);
        var removed = After(300, () => Browser.EvaluateAsync<object>("() => { document.querySelector('iframe').remove(); }"));
        await Expect.That(Browser.FrameLocator("iframe").Locator("button")).ToBeHiddenAsync();
        await removed;
    }

    [Test]
    public async Task Should_not_wait_for_frame()
    {
        await Browser.GotoAsync("/empty.html");
        await Expect.That(Browser.FrameLocator("iframe").Locator("span")).ToBeHiddenAsync();
    }

    [Test]
    public async Task Should_not_wait_for_frame_2()
    {
        await Browser.GotoAsync("/empty.html");
        await Expect.That(Browser.FrameLocator("iframe").Locator("span")).ToBeVisibleAsync(false);
    }

    [Test]
    public async Task Should_not_wait_for_frame_3()
    {
        await Browser.GotoAsync("/empty.html");
        await Expect.That(Browser.FrameLocator("iframe").Locator("span")).ToHaveCountAsync(0);
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
        await Browser.EvaluateAsync<object>("(src) => { const iframe = document.createElement('iframe'); iframe.src = src; document.body.appendChild(iframe); }", Nested);
        await promise;
    }

    [Test]
    public async Task Click_should_survive_frame_reattach()
    {
        await Browser.GotoAsync(Routed);
        var button = Browser.FrameLocator("iframe").Locator("button").Filter(hasText: "Hello nested iframe");
        var promise = button.TapAsync();
        await Browser.EvaluateAsync<object>("() => { document.querySelector('iframe').remove(); }");
        await Browser.EvaluateAsync<object>("(src) => { const iframe = document.createElement('iframe'); iframe.src = src; document.body.appendChild(iframe); }", Nested);
        await promise;
    }

    [Test]
    public async Task Click_should_survive_iframe_navigation()
    {
        await Browser.GotoAsync(Routed);
        var button = Browser.FrameLocator("iframe").Locator("button").Filter(hasText: "Hello nested iframe");
        var promise = button.TapAsync();
        var navigated = Browser.EvaluateAsync<object>("(src) => { document.querySelector('iframe').src = src; }", Nested);
        await promise;
        await navigated;
    }

    // Known difference, hardkoded/e2e-dotnet#1066: a frame-scoped getByRole also matches the nested frame's button.
    [Test]
    public async Task GetBy_coverage_role_text_test_id_label_placeholder()
    {
        await ExpectedToFailAsync("observed matched 2 nodes", async () =>
        {
            await Browser.GotoAsync(Routed);
            var button1 = Browser.FrameLocator("iframe").GetByRole("button");
            var button2 = Browser.FrameLocator("iframe").GetByText("Hello", exact: false);
            var button3 = Browser.FrameLocator("iframe").GetByTestId("buttonId");
            await Expect.That(button1).ToHaveTextAsync("Hello iframe");
            await Expect.That(button2).ToHaveTextAsync("Hello iframe");
            await Expect.That(button3).ToHaveTextAsync("Hello iframe");
            var input1 = Browser.FrameLocator("iframe").GetByLabel("Name");
            await Expect.That(input1).ToHaveValueAsync("");
            var input2 = Browser.FrameLocator("iframe").GetByPlaceholder("Placeholder");
            await Expect.That(input2).ToHaveValueAsync("");
        });
    }

    [Test]
    public async Task Wait_for_hidden_should_succeed_when_frame_is_not_in_dom()
    {
        await Browser.GotoAsync("about:blank");
        var button = Browser.FrameLocator("iframe1").Locator("button");
        Assert.That(await button.IsHiddenAsync(), Is.True);
        await button.WaitForAsync(new LocatorWaitForOptions { State = WaitForState.Hidden, Timeout = TimeSpan.FromSeconds(1) });
        await button.WaitForAsync(new LocatorWaitForOptions { State = WaitForState.Detached, Timeout = TimeSpan.FromSeconds(1) });
        var error = await Assert.CatchAsync(() => button.WaitForAsync(new LocatorWaitForOptions { State = WaitForState.Attached, Timeout = TimeSpan.FromSeconds(1) }));
        // Upstream only expects the error to be defined; the port's message names the missing frame.
        Assert.That(error!.Message, Does.Contain("no frame matches iframe1"));
    }

    // Known difference, hardkoded/e2e-dotnet#1067: an element inside a `visibility: hidden` iframe is not found, instead of hidden.
    [Test]
    public async Task Should_treat_elements_inside_hidden_iframe_as_hidden()
    {
        await ExpectedToFailAsync("observed no node", async () =>
        {
            await Browser.GotoAsync(Content("""
                <iframe name="hidden" style="visibility: hidden" srcdoc="<button>Hidden</button>"></iframe>
                <div style="visibility: hidden">
                  <iframe name="hidden-parent" srcdoc="<button>Hidden parent</button>"></iframe>
                  <iframe name="visible-override" style="visibility: visible" srcdoc="<button>Visible override</button>"></iframe>
                </div>
                <iframe name="outer" style="visibility: hidden" srcdoc="<iframe name='inner' srcdoc='<button>Nested</button>'></iframe>"></iframe>
                """));

            foreach (var button in new[]
            {
                Browser.FrameLocator("[name=hidden]").Locator("button"),
                Browser.FrameLocator("[name=hidden-parent]").Locator("button"),
                Browser.FrameLocator("[name=outer]").FrameLocator("[name=inner]").Locator("button"),
            })
            {
                await Expect.That(button).ToBeAttachedAsync();
                Equal(false, await button.IsVisibleAsync());
                Equal(true, await button.IsHiddenAsync());
                await Expect.That(button).ToBeHiddenAsync();
                await Expect.That(button).ToBeVisibleAsync(false);
                await button.WaitForAsync(new LocatorWaitForOptions { State = WaitForState.Hidden });
            }

            var visible = Browser.FrameLocator("[name=visible-override]").Locator("button");
            Equal(true, await visible.IsVisibleAsync());
            await Expect.That(visible).ToBeVisibleAsync();
            await visible.WaitForAsync(new LocatorWaitForOptions { State = WaitForState.Visible });
        });
    }

    [Test]
    public async Task Should_wait_for_hidden_iframe_to_become_visible()
    {
        // Upstream marks this test `fail`; it passes in the port.
        await Browser.GotoAsync(Content("""<iframe name="frame" style="visibility: hidden" srcdoc="<button onclick='window.__clicked = true'>Button</button>"></iframe>"""));
        var button = Browser.FrameLocator("[name=frame]").Locator("button");
        await Expect.That(button).ToBeHiddenAsync();

        var error = await Record(() => button.TapAsync(new ActionOptions { Timeout = TimeSpan.FromSeconds(1) }));
        Assert.That(error, Is.Not.Null);
        Assert.That(await Browser.EvaluateAsync<bool>("() => document.querySelector('iframe').contentWindow.__clicked === true"), Is.False);

        var clickPromise = button.TapAsync();
        var visiblePromise = Expect.That(button).ToBeVisibleAsync();
        await Browser.EvaluateAsync<object>("() => { document.querySelector('iframe').style.visibility = 'visible'; }");
        await Task.WhenAll(clickPromise, visiblePromise);
        Assert.That(await Browser.EvaluateAsync<bool>("() => document.querySelector('iframe').contentWindow.__clicked"), Is.True);

        await Browser.EvaluateAsync<object>("() => { document.querySelector('iframe').style.visibility = 'hidden'; }");
        await Expect.That(button).ToBeHiddenAsync();
        Assert.That(await button.IsVisibleAsync(), Is.False);
    }

    // Known difference, hardkoded/e2e-dotnet#1067: see above.
    [Test]
    public async Task Should_treat_elements_inside_hidden_cross_origin_iframe_as_hidden()
    {
        _ = PREFIX;
        if (!E2E.Playground.Testbed.CrossOriginAvailable)
        {
            Assert.Ignore("The testbed could not bind localhost, the second origin of this test (CROSS).");
        }

        await ExpectedToFailAsync("is not in the observation", async () =>
        {
            await Browser.GotoAsync("/empty.html");
            await Browser.GotoAsync(Content($"""<iframe style="visibility: hidden" src="{CROSS}/frames/frame.html"></iframe>"""));
            var div = Browser.FrameLocator("iframe").Locator("div");
            await Expect.That(div).ToHaveTextAsync("Hi, I'm frame");
            Equal(false, await div.IsVisibleAsync());
            await Expect.That(div).ToBeHiddenAsync();

            await Browser.EvaluateAsync<object>("() => { document.querySelector('iframe').style.visibility = 'visible'; }");
            await Expect.That(div).ToBeVisibleAsync();
            Equal(true, await div.IsVisibleAsync());
        });
    }
}
