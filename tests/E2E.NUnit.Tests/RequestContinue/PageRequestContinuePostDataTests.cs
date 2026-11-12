// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.RequestContinue;

/// <summary>
/// The nested <c>test.describe('post data')</c> of <c>page request continue</c> in upstream's <c>apps/testbed/tests/request-continue.e2e.ts</c>
/// (Playwright's <c>page-request-continue.spec.ts</c>).
/// </summary>
public sealed class PageRequestContinuePostDataTests : PlaywrightTest
{
    private static string EmptyPage => $"{PREFIX}/empty.html";

    private async Task<Served> FetchedAsync(string url, string init) =>
        Served.Parse((await Browser.EvaluateAsync<string>($"(url) => fetch(url, {init}).then((response) => response.text())", url))!);

    [Test]
    public async Task Should_amend_post_data()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.RouteAsync("**/*", route => route.ContinueAsync(new RouteContinueOverrides { PostData = "doggo" }));
        var serverRequest = await FetchedAsync("/cors-echo", "{ method: 'POST', body: 'birdy' }");
        Assert.That(serverRequest.Body, Is.EqualTo("doggo"));
    }

    [Test]
    public async Task Should_compute_content_length_from_post_data()
    {
        await Browser.GotoAsync(EmptyPage);
        var data = new string('a', 7500);
        await Browser.RouteAsync("**/*", route => route.ContinueAsync(new RouteContinueOverrides
        {
            PostData = data,
            Headers = WithHeaders(route.Request.Headers, ("content-type", "application/json")),
        }));
        var serverRequest = await FetchedAsync("/cors-echo", "{ method: 'PATCH', body: 'birdy' }");
        Assert.That(serverRequest.Body, Is.EqualTo(data));
        Assert.That(serverRequest.Header("content-length"), Is.EqualTo(data.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.That(serverRequest.Header("content-type"), Is.EqualTo("application/json"));
    }

    [Test]
    public async Task Should_amend_method_and_post_data()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.RouteAsync("**/*", route => route.ContinueAsync(new RouteContinueOverrides { Method = "POST", PostData = "doggo" }));
        var serverRequest = await FetchedAsync("/cors-echo", "{ method: 'GET' }");
        Assert.That(serverRequest.Method, Is.EqualTo("POST"));
        Assert.That(serverRequest.Body, Is.EqualTo("doggo"));
    }

    [Test]
    public async Task Should_amend_utf8_post_data()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.RouteAsync("**/*", route => route.ContinueAsync(new RouteContinueOverrides { PostData = "пушкин" }));
        var serverRequest = await FetchedAsync("/cors-echo", "{ method: 'POST', body: 'birdy' }");
        Assert.That(serverRequest.Method, Is.EqualTo("POST"));
        Assert.That(serverRequest.Body, Is.EqualTo("пушкин"));
    }

    [Test]
    public async Task Should_amend_longer_post_data()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.RouteAsync("**/*", route => route.ContinueAsync(new RouteContinueOverrides { PostData = "doggo-is-longer-than-birdy" }));
        var serverRequest = await FetchedAsync("/cors-echo", "{ method: 'POST', body: 'birdy' }");
        Assert.That(serverRequest.Method, Is.EqualTo("POST"));
        Assert.That(serverRequest.Body, Is.EqualTo("doggo-is-longer-than-birdy"));
    }

    [Test]
    public async Task Should_use_content_type_from_original_request()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.RouteAsync($"{PREFIX}/cors-echo", route => route.ContinueAsync(new RouteContinueOverrides { PostData = """{"b":2}""" }));
        var request = await FetchedAsync($"{PREFIX}/cors-echo", """{ method: 'POST', body: '{"a":1}', headers: { 'content-type': 'application/json' } }""");
        Assert.That(request.Header("content-type"), Is.EqualTo("application/json"));
        Assert.That(request.Body, Is.EqualTo("""{"b":2}"""));
    }
}
