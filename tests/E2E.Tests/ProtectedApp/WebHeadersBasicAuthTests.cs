// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using E2E.Engine;

namespace E2E.Tests.ProtectedApp;

[Collection(BrowserCollection.Name)]
public sealed class WebHeadersBasicAuthTests
{
    [Fact]
    public async Task Sends_the_headers_to_the_apps_site_only_on_every_context_of_the_attempt()
    {
        // The same server under another host name stands in for a third-party site: `localhost` is a site of its own, `127.0.0.1` another.
        static Task Echo(HttpListenerContext context)
        {
            return TinySite.RespondAsync(context, $"<!DOCTYPE html><html><body><h1>{context.Request.Headers["X-Fixture-Header"] ?? "none"}</h1></body></html>");
        }

        using var app = await TinySite.StartAsync(Echo);
        using var other = await TinySite.StartAsync(Echo, host: "localhost");
        await using var session = await new WebEngine(new WebEngineOptions
        {
            Headless = true,
            Headers = new Dictionary<string, string> { ["X-Fixture-Header"] = "let-me-in" },
        }).StartAsync(new EngineStartOptions { BaseUrl = app.Url, ActionTimeout = TimeSpan.FromSeconds(10) }, CancellationToken.None);

        async Task<string> HeadingAtAsync(string url)
        {
            await session.OpenAsync(url, CancellationToken.None);
            var observation = await session.ObserveAsync(CancellationToken.None);
            return observation.Roots.Single(node => node.Role == "heading").Name ?? "";
        }

        Assert.Equal("let-me-in", await HeadingAtAsync(app.Url));
        // A third-party site gets the request without them.
        Assert.Equal("none", await HeadingAtAsync(other.Url));
        // A state reset replaces the context; the headers come along.
        await ((IBrowserSession)session).ClearStateAsync(CancellationToken.None);
        Assert.Equal("let-me-in", await HeadingAtAsync(app.Url));
    }

    [Fact]
    public async Task Runs_the_page_in_the_configured_locale_and_time_zone_after_a_context_reset_too()
    {
        using var site = await TinySite.StartAsync(context => TinySite.RespondAsync(context, "<!DOCTYPE html><html><body></body></html>"));
        await using var session = (IBrowserSession)await WebSemanticsTests.OpenAsync(site.Url, new WebEngineOptions
        {
            Headless = true,
            Locale = "de-DE",
            TimezoneId = "Asia/Tokyo",
        });
        const string Read = "() => [navigator.language, Intl.DateTimeFormat().resolvedOptions().timeZone, new Date('2026-01-01T00:00:00Z').getTimezoneOffset()].join(' ')";
        const string Expected = "de-DE Asia/Tokyo -540";

        Assert.Equal(Expected, (await session.EvaluateAsync(Read, null, false, CancellationToken.None))?.GetString());
        await session.ClearStateAsync(CancellationToken.None);
        await session.OpenAsync(site.Url, CancellationToken.None);
        Assert.Equal(Expected, (await session.EvaluateAsync(Read, null, false, CancellationToken.None))?.GetString());
    }
}
