// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.NetworkIdle;

/// <summary>
/// <c>test.describe('page network idle')</c> of upstream's <c>apps/testbed/tests/network-idle.e2e.ts</c> (Playwright's
/// <c>page-network-idle.spec.ts</c>), where <c>page.setContent(html, { waitUntil })</c> is <c>Browser.GotoAsync(Content(html), ...)</c>
/// and a request the server holds is one a route holds. Not ported, as upstream: should wait for networkidle in waitForNavigation
/// (no <c>waitForNavigation</c>), should wait for networkidle when navigating iframe (no <c>frame.goto</c>), should wait for networkidle
/// from the popup (no popup event and <c>waitForLoadState</c>).
/// </summary>
public sealed class PageNetworkIdleTests : PlaywrightTest
{
    private static readonly GotoOptions NetworkIdle = new() { WaitUntil = GotoWaitUntil.NetworkIdle };

    private static string EmptyPage => $"{PREFIX}/empty.html";

    /// <summary>
    /// Playwright's <c>networkIdleTest</c>: holds <c>fetch-request-a.js</c> and <c>fetch-request-b.js</c>
    /// until the test answers them, and checks <paramref name="action"/> settles only 500ms after the last.
    /// </summary>
    private async Task NetworkIdleTestAsync(Func<Task> action)
    {
        var aRequested = Deferred();
        var aAnswer = Deferred();
        var bRequested = Deferred();
        var bAnswer = Deferred();
        foreach (var (suffix, requested, answer) in new[] { ("/fetch-request-a.js", aRequested, aAnswer), ("/fetch-request-b.js", bRequested, bAnswer) })
        {
            await Browser.RouteAsync($"**{suffix}", async route =>
            {
                requested.TrySetResult();
                await answer.Task;
                await route.FulfillAsync(new RouteFulfillResponse { Status = 404, Body = "File not found" });
            });
        }

        var actionPromise = action();
        var actionFinished = false;
        _ = actionPromise.ContinueWith(_ => actionFinished = true, TaskScheduler.Default);

        await aRequested.Task;
        Assert.That(actionFinished, Is.False);

        await Browser.EvaluateAsync<object>("() => window['fetchSecond']()");
        aAnswer.TrySetResult();

        await bRequested.Task;
        Assert.That(actionFinished, Is.False);

        var timerTriggered = false;
        using var cancel = new CancellationTokenSource();
        _ = Task.Delay(500, cancel.Token).ContinueWith(t => timerTriggered = !t.IsCanceled, TaskScheduler.Default);
        bAnswer.TrySetResult();

        await actionPromise;
        await cancel.CancelAsync();
        Assert.That(timerTriggered, Is.True);
    }

    [Test]
    public async Task Should_navigate_to_empty_page_with_networkidle()
    {
        var response = await AllAsync(Browser.WaitForResponseAsync("**/empty.html"), Browser.GotoAsync(EmptyPage, NetworkIdle));
        Assert.That(response.Status, Is.EqualTo(200));
    }

    [Test]
    public async Task Should_wait_for_networkidle_to_succeed_navigation()
    {
        await NetworkIdleTestAsync(() => Browser.GotoAsync("/networkidle.html", NetworkIdle));
    }

    [Test]
    public async Task Should_wait_for_networkidle_to_succeed_navigation_with_request_from_previous_navigation()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.GotoAsync(Content("<script>fetch('/never');</script>"));
        await NetworkIdleTestAsync(() => Browser.GotoAsync("/networkidle.html", NetworkIdle));
    }

    [Test]
    public async Task Should_wait_for_networkidle_in_setContent()
    {
        await Browser.GotoAsync(EmptyPage);
        await NetworkIdleTestAsync(() => Browser.GotoAsync(Content("<script src='networkidle.js'></script>"), NetworkIdle));
    }

    [Test]
    public async Task Should_wait_for_networkidle_in_setContent_with_request_from_previous_navigation()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.GotoAsync(Content("<script>fetch('/never');</script>"));
        await NetworkIdleTestAsync(() => Browser.GotoAsync(Content("<script src='networkidle.js'></script>"), NetworkIdle));
    }

    [Test]
    public async Task Should_wait_for_networkidle_in_setContent_from_the_child_frame()
    {
        await Browser.GotoAsync(EmptyPage);
        await NetworkIdleTestAsync(() => Browser.GotoAsync(Content("<iframe src='networkidle.html'></iframe>"), NetworkIdle));
    }

    [Test]
    public async Task Should_wait_for_networkidle_from_the_child_frame()
    {
        await NetworkIdleTestAsync(() => Browser.GotoAsync("/networkidle-frame.html", NetworkIdle));
    }

    [Test]
    public async Task Should_wait_for_networkidle_when_iframe_attaches_and_detaches()
    {
        var done = false;
        var promise = Task.Run(async () =>
        {
            await Browser.GotoAsync(Content($$"""
                <body>
                  <script>
                    const iframe = document.createElement('iframe');
                    iframe.src = {{System.Text.Json.JsonSerializer.Serialize($"{PREFIX}/never")}};
                    document.body.appendChild(iframe);
                  </script>
                </body>
                """), NetworkIdle);
            done = true;
        });
        await Task.Delay(600);
        Assert.That(done, Is.False);
        await Browser.EvaluateAsync<object>("() => { document.querySelector('iframe').remove(); }");
        await promise;
        Assert.That(done, Is.True);
    }

    [Test]
    public async Task Should_work_after_repeated_navigations_in_the_same_page()
    {
        var requestCount = 0;
        await Browser.RouteAsync("**/empty.html", route => route.FulfillAsync(new RouteFulfillResponse
        {
            ContentType = "text/html",
            Body = """
                <script>
                  fetch('http://localhost:8000/sample').then(res => console.log(res.json()))
                </script>
                """,
        }));
        await Browser.RouteAsync("**/sample", async route =>
        {
            Interlocked.Increment(ref requestCount);
            await route.FulfillAsync(new RouteFulfillResponse { ContentType = "application/json", Body = """{"content":"sample"}""" });
        });
        await Browser.GotoAsync(EmptyPage, NetworkIdle);
        Assert.That(requestCount, Is.EqualTo(1));
        await Browser.GotoAsync(EmptyPage, NetworkIdle);
        Assert.That(requestCount, Is.EqualTo(2));
    }

    [Test]
    public async Task Should_not_wait_for_an_open_EventSource_connection()
    {
        var page = Content("<script>new EventSource('/sse');</script>");
        var response = await AllAsync(Browser.WaitForResponseAsync("**/content?**"), Browser.GotoAsync(page, NetworkIdle));
        Assert.That(response.Status, Is.EqualTo(200));
    }

    [Test]
    public async Task Should_not_wait_for_an_open_EventSource_connection_in_setContent()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.GotoAsync(Content("""
            <script>
              window.__sseOpened = new Promise(resolve => {
                const es = new EventSource('/sse');
                es.onmessage = () => resolve();
              });
            </script>
            """), NetworkIdle);
        await Browser.EvaluateAsync<object>("() => window['__sseOpened'].then(() => null)");
    }
}
