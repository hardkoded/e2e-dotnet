// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.Tests.RoutePattern;

/// <summary>Upstream's <c>routeUrlMatches and routeMethodMatches</c> describe.</summary>
public sealed class RoutePatternMatchesTests
{
    [Fact]
    public void Supports_regexp_wire_patterns()
    {
        Assert.True(
            E2E.Internal.Routes.UrlMatches(
                E2E.Internal.Routes.ToRoutePattern("route", new Regex(@"api/\w+$")),
                "http://x.test/api/flags"
            )
        );
    }

    [Fact]
    public void Supports_a_method_narrowed_regexp_url()
    {
        var post = E2E.Internal.Routes.ToRoutePattern(
            "route",
            new RouteMatcher { Method = "POST", UrlRegex = new Regex(@"api/\w+$") }
        );
        Assert.True(E2E.Internal.Routes.UrlMatches(post, "http://x.test/api/flags"));
        Assert.False(E2E.Internal.Routes.UrlMatches(post, "http://x.test/api-flags"));
        Assert.True(E2E.Internal.Routes.MethodMatches(post, "POST"));
        Assert.False(E2E.Internal.Routes.MethodMatches(post, "GET"));
    }

    [Fact]
    public void Matches_a_method_case_insensitively_and_any_method_without_one()
    {
        var post = E2E.Internal.Routes.ToRoutePattern(
            "route",
            new RouteMatcher { Method = "post", Url = "**/api/flags" }
        );
        Assert.True(E2E.Internal.Routes.UrlMatches(post, "http://x.test/api/flags"));
        Assert.True(E2E.Internal.Routes.MethodMatches(post, "POST"));
        Assert.True(E2E.Internal.Routes.MethodMatches(post, "post"));
        Assert.False(E2E.Internal.Routes.MethodMatches(post, "GET"));
        var any = E2E.Internal.Routes.ToRoutePattern("route", "**/api/flags");
        Assert.True(E2E.Internal.Routes.UrlMatches(any, "http://x.test/api/flags"));
        Assert.True(E2E.Internal.Routes.MethodMatches(any, "DELETE"));
    }

    [Fact]
    public void Rejects_a_malformed_method_narrowed_pattern()
    {
        static void Rejects(object pattern, string message)
        {
            var error = Assert.Throws<TestException>(() =>
                E2E.Internal.Routes.ToRoutePattern("browser.route", pattern)
            );
            Assert.Equal("INVALID_ARGUMENT", error.Code);
            Assert.Contains(message, error.Message, StringComparison.Ordinal);
        }

        Rejects(new RouteMatcher { Method = "PO ST", Url = "**/a" }, "must be an HTTP method");
        Rejects(new RouteMatcher { Url = "**/a" }, "requires method");
        Rejects(new RouteMatcher { Method = "POST" }, "requires url");
        Rejects(new RouteMatcher(), "must be a string, RegExp, or { method, url }; got an object");
        Rejects(new Uri("https://x.test/api"), "must be a string, RegExp, or { method, url }");
        Rejects(new RouteMatcher { Method = "*", Url = "**/a" }, "one HTTP method");
        Rejects(new RouteMatcher { Method = "GET|POST", Url = "**/a" }, "one HTTP method");
        Rejects(
            new RouteMatcher
            {
                Method = "POST",
                Url = "**/a",
                UrlRegex = new Regex("a"),
            },
            "string or RegExp"
        );
        Assert.Equal(
            "M-SEARCH",
            E2E.Internal.Routes.ToRoutePattern(
                "browser.route",
                new RouteMatcher { Method = "M-SEARCH", Url = "**/a" }
            ).Method
        );
    }
}
