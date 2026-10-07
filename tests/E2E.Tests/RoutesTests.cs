// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests;

public sealed class RoutesTests
{
    [Theory]
    [InlineData("http://h/app/", "/x", "http://h/x")]
    [InlineData("http://h/app/", "x", "http://h/app/x")]
    [InlineData("http://h/app", "x", "http://h/x")]
    [InlineData("http://h/app/", "../x", "http://h/x")]
    [InlineData("http://h/app/", "?q=1", "http://h/app/?q=1")]
    [InlineData("http://h/app/", "//other/x", "http://other/x")]
    [InlineData("http://h/app/", "https://other/x", "https://other/x")]
    [InlineData("http://h/app/", "", "http://h/app/")]
    [InlineData("http://h/app/", null, "http://h/app/")]
    [InlineData(null, "https://other/x", "https://other/x")]
    public void Resolve_follows_url_resolution(string? baseUrl, string? url, string expected)
    {
        Assert.Equal(expected, E2E.Internal.Routes.Resolve(baseUrl, url));
    }

    [Theory]
    [InlineData("/x")]
    [InlineData("x")]
    [InlineData("")]
    [InlineData(null)]
    public void A_relative_url_without_a_base_url_needs_an_app_url(string? url)
    {
        var error = Assert.Throws<TestException>(() => E2E.Internal.Routes.Resolve(null, url));
        Assert.Equal("APP_URL_REQUIRED", error.Code);
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("data:text/html,hi")]
    [InlineData("javascript:alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    public void Forbidden_schemes_are_denied(string url)
    {
        Assert.Equal("POLICY_DENIED", Assert.Throws<TestException>(() => E2E.Internal.Routes.Resolve("http://h/", url)).Code);
        Assert.Equal("POLICY_DENIED", Assert.Throws<TestException>(() => E2E.Internal.Routes.Resolve(null, url)).Code);
    }

    [Theory]
    [InlineData("view-source:file:///etc/passwd", "view-source:")]
    [InlineData("view-source:http://localhost:3000/", "view-source:")]
    [InlineData("VIEW-SOURCE:file:///etc/passwd", "view-source:")]
    [InlineData("  view-source:file:///etc/passwd", "view-source:")]
    [InlineData("\tview-source:file:///etc/passwd\n", "view-source:")]
    [InlineData("FiLe:///etc/passwd", "file:")]
    [InlineData(" file:///etc/passwd", "file:")]
    [InlineData("blob:http://localhost:3000/0b7c4c1e", "blob:")]
    [InlineData("filesystem:http://localhost:3000/temporary/x", "filesystem:")]
    [InlineData("chrome://version", "chrome:")]
    [InlineData("chrome-extension://abcdefghijklmnop/page.html", "chrome-extension:")]
    [InlineData("devtools://devtools/bundled/inspector.html", "devtools:")]
    [InlineData("about:blank#x", "about:")]
    [InlineData("about:blank?x", "about:")]
    [InlineData("about:srcdoc", "about:")]
    [InlineData("about:version", "about:")]
    [InlineData("ftp://example.test/x", "ftp:")]
    [InlineData("ws://localhost:3000/socket", "ws:")]
    [InlineData("myapp://orders/42", "myapp:")]
    public void Only_http_and_https_are_navigable(string url, string scheme)
    {
        var error = Assert.Throws<TestException>(() => Routes.Resolve("http://localhost:3000/app/", url));
        Assert.Equal("POLICY_DENIED", error.Code);
        Assert.Equal("Forbidden URL scheme: " + scheme, error.Message);
        Assert.Equal("POLICY_DENIED", Assert.Throws<TestException>(() => Routes.Resolve(null, url)).Code);
    }

    [Fact]
    public void A_percent_encoded_scheme_is_a_path_not_a_scheme()
    {
        // System.Uri decodes an unreserved %66 to f where WHATWG keeps it; the result is still an http path.
        Assert.Equal("http://localhost:3000/app/view-source%3Afile:///etc/passwd", Routes.Resolve("http://localhost:3000/app/", "view-source%3Afile:///etc/passwd"));
        Assert.Equal("http://localhost:3000/app/file:///etc/passwd", Routes.Resolve("http://localhost:3000/app/", "%66ile:///etc/passwd"));
    }

    [Fact]
    public void A_tab_inside_a_scheme_is_escaped_into_a_path_not_stripped()
    {
        // WHATWG strips the tab and denies view-source:; System.Uri escapes it, so the URL stays an http path.
        Assert.Equal("http://localhost:3000/app/view-%09source:file:///etc/passwd", Routes.Resolve("http://localhost:3000/app/", "view-\tsource:file:///etc/passwd"));
        Assert.Equal("APP_URL_REQUIRED", Assert.Throws<TestException>(() => Routes.Resolve(null, "view-\tsource:file:///etc/passwd")).Code);
    }

    [Fact]
    public void Exactly_about_blank_is_admitted()
    {
        Assert.Equal("about:blank", Routes.Resolve("http://localhost:3000/app/", "about:blank"));
        Assert.Equal("about:blank", Routes.Resolve(null, " ABOUT:blank "));
    }

    [Fact]
    public void Http_and_https_are_admitted_in_any_case_and_with_surrounding_whitespace()
    {
        Assert.Equal("https://other.test/x", Routes.Resolve("http://localhost:3000/app/", "HTTPS://Other.test/x"));
        Assert.Equal("http://localhost:3000/a", Routes.Resolve("http://localhost:3000/app/", "  http://localhost:3000/a "));
        Assert.Equal("http://other.test/x", Routes.Resolve("http://localhost:3000/app/", "//other.test/x"));
    }
}
