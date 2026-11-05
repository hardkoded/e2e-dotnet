// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.WaitForNavigation;

/// <summary>
/// <c>test.describe('page wait for navigation')</c> of upstream's <c>apps/testbed/tests/wait-for-navigation.e2e.ts</c> (Playwright's
/// <c>page-wait-for-navigation.spec.ts</c>), where <c>page.waitForNavigation()</c> is <c>Browser.WaitForURLAsync</c> with the URL the
/// navigation lands on. Not ported, as upstream: should work with both domcontentloaded and load, should work with commit
/// (no <c>waitForNavigation({ waitUntil })</c>), should work with clicking on links which do not commit navigation (no HTTPS server
/// with a bad certificate), should work when subframe issues window.stop() (no <c>frameattached</c> and <c>framenavigated</c>
/// events), should work on frame, should fail when frame detaches (no <c>frame.waitForNavigation</c>).
/// </summary>
public sealed class PageWaitForNavigationTests : PlaywrightTest
{
    private static string EmptyPage => $"{PREFIX}/empty.html";

    [Test]
    public async Task Should_work()
    {
        await Browser.GotoAsync(EmptyPage);
        var navigation = Browser.WaitForURLAsync("/grid.html");
        await Browser.EvaluateAsync<object>("(url) => { window.location.href = url; }", $"{PREFIX}/grid.html");
        await navigation;
        Assert.That(await Browser.UrlAsync(), Does.Contain("grid.html"));
    }

    [Test]
    public async Task Should_respect_timeout()
    {
        // e2e opens no page before the first goto, where Playwright starts on about:blank.
        await Browser.GotoAsync("/one-style.html");
        var promise = Assert.CatchAsync(() => Browser.WaitForURLAsync("/frame.html", TimeSpan.FromMilliseconds(5000)));
        await Browser.GotoAsync(EmptyPage);
        var error = await promise;
        Assert.That(error!.Message, Does.Contain("/frame.html"));
        Assert.That(error.Message, Does.Contain(EmptyPage));
    }

    [Test]
    public async Task Should_work_with_clicking_on_anchor_links()
    {
        await Browser.GotoAsync(EmptyPage);
        var page = Content("<a href='#foobar'>foobar</a>");
        await Browser.GotoAsync(page);
        var navigation = Browser.WaitForURLAsync($"{page}#foobar");
        await Browser.Locator("a").TapAsync();
        await navigation;
        Assert.That(await Browser.UrlAsync(), Is.EqualTo($"{PREFIX}{page}#foobar"));
    }

    [Test]
    public async Task Should_work_with_history_pushState()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.GotoAsync(Content("""
            <a onclick='javascript:pushState()'>SPA</a>
            <script>
              function pushState() { history.pushState({}, '', 'wow.html') }
            </script>
            """));
        var navigation = Browser.WaitForURLAsync("/wow.html");
        await Browser.Locator("a").TapAsync();
        await navigation;
        Assert.That(await Browser.UrlAsync(), Is.EqualTo($"{PREFIX}/wow.html"));
    }

    [Test]
    public async Task Should_work_with_history_replaceState()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.GotoAsync(Content("""
            <a onclick='javascript:replaceState()'>SPA</a>
            <script>
              function replaceState() { history.replaceState({}, '', '/replaced.html') }
            </script>
            """));
        var navigation = Browser.WaitForURLAsync("/replaced.html");
        await Browser.Locator("a").TapAsync();
        await navigation;
        Assert.That(await Browser.UrlAsync(), Is.EqualTo($"{PREFIX}/replaced.html"));
    }

    [Test]
    public async Task Should_work_with_DOM_history_back_history_forward()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.GotoAsync(Content("""
            <a id=back onclick='javascript:goBack()'>back</a>
            <a id=forward onclick='javascript:goForward()'>forward</a>
            <script>
              function goBack() { history.back(); }
              function goForward() { history.forward(); }
              history.pushState({}, '', '/first.html');
              history.pushState({}, '', '/second.html');
            </script>
            """));
        Assert.That(await Browser.UrlAsync(), Is.EqualTo($"{PREFIX}/second.html"));
        var back = Browser.WaitForURLAsync("/first.html");
        await Browser.Locator("a#back").TapAsync();
        await back;
        Assert.That(await Browser.UrlAsync(), Is.EqualTo($"{PREFIX}/first.html"));
        var forward = Browser.WaitForURLAsync("/second.html");
        await Browser.Locator("a#forward").TapAsync();
        await forward;
        Assert.That(await Browser.UrlAsync(), Is.EqualTo($"{PREFIX}/second.html"));
    }

    [Test]
    public async Task Should_work_with_url_match()
    {
        // e2e opens no page before the first goto, where Playwright starts on about:blank.
        await Browser.GotoAsync(Content("<p>blank</p>"));
        var response1 = false;
        var response1Promise = Task.Run(async () =>
        {
            await Browser.WaitForURLAsync(new Regex(@"one-style\.html"));
            response1 = true;
        });
        var response2 = false;
        var response2Promise = Task.Run(async () =>
        {
            await Browser.WaitForURLAsync(new Regex(@"/frame.html"));
            response2 = true;
        });
        var response3 = false;
        var response3Promise = Task.Run(async () =>
        {
            await Browser.WaitForURLAsync(new Regex(@"[?&]foo=bar"));
            response3 = true;
        });
        Assert.That(response1, Is.False);
        Assert.That(response2, Is.False);
        Assert.That(response3, Is.False);
        await Browser.GotoAsync(EmptyPage);
        Assert.That(response1, Is.False);
        Assert.That(response2, Is.False);
        Assert.That(response3, Is.False);
        await Browser.GotoAsync("/frame.html");
        Assert.That(response1, Is.False);
        await response2Promise;
        Assert.That(response2, Is.True);
        Assert.That(response3, Is.False);
        await Browser.GotoAsync("/one-style.html");
        await response1Promise;
        Assert.That(response1, Is.True);
        Assert.That(response2, Is.True);
        Assert.That(response3, Is.False);
        await Browser.GotoAsync("/frame.html?foo=bar");
        await response3Promise;
        Assert.That(response1, Is.True);
        Assert.That(response2, Is.True);
        Assert.That(response3, Is.True);
        await Browser.GotoAsync("/empty.html");
    }

    [Test]
    public async Task Should_work_with_url_match_for_same_document_navigations()
    {
        await Browser.GotoAsync(EmptyPage);
        var resolved = false;
        var waitPromise = Task.Run(async () =>
        {
            await Browser.WaitForURLAsync(new Regex(@"third\.html"));
            resolved = true;
        });
        Assert.That(resolved, Is.False);
        await Browser.EvaluateAsync<object>("() => { history.pushState({}, '', '/first.html'); }");
        Assert.That(resolved, Is.False);
        await Browser.EvaluateAsync<object>("() => { history.pushState({}, '', '/second.html'); }");
        Assert.That(resolved, Is.False);
        await Browser.EvaluateAsync<object>("() => { history.pushState({}, '', '/third.html'); }");
        await waitPromise;
        Assert.That(resolved, Is.True);
    }

    [Test]
    public async Task Should_work_for_cross_process_navigations()
    {
        await Browser.GotoAsync(EmptyPage);
        var url = $"{CROSS}/empty.html";
        var waitPromise = Browser.WaitForURLAsync(url);
        var gotoPromise = Browser.GotoAsync(url, new GotoOptions { WaitUntil = GotoWaitUntil.DomContentLoaded });
        await waitPromise;
        Assert.That(await Browser.UrlAsync(), Is.EqualTo(url));
        Assert.That(await Browser.EvaluateAsync<string>("document.location.href"), Is.EqualTo(url));
        await gotoPromise;
    }
}
