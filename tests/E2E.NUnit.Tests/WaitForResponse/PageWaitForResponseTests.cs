// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.WaitForResponse;

/// <summary>
/// <c>test.describe('page wait for response')</c> of upstream's <c>apps/testbed/tests/wait-for-response.e2e.ts</c> (Playwright's
/// <c>page-wait-for-response.spec.ts</c>). Not ported, as upstream: should respect default timeout (no <c>setDefaultTimeout</c>: the
/// action timeout comes from the config), and the <c>TimeoutError</c> class and the error stack pointing at the call (a step fails
/// with a code instead).
/// </summary>
public sealed class PageWaitForResponseTests : PlaywrightTest
{
    private static string EmptyPage => $"{PREFIX}/empty.html";

    [Test]
    public async Task Should_work()
    {
        await Browser.GotoAsync(EmptyPage);
        var waiting = Browser.WaitForResponseAsync($"{PREFIX}/digits/2.png");
        await Browser.EvaluateAsync<object>(
            """
            () => {
              void fetch('/digits/1.png');
              void fetch('/digits/2.png');
              void fetch('/digits/3.png');
            }
            """
        );
        var response = await waiting;
        Assert.That(response.Url, Is.EqualTo($"{PREFIX}/digits/2.png"));
    }

    [Test]
    public async Task Should_respect_timeout()
    {
        var error = await Assert.CatchAsync(() =>
            Browser.WaitForResponseAsync(new Func<PageResponse, bool>(_ => false), TimeSpan.FromMilliseconds(1))
        );
        Contain("waitForResponse", error!.Message);
        Assert.That(error.Message, Does.Match("exceeded|timed out"));
    }

    [Test]
    public async Task Should_log_the_url()
    {
        await Browser.GotoAsync(EmptyPage);
        var error1 = await Assert.CatchAsync(() =>
            Browser.WaitForResponseAsync("foo.css", TimeSpan.FromMilliseconds(1000))
        );
        Contain("\"foo.css\"", error1!.Message);
        var error2 = await Assert.CatchAsync(() =>
            Browser.WaitForResponseAsync(new Regex("foo.css", RegexOptions.IgnoreCase), TimeSpan.FromMilliseconds(1000))
        );
        Contain("/foo.css/i", error2!.Message);
    }

    [Test]
    public async Task Should_work_with_no_timeout()
    {
        await Browser.GotoAsync(EmptyPage);
        var waiting = Browser.WaitForResponseAsync($"{PREFIX}/digits/2.png", TimeSpan.Zero);
        await Browser.EvaluateAsync<object>(
            """
            () => setTimeout(() => {
              void fetch('/digits/1.png');
              void fetch('/digits/2.png');
              void fetch('/digits/3.png');
            }, 50) && null
            """
        );
        var response = await waiting;
        Assert.That(response.Url, Is.EqualTo($"{PREFIX}/digits/2.png"));
    }

    [Test]
    public async Task Should_work_with_predicate()
    {
        await Browser.GotoAsync(EmptyPage);
        var waiting = Browser.WaitForResponseAsync(candidate => candidate.Url == $"{PREFIX}/digits/2.png");
        await Browser.EvaluateAsync<object>(
            """
            () => {
              void fetch('/digits/1.png');
              void fetch('/digits/2.png');
              void fetch('/digits/3.png');
            }
            """
        );
        var response = await waiting;
        Assert.That(response.Url, Is.EqualTo($"{PREFIX}/digits/2.png"));
    }

    [Test]
    public async Task Should_work_with_async_predicate()
    {
        await Browser.GotoAsync(EmptyPage);
        var waiting1 = Browser.WaitForResponseAsync(async candidate =>
        {
            var text = await candidate.TextAsync();
            return text.Contains("contents of the file", StringComparison.Ordinal);
        });
        var waiting2 = Browser.WaitForResponseAsync(async candidate =>
        {
            var text = await candidate.TextAsync();
            return text.Contains("bar", StringComparison.Ordinal);
        });
        await Browser.EvaluateAsync<object>(
            """
            () => {
              void fetch('/simple.json').then(r => r.json());
              void fetch('/file-to-upload.txt').then(r => r.text());
            }
            """
        );
        var response1 = await waiting1;
        var response2 = await waiting2;
        Assert.That(response1.Url, Is.EqualTo($"{PREFIX}/file-to-upload.txt"));
        Assert.That(response2.Url, Is.EqualTo($"{PREFIX}/simple.json"));
    }

    [Test]
    public async Task Sync_predicate_should_be_only_called_once()
    {
        await Browser.GotoAsync(EmptyPage);
        var counter = 0;
        var waiting = Browser.WaitForResponseAsync(candidate =>
        {
            Interlocked.Increment(ref counter);
            return candidate.Url == $"{PREFIX}/digits/1.png";
        });
        await Browser.EvaluateAsync<object>(
            """
            async () => {
              await fetch('/digits/1.png');
              await fetch('/digits/2.png');
              await fetch('/digits/3.png');
              return null;
            }
            """
        );
        var response = await waiting;
        Assert.That(response.Url, Is.EqualTo($"{PREFIX}/digits/1.png"));
        Assert.That(counter, Is.EqualTo(1));
    }
}
