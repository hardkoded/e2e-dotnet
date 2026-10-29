// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text;
using E2E;
using E2E.Engine;
using E2E.NUnit.Tests.SkipAfterFailure;

namespace E2E.NUnit.Tests.WebPlatform;

/// <summary>
/// The test of upstream's <c>web-platform.test.ts</c> that needs a teardown, as an NUnit body would write it: the <c>afterEach</c> navigates
/// away from the page the handler assertion failed on. The fixture is explicit, so a normal run never starts it.
/// </summary>
[Explicit("Run by WebPlatformTests")]
public sealed class LateHandlerFixture : E2ETest
{
    private static readonly E2EConfig FixtureConfig = E2EConfig.Parse(
        """{ "cache": { "mode": "off" } }""",
        AppContext.BaseDirectory,
        _ => null);

    private static readonly Lazy<string> Site = new(StartSite);

    /// <summary>Where the fixture's attempt writes its evidence; the run's report.json goes beside it.</summary>
    public static string Results { get; } = Path.Combine(Path.GetTempPath(), "e2e-web-platform-" + Guid.NewGuid().ToString("N"), "results");

    protected override E2EConfig Config => FixtureConfig;

    protected override string? BaseUrl => Site.Value;

    protected override string? ResultsDirectory => Results;

    [TearDown]
    public async Task OpenHome()
    {
        await App.OpenAsync("/");
    }

    [Test]
    public async Task A_route_handler_assertion_that_no_step_follows_then_a_teardown_that_navigates()
    {
        await Browser.RouteAsync("**/api/flags.js", async route =>
        {
            await route.FulfillAsync(new RouteFulfillResponse { Body = "", ContentType = "text/javascript" });

            // The port has no value matcher (upstream's expect(value).toBe), so the assertion throws the ASSERTION_FAILED it would.
            if (!string.Equals(route.Request.Method, "POST", StringComparison.Ordinal))
            {
                throw new TestException("ASSERTION_FAILED", "expect(received).toBe(expected)\nExpected: \"POST\"\nReceived: \"" + route.Request.Method + "\"");
            }
        });
        await App.OpenAsync("/script-flags");
    }

    private static string StartSite()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var url = "http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/";
        var http = new HttpListener();
        http.Prefixes.Add(url);
        http.Start();
        _ = Task.Run(async () =>
        {
            while (true)
            {
                var context = await http.GetContextAsync();
                var html = context.Request.Url!.AbsolutePath switch
                {
                    "/script-flags" => "<!doctype html><html><head><title>Flags</title><script src=\"/api/flags.js\"></script></head><body><h1>Flags</h1></body></html>",
                    "/" => "<!doctype html><html><head><title>Home</title></head><body><h1>Home</h1></body></html>",
                    _ => null,
                };
                if (html is null)
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    continue;
                }

                var page = Encoding.UTF8.GetBytes(html);
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = page.Length;
                await context.Response.OutputStream.WriteAsync(page);
                context.Response.Close();
            }
        });
        return url;
    }
}
