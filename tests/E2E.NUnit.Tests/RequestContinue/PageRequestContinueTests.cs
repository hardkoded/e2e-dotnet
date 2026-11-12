// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.RegularExpressions;
using E2E.Engine;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.RequestContinue;

/// <summary>
/// <c>test.describe('page request continue')</c> of upstream's <c>apps/testbed/tests/request-continue.e2e.ts</c> (Playwright's
/// <c>page-request-continue.spec.ts</c>); its <c>post data</c> tests are in <see cref="PageRequestContinuePostDataTests"/>.
/// Not ported, as upstream: should amend binary post data (a string <c>postData</c> only), should work with Cross-Origin-Opener-Policy
/// (no request and response events), redirected requests should report overridden headers (no <c>fallback({ headers })</c>, no
/// <c>response.request()</c>), postData should return empty string when overriding body with empty string (no <c>waitForRequest</c>).
/// </summary>
public sealed class PageRequestContinueTests : PlaywrightTest
{
    private static string EmptyPage => $"{PREFIX}/empty.html";

    /// <summary>The request the server received for the page now open, which <c>/cors-echo</c> printed as its text.</summary>
    private async Task<Served> ServedAsync() => Served.Parse((await Browser.EvaluateAsync<string>("document.body.textContent"))!);

    /// <summary>The request the server received for a page <c>fetch</c> to <paramref name="url"/> with <paramref name="init"/>, which <c>/cors-echo</c> answered with.</summary>
    private async Task<Served> FetchedAsync(string url, string init = "{}") =>
        Served.Parse((await Browser.EvaluateAsync<string>($"(url) => fetch(url, {init}).then((response) => response.text())", url))!);

    [Test]
    public async Task Should_work()
    {
        await Browser.RouteAsync("**/*", route => route.ContinueAsync());
        await Browser.GotoAsync(EmptyPage);
    }

    [Test]
    public async Task Should_amend_HTTP_headers()
    {
        await Browser.RouteAsync("**/*", route => route.ContinueAsync(new RouteContinueOverrides { Headers = WithHeaders(route.Request.Headers, ("FOO", "bar")) }));
        await Browser.GotoAsync(EmptyPage);
        var request = await FetchedAsync("/cors-echo");
        Assert.That(request.Header("foo"), Is.EqualTo("bar"));
    }

    [Test]
    public async Task Should_not_allow_to_override_unsafe_HTTP_headers()
    {
        await Browser.RouteAsync("**/*", route => route.ContinueAsync(new RouteContinueOverrides { Headers = WithHeaders(route.Request.Headers, ("host", "bar"), ("trailer", "baz")) }));
        await Browser.GotoAsync("/cors-echo");
        var serverRequest = await ServedAsync();
        Assert.That(serverRequest.Header("trailer"), Is.Null);
        Assert.That(serverRequest.Header("host"), Is.EqualTo(new Uri(EmptyPage).Authority));
    }

    [Test]
    public async Task Should_delete_header_with_undefined_value()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.RouteAsync($"{PREFIX}/cors-echo", route => route.ContinueAsync(new RouteContinueOverrides { Headers = route.Request.Headers, DropHeaders = ["foo"] }));
        var serverRequest = await FetchedAsync($"{PREFIX}/cors-echo", "{ headers: { foo: 'a', bar: 'b' } }");
        Assert.That(serverRequest.Method, Is.EqualTo("GET"));
        Assert.That(serverRequest.Header("foo"), Is.Null);
        Assert.That(serverRequest.Header("bar"), Is.EqualTo("b"));
    }

    [Test]
    public async Task Should_amend_method()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.RouteAsync("**/*", route => route.ContinueAsync(new RouteContinueOverrides { Method = "POST" }));
        var request = await FetchedAsync("/cors-echo");
        Assert.That(request.Method, Is.EqualTo("POST"));
    }

    [Test]
    public async Task Should_override_request_url()
    {
        await Browser.RouteAsync("**/foo", route => route.ContinueAsync(new RouteContinueOverrides { Url = $"{PREFIX}/global-var.html" }));
        var response = await AllAsync(Browser.WaitForResponseAsync(new Regex(@"/(foo|global-var\.html)$")), Browser.GotoAsync($"{PREFIX}/foo"));
        Assert.That(response.Url, Is.EqualTo($"{PREFIX}/global-var.html"));
        Assert.That(await Browser.EvaluateAsync<int>("() => window['globalVar']"), Is.EqualTo(123));
    }

    [Test]
    public async Task Should_not_allow_changing_protocol_when_overriding_url()
    {
        var failed = Deferred<Exception?>();
        await Browser.RouteAsync("**/*", async route =>
        {
            try
            {
                await route.ContinueAsync(new RouteContinueOverrides { Url = "file:///tmp/foo" });
                failed.TrySetResult(null);
            }
            catch (Exception caught)
            {
                failed.TrySetResult(caught);
                await route.AbortAsync();
            }
        });
        var navigation = Browser.GotoAsync(EmptyPage).ContinueWith(_ => { }, TaskScheduler.Default);
        var error = await failed.Task;
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Message, Does.Match("scheme"));
        await navigation;
    }

    [Test]
    public async Task Should_not_throw_if_request_was_cancelled_by_the_page()
    {
        var routed = Deferred();
        var release = Deferred();
        await Browser.RouteAsync("**/data.json", async route =>
        {
            routed.TrySetResult();
            await release.Task;
            await route.ContinueAsync();
        });
        await Browser.GotoAsync(EmptyPage);
        await Browser.EvaluateAsync<object>("""
            (url) => {
              globalThis.controller = new AbortController();
              globalThis.failure = fetch(url, { signal: globalThis.controller.signal }).then(() => 'loaded', (error) => error.name);
            }
            """, $"{PREFIX}/data.json");
        await routed.Task;
        Assert.That(await Browser.EvaluateAsync<string>("() => { globalThis.controller.abort(); return globalThis.failure; }"), Is.EqualTo("AbortError"));
        release.TrySetResult();
        await Browser.EvaluateAsync<object>("() => new Promise((resolve) => setTimeout(resolve, 100))");
    }

    [Test]
    public async Task Should_override_method_along_with_url()
    {
        await Browser.RouteAsync("**/foo", route => route.ContinueAsync(new RouteContinueOverrides { Url = $"{PREFIX}/cors-echo", Method = "POST" }));
        await Browser.GotoAsync($"{PREFIX}/foo");
        Assert.That((await ServedAsync()).Method, Is.EqualTo("POST"));
    }

    [Test]
    public async Task Should_amend_method_on_main_request()
    {
        await Browser.RouteAsync("**/*", route => route.ContinueAsync(new RouteContinueOverrides { Method = "POST" }));
        await Browser.GotoAsync("/cors-echo");
        Assert.That((await ServedAsync()).Method, Is.EqualTo("POST"));
    }

    [Test]
    public async Task Should_not_delete_the_origin_header()
    {
        await Browser.GotoAsync("/empty.html");
        string? interceptedOrigin = null;
        await Browser.RouteAsync($"{CROSS}/cors-echo", async route =>
        {
            var headers = WithHeaders(route.Request.Headers);
            interceptedOrigin = headers.GetValueOrDefault("origin");
            headers.Remove("origin");
            await route.ContinueAsync(new RouteContinueOverrides { Headers = headers });
        });
        var serverRequest = await FetchedAsync($"{CROSS}/cors-echo");
        Assert.That(serverRequest.Method, Is.EqualTo("GET"));
        Assert.That(interceptedOrigin, Is.EqualTo(PREFIX));
        Assert.That(serverRequest.Header("origin"), Is.EqualTo(PREFIX));
    }

    [Test]
    public async Task Should_continue_preload_link_requests()
    {
        var intercepted = false;
        await Browser.RouteAsync("**/one-style.css", async route =>
        {
            intercepted = true;
            await route.ContinueAsync(new RouteContinueOverrides { Headers = WithHeaders(route.Request.Headers, ("custom", "value")) });
        });
        await Browser.GotoAsync("/preload.html");
        await Browser.EvaluateAsync<bool>("() => new Promise((resolve) => { const check = () => window.preloadedStyles ? resolve(true) : setTimeout(check, 20); check(); })");
        Assert.That(intercepted, Is.True);
        // preload.html sets the flag before switching the link to a stylesheet, which applies a task later.
        await Expect.Poll(() => Browser.EvaluateAsync<string>("() => window.getComputedStyle(document.body).backgroundColor")).ToMatchAsync(Is.EqualTo("rgb(255, 192, 203)"));
    }

    [Test]
    public async Task Should_respect_set_cookie_in_redirect_response()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.RouteAsync("**/bounce?**", route => route.ContinueAsync(new RouteContinueOverrides { Headers = WithHeaders(route.Request.Headers) }));
        await Browser.GotoAsync($"/bounce?cookie={Uri.EscapeDataString("foo=bar;  max-age=36000")}&to=/cors-echo");
        Assert.That((await ServedAsync()).Header("cookie"), Is.EqualTo("foo=bar"));
        Assert.That(await Browser.EvaluateAsync<string>("() => document.cookie"), Is.EqualTo("foo=bar"));
    }

    [Test]
    public async Task Continue_should_not_propagate_cookie_override_to_redirects()
    {
        await Browser.GotoAsync("/set-cookie?cookie=foo=bar;");
        Assert.That(await Browser.EvaluateAsync<string>("() => document.cookie"), Is.EqualTo("foo=bar"));
        await Browser.RouteAsync("**/redirect?**", route => route.ContinueAsync(new RouteContinueOverrides { Headers = WithHeaders(route.Request.Headers, ("cookie", "override")) }));
        await Browser.GotoAsync($"/redirect?to={Uri.EscapeDataString($"{PREFIX}/cors-echo")}");
        Assert.That((await ServedAsync()).Header("cookie"), Is.EqualTo("foo=bar"));
    }

    [Test]
    public async Task Continue_should_not_override_cookie()
    {
        await Browser.GotoAsync("/set-cookie?cookie=foo=bar;");
        Assert.That(await Browser.EvaluateAsync<string>("() => document.cookie"), Is.EqualTo("foo=bar"));
        await Browser.RouteAsync("**", route => route.ContinueAsync(new RouteContinueOverrides { Headers = WithHeaders(route.Request.Headers, ("cookie", "override"), ("custom", "value")) }));
        await Browser.GotoAsync("/cors-echo");
        var serverRequest = await ServedAsync();
        Assert.That(serverRequest.Header("cookie"), Is.EqualTo("foo=bar"));
        Assert.That(serverRequest.Header("custom"), Is.EqualTo("value"));
    }

    [Test]
    public async Task Continue_with_headers_should_send_fresh_cookie_from_the_browser_cookie_store()
    {
        await Browser.GotoAsync("/set-cookie?cookie=foo=v1;");
        Assert.That(await Browser.EvaluateAsync<string>("() => document.cookie"), Is.EqualTo("foo=v1"));
        await Browser.RouteAsync("**/cors-echo", async route =>
        {
            await Browser.SetCookiesAsync([new BrowserCookie { Name = "foo", Value = "v2", Url = PREFIX }]);
            await route.ContinueAsync(new RouteContinueOverrides { Headers = WithHeaders(route.Request.Headers) });
        });
        await Browser.GotoAsync("/cors-echo");
        Assert.That((await ServedAsync()).Header("cookie"), Is.EqualTo("foo=v2"));
    }

    [Test]
    public async Task Continue_with_headers_should_send_fresh_cookie_after_a_redirect()
    {
        await Browser.GotoAsync("/set-cookie?cookie=foo=v1;");
        Assert.That(await Browser.EvaluateAsync<string>("() => document.cookie"), Is.EqualTo("foo=v1"));
        await Browser.RouteAsync("**/bounce?**", route => route.ContinueAsync(new RouteContinueOverrides { Headers = WithHeaders(route.Request.Headers) }));
        await Browser.GotoAsync($"/bounce?cookie={Uri.EscapeDataString("foo=v2;")}&to={Uri.EscapeDataString($"{PREFIX}/cors-echo")}");
        Assert.That((await ServedAsync()).Header("cookie"), Is.EqualTo("foo=v2"));
    }

    [Test]
    public async Task Redirect_after_continue_should_be_able_to_delete_cookie()
    {
        await Browser.GotoAsync("/set-cookie?cookie=foo=bar;");
        Assert.That(await Browser.EvaluateAsync<string>("() => document.cookie"), Is.EqualTo("foo=bar"));
        await Browser.RouteAsync("**/redirect?**", route => route.ContinueAsync(new RouteContinueOverrides { Headers = WithHeaders(route.Request.Headers) }));
        await Browser.GotoAsync($"/redirect?to={Uri.EscapeDataString($"/set-cookie?cookie={Uri.EscapeDataString("foo=bar; expires=Thu, 01 Jan 1970 00:00:00 GMT")}")}");
        await Browser.GotoAsync("/cors-echo");
        Assert.That((await ServedAsync()).Header("cookie"), Is.Null.Or.Empty);
    }

    [Test]
    public async Task Continue_should_propagate_headers_to_redirects()
    {
        await Browser.RouteAsync("**/redirect?**", route => route.ContinueAsync(new RouteContinueOverrides { Headers = WithHeaders(route.Request.Headers, ("custom", "value")) }));
        await Browser.GotoAsync("/redirect?to=/cors-echo");
        Assert.That((await ServedAsync()).Header("custom"), Is.EqualTo("value"));
    }

    [Test]
    public async Task Continue_should_drop_content_length_on_redirects()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.RouteAsync("**/redirect?**", route => route.ContinueAsync(new RouteContinueOverrides { Headers = WithHeaders(route.Request.Headers, ("custom", "value")) }));
        var serverRequest = await FetchedAsync($"{PREFIX}/redirect?to=/cors-echo", "{ method: 'POST', body: 'foo' }");
        Assert.That(serverRequest.Method, Is.EqualTo("GET"));
        Assert.That(serverRequest.Header("content-length"), Is.Null);
        Assert.That(serverRequest.Header("content-type"), Is.Null);
        Assert.That(serverRequest.Header("custom"), Is.EqualTo("value"));
    }

    [Test]
    public async Task Continue_should_delete_headers_on_redirects()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.RouteAsync("**/redirect?**", route => route.ContinueAsync(new RouteContinueOverrides { Headers = route.Request.Headers, DropHeaders = ["foo"] }));
        var serverRequest = await FetchedAsync($"{PREFIX}/redirect?to=/cors-echo", "{ headers: { foo: 'a' } }");
        Assert.That(serverRequest.Method, Is.EqualTo("GET"));
        Assert.That(serverRequest.Header("foo"), Is.Null);
    }

    [Test]
    public async Task Propagate_headers_same_origin_redirect()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.EvaluateAsync<object>("() => { document.cookie = 'a=b'; }");
        var serverRequest = await FetchedAsync(
            $"{PREFIX}/redirect?to={Uri.EscapeDataString("/cors-echo?preflight")}",
            "{ headers: { authorization: 'credentials', custom: 'foo' }, credentials: 'include' }");
        Assert.That(serverRequest.Header("authorization"), Is.EqualTo("credentials"));
        Assert.That(serverRequest.Header("cookie"), Is.EqualTo("a=b"));
        Assert.That(serverRequest.Header("custom"), Is.EqualTo("foo"));
    }

    [Test]
    public async Task Propagate_headers_cross_origin()
    {
        await Browser.GotoAsync("/empty.html");
        var serverRequest = await FetchedAsync(
            $"{CROSS}/cors-echo?preflight",
            "{ headers: { authorization: 'credentials', custom: 'foo' }, credentials: 'include' }");
        Assert.That(serverRequest.Header("authorization"), Is.EqualTo("credentials"));
        Assert.That(serverRequest.Header("custom"), Is.EqualTo("foo"));
    }

    [Test]
    public async Task Propagate_headers_cross_origin_redirect()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.EvaluateAsync<object>("() => { document.cookie = 'a=b'; }");
        var serverRequest = await FetchedAsync(
            $"{PREFIX}/bounce?status=301&to={Uri.EscapeDataString($"{CROSS}/cors-echo?preflight")}",
            "{ headers: { authorization: 'credentials', custom: 'foo' }, credentials: 'include' }");
        Assert.That(serverRequest.Header("authorization"), Is.Null.Or.Empty);
        Assert.That(serverRequest.Header("cookie"), Is.Null.Or.Empty);
        Assert.That(serverRequest.Header("custom"), Is.EqualTo("foo"));
    }

    [Test]
    public async Task Propagate_headers_cross_origin_redirect_after_interception()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.EvaluateAsync<object>("() => { document.cookie = 'a=b'; }");
        await Browser.RouteAsync("**/bounce?**", route => route.ContinueAsync(new RouteContinueOverrides { Headers = WithHeaders(route.Request.Headers, ("authorization", "credentials"), ("custom", "foo")) }));
        var serverRequest = await FetchedAsync(
            $"{PREFIX}/bounce?status=301&to={Uri.EscapeDataString($"{CROSS}/cors-echo?preflight")}",
            "{ headers: { authorization: 'none' }, credentials: 'include' }");
        Assert.That(serverRequest.Header("authorization"), Is.EqualTo("credentials"));
        Assert.That(serverRequest.Header("cookie"), Is.Null.Or.Empty);
        Assert.That(serverRequest.Header("custom"), Is.EqualTo("foo"));
    }

    [Test]
    public async Task Continue_should_pass_on_307_cross_origin_redirect()
    {
        var final = $"{PREFIX}{Content("<!doctype html><title>final</title><p>ok</p>")}";
        await Browser.GotoAsync("/empty.html");
        await Browser.GotoAsync(Content($"""
            <form id="f" method="POST" action="{CROSS}/bounce?status=307&to={Uri.EscapeDataString(final)}">
              <input type="submit">
            </form>
            """));
        await Browser.RouteAsync("**/*", route => route.ContinueAsync());
        var navigation = Browser.WaitForURLAsync(final);
        await Browser.Locator("input").TapAsync();
        await navigation;
        await Expect.That(Browser.Locator("p")).ToHaveTextAsync("ok");
    }

    [Test]
    public async Task Should_intercept_css_variable_with_background_url()
    {
        var intercepted = Deferred();
        var interceptedRequests = 0;
        await Browser.RouteAsync($"{PREFIX}/pptr.png", async route =>
        {
            Interlocked.Increment(ref interceptedRequests);
            intercepted.TrySetResult();
            await route.ContinueAsync();
        });
        await Browser.GotoAsync(Content("""
            <style>
              @keyframes JNDzq {
                0% { background-position: 0 0 }
                to { background-position: 100 0 }
              }
              div {
                --background: url(/pptr.png);
                background-image: var(--background);
                animation: JNDzq 1s linear infinite;
              }
            </style>
            <div>Yo!</div>
            """));
        Assert.That(await Browser.Locator("div").TextContentAsync(), Is.EqualTo("Yo!"));
        await intercepted.Task;
        await Task.Delay(1000);
        Assert.That(interceptedRequests, Is.EqualTo(1));
    }

    [Test]
    public async Task Continue_should_not_change_multipart_form_data_body()
    {
        await Browser.GotoAsync(EmptyPage);

        async Task<string> SendFormData()
        {
            var result = await Browser.EvaluateAsync<JsonElement>("""
                async () => {
                  const newFile = new File(['file content'], 'file.txt');
                  const formData = new FormData();
                  formData.append('file', newFile);
                  const response = await fetch('/cors-echo', { method: 'POST', credentials: 'include', body: formData });
                  return [response.status, (await response.json()).body];
                }
                """);
            Assert.That(result[0].GetInt32(), Is.EqualTo(200));
            return result[1].GetString()!;
        }

        var reqBefore = await SendFormData();
        await Browser.RouteAsync("**/*", route => route.ContinueAsync());
        var reqAfter = await SendFormData();
        var fileContent = string.Join(
            "\r\n",
            "Content-Disposition: form-data; name=\"file\"; filename=\"file.txt\"",
            "Content-Type: application/octet-stream",
            "",
            "file content",
            "------");
        Assert.That(reqBefore, Does.Contain(fileContent));
        Assert.That(reqAfter, Does.Contain(fileContent));
    }

    [Test]
    public async Task Should_not_forward_Host_header_on_cross_origin_redirect()
    {
        await Browser.RouteAsync("**/*", async route =>
        {
            var headers = route.Request.Headers;
            Assert.That(headers.ContainsKey("host"), Is.False);
            await route.ContinueAsync(new RouteContinueOverrides { Headers = WithHeaders(headers) });
        });
        var response = await AllAsync(
            Browser.WaitForResponseAsync("**/cors-echo"),
            Browser.GotoAsync($"{PREFIX}/bounce?to={Uri.EscapeDataString($"{CROSS}/cors-echo")}"));
        Assert.That(response.Status, Is.EqualTo(200));
        Assert.That((await ServedAsync()).Header("host"), Is.EqualTo(new Uri(CROSS).Authority));
    }
}
