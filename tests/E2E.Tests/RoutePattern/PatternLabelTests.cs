// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.Tests.RoutePattern;

/// <summary>
/// .NET-only: upstream's <c>patternLabel</c> is covered by the <c>wait</c> case of <c>fixture-runtime.test.ts</c>'s "maps an EngineError a
/// contributed method throws onto the runner taxonomy" (the label in a timeout message). The port has no <c>FixtureRecorder</c>
/// or <c>translated</c>, so that <c>it</c> cannot be ported and only the label itself is checked here.
/// </summary>
public sealed class PatternLabelTests
{
    [Fact]
    public void Names_a_predicate_without_its_source()
    {
        Assert.Equal("predicate", E2E.Internal.Routes.PatternLabel(new Func<PageResponse, bool>(r => r.Status == 201)));
    }

    [Fact]
    public void Quotes_a_glob_and_writes_a_regex_as_its_literal()
    {
        Assert.Equal("\"foo.css\"", E2E.Internal.Routes.PatternLabel("foo.css"));
        Assert.Equal("\"**/a \\\"b\\\"\"", E2E.Internal.Routes.PatternLabel("**/a \"b\""));
        Assert.Equal(
            "/api\\/\\w+$/i",
            E2E.Internal.Routes.PatternLabel(new Regex(@"api\/\w+$", RegexOptions.IgnoreCase))
        );
    }

    [Fact]
    public void Names_a_string_a_regexp_and_a_method_narrowed_pattern()
    {
        Assert.Equal("\"**/api/orders\"", E2E.Internal.Routes.PatternLabel("**/api/orders"));
        Assert.Equal(
            "POST \"**/api/orders\"",
            E2E.Internal.Routes.PatternLabel(new RouteMatcher { Method = "post", Url = "**/api/orders" })
        );
        Assert.Equal(
            "POST /orders$/",
            E2E.Internal.Routes.PatternLabel(new RouteMatcher { Method = "POST", UrlRegex = new Regex("orders$") })
        );
    }

    [Fact]
    public void Never_throws_on_a_value_without_a_usable_label()
    {
        Assert.Equal("POST undefined", E2E.Internal.Routes.PatternLabel(new RouteMatcher { Method = "POST" }));
        Assert.Equal("an object", E2E.Internal.Routes.PatternLabel(new RouteMatcher()));
        Assert.Equal("an object", E2E.Internal.Routes.PatternLabel(new Uri("https://x.test/")));
    }
}
