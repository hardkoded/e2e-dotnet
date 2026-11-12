// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using E2E.Engine;
using static E2E.Tests.ProtectedApp.ProtectedAppFixture;

namespace E2E.Tests.ProtectedApp;

/// <summary>
/// Reaching an app behind a gate: configured request headers ride requests that start at the app's origin,
/// on every context the attempt opens, and basic-auth credentials answer only its challenge. A request that
/// starts at another origin gets neither unless <c>CredentialOrigins</c> lists it; a redirect keeps the headers
/// its request started with. A second fixture instance stands in for another origin, on the same host under
/// another port, and as <c>localhost</c> rather than <c>127.0.0.1</c> for a third-party site.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class WebHeadersBasicAuthTests
{
    [Fact]
    public async Task Sends_the_headers_to_the_apps_origin_only_on_every_context_of_the_attempt()
    {
        using var app = await StartAsync();
        using var otherPort = await StartAsync();
        using var otherSite = await StartAsync("localhost");
        await using var session = await StartSessionAsync(app, new WebEngineOptions { Headless = true, Headers = new Dictionary<string, string> { ["X-Fixture-Header"] = "let-me-in" } });

        Assert.Equal("let-me-in", await HeadingAtAsync(session, app.Url + "headers"));
        // Another port on the same host, and a third-party site, get the request without them.
        Assert.Equal("none", await HeadingAtAsync(session, otherPort.Url + "headers"));
        Assert.Equal("none", await HeadingAtAsync(session, otherSite.Url + "headers"));
        // A state reset replaces the context; the headers come along.
        await session.ClearStateAsync(CancellationToken.None);
        Assert.Equal("let-me-in", await HeadingAtAsync(session, app.Url + "headers"));
    }

    [Fact]
    public async Task Sends_the_headers_to_an_origin_credentialOrigins_lists_and_no_other()
    {
        using var app = await StartAsync();
        using var otherPort = await StartAsync();
        using var otherSite = await StartAsync("localhost");
        await using var session = await StartSessionAsync(app, new WebEngineOptions
        {
            Headless = true,
            Headers = new Dictionary<string, string> { ["x-fixture-header"] = "listed" },
            CredentialOrigins = [otherPort.Url],
        });

        Assert.Equal("listed", await HeadingAtAsync(session, otherPort.Url + "headers"));
        Assert.Equal("none", await HeadingAtAsync(session, otherSite.Url + "headers"));
    }

    [Fact]
    public async Task Follows_the_origin_a_request_starts_on_through_its_redirects()
    {
        using var app = await StartAsync();
        using var otherSite = await StartAsync("localhost");
        await using var session = await StartSessionAsync(app, new WebEngineOptions { Headless = true, Headers = new Dictionary<string, string> { ["x-fixture-header"] = "rides-redirects" } });

        // Playwright reapplies a routed request's headers to its redirects without routing them:
        // they reach whatever origin a redirect names, and a redirect into the app's origin gets none.
        app.Redirects["/to-other-headers"] = otherSite.Url + "headers";
        Assert.Equal("rides-redirects", await HeadingAtAsync(session, app.Url + "to-other-headers"));
        otherSite.Redirects["/to-app-headers"] = app.Url + "headers";
        Assert.Equal("none", await HeadingAtAsync(session, otherSite.Url + "to-app-headers"));
    }

    [Fact]
    public async Task Keeps_the_headers_on_a_request_an_attempt_route_lets_through()
    {
        using var app = await StartAsync();
        await using var session = await StartSessionAsync(app, new WebEngineOptions { Headless = true, Headers = new Dictionary<string, string> { ["x-fixture-header"] = "through-the-route" } });
        var routed = 0;
        // Registered after the header route, so it runs first, as a browser route handler does.
        await ((IBrowserSession)session).RouteAsync(
            global::E2E.Internal.Routes.Matcher("**/headers"),
            route =>
            {
                routed++;
                return route.FallbackAsync();
            },
            CancellationToken.None);

        Assert.Equal("through-the-route", await HeadingAtAsync(session, app.Url + "headers"));
        Assert.Equal(1, routed);
    }

    [Fact]
    public async Task Blocks_service_workers_under_headers_since_routing_never_sees_a_workers_requests()
    {
        using var app = await StartAsync();
        // Registers the fixture's worker and counts what the browser now holds:
        // a blocked registration resolves like a real one but registers nothing.
        const string Registrations = """
            async () => {
              await navigator.serviceWorker.register('/sw.js').catch(() => undefined);
              return (await navigator.serviceWorker.getRegistrations()).length;
            }
            """;
        await using (var plain = await StartSessionAsync(app, new WebEngineOptions { Headless = true }))
        {
            await HeadingAtAsync(plain, app.Url);
            // The fixture itself can register one: 127.0.0.1 is a secure context.
            Assert.Equal(1, await WebEngine.SurfaceOf(plain)!.Page().EvaluateAsync<int>(Registrations));
        }

        await using var session = await StartSessionAsync(app, new WebEngineOptions { Headless = true, Headers = new Dictionary<string, string> { ["x-fixture-header"] = "no-workers" } });
        await HeadingAtAsync(session, app.Url);
        Assert.Equal(0, await WebEngine.SurfaceOf(session)!.Page().EvaluateAsync<int>(Registrations));
    }

    [Fact]
    public async Task Answers_a_basic_auth_challenge_from_the_apps_origin_only()
    {
        using var app = await StartAsync();
        using var otherPort = await StartAsync();
        using var otherSite = await StartAsync("localhost");
        await using (var unauthenticated = await StartSessionAsync(app, new WebEngineOptions { Headless = true }))
        {
            Assert.Equal("Unauthorized", await HeadingAtAsync(unauthenticated, app.Url + "protected"));
        }

        await using var session = await StartSessionAsync(app, new WebEngineOptions { Headless = true, BasicAuth = new WebBasicAuth(Username, Password) });
        Assert.Equal("Protected", await HeadingAtAsync(session, app.Url + "protected"));
        Assert.Equal("Unauthorized", await HeadingAtAsync(session, otherPort.Url + "protected"));
        Assert.Equal("Unauthorized", await HeadingAtAsync(session, otherSite.Url + "protected"));
        // The challenge comes from the origin a redirect lands on, so redirects do not carry the credential.
        app.Redirects["/to-other-protected"] = otherPort.Url + "protected";
        Assert.Equal("Unauthorized", await HeadingAtAsync(session, app.Url + "to-other-protected"));
    }

    [Fact]
    public async Task Answers_a_challenge_from_an_origin_credentialOrigins_lists_and_no_other()
    {
        using var app = await StartAsync();
        using var otherPort = await StartAsync();
        using var otherSite = await StartAsync("localhost");
        await using var session = await StartSessionAsync(app, new WebEngineOptions
        {
            Headless = true,
            BasicAuth = new WebBasicAuth(Username, Password),
            CredentialOrigins = [otherPort.Url],
        });

        Assert.Equal("Protected", await HeadingAtAsync(session, app.Url + "protected"));
        Assert.Equal("Protected", await HeadingAtAsync(session, otherPort.Url + "protected"));
        Assert.Equal("Unauthorized", await HeadingAtAsync(session, otherSite.Url + "protected"));
    }

    [Fact]
    public async Task Answers_the_challenge_with_a_secret_password()
    {
        using var app = await StartAsync();
        // The port has no deferred secret: the password is a Secret value, so there is no resolver to ask.
        await using var session = await StartSessionAsync(app, new WebEngineOptions { Headless = true, BasicAuth = new WebBasicAuth(Username, Secret.Create("previewPassword", Password)) });

        Assert.Equal("Protected", await HeadingAtAsync(session, app.Url + "protected"));
    }

    [Fact]
    public async Task Reports_the_configured_user_agent_to_the_page_after_a_context_reset_too()
    {
        using var app = await StartAsync();
        await using var session = await StartSessionAsync(app, new WebEngineOptions { Headless = true, UserAgent = "Mozilla/5.0 e2e-probe playwright" });
        async Task<string?> UserAgent() => (await ((IBrowserSession)session).EvaluateAsync("() => navigator.userAgent", null, false, CancellationToken.None))?.GetString();

        await session.OpenAsync(app.Url, CancellationToken.None);
        Assert.Equal("Mozilla/5.0 e2e-probe playwright", await UserAgent());
        await session.ClearStateAsync(CancellationToken.None);
        await session.OpenAsync(app.Url, CancellationToken.None);
        Assert.Equal("Mozilla/5.0 e2e-probe playwright", await UserAgent());
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

    private static Task<IEngineSession> StartSessionAsync(ProtectedFixtureApp app, WebEngineOptions options) =>
        new WebEngine(options).StartAsync(new EngineStartOptions { BaseUrl = app.Url, ActionTimeout = TimeSpan.FromSeconds(10) }, CancellationToken.None);

    /// <summary>Navigates and reads the page's one heading.</summary>
    private static async Task<string?> HeadingAtAsync(IEngineSession session, string url)
    {
        await session.OpenAsync(url, CancellationToken.None);
        return WebSemanticsTests.Flatten((await session.ObserveAsync(CancellationToken.None)).Roots).First(node => node.Role == "heading").Name;
    }
}
