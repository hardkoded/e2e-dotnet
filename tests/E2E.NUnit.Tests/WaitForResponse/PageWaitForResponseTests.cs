// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.WaitForResponse;

/// <summary>
/// <c>test.describe('page wait for response')</c> of upstream's <c>apps/testbed/tests/wait-for-response.e2e.ts</c> (Playwright's
/// <c>page-wait-for-response.spec.ts</c>). Not ported, as upstream: should respect timeout (needs <c>page.waitForEvent('response')</c>),
/// should respect default timeout (needs <c>page.setDefaultTimeout</c> and a predicate), should work with predicate, should work with
/// async predicate, and sync predicate should be only called once (no response predicates).
/// </summary>
public sealed class PageWaitForResponseTests : PlaywrightTest
{
    private static string EmptyPage => $"{PREFIX}/empty.html";

    [Test]
    public async Task Should_work()
    {
        await Browser.GotoAsync(EmptyPage);
        var response = await AllAsync(
            Browser.WaitForResponseAsync($"{PREFIX}/digits/2.png"),
            Browser.EvaluateAsync<object>("""
                () => {
                  void fetch('/digits/1.png');
                  void fetch('/digits/2.png');
                  void fetch('/digits/3.png');
                }
                """));
        Assert.That(response.Url, Is.EqualTo($"{PREFIX}/digits/2.png"));
    }

    // Partial: the timeout error here is Playwright's own, which does not name the pattern, where upstream's names "foo.css" and /foo.css/i. So
    // it checks that both patterns time out, not that the message logs the url.
    [Test]
    public async Task Partial_Should_log_the_url()
    {
        await Browser.GotoAsync(EmptyPage);
        var error1 = await Assert.CatchAsync(() => Browser.WaitForResponseAsync("foo.css", TimeSpan.FromSeconds(1)));
        Assert.That(error1!.Message, Does.Contain("Timeout 1000ms exceeded"));
        var error2 = await Assert.CatchAsync(() => Browser.WaitForResponseAsync(new Regex("foo.css", RegexOptions.IgnoreCase), TimeSpan.FromSeconds(1)));
        Assert.That(error2!.Message, Does.Contain("Timeout 1000ms exceeded"));
    }

    [Test]
    public async Task Should_work_with_no_timeout()
    {
        await Browser.GotoAsync(EmptyPage);
        var response = await AllAsync(
            Browser.WaitForResponseAsync($"{PREFIX}/digits/2.png", TimeSpan.Zero),
            Browser.EvaluateAsync<object>("""
                () => setTimeout(() => {
                  void fetch('/digits/1.png');
                  void fetch('/digits/2.png');
                  void fetch('/digits/3.png');
                }, 50) && null
                """));
        Assert.That(response.Url, Is.EqualTo($"{PREFIX}/digits/2.png"));
    }
}
