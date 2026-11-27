// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.WaitForRequest;

/// <summary>
/// <c>test.describe('page wait for request')</c> of upstream's <c>apps/testbed/tests/wait-for-request.e2e.ts</c> (Playwright's
/// <c>page-wait-for-request.spec.ts</c>). Not ported, as upstream: should respect default timeout (no <c>setDefaultTimeout</c>: the
/// action timeout comes from the config), and the <c>TimeoutError</c> class and the error stack pointing at the call (a step fails
/// with a code instead), and the URL Playwright cuts short in its log line (e2e prints the pattern whole). Not ported, .NET has no
/// such feature: should work with url match regular expression from a different context (a <c>Regex</c> has no other realm).
/// </summary>
public sealed class PageWaitForRequestTests : PlaywrightTest
{
    private static string EmptyPage => $"{PREFIX}/empty.html";

    [Test]
    public async Task Should_work()
    {
        await Browser.GotoAsync(EmptyPage);
        var waiting = Browser.WaitForRequestAsync($"{PREFIX}/digits/2.png");
        await Browser.EvaluateAsync<object>(
            """
            () => {
              void fetch('/digits/1.png');
              void fetch('/digits/2.png');
              void fetch('/digits/3.png');
            }
            """
        );
        var request = await waiting;
        Assert.That(request.Url, Is.EqualTo($"{PREFIX}/digits/2.png"));
    }

    [Test]
    public async Task Should_work_with_predicate()
    {
        await Browser.GotoAsync(EmptyPage);
        var waiting = Browser.WaitForRequestAsync(candidate => candidate.Url == $"{PREFIX}/digits/2.png");
        await Browser.EvaluateAsync<object>(
            """
            () => {
              void fetch('/digits/1.png');
              void fetch('/digits/2.png');
              void fetch('/digits/3.png');
            }
            """
        );
        var request = await waiting;
        Assert.That(request.Url, Is.EqualTo($"{PREFIX}/digits/2.png"));
    }

    [Test]
    public async Task Should_respect_timeout()
    {
        var error = await Assert.CatchAsync(() =>
            Browser.WaitForRequestAsync(new Func<E2E.Engine.WebRequest, bool>(_ => false), TimeSpan.FromMilliseconds(1))
        );
        Contain("waitForRequest", error!.Message);
        Assert.That(error.Message, Does.Match("exceeded|timed out"));
    }

    [Test]
    public async Task Should_log_the_url()
    {
        var pattern = "long-long-long-long-long-long-long-long-long-long-long-long-long-long.css";
        var error = await Assert.CatchAsync(() =>
            Browser.WaitForRequestAsync(pattern, TimeSpan.FromMilliseconds(1000))
        );
        Contain($"\"{pattern}\"", error!.Message);
    }

    [Test]
    public async Task Should_work_with_no_timeout()
    {
        await Browser.GotoAsync(EmptyPage);
        var waiting = Browser.WaitForRequestAsync($"{PREFIX}/digits/2.png", TimeSpan.Zero);
        await Browser.EvaluateAsync<object>(
            """
            () => setTimeout(() => {
              void fetch('/digits/1.png');
              void fetch('/digits/2.png');
              void fetch('/digits/3.png');
            }, 50) && null
            """
        );
        var request = await waiting;
        Assert.That(request.Url, Is.EqualTo($"{PREFIX}/digits/2.png"));
    }

    [Test]
    public async Task Should_work_with_url_match()
    {
        await Browser.GotoAsync(EmptyPage);
        var waiting = Browser.WaitForRequestAsync(new Regex(@"digits\/\d\.png"));
        await Browser.EvaluateAsync<object>(
            """
            () => {
              void fetch('/digits/1.png');
            }
            """
        );
        var request = await waiting;
        Assert.That(request.Url, Is.EqualTo($"{PREFIX}/digits/1.png"));
    }
}
