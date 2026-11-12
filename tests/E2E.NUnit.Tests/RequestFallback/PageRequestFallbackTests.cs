// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.RequestFallback;

/// <summary>
/// <c>test.describe('page request fallback')</c> of upstream's <c>apps/testbed/tests/request-fallback.e2e.ts</c> (Playwright's
/// <c>page-request-fallback.spec.ts</c>). Not ported, as upstream: should chain once (no <c>times</c>), should amend HTTP headers,
/// should delete header with undefined value, should amend method, should override request url, and the <c>post data</c> tests
/// (no options on <c>fallback</c>).
/// </summary>
public sealed class PageRequestFallbackTests : PlaywrightTest
{
    private static string EmptyPage => $"{PREFIX}/empty.html";

    [Test]
    public async Task Should_work()
    {
        await Browser.RouteAsync("**/*", route => route.FallbackAsync());
        await Browser.GotoAsync(EmptyPage);
    }

    [Test]
    public async Task Should_fall_back()
    {
        var intercepted = new List<int>();
        foreach (var index in new[] { 1, 2, 3 })
        {
            await Browser.RouteAsync("**/empty.html", async route =>
            {
                intercepted.Add(index);
                await route.FallbackAsync();
            });
        }

        await Browser.GotoAsync(EmptyPage);
        Assert.That(intercepted, Is.EqualTo(new[] { 3, 2, 1 }));
    }

    [Test]
    public async Task Should_fall_back_async()
    {
        var intercepted = new List<int>();
        foreach (var index in new[] { 1, 2, 3 })
        {
            await Browser.RouteAsync("**/empty.html", async route =>
            {
                intercepted.Add(index);
                await Task.Delay(100);
                await route.FallbackAsync();
            });
        }

        await Browser.GotoAsync(EmptyPage);
        Assert.That(intercepted, Is.EqualTo(new[] { 3, 2, 1 }));
    }

    [Test]
    public async Task Should_not_chain_fulfill()
    {
        var failed = false;
        await Browser.RouteAsync("**/empty.html", async route =>
        {
            failed = true;
            await route.ContinueAsync();
        });
        await Browser.RouteAsync("**/empty.html", route => route.FulfillAsync(new RouteFulfillResponse { Status = 200, Body = "fulfilled" }));
        await Browser.RouteAsync("**/empty.html", route => route.FallbackAsync());
        var response = await AllAsync(Browser.WaitForResponseAsync("**/empty.html"), Browser.GotoAsync(EmptyPage));
        Assert.That(await response.TextAsync(), Is.EqualTo("fulfilled"));
        Assert.That(failed, Is.False);
    }

    [Test]
    public async Task Should_not_chain_abort()
    {
        var failed = false;
        await Browser.RouteAsync("**/empty.html", async route =>
        {
            failed = true;
            await route.ContinueAsync();
        });
        await Browser.RouteAsync("**/empty.html", route => route.AbortAsync());
        await Browser.RouteAsync("**/empty.html", route => route.FallbackAsync());
        var e = await Assert.CatchAsync(() => Browser.GotoAsync(EmptyPage));
        Assert.That(e, Is.Not.Null);
        Assert.That(failed, Is.False);
    }

    [Test]
    public async Task Should_fall_back_after_exception()
    {
        await Browser.RouteAsync("**/empty.html", route => route.ContinueAsync());
        await Browser.RouteAsync("**/empty.html", async route =>
        {
            try
            {
                // Upstream passes an unknown option; here a body and a json together, which fulfill refuses before it decides.
                await route.FulfillAsync(new RouteFulfillResponse { Body = "", Json = new { } });
            }
            catch
            {
                await route.FallbackAsync();
            }
        });
        await Browser.GotoAsync(EmptyPage);
    }
}
