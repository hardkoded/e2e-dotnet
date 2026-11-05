// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.RequestFulfill;

/// <summary>
/// <c>test.describe('page request fulfill')</c> of upstream's <c>apps/testbed/tests/request-fulfill.e2e.ts</c> (Playwright's
/// <c>page-request-fulfill.spec.ts</c>). Not ported, as upstream: should allow
/// mocking binary responses (needs a snapshot match), should fulfill with global fetch result, should fulfill with fetch result, should fulfill with fetch
/// result and overrides (no <c>fulfill({ response })</c>), headerValue should return set-cookie from intercepted response (Playwright
/// expects it to fail on Chromium), should fulfill with har response (HAR), should fulfill with gzip and readback.
/// </summary>
public sealed class PageRequestFulfillTests : PlaywrightTest
{
    private static string EmptyPage => $"{PREFIX}/empty.html";

    /// <summary>Polls the page until <paramref name="expression"/> is truthy, Playwright's <c>page.waitForFunction</c>.</summary>
    private static string WaitForFunction(string expression) =>
        $"() => new Promise((resolve) => {{ const check = () => ({expression}) ? resolve(true) : setTimeout(check, 20); check(); }})";

    [Test]
    public async Task Should_work()
    {
        await Browser.RouteAsync("**/*", route => route.FulfillAsync(new RouteFulfillResponse
        {
            Status = 201,
            Headers = new Dictionary<string, string> { ["foo"] = "bar" },
            ContentType = "text/html",
            Body = "Yo, page!",
        }));
        var response = await AllAsync(Browser.WaitForResponseAsync("**/empty.html"), Browser.GotoAsync(EmptyPage));
        Assert.That(response.Status, Is.EqualTo(201));
        Assert.That(response.Headers["foo"], Is.EqualTo("bar"));
        Assert.That(await Browser.EvaluateAsync<string>("() => document.body.textContent"), Is.EqualTo("Yo, page!"));
    }

    [Test]
    public async Task Should_work_with_buffer_as_body()
    {
        await Browser.RouteAsync("**/*", route => route.FulfillAsync(new RouteFulfillResponse { Status = 200, ContentType = "text/plain", BodyBytes = "Yo, page!"u8.ToArray() }));
        var response = await AllAsync(Browser.WaitForResponseAsync("**/empty.html"), Browser.GotoAsync(EmptyPage));
        Assert.That(response.Status, Is.EqualTo(200));
        Assert.That(await Browser.EvaluateAsync<string>("() => document.body.textContent"), Is.EqualTo("Yo, page!"));
    }

    [Test]
    public async Task Should_work_with_status_code_422()
    {
        await Browser.RouteAsync("**/*", route => route.FulfillAsync(new RouteFulfillResponse { Status = 422, Body = "Yo, page!" }));
        var response = await AllAsync(Browser.WaitForResponseAsync("**/empty.html"), Browser.GotoAsync(EmptyPage));
        Assert.That(response.Status, Is.EqualTo(422));
        Assert.That(await Browser.EvaluateAsync<string>("() => document.body.textContent"), Is.EqualTo("Yo, page!"));
    }

    [Test]
    public async Task Should_fulfill_with_unuassigned_status_codes()
    {
        var fulfilled = Deferred<Exception?>();
        await Browser.RouteAsync("**/data.json", async route =>
        {
            try
            {
                await route.FulfillAsync(new RouteFulfillResponse { Status = 430, Body = "Yo, page!" });
                fulfilled.TrySetResult(null);
            }
            catch (Exception caught)
            {
                fulfilled.TrySetResult(caught);
            }
        });
        await Browser.GotoAsync(EmptyPage);
        var response = await Browser.EvaluateAsync<JsonElement>(
            "async (url) => { const { status, statusText } = await fetch(url); return { status, statusText }; }",
            $"{PREFIX}/data.json");
        var error = await fulfilled.Task;
        Assert.That(error, Is.Null);
        Assert.That(response.GetProperty("status").GetInt32(), Is.EqualTo(430));
        Assert.That(response.GetProperty("statusText").GetString(), Is.EqualTo("Unknown"));
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
            await route.FulfillAsync(new RouteFulfillResponse { Status = 200 });
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
    public async Task Should_allow_mocking_svg_with_charset()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.RouteAsync("**/*", route => route.FulfillAsync(new RouteFulfillResponse
        {
            ContentType = "image/svg+xml ; charset=utf-8",
            Body = """<svg width="50" height="50" version="1.1" xmlns="http://www.w3.org/2000/svg"><rect x="10" y="10" width="30" height="30" stroke="black" fill="transparent" stroke-width="5"/></svg>""",
        }));
        var size = await Browser.EvaluateAsync<int[]>("""
            (PREFIX) => {
              const img = document.createElement('img');
              img.src = PREFIX + '/does-not-exist.svg';
              document.body.appendChild(img);
              return new Promise((resolve, reject) => { img.onload = () => resolve([img.naturalWidth, img.naturalHeight]); img.onerror = reject; });
            }
            """, PREFIX);
        Assert.That(size, Is.EqualTo(new[] { 50, 50 }));
    }

    [Test]
    public async Task Should_work_with_file_path()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.RouteAsync("**/*", route => route.FulfillAsync(new RouteFulfillResponse { ContentType = "shouldBeIgnored", Path = Asset("pptr.png") }));
        var size = await Browser.EvaluateAsync<int[]>("""
            (PREFIX) => {
              const img = document.createElement('img');
              img.src = PREFIX + '/does-not-exist.png';
              document.body.appendChild(img);
              return new Promise((resolve) => { img.onload = () => resolve([img.naturalWidth, img.naturalHeight]); });
            }
            """, PREFIX);
        Assert.That(size, Is.EqualTo(new[] { 69, 100 }));
    }

    [Test]
    public async Task Should_stringify_intercepted_request_response_headers()
    {
        await Browser.RouteAsync("**/*", route => route.FulfillAsync(new RouteFulfillResponse
        {
            Status = 200,
            Headers = new Dictionary<string, string> { ["foo"] = "true" },
            Body = "Yo, page!",
        }));
        var response = await AllAsync(Browser.WaitForResponseAsync("**/empty.html"), Browser.GotoAsync(EmptyPage));
        Assert.That(response.Status, Is.EqualTo(200));
        Assert.That(response.Headers["foo"], Is.EqualTo("true"));
        Assert.That(await Browser.EvaluateAsync<string>("() => document.body.textContent"), Is.EqualTo("Yo, page!"));
    }

    [Test]
    public async Task Should_not_modify_the_headers_sent_to_the_server()
    {
        await Browser.GotoAsync("/empty.html");
        await Browser.RouteAsync($"{PREFIX}/unused", route => route.ContinueAsync());
        const string fetchServed = "async (url) => { const data = await fetch(url); return data.text(); }";
        var before = Served.Parse((await Browser.EvaluateAsync<string>(fetchServed, $"{CROSS}/cors-echo"))!);
        Assert.That(before.Method, Is.EqualTo("GET"));

        await Browser.RouteAsync($"{CROSS}/cors-echo", route => route.ContinueAsync(new RouteContinueOverrides { Headers = WithHeaders(route.Request.Headers) }));
        var after = Served.Parse((await Browser.EvaluateAsync<string>(fetchServed, $"{CROSS}/cors-echo"))!);
        Assert.That(after.Method, Is.EqualTo("GET"));
        Assert.That(after.Headers, Is.EqualTo(before.Headers));
    }

    [Test]
    public async Task Should_include_the_origin_header()
    {
        await Browser.GotoAsync("/empty.html");
        string? origin = null;
        await Browser.RouteAsync($"{CROSS}/something", async route =>
        {
            origin = route.Request.Headers.GetValueOrDefault("origin");
            await route.FulfillAsync(new RouteFulfillResponse
            {
                Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
                ContentType = "text/plain",
                Body = "done",
            });
        });
        var text = await Browser.EvaluateAsync<string>("async (url) => { const data = await fetch(url); return data.text(); }", $"{CROSS}/something");
        Assert.That(text, Is.EqualTo("done"));
        Assert.That(origin, Is.EqualTo(PREFIX));
    }

    [Test]
    public async Task Should_fetch_original_request_and_fulfill()
    {
        await Browser.RouteAsync("**/*", async route =>
        {
            var response = await route.FetchAsync();
            await route.FulfillAsync(new RouteFulfillResponse
            {
                Status = response.Status,
                Headers = new Dictionary<string, string>(response.Headers),
                Body = await response.TextAsync(),
            });
        });
        var response = await AllAsync(Browser.WaitForResponseAsync("**/title.html"), Browser.GotoAsync("/title.html"));
        Assert.That(response.Status, Is.EqualTo(200));
        Assert.That(await Browser.TitleAsync(), Is.EqualTo("Woof-Woof"));
    }

    [Test]
    public async Task Should_fulfill_with_multiple_set_cookie()
    {
        string[] cookies = ["a=b", "c=d"];
        await Browser.RouteAsync("**/multiple-set-cookie.html", route => route.FulfillAsync(new RouteFulfillResponse
        {
            Status = 200,
            Headers = new Dictionary<string, string>
            {
                ["X-Header-1"] = "v1",
                ["Set-Cookie"] = string.Join('\n', cookies),
                ["X-Header-2"] = "v2",
            },
            Body = "",
        }));
        var response = await AllAsync(Browser.WaitForResponseAsync("**/multiple-set-cookie.html"), Browser.GotoAsync("/multiple-set-cookie.html"));
        var cookie = await Browser.EvaluateAsync<string>("() => document.cookie");
        Assert.That(cookie!.Split(';').Select(s => s.Trim()).Order(), Is.EqualTo(cookies));
        Assert.That(response.Headers["x-header-1"], Is.EqualTo("v1"));
        Assert.That(response.Headers["x-header-2"], Is.EqualTo("v2"));
    }

    [Test]
    public async Task Should_fulfill_with_fetch_response_that_has_multiple_set_cookie()
    {
        await Browser.RouteAsync("**/respond?*", async route =>
        {
            var response = await route.FetchAsync();
            await route.FulfillAsync(new RouteFulfillResponse
            {
                Status = response.Status,
                Headers = new Dictionary<string, string>(response.Headers),
                Body = await response.TextAsync(),
            });
        });
        var headers = JsonSerializer.Serialize(new Dictionary<string, object> { ["set-cookie"] = new[] { "a=b", "c=d" }, ["content-type"] = "text/html" });
        await Browser.GotoAsync($"/respond?headers={Uri.EscapeDataString(headers)}");
        var cookie = await Browser.EvaluateAsync<string>("() => document.cookie");
        Assert.That(cookie!.Split(';').Select(s => s.Trim()).Order(), Is.EqualTo(new[] { "a=b", "c=d" }));
    }

    [Test]
    public async Task Should_fulfill_preload_link_requests()
    {
        var intercepted = false;
        await Browser.RouteAsync("**/one-style.css", async route =>
        {
            intercepted = true;
            await route.FulfillAsync(new RouteFulfillResponse
            {
                Status = 200,
                Headers = new Dictionary<string, string>
                {
                    ["content-type"] = "text/css; charset=utf-8",
                    ["cache-control"] = "no-cache, no-store",
                    ["custom"] = "value",
                },
                Body = "body { background-color: green; }",
            });
        });
        var response = await AllAsync(Browser.WaitForResponseAsync("**/one-style.css"), Browser.GotoAsync("/preload.html"));
        Assert.That(response.Headers["custom"], Is.EqualTo("value"));
        await Browser.EvaluateAsync<bool>(WaitForFunction("window.preloadedStyles"));
        Assert.That(intercepted, Is.True);
        // preload.html sets the flag before switching the link to a stylesheet, which applies a task later.
        await Expect.Poll(() => Browser.EvaluateAsync<string>("() => window.getComputedStyle(document.body).backgroundColor")).ToMatchAsync(Is.EqualTo("rgb(0, 128, 0)"));
    }

    [Test]
    public async Task Should_fulfill_json()
    {
        await Browser.GotoAsync(EmptyPage);
        await Browser.RouteAsync("**/data.json", route => route.FulfillAsync(new RouteFulfillResponse
        {
            Status = 201,
            Headers = new Dictionary<string, string> { ["foo"] = "bar" },
            Json = new { bar = "baz" },
        }));
        var waiting = Browser.WaitForResponseAsync("**/*");
        var body = await Browser.EvaluateAsync<string>("() => fetch('./data.json').then((r) => r.text())");
        var response = await waiting;
        Assert.That(response.Status, Is.EqualTo(201));
        Assert.That(response.Headers["content-type"], Is.EqualTo("application/json"));
        Assert.That(body, Is.EqualTo("""{"bar":"baz"}"""));
    }

    [Test]
    public async Task Should_not_go_to_the_network_for_fulfilled_requests_body()
    {
        var key = $"fulfilled-{DateTime.UtcNow.Ticks}-{Random.Shared.NextDouble()}";
        await Browser.RouteAsync($"**/count?key={key}", route => route.FulfillAsync(new RouteFulfillResponse { Status = 404, ContentType = "text/plain", Body = "Not Found! (mocked)" }));
        var response = await AllAsync(
            Browser.WaitForResponseAsync($"**/count?key={key}"),
            Browser.GotoAsync(Content($"""<link rel="stylesheet" href="/count?key={key}"><div>hello, world!</div>""")));
        Assert.That(await response.TextAsync(), Is.EqualTo("Not Found! (mocked)"));
        await Browser.UnrouteAllAsync();
        var serverHits = await Browser.EvaluateAsync<string>("(key) => fetch('/count?key=' + key).then((r) => r.text())", key);
        Assert.That(serverHits, Is.EqualTo("11"));
    }

    [Test]
    public async Task Should_return_body_for_fulfilled_responses()
    {
        foreach (var status in new[] { 100, 200, 404, 500 })
        {
            var bodyOverride = $"Custom body {status}";
            await Browser.RouteAsync("**/one-style.css", route => route.FulfillAsync(new RouteFulfillResponse { Status = status, ContentType = "text/plain", Body = bodyOverride }));
            var response = await AllAsync(Browser.WaitForResponseAsync("**/one-style.css"), Browser.GotoAsync("/one-style.html"));
            Assert.That(await response.TextAsync(), Is.EqualTo(bodyOverride));
            await Browser.UnrouteAllAsync();
        }
    }
}
