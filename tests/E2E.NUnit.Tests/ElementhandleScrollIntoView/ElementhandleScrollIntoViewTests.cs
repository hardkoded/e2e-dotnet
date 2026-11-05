// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.ElementhandleScrollIntoView;

/// <summary>
/// <c>test.describe('elementhandle scroll into view')</c> of upstream's <c>apps/testbed/tests/elementhandle-scroll-into-view.e2e.ts</c>
/// (Playwright's <c>elementhandle-scroll-into-view.spec.ts</c>). An element handle's <c>scrollIntoViewIfNeeded</c> is
/// <c>ScrollIntoViewAsync</c> on a locator. Not ported, as upstream: should throw for detached element (a handle to a removed element; a
/// locator re-resolves).
/// </summary>
public sealed class ElementhandleScrollIntoViewTests : PlaywrightTest
{
    /// <summary>
    /// Starts scrolling the page's one <c>div</c> into view, checks it is still waiting a second later, runs <paramref name="after"/> on the
    /// div in the page, and checks the scroll then finishes: Playwright's <c>testWaiting</c>.
    /// </summary>
    private async Task TestWaitingAsync(string after)
    {
        var done = false;
        var promise = Task.Run(async () =>
        {
            await Browser.Locator("div").ScrollIntoViewAsync();
            done = true;
        });
        await Task.Delay(1000);
        Assert.That(done, Is.False);
        await Browser.EvaluateAsync<object>($"() => {{ const div = document.querySelector('div'); ({after})(div); }}");
        await promise;
        Assert.That(done, Is.True);
    }

    [Test]
    public async Task Should_work_smoke()
    {
        await Browser.GotoAsync("/offscreenbuttons.html");
        const string overflow = "(id) => document.getElementById(id).getBoundingClientRect().right - window.innerWidth";
        for (var i = 0; i < 11; ++i)
        {
            var before = await Browser.EvaluateAsync<double>(overflow, $"btn{i}");
            Assert.That(before, Is.EqualTo(10 * i));
            await Browser.Locator($"#btn{i}").ScrollIntoViewAsync();
            var after = await Browser.EvaluateAsync<double>(overflow, $"btn{i}");
            Assert.That(after <= 0, Is.True);
            await Browser.EvaluateAsync<object>("() => window.scrollTo(0, 0)");
        }
    }

    [Test]
    public async Task Should_wait_for_display_none_to_become_visible()
    {
        await Browser.GotoAsync(Content("<div style=\"display:none\">Hello</div>"));
        await TestWaitingAsync("div => div.style.display = 'block'");
    }

    [Test]
    public async Task Should_scroll_display_contents_into_view()
    {
        await Browser.GotoAsync(Content("""
            <style>
              html, body { margin: 0; padding: 0; width: 100%; height: 100%; }
              ::-webkit-scrollbar { display: none; }
              * { scrollbar-width: none; }
            </style>
            <div id=container style="width:200px;height:200px;overflow:scroll;border:1px solid black;">
              <div style="margin-top:500px;background:red;">
                <div style="height:50px;width:100px;background:cyan;">
                  <div id=target style="display:contents">Hello</div>
                </div>
              <div>
            </div>
            """));
        await Browser.Locator("#target").ScrollIntoViewAsync();
        var scrollTop = await Browser.EvaluateAsync<double>("() => document.querySelector('#container').scrollTop");
        Assert.That(Math.Abs(scrollTop - 350), Is.LessThan(1));
    }

    [Test]
    public async Task Should_work_for_visibility_hidden_element()
    {
        // Known difference (hardkoded/e2e-dotnet#1081): ScrollIntoViewAsync refuses a hidden node.
        await ExpectedToFailAsync("is hidden", async () =>
        {
            await Browser.GotoAsync(Content("<div style=\"visibility:hidden\">Hello</div>"));
            await Browser.Locator("div").ScrollIntoViewAsync(new ActionOptions { Timeout = TimeSpan.FromSeconds(3) });
        });
    }

    [Test]
    public async Task Should_work_for_zero_sized_element()
    {
        await Browser.GotoAsync(Content("<div style=\"height:0\">Hello</div>"));
        await Browser.Locator("div").ScrollIntoViewAsync();
    }

    [Test]
    public async Task Should_wait_for_nested_display_none_to_become_visible()
    {
        await Browser.GotoAsync(Content("<span style=\"display:none\"><div>Hello</div></span>"));
        await TestWaitingAsync("div => div.parentElement.style.display = 'block'");
    }

    [Test]
    public async Task Should_wait_for_element_to_stop_moving()
    {
        await Browser.GotoAsync(Content("""
            <style>
              @keyframes move {
                from { margin-left: 0; }
                to { margin-left: 200px; }
              }
              div.animated {
                animation: 2s linear 0s infinite alternate move;
              }
            </style>
            <div class=animated>moving</div>
            """));
        await TestWaitingAsync("div => div.classList.remove('animated')");
    }

    [Test]
    public async Task Should_timeout_waiting_for_visible()
    {
        await Browser.GotoAsync(Content("<div style=\"display:none\">Hello</div>"));
        var error = await Assert.CatchAsync(() => Browser.Locator("div").ScrollIntoViewAsync(new ActionOptions { Timeout = TimeSpan.FromSeconds(3) }));
        Assert.That(error, Is.InstanceOf<Exception>());
    }
}
