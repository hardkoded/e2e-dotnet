// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using E2E.Engine;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.Route;

/// <summary>
/// <c>test.describe('page route')</c> of upstream's <c>apps/testbed/tests/route.e2e.ts</c> (Playwright's <c>page-route.spec.ts</c>).
/// Not ported, as upstream: should navigate to dataURL and not fire dataURL requests (<c>goto</c> refuses <c>data:</c> URLs),
/// should show custom HTTP headers, should work with custom referer headers and should send referer (no <c>setExtraHTTPHeaders</c>),
/// should be abortable with custom error codes (no <c>abort(errorCode)</c>), should chain fallback w/ dynamic URL (no <c>fallback({ url })</c>),
/// should not fulfill with redirect status (WebKit only), the four tests with the <c>times</c> parameter (no <c>times</c>),
/// should contain sec-fetch headers in route (no <c>allHeaders()</c>), should contain raw response header and
/// should contain raw response header after fulfill (no <c>request.response()</c>), should be able to intercept every navigation
/// to a page controlled by service worker (service workers), should respect URLPattern ignoreCase (no URLPattern).
/// </summary>
public sealed class PageRouteTests : PlaywrightTest
{
    private static string EmptyPage => $"{PREFIX}/empty.html";

    private Task<string> TextContentAsync() => Browser.EvaluateAsync<string>("document.body.textContent")!;

    [Test]
    public async Task Should_intercept_smoke()
    {
        var intercepted = false;
        await Browser.RouteAsync("**/empty.html", async route =>
        {
            Assert.That(route.Request.Url, Does.Contain("empty.html"));
            Assert.That(route.Request.Headers["user-agent"], Is.Not.Empty);
            Assert.That(route.Request.Method, Is.EqualTo("GET"));
            Assert.That(route.Request.PostData, Is.Null);
            await route.ContinueAsync();
            intercepted = true;
        });
        var response = await AllAsync(Browser.WaitForResponseAsync("**/empty.html"), Browser.GotoAsync(EmptyPage));
        Assert.That(response.Status, Is.EqualTo(200));
        Assert.That(intercepted, Is.True);
    }

    [Test]
    public async Task Should_unroute()
    {
        var intercepted = new List<int>();
        await Browser.RouteAsync("**/*", async route =>
        {
            intercepted.Add(1);
            await route.FallbackAsync();
        });
        await Browser.RouteAsync("**/empty.html", async route =>
        {
            intercepted.Add(2);
            await route.FallbackAsync();
        });
        await Browser.RouteAsync("**/empty.html", async route =>
        {
            intercepted.Add(3);
            await route.FallbackAsync();
        });
        await Browser.RouteAsync(new Regex("empty.html"), async route =>
        {
            intercepted.Add(4);
            await route.FallbackAsync();
        });
        await Browser.GotoAsync(EmptyPage);
        Assert.That(intercepted, Is.EqualTo(new[] { 4, 3, 2, 1 }));

        intercepted.Clear();
        await Browser.UnrouteAsync(new Regex("empty.html"));
        await Browser.GotoAsync(EmptyPage);
        Assert.That(intercepted, Is.EqualTo(new[] { 3, 2, 1 }));

        intercepted.Clear();
        await Browser.UnrouteAsync("**/empty.html");
        await Browser.GotoAsync(EmptyPage);
        Assert.That(intercepted, Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public async Task Should_not_support_question_mark_in_glob_pattern()
    {
        await ExpectedToFailAsync("index1hello", async () =>
        {
            await Browser.RouteAsync(new Regex(@"/index"), async route =>
            {
                var url = new Uri(route.Request.Url);
                var body = url.AbsolutePath switch
                {
                    "/index" => "index-no-hello",
                    "/index123hello" => "index123hello",
                    "/index1hello" => "index1hello",
                    _ => url.AbsolutePath[1..] + url.Query,
                };
                await route.FulfillAsync(new RouteFulfillResponse { Body = body, ContentType = "text/html" });
            });
            await Browser.RouteAsync("**/index?hello", route => route.FulfillAsync(new RouteFulfillResponse { Body = "intercepted any character" }));
            await Browser.RouteAsync(@"**/index\?hello", route => route.FulfillAsync(new RouteFulfillResponse { Body = "intercepted question mark" }));

            await Browser.GotoAsync($"{PREFIX}/index?hello");
            Contain("intercepted question mark", (await Browser.EvaluateAsync<string>("document.documentElement.outerHTML"))!);

            await Browser.GotoAsync($"{PREFIX}/index");
            Contain("index-no-hello", (await Browser.EvaluateAsync<string>("document.documentElement.outerHTML"))!);

            await Browser.GotoAsync($"{PREFIX}/index1hello");
            Contain("index1hello", (await Browser.EvaluateAsync<string>("document.documentElement.outerHTML"))!);

            await Browser.GotoAsync($"{PREFIX}/index123hello");
            Contain("index123hello", (await Browser.EvaluateAsync<string>("document.documentElement.outerHTML"))!);
        });
    }

    [Test]
    public async Task Should_work_when_POST_is_redirected_with_302()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.RouteAsync("**/*", route => route.ContinueAsync());
        await Browser.GotoAsync(Content("""
            <form action='/redirect?to=/empty.html' method='post'>
              <input type="hidden" id="foo" name="foo" value="FOOBAR">
            </form>
            """));
        var navigation = Browser.WaitForURLAsync("/empty.html");
        await Browser.EvaluateAsync<object>("() => { document.querySelector('form').submit(); }");
        await navigation;
    }

    [Test]
    public async Task Should_work_when_header_manipulation_headers_with_redirect()
    {
        await Browser.RouteAsync("**/*", route => route.ContinueAsync(new RouteContinueOverrides { Headers = WithHeaders(route.Request.Headers, ("foo", "bar")) }));
        await Browser.GotoAsync("/redirect?to=/empty.html");
    }

    [Test]
    public async Task Should_be_able_to_remove_headers()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.RouteAsync("**/*", async route =>
        {
            var headers = WithHeaders(route.Request.Headers);
            headers.Remove("foo");
            await route.ContinueAsync(new RouteContinueOverrides { Headers = headers });
        });
        var served = Served.Parse((await Browser.EvaluateAsync<string>(
            "(url) => fetch(url, { headers: { foo: 'bar' } }).then((response) => response.text())",
            $"{PREFIX}/cors-echo"))!);
        Assert.That(served.Header("foo"), Is.Null);
    }

    [Test]
    public async Task Should_contain_referer_header()
    {
        var requests = new ConcurrentQueue<WebRouteRequest>();
        await Browser.RouteAsync("**/*", async route =>
        {
            requests.Enqueue(route.Request);
            await route.ContinueAsync();
        });
        await Browser.GotoAsync("/one-style.html");
        var seen = requests.ToArray();
        Assert.That(seen[1].Url, Does.Contain("/one-style.css"));
        Assert.That(seen[1].Headers["referer"], Does.Contain("/one-style.html"));
    }

    [Test]
    public async Task Should_properly_return_navigation_response_when_URL_has_cookies()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.SetCookiesAsync([new BrowserCookie { Url = EmptyPage, Name = "foo", Value = "bar" }]);
        await Browser.RouteAsync("**/*", route => route.ContinueAsync());
        var response = await AllAsync(Browser.WaitForResponseAsync("**/empty.html"), Browser.ReloadAsync());
        Assert.That(response.Status, Is.EqualTo(200));
    }

    [Test]
    public async Task Should_not_override_cookie_header()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.EvaluateAsync<object>("() => { document.cookie = 'original=value'; }");
        string? cookieValueInRoute = null;
        await Browser.RouteAsync("**", async route =>
        {
            cookieValueInRoute = route.Request.Headers.GetValueOrDefault("cookie");
            await route.ContinueAsync(new RouteContinueOverrides { Headers = WithHeaders(route.Request.Headers, ("cookie", "overridden=value")) });
        });
        await Browser.GotoAsync("/cors-echo");
        var served = Served.Parse((await TextContentAsync())!);
        Assert.That(cookieValueInRoute, Is.EqualTo("original=value"));
        Assert.That(served.Header("cookie"), Is.EqualTo("original=value"));
    }

    [Test]
    public async Task Should_work_with_redirect_inside_sync_XHR()
    {
        await Browser.GotoAsync(EmptyPage);
        var continued = false;
        await Browser.RouteAsync("**/*", async route =>
        {
            await route.ContinueAsync();
            continued = true;
        });
        var status = await Browser.EvaluateAsync<int>("""
            () => {
              const request = new XMLHttpRequest();
              request.open('GET', '/redirect?to=/pptr.png', false);
              request.send(null);
              return request.status;
            }
            """);
        Assert.That(status, Is.EqualTo(200));
        Assert.That(continued, Is.True);
    }

    [Test]
    public async Task Should_pause_intercepted_XHR_until_continue()
    {
        await Browser.GotoAsync(EmptyPage);
        var routed = Deferred();
        var release = Deferred();
        await Browser.RouteAsync("**/global-var.html", async route =>
        {
            routed.TrySetResult();
            await release.Task;
            await route.ContinueAsync();
        });
        var xhrFinished = false;
        var statusPromise = Task.Run(async () =>
        {
            var status = await Browser.EvaluateAsync<int>("""
                () => {
                  const request = new XMLHttpRequest();
                  request.open('GET', '/global-var.html', false);
                  request.send(null);
                  return request.status;
                }
                """);
            xhrFinished = true;
            return status;
        });
        await routed.Task;
        await Task.Delay(500);
        Assert.That(xhrFinished, Is.False);
        release.TrySetResult();
        Assert.That(await statusPromise, Is.EqualTo(200));
    }

    [Test]
    public async Task Should_pause_intercepted_fetch_request_until_continue()
    {
        await Browser.GotoAsync(EmptyPage);
        var routed = Deferred();
        var release = Deferred();
        await Browser.RouteAsync("**/global-var.html", async route =>
        {
            routed.TrySetResult();
            await release.Task;
            await route.ContinueAsync();
        });
        var fetchFinished = false;
        var statusPromise = Task.Run(async () =>
        {
            var status = await Browser.EvaluateAsync<int>("async () => (await fetch('/global-var.html')).status");
            fetchFinished = true;
            return status;
        });
        await routed.Task;
        await Task.Delay(500);
        Assert.That(fetchFinished, Is.False);
        release.TrySetResult();
        Assert.That(await statusPromise, Is.EqualTo(200));
    }

    [Test]
    public async Task Should_be_abortable()
    {
        await Browser.RouteAsync(new Regex(@"\.css$"), route => route.AbortAsync());
        var response = await AllAsync(Browser.WaitForResponseAsync("**/one-style.html"), Browser.GotoAsync("/one-style.html"));
        Assert.That(response.Status, Is.EqualTo(200));
        Assert.That(await Browser.EvaluateAsync<string>("getComputedStyle(document.body).backgroundColor"), Is.Not.EqualTo("rgb(255, 192, 203)"));
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
            await route.AbortAsync();
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
    public async Task Should_fail_navigation_when_aborting_main_resource()
    {
        await Browser.RouteAsync("**/*", route => route.AbortAsync());
        var error = await Assert.CatchAsync(() => Browser.GotoAsync(EmptyPage));
        Assert.That(error!.Message, Does.Contain("net::ERR_FAILED"));
    }

    [Test]
    public async Task Should_not_work_with_redirects()
    {
        var intercepted = new ConcurrentQueue<WebRouteRequest>();
        await Browser.RouteAsync("**/*", async route =>
        {
            await route.ContinueAsync();
            intercepted.Enqueue(route.Request);
        });
        string[] chain = ["/non-existing-page.html", "/non-existing-page-2.html", "/non-existing-page-3.html", "/non-existing-page-4.html"];
        var start = "/empty.html";
        foreach (var from in chain.Reverse())
        {
            start = $"/redirect?from={from}&to={Uri.EscapeDataString(start)}";
        }

        var response = await AllAsync(Browser.WaitForResponseAsync("**/empty.html"), Browser.GotoAsync(start));
        Assert.That(response.Status, Is.EqualTo(200));
        Assert.That(response.Url, Does.Contain("empty.html"));

        var seen = intercepted.ToArray();
        Assert.That(seen, Has.Length.EqualTo(1));
        Assert.That(seen[0].Url, Does.Contain("/non-existing-page.html"));
    }

    [Test]
    public async Task Should_work_with_redirects_for_subresources()
    {
        var intercepted = new ConcurrentQueue<WebRouteRequest>();
        await Browser.RouteAsync("**/*", async route =>
        {
            await route.ContinueAsync();
            intercepted.Enqueue(route.Request);
        });
        string[] chain = ["/one-style.css", "/two-style.css", "/three-style.css"];
        var stylesheet = "/one-style.css";
        foreach (var from in chain.Reverse())
        {
            stylesheet = $"/redirect?from={from}&to={Uri.EscapeDataString(stylesheet)}";
        }

        var page = Content($"""<link rel="stylesheet" href="{stylesheet}"><div>hello, world!</div>""");
        var response = await AllAsync(Browser.WaitForResponseAsync("**/content?**"), Browser.GotoAsync(page));
        Assert.That(response.Status, Is.EqualTo(200));
        Assert.That(response.Url, Does.Contain("/content?"));

        var seen = intercepted.ToArray();
        Assert.That(seen, Has.Length.EqualTo(2));
        Assert.That(seen[0].Url, Does.Contain("/content?"));
        Assert.That(seen[1].Url, Does.Contain("/one-style.css"));
    }

    [Test]
    public async Task Should_work_with_equal_requests()
    {
        await Browser.GotoAsync(EmptyPage);
        var key = $"equal-{DateTime.UtcNow.Ticks}-{Random.Shared.NextDouble()}";
        var spinner = false;
        await Browser.RouteAsync("**/*", async route =>
        {
            await (spinner ? route.AbortAsync() : route.ContinueAsync());
            spinner = !spinner;
        });
        var results = new List<string?>();
        for (var i = 0; i < 3; i++)
        {
            results.Add(await Browser.EvaluateAsync<string>("(key) => fetch('/count?key=' + key).then((response) => response.text()).catch(() => 'FAILED')", key));
        }

        Assert.That(results, Is.EqualTo(new[] { "11", "FAILED", "22" }));
    }

    [Test]
    public async Task Should_be_able_to_fetch_dataURL_and_not_fire_dataURL_requests()
    {
        await Browser.GotoAsync(EmptyPage);
        var requests = new ConcurrentQueue<WebRouteRequest>();
        await Browser.RouteAsync("**/*", async route =>
        {
            requests.Enqueue(route.Request);
            await route.ContinueAsync();
        });
        var text = await Browser.EvaluateAsync<string>("(url) => fetch(url).then((response) => response.text())", "data:text/html,<div>yo</div>");
        Assert.That(text, Is.EqualTo("<div>yo</div>"));
        Assert.That(requests, Is.Empty);
    }

    [Test]
    public async Task Should_navigate_to_URL_with_hash_and_and_fire_requests_without_hash()
    {
        var requests = new ConcurrentQueue<WebRouteRequest>();
        await Browser.RouteAsync("**/*", async route =>
        {
            requests.Enqueue(route.Request);
            await route.ContinueAsync();
        });
        var response = await AllAsync(Browser.WaitForResponseAsync("**/empty.html"), Browser.GotoAsync($"{EmptyPage}#hash"));
        Assert.That(response.Status, Is.EqualTo(200));
        Assert.That(response.Url, Is.EqualTo(EmptyPage));
        var seen = requests.ToArray();
        Assert.That(seen, Has.Length.EqualTo(1));
        Assert.That(seen[0].Url, Is.EqualTo(EmptyPage));
    }

    [Test]
    public async Task Should_work_with_encoded_server()
    {
        await Browser.RouteAsync("**/*", route => route.ContinueAsync());
        var response = await AllAsync(Browser.WaitForResponseAsync(new Regex("nonexisting")), Browser.GotoAsync($"{PREFIX}/some nonexisting page"));
        Assert.That(response.Status, Is.EqualTo(404));
    }

    [Test]
    public async Task Should_work_with_badly_encoded_server()
    {
        await Browser.RouteAsync("**/*", route => route.ContinueAsync());
        var response = await AllAsync(Browser.WaitForResponseAsync(new Regex("rnd=")), Browser.GotoAsync($"{PREFIX}/status?rnd=%911"));
        Assert.That(response.Status, Is.EqualTo(200));
    }

    [Test]
    public async Task Should_work_with_encoded_server_2()
    {
        await Browser.GotoAsync(EmptyPage);
        var requests = new ConcurrentQueue<WebRouteRequest>();
        await Browser.RouteAsync("**/*", async route =>
        {
            await route.ContinueAsync();
            if (!route.Request.Url.Contains("/content?", StringComparison.Ordinal))
            {
                requests.Enqueue(route.Request);
            }
        });
        var response = await AllAsync(
            Browser.WaitForResponseAsync(new Regex(@"/fonts\?")),
            Browser.GotoAsync(Content($"""<link rel="stylesheet" href="{PREFIX}/fonts?helvetica|arial"/>""")));
        Assert.That(requests, Has.Count.EqualTo(1));
        Assert.That(response.Status, Is.EqualTo(404));
    }

    [Test]
    public async Task Should_not_throw_Invalid_Interception_Id_if_the_request_was_cancelled()
    {
        await Browser.GotoAsync(Content("<iframe></iframe>"));
        var routed = Deferred();
        var release = Deferred();
        await Browser.RouteAsync("**/*", async route =>
        {
            routed.TrySetResult();
            await release.Task;
            await route.ContinueAsync();
        });
        await Browser.EvaluateAsync<object>("(url) => { document.querySelector('iframe').src = url; }", EmptyPage);
        await routed.Task;
        await Browser.EvaluateAsync<object>("() => document.querySelector('iframe').remove()");
        release.TrySetResult();
        await Browser.EvaluateAsync<object>("() => new Promise((resolve) => setTimeout(resolve, 100))");
    }

    [Test]
    public async Task Should_intercept_main_resource_during_cross_process_navigation()
    {
        await Browser.GotoAsync(EmptyPage);
        var intercepted = false;
        await Browser.RouteAsync($"{CROSS}/empty.html", async route =>
        {
            intercepted = true;
            await route.ContinueAsync();
        });
        var response = await AllAsync(Browser.WaitForResponseAsync($"{CROSS}/empty.html"), Browser.GotoAsync($"{CROSS}/empty.html"));
        Assert.That(response.Status, Is.EqualTo(200));
        Assert.That(intercepted, Is.True);
    }

    [Test]
    public async Task Should_fulfill_with_redirect_status()
    {
        await Browser.GotoAsync("/title.html");
        await Browser.RouteAsync("**/*", async route =>
        {
            if (route.Request.Url != $"{PREFIX}/redirect_this")
            {
                await route.ContinueAsync();
                return;
            }

            await route.FulfillAsync(new RouteFulfillResponse { Status = 301, Headers = new Dictionary<string, string> { ["location"] = "/status?body=foo" } });
        });
        var text = await Browser.EvaluateAsync<string>("async (url) => (await fetch(url)).text()", $"{PREFIX}/redirect_this");
        Assert.That(text, Is.EqualTo("foo"));
    }

    [Test]
    public async Task Should_support_cors_with_GET()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.RouteAsync("**/cars*", async route =>
        {
            var headers = new Dictionary<string, string> { ["access-control-allow-origin"] = route.Request.Url.EndsWith("allow", StringComparison.Ordinal) ? "*" : "none" };
            await route.FulfillAsync(new RouteFulfillResponse { ContentType = "application/json", Headers = headers, Status = 200, Body = """["electric","gas"]""" });
        });
        var resp = await Browser.EvaluateAsync<string[]>("async () => (await fetch('https://example.com/cars?allow', { mode: 'cors' })).json()");
        Assert.That(resp, Is.EqualTo(new[] { "electric", "gas" }));
        var error = await Assert.CatchAsync(() => Browser.EvaluateAsync<string[]>("async () => (await fetch('https://example.com/cars?reject', { mode: 'cors' })).json()"));
        Assert.That(error!.Message, Does.Contain("Failed"));
    }

    [Test]
    public async Task Should_add_Access_Control_Allow_Origin_by_default_when_fulfill()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.RouteAsync("**/cars", route => route.FulfillAsync(new RouteFulfillResponse { ContentType = "application/json", Status = 200, Body = """["electric","gas"]""" }));
        var waiting = Browser.WaitForResponseAsync("https://example.com/cars");
        var result = await Browser.EvaluateAsync<string[]>("""
            async () => {
              const response = await fetch('https://example.com/cars', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                mode: 'cors',
                body: JSON.stringify({ number: 1 }),
              });
              return response.json();
            }
            """);
        var response = await waiting;
        Assert.That(result, Is.EqualTo(new[] { "electric", "gas" }));
        Assert.That(response.Headers["access-control-allow-origin"], Is.EqualTo(PREFIX));
    }

    [Test]
    public async Task Should_allow_null_origin_for_about_blank()
    {
        // e2e opens no page before the first goto, where Playwright starts on about:blank.
        await Browser.GotoAsync("about:blank");
        await Browser.RouteAsync("**/something", route => route.FulfillAsync(new RouteFulfillResponse { ContentType = "text/plain", Status = 200, Body = "done" }));
        var waiting = Browser.WaitForResponseAsync($"{CROSS}/something");
        var text = await Browser.EvaluateAsync<string>("""
            async (url) => {
              const data = await fetch(url, { method: 'GET', headers: { 'X-PINGOTHER': 'pingpong' } });
              return data.text();
            }
            """, $"{CROSS}/something");
        var response = await waiting;
        Assert.That(text, Is.EqualTo("done"));
        Assert.That(response.Headers["access-control-allow-origin"], Is.EqualTo("null"));
    }

    [Test]
    public async Task Should_respect_cors_overrides()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.RouteAsync("**/something", route => route.FulfillAsync(new RouteFulfillResponse
        {
            ContentType = "text/plain",
            Status = 200,
            Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "http://non-existent" },
            Body = "done",
        }));
        var error = await Assert.CatchAsync(() => Browser.EvaluateAsync<string>("""
            async (url) => {
              const data = await fetch(url, { method: 'GET', headers: { 'X-PINGOTHER': 'pingpong' } });
              return data.text();
            }
            """, $"{CROSS}/something"));
        Assert.That(error!.Message, Does.Contain("Failed to fetch"));
    }

    [Test]
    public async Task Should_not_auto_intercept_non_preflight_OPTIONS_without_network_interception()
    {
        await Browser.GotoAsync(EmptyPage);
        var key = $"options-{DateTime.UtcNow.Ticks}-{Random.Shared.NextDouble()}";
        var texts = await Browser.EvaluateAsync<string[]>(OptionsRequests, $"{CROSS}/options?key={key}");
        Assert.That(texts![0], Is.EqualTo("Hello"));
        Assert.That(texts[1], Is.EqualTo("World"));
        Assert.That(
            await Browser.EvaluateAsync<string[]>("(key) => fetch(`/options-log?key=${key}`).then((response) => response.json())", key),
            Is.EqualTo(new[] { "OPTIONS:/options", "OPTIONS:/options", "GET:/options" }));
    }

    [Test]
    public async Task Should_not_auto_intercept_non_preflight_OPTIONS_with_network_interception()
    {
        await Browser.GotoAsync(EmptyPage);
        var key = $"options-{DateTime.UtcNow.Ticks}-{Random.Shared.NextDouble()}";
        await Browser.RouteAsync("**/options?*", route => route.ContinueAsync());
        var texts = await Browser.EvaluateAsync<string[]>(OptionsRequests, $"{CROSS}/options?key={key}");
        Assert.That(texts![0], Is.EqualTo("Hello"));
        Assert.That(texts[1], Is.EqualTo("World"));
        var requests = await Browser.EvaluateAsync<string[]>("(key) => fetch(`/options-log?key=${key}`).then((response) => response.json())", key);
        Assert.That(requests, Is.EqualTo(new[] { "OPTIONS:/options", "GET:/options" }));
    }

    private const string OptionsRequests = """
        async (url) => {
          const response1 = await fetch(url, { method: 'OPTIONS' });
          const text1 = await response1.text();
          const response2 = await fetch(url, { method: 'GET' });
          const text2 = await response2.text();
          return [text1, text2];
        }
        """;

    private const string PostCars = """
        async () => {
          const response = await fetch('https://example.com/cars', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            mode: 'cors',
            body: JSON.stringify({ number: 1 }),
          });
          return response.json();
        }
        """;

    [Test]
    public async Task Should_support_cors_with_POST()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.RouteAsync("**/cars", route => route.FulfillAsync(new RouteFulfillResponse
        {
            ContentType = "application/json",
            Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
            Status = 200,
            Body = """["electric","gas"]""",
        }));
        var resp = await Browser.EvaluateAsync<string[]>(PostCars);
        Assert.That(resp, Is.EqualTo(new[] { "electric", "gas" }));
    }

    [Test]
    public async Task Should_support_cors_with_credentials()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.RouteAsync("**/cars", route => route.FulfillAsync(new RouteFulfillResponse
        {
            ContentType = "application/json",
            Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = PREFIX, ["Access-Control-Allow-Credentials"] = "true" },
            Status = 200,
            Body = """["electric","gas"]""",
        }));
        var resp = await Browser.EvaluateAsync<string[]>("""
            async () => {
              const response = await fetch('https://example.com/cars', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                mode: 'cors',
                body: JSON.stringify({ number: 1 }),
                credentials: 'include',
              });
              return response.json();
            }
            """);
        Assert.That(resp, Is.EqualTo(new[] { "electric", "gas" }));
    }

    [Test]
    public async Task Should_not_abort_preflighted_cross_origin_redirect_when_a_route_is_registered()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.RouteAsync("**/never-matches", route => route.ContinueAsync());
        var result = await Browser.EvaluateAsync<string>("""
            async (url) => {
              const response = await fetch(url, { credentials: 'include', headers: { 'X-App-Version': '1.2.3' } });
              const served = await response.json();
              return response.status + ' ' + served.method + ' ' + served.headers['x-app-version'];
            }
            """, $"{CROSS}/bounce?to={Uri.EscapeDataString($"{PREFIX}/cors-echo?preflight")}");
        Assert.That(result, Is.EqualTo("200 GET 1.2.3"));
    }

    [Test]
    public async Task Should_reject_cors_with_disallowed_credentials()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.RouteAsync("**/cars", route => route.FulfillAsync(new RouteFulfillResponse
        {
            ContentType = "application/json",
            Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = PREFIX },
            Status = 200,
            Body = """["electric","gas"]""",
        }));
        var error = await Assert.CatchAsync(() => Browser.EvaluateAsync<string[]>("""
            async () => {
              const response = await fetch('https://example.com/cars', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                mode: 'cors',
                body: JSON.stringify({ number: 1 }),
                credentials: 'include',
              });
              return response.json();
            }
            """));
        Assert.That(error, Is.Not.Null);
    }

    [Test]
    public async Task Should_support_cors_for_different_methods()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.RouteAsync("**/cars", route => route.FulfillAsync(new RouteFulfillResponse
        {
            ContentType = "application/json",
            Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
            Status = 200,
            Json = new[] { route.Request.Method, "electric", "gas" },
        }));
        var post = await Browser.EvaluateAsync<string[]>(PostCars);
        Assert.That(post, Is.EqualTo(new[] { "POST", "electric", "gas" }));
        var remove = await Browser.EvaluateAsync<string[]>("""
            async () => {
              const response = await fetch('https://example.com/cars', { method: 'DELETE', headers: {}, mode: 'cors', body: '' });
              return response.json();
            }
            """);
        Assert.That(remove, Is.EqualTo(new[] { "DELETE", "electric", "gas" }));
    }

    [Test]
    public async Task Should_contain_raw_request_header()
    {
        IReadOnlyDictionary<string, string>? headers = null;
        await Browser.RouteAsync("**/*", async route =>
        {
            headers = route.Request.Headers;
            await route.ContinueAsync();
        });
        await Browser.GotoAsync("/empty.html");
        Assert.That(headers!["accept"], Is.Not.Empty);
    }

    [TestCase("fulfill", TestName = "Route_fulfill_should_throw_if_called_twice")]
    [TestCase("continue", TestName = "Route_continue_should_throw_if_called_twice")]
    [TestCase("fallback", TestName = "Route_fallback_should_throw_if_called_twice")]
    [TestCase("abort", TestName = "Route_abort_should_throw_if_called_twice")]
    public async Task Route_method_should_throw_if_called_twice(string method)
    {
        var handled = Deferred<Exception>();
        await Browser.RouteAsync("**/*", async route =>
        {
            Task Decide() => method switch
            {
                "fulfill" => route.FulfillAsync(new RouteFulfillResponse()),
                "continue" => route.ContinueAsync(),
                "fallback" => route.FallbackAsync(),
                _ => route.AbortAsync(),
            };

            await Decide();
            try
            {
                await Decide();
                handled.TrySetResult(new InvalidOperationException("decided twice"));
            }
            catch (Exception caught)
            {
                handled.TrySetResult(caught);
            }
        });
        var navigation = Browser.GotoAsync("/empty.html").ContinueWith(_ => { }, TaskScheduler.Default);
        var e = await handled.Task;
        Assert.That(e.Message, Does.Contain("called twice"));
        await navigation;
    }

    [Test]
    public async Task Should_intercept_when_postData_is_more_than_1MB()
    {
        await Browser.GotoAsync(EmptyPage);
        var intercepted = Deferred<string?>();
        var postBody = new string('0', 2 * 1024 * 1024);
        await Browser.RouteAsync("**/404.html", async route =>
        {
            await route.AbortAsync();
            intercepted.TrySetResult(route.Request.PostData);
        });
        await Browser.EvaluateAsync<object>("(body) => fetch('/404.html', { method: 'POST', body }).then(() => null, () => null)", postBody);
        Assert.That(await intercepted.Task, Is.EqualTo(postBody));
    }
}
