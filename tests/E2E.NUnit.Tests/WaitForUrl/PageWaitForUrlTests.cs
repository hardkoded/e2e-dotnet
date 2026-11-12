// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.WaitForUrl;

/// <summary>
/// <c>test.describe('page wait for url')</c> of upstream's <c>apps/testbed/tests/wait-for-url.e2e.ts</c> (Playwright's
/// <c>page-wait-for-url.spec.ts</c>). Not ported, as upstream: should work with both domcontentloaded and load, should work with
/// commit, should work with commit and about:blank (no <c>waitUntil</c>), should work on frame (no <c>frame.waitForURL</c>).
/// </summary>
public sealed partial class PageWaitForUrlTests : PlaywrightTest
{
    private static string EmptyPage => $"{PREFIX}/empty.html";

    [Test]
    public async Task Should_work()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.EvaluateAsync<object>("(url) => { window.location.href = url; }", $"{PREFIX}/grid.html");
        await Browser.WaitForURLAsync("/grid.html");
    }

    [Test]
    public async Task Should_respect_timeout()
    {
        // e2e opens no page before the first goto, where Playwright starts on about:blank.
        await Browser.GotoAsync(EmptyPage);
        var promise = Assert.CatchAsync(() => Browser.WaitForURLAsync("/frame.html", TimeSpan.FromMilliseconds(2500)));
        await Browser.GotoAsync(EmptyPage);
        var error = await promise;
        Assert.That(error!.Message, Does.Contain("frame.html"));
    }

    [Test]
    public async Task Should_work_with_clicking_on_anchor_links()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.GotoAsync(Content("<a href='#foobar'>foobar</a>"));
        await Browser.Locator("a").TapAsync();
        await Browser.WaitForURLAsync(FoobarSuffix());
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
        await Browser.Locator("a").TapAsync();
        await Browser.WaitForURLAsync("/wow.html");
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
        await Browser.Locator("a").TapAsync();
        await Browser.WaitForURLAsync("/replaced.html");
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

        await Browser.Locator("a#back").TapAsync();
        await Browser.WaitForURLAsync("/first.html");
        Assert.That(await Browser.UrlAsync(), Is.EqualTo($"{PREFIX}/first.html"));

        await Browser.Locator("a#forward").TapAsync();
        await Browser.WaitForURLAsync("/second.html");
        Assert.That(await Browser.UrlAsync(), Is.EqualTo($"{PREFIX}/second.html"));
    }

    [Test]
    public async Task Should_work_with_url_match_for_same_document_navigations()
    {
        await Browser.GotoAsync(EmptyPage);
        var resolved = false;
        var waitPromise = Task.Run(async () =>
        {
            await Browser.WaitForURLAsync(ThirdHtml());
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

    [GeneratedRegex(@"#foobar$")]
    private static partial Regex FoobarSuffix();

    [GeneratedRegex(@"third\.html")]
    private static partial Regex ThirdHtml();
}
