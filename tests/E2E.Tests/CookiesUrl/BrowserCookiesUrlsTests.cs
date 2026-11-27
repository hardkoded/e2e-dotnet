// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.CookiesUrl;

/// <summary>
/// <c>browser.cookies(urls)</c> against a real browser context: only the cookies a request to the URL would
/// carry come back, by domain, path, and <c>Secure</c>, and no URL still returns every cookie. Ported from
/// upstream's <c>describe.each(browsers)('browser.cookies(urls) in $name')</c>, run in Chromium, the one
/// browser <c>WebEngine</c> launches.
/// </summary>
/// <remarks>
/// Not ported: upstream's Firefox and WebKit runs (<c>WebEngine</c> drives Chromium only) and the
/// <c>INVALID_ARGUMENT</c> case of a value that is not a string or an array of strings, which the C#
/// signature refuses at compile time (a null entry is still <c>INVALID_ARGUMENT</c>, checked below).
/// </remarks>
[Collection(BrowserCollection.Name)]
public sealed class BrowserCookiesUrlsTests
{
    [Fact]
    public Task Every_cookie_without_a_URL() =>
        ExpectAsync(null, ["admin", "api", "host", "local", "other", "secure", "site"]);

    [Fact]
    public Task A_relative_URL_resolves_against_the_base_URL() => ExpectAsync(["/"], ["host", "site"]);

    [Fact]
    public Task A_path_cookie_under_its_path() => ExpectAsync(["/admin/users?page=2"], ["admin", "host", "site"]);

    [Fact]
    public Task No_path_cookie_for_a_path_that_only_shares_its_prefix() =>
        ExpectAsync(["/administrator"], ["host", "site"]);

    [Fact]
    public Task A_Secure_cookie_over_https() => ExpectAsync(["https://app.example/"], ["host", "secure", "site"]);

    [Fact]
    public Task A_Secure_cookie_over_http_on_localhost() => ExpectAsync(["http://localhost:3000/"], ["local"]);

    [Fact]
    public Task A_subdomain_gets_the_domain_cookie_not_the_host_only_one() =>
        ExpectAsync(["http://api.app.example/"], ["api", "site"]);

    [Fact]
    public Task Another_domain_gets_only_its_own() => ExpectAsync(["http://other.example/"], ["other"]);

    [Fact]
    public Task A_parent_domain_gets_none_of_its_subdomains() => ExpectAsync(["http://example/"], []);

    [Fact]
    public Task Several_URLs_union() =>
        ExpectAsync(["http://other.example/", "/admin"], ["admin", "host", "other", "site"]);

    [Fact]
    public Task An_empty_list_matches_nothing() => ExpectAsync([], []);

    [Fact]
    public Task A_single_URL_overload_takes_the_same_rules() =>
        WithCookiesAsync(async browser =>
        {
            Assert.Equal(["admin", "host", "site"], Names(await browser.CookiesAsync("/admin/users")));
        });

    [Fact]
    public Task Refuses_a_value_that_is_not_a_URL_and_a_URL_that_holds_no_cookies() =>
        WithCookiesAsync(browser =>
        {
            Assert.Equal(
                "INVALID_ARGUMENT",
                Assert
                    .Throws<TestException>(() =>
                    {
                        _ = browser.CookiesAsync(["/", null!]);
                    })
                    .Code
            );
            Assert.Equal(
                "POLICY_DENIED",
                Assert
                    .Throws<TestException>(() =>
                    {
                        _ = browser.CookiesAsync("about:blank");
                    })
                    .Code
            );
            return Task.CompletedTask;
        });

    private static string[] Names(IReadOnlyList<BrowserCookie> cookies) =>
        cookies.Select(cookie => cookie.Name).Order(StringComparer.Ordinal).ToArray();

    private static Task ExpectAsync(IReadOnlyList<string>? urls, string[] expected) =>
        WithCookiesAsync(async browser => Assert.Equal(expected, Names(await browser.CookiesAsync(urls))));

    /// <summary>
    /// Starts an engine session and a <c>browser</c> fixture whose base URL is <c>http://app.example/</c> (upstream's
    /// fixture stub resolves against it; the config rejects plain HTTP off loopback) and sets the seven cookies every test starts from.
    /// </summary>
    private static async Task WithCookiesAsync(Func<Browser, Task> body)
    {
        var artifactsDir = Directory.CreateTempSubdirectory("e2e-cookies-url-").FullName;
        try
        {
            await using var session = await new WebEngine(headless: true).StartAsync(
                new EngineStartOptions { ArtifactsDir = artifactsDir, ActionTimeout = TimeSpan.FromSeconds(10) },
                CancellationToken.None
            );
            var browser = new Browser(
                session,
                "web",
                "http://app.example/",
                Directory.GetCurrentDirectory(),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(5),
                () => CancellationToken.None,
                () => { },
                new SoftFailures()
            );
            await browser.SetCookiesAsync([
                new BrowserCookie
                {
                    Name = "host",
                    Value = "1",
                    Domain = "app.example",
                    Path = "/",
                },
                new BrowserCookie
                {
                    Name = "site",
                    Value = "1",
                    Domain = ".app.example",
                    Path = "/",
                },
                new BrowserCookie
                {
                    Name = "admin",
                    Value = "1",
                    Domain = "app.example",
                    Path = "/admin",
                },
                new BrowserCookie
                {
                    Name = "secure",
                    Value = "1",
                    Domain = "app.example",
                    Path = "/",
                    Secure = true,
                },
                new BrowserCookie
                {
                    Name = "api",
                    Value = "1",
                    Domain = "api.app.example",
                    Path = "/",
                },
                new BrowserCookie
                {
                    Name = "other",
                    Value = "1",
                    Domain = "other.example",
                    Path = "/",
                },
                new BrowserCookie
                {
                    Name = "local",
                    Value = "1",
                    Domain = "localhost",
                    Path = "/",
                    Secure = true,
                },
            ]);
            await body(browser);
        }
        finally
        {
            Directory.Delete(artifactsDir, recursive: true);
        }
    }
}
