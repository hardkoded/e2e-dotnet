// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cache;

namespace E2E.Tests.TraceRoute;

/// <summary>Upstream's <c>appNavigation</c> describe (<c>tests/unit/trace-route.test.ts</c>).</summary>
public sealed class AppNavigationTests
{
    private const string App = "http://127.0.0.1:4291";

    [Fact]
    public void Cuts_the_app_origin_off_an_absolute_URL_and_keeps_the_rest_as_typed()
    {
        Assert.Equal("/todos?tab=open#new", CacheRoute.AppNavigation("http://127.0.0.1:4291/todos?tab=open#new", App));

        // Not re-encoded, so `unique()` slots still match.
        Assert.Equal("/companies/a&b c?q=x y", CacheRoute.AppNavigation("http://127.0.0.1:4291/companies/a&b c?q=x y", App));

        // Leading whitespace and extra slashes, which the URL parser skips.
        Assert.Equal("/todos", CacheRoute.AppNavigation(" http:////127.0.0.1:4291/todos", App));
        Assert.Equal("/x", CacheRoute.AppNavigation("https://app.test:443/x", "https://app.test"));
        Assert.Equal("/x", CacheRoute.AppNavigation("https://APP.test/x", "https://app.test"));
    }

    [Fact]
    public void Opens_a_URL_with_no_path_at_the_root_never_relative_to_a_base_path()
    {
        Assert.Equal("/", CacheRoute.AppNavigation("http://127.0.0.1:4291", App));
        Assert.Equal("/?q=1", CacheRoute.AppNavigation("http://127.0.0.1:4291?q=1", App));
        Assert.Equal("/#top", CacheRoute.AppNavigation("http://127.0.0.1:4291#top", App));
    }

    [Theory]
    [InlineData("http://127.0.0.1:4291/todos?tab=open#new")]
    [InlineData("http://127.0.0.1:4291/companies/a&b c")]
    [InlineData("http://127.0.0.1:4291?q=1")]
    [InlineData("http:////127.0.0.1:4291/todos")]
    public void Resolves_to_the_URL_it_was_cut_from_on_the_recorded_origin_and_to_the_same_path_on_another(string url)
    {
        var cut = CacheRoute.AppNavigation(url, App);

        // .NET's parser rejects the extra slashes the web's skips, so the expectation parses the URL without them.
        var plain = url.Replace("////", "//", StringComparison.Ordinal);
        Assert.Equal(new Uri(plain).AbsoluteUri, new Uri(new Uri(App + "/app/"), cut).AbsoluteUri);
        Assert.Equal(new Uri("http://127.0.0.1:4292" + CacheRoute.AppLocation(plain, App)).AbsoluteUri, new Uri(new Uri("http://127.0.0.1:4292/app/"), cut).AbsoluteUri);
    }

    [Fact]
    public void Keeps_another_origin_a_relative_reference_credentials_and_no_app_URL_as_given()
    {
        Assert.Equal("http://127.0.0.1:4292/todos", CacheRoute.AppNavigation("http://127.0.0.1:4292/todos", App));
        Assert.Equal("https://docs.example.test/pay", CacheRoute.AppNavigation("https://docs.example.test/pay", App));
        Assert.Equal("/todos", CacheRoute.AppNavigation("/todos", App));
        Assert.Equal("todos", CacheRoute.AppNavigation("todos", App));
        Assert.Equal("about:blank", CacheRoute.AppNavigation("about:blank", App));
        Assert.Equal("http://user:pass@127.0.0.1:4291/todos", CacheRoute.AppNavigation("http://user:pass@127.0.0.1:4291/todos", App));
        Assert.Equal("http://127.0.0.1:4291/todos", CacheRoute.AppNavigation("http://127.0.0.1:4291/todos", null));
    }

    [Fact]
    public void Keeps_a_URL_whose_rest_a_parser_would_read_as_another_host()
    {
        Assert.Equal("http://127.0.0.1:4291//evil.test/x", CacheRoute.AppNavigation("http://127.0.0.1:4291//evil.test/x", App));
        Assert.Equal("http://127.0.0.1:4291/\\evil.test/x", CacheRoute.AppNavigation("http://127.0.0.1:4291/\\evil.test/x", App));
        Assert.Equal("http://127.0.0.1:4291/to\ndos", CacheRoute.AppNavigation("http://127.0.0.1:4291/to\ndos", App));
    }
}
