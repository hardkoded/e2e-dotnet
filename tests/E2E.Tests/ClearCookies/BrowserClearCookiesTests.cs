// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;

namespace E2E.Tests.ClearCookies;

/// <summary>
/// <c>browser.clearCookies</c> against a real browser context: filters combine, a string matches a field
/// exactly and a RegExp matches its text, no options clears every cookie, HttpOnly ones included, and
/// page storage stays.
/// </summary>
/// <remarks>
/// Not ported: upstream's <c>refuses an unknown filter and a value that is not a string or RegExp, clearing nothing</c>
/// and <c>tests/types/clear-cookies.ts</c>. <c>CookieFilter</c> has no <c>url</c> member and each field is a
/// <c>TextMatch</c>, so the compiler refuses both and the port has no <c>INVALID_ARGUMENT</c> path to test.
/// </remarks>
[Collection(BrowserCollection.Name)]
public sealed class BrowserClearCookiesTests
{
    [Fact]
    public Task Combines_filters() => WithCookiesAsync(async browser =>
    {
        await browser.ClearCookiesAsync(new CookieFilter { Name = "session", Path = "/admin" });
        Assert.Equal(["language=en", "session-id=x", "session=root"], await RemainingAsync(browser));
    });

    [Fact]
    public Task Matches_a_string_exactly() => WithCookiesAsync(async browser =>
    {
        await browser.ClearCookiesAsync(new CookieFilter { Name = "session" });
        Assert.Equal(["language=en", "session-id=x"], await RemainingAsync(browser));
    });

    [Fact]
    public Task Matches_a_RegExp() => WithCookiesAsync(async browser =>
    {
        await browser.ClearCookiesAsync(new CookieFilter { Name = new Regex("^session") });
        Assert.Equal(["language=en"], await RemainingAsync(browser));
    });

    [Fact]
    public Task Clears_HttpOnly_cookies_by_domain() => WithCookiesAsync(async browser =>
    {
        // Playwright hands the pattern's source to the browser, which reads it as JavaScript: no \z there.
        await browser.ClearCookiesAsync(new CookieFilter { Domain = "127.0.0.1", Path = new Regex(@"^/admin$") });
        Assert.Equal(["language=en", "session-id=x", "session=root"], await RemainingAsync(browser));
    });

    [Fact]
    public Task Clears_every_cookie_without_options_and_keeps_page_storage() => WithCookiesAsync(async browser =>
    {
        await browser.EvaluateAsync<object?>("() => { localStorage.setItem('preference', 'kept'); sessionStorage.setItem('tab', 'kept'); }");
        await browser.ClearCookiesAsync();
        Assert.Empty(await RemainingAsync(browser));
        Assert.Equal(new[] { "kept", "kept" }, await browser.EvaluateAsync<string[]>("() => [localStorage.getItem('preference'), sessionStorage.getItem('tab')]"));
    });

    /// <summary>The remaining cookies as sorted <c>name=value</c> pairs.</summary>
    private static async Task<string[]> RemainingAsync(Browser browser) =>
        (await browser.CookiesAsync()).Select(cookie => cookie.Name + "=" + cookie.Value).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Opens an empty page on a local site and sets the four cookies every test starts from.</summary>
    private static async Task WithCookiesAsync(Func<Browser, Task> body)
    {
        using var site = await TinySite.StartAsync("<!doctype html><title>Cookies</title>");
        var projectRoot = Directory.CreateTempSubdirectory("e2e-clear-cookies-").FullName;
        try
        {
            var session = await E2ESession.StartAsync(new E2ESessionOptions
            {
                Engine = new WebEngine(new WebEngineOptions { Headless = true }),
                BaseUrl = site.Url,
                ProjectRoot = projectRoot,
                TestTitle = "browser.clearCookies",
                ActionTimeout = TimeSpan.FromSeconds(10),
                TestTimeout = TimeSpan.FromSeconds(30),
            });
            await WebEngineTests.RunAsync(session, async () =>
            {
                await session.App.OpenAsync("/");
                await session.Browser.SetCookiesAsync(
                [
                    new BrowserCookie { Name = "session", Value = "root", Domain = "127.0.0.1", Path = "/", HttpOnly = true },
                    new BrowserCookie { Name = "session", Value = "admin", Domain = "127.0.0.1", Path = "/admin", HttpOnly = true },
                    new BrowserCookie { Name = "session-id", Value = "x", Domain = "127.0.0.1", Path = "/" },
                    new BrowserCookie { Name = "language", Value = "en", Domain = "127.0.0.1", Path = "/" },
                ]);
                await body(session.Browser);
            });
        }
        finally
        {
            Directory.Delete(projectRoot, recursive: true);
        }
    }
}
