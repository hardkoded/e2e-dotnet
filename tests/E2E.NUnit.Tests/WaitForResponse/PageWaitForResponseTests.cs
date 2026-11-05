// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.WaitForResponse;

/// <summary>
/// <c>test.describe('page wait for response')</c> of upstream's <c>apps/testbed/tests/wait-for-response.e2e.ts</c> (Playwright's
/// <c>page-wait-for-response.spec.ts</c>). Not ported, as upstream: should respect timeout (no <c>waitForEvent('response')</c>),
/// should respect default timeout (no <c>setDefaultTimeout</c>, no predicate), should work with predicate, should work with async
/// predicate, sync predicate should be only called once (no response predicates). The <c>/digits/*.png</c> assets are not copied: the tests
/// match the URL of the response, which a 404 has too, and <c>grid.html</c> lays out one pixel lower once its digit images load
/// (<c>LocatorMisc2Tests.Should_return_bounding_box</c> expects <c>y</c> 50).
/// </summary>
public sealed class PageWaitForResponseTests : PlaywrightTest
{
    private static string EmptyPage => $"{PREFIX}/empty.html";

    [Test]
    public async Task Should_work()
    {
        await Browser.GotoAsync(EmptyPage);
        var waiting = Browser.WaitForResponseAsync($"{PREFIX}/digits/2.png");
        await Browser.EvaluateAsync<object>("""
            () => {
              void fetch('/digits/1.png');
              void fetch('/digits/2.png');
              void fetch('/digits/3.png');
            }
            """);
        var response = await waiting;
        Assert.That(response.Url, Is.EqualTo($"{PREFIX}/digits/2.png"));
    }

    [Test]
    public async Task Should_log_the_url()
    {
        await Browser.GotoAsync(EmptyPage);
        var error1 = await Assert.CatchAsync(() => Browser.WaitForResponseAsync("foo.css", TimeSpan.FromMilliseconds(1000)));
        Contain("\"foo.css\"", error1!.Message);
        var error2 = await Assert.CatchAsync(() => Browser.WaitForResponseAsync(new Regex("foo.css", RegexOptions.IgnoreCase), TimeSpan.FromMilliseconds(1000)));
        Contain("/foo.css/i", error2!.Message);
    }

    [Test]
    public async Task Should_work_with_no_timeout()
    {
        await Browser.GotoAsync(EmptyPage);
        var waiting = Browser.WaitForResponseAsync($"{PREFIX}/digits/2.png", TimeSpan.Zero);
        await Browser.EvaluateAsync<object>("""
            () => setTimeout(() => {
              void fetch('/digits/1.png');
              void fetch('/digits/2.png');
              void fetch('/digits/3.png');
            }, 50) && null
            """);
        var response = await waiting;
        Assert.That(response.Url, Is.EqualTo($"{PREFIX}/digits/2.png"));
    }
}
