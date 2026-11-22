// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Internal;

namespace E2E.Tests.RoutePattern;

public sealed class RoutePatternsEqualTests
{
    private static NormalizedRoutePattern Pattern(object pattern) =>
        E2E.Internal.Routes.ToRoutePattern("unroute", pattern);

    // .NET has no global flag, so Multiline stands in for upstream's /a/g: another option.
    [Fact]
    public void Compares_string_and_regexp_forms_structurally()
    {
        Assert.True(E2E.Internal.Routes.PatternsEqual(Pattern("**/a"), Pattern("**/a")));
        Assert.True(
            E2E.Internal.Routes.PatternsEqual(
                Pattern(new Regex("a", RegexOptions.IgnoreCase)),
                Pattern(new Regex("a", RegexOptions.IgnoreCase))
            )
        );
        Assert.False(
            E2E.Internal.Routes.PatternsEqual(
                Pattern(new Regex("a", RegexOptions.IgnoreCase)),
                Pattern(new Regex("a", RegexOptions.Multiline))
            )
        );
        Assert.False(E2E.Internal.Routes.PatternsEqual(Pattern("a"), Pattern(new Regex("a"))));
    }

    [Fact]
    public void Compares_methods_too()
    {
        Assert.True(
            E2E.Internal.Routes.PatternsEqual(
                Pattern(new RouteMatcher { Method = "POST", Url = "**/a" }),
                Pattern(new RouteMatcher { Method = "post", Url = "**/a" })
            )
        );
        Assert.True(
            E2E.Internal.Routes.PatternsEqual(
                Pattern(new RouteMatcher { Method = "POST", UrlRegex = new Regex("a") }),
                Pattern(new RouteMatcher { Method = "post", UrlRegex = new Regex("a") })
            )
        );
        Assert.False(
            E2E.Internal.Routes.PatternsEqual(
                Pattern(new RouteMatcher { Method = "POST", Url = "**/a" }),
                Pattern("**/a")
            )
        );
        Assert.False(
            E2E.Internal.Routes.PatternsEqual(
                Pattern(new RouteMatcher { Method = "GET", Url = "**/a" }),
                Pattern(new RouteMatcher { Method = "POST", Url = "**/a" })
            )
        );
    }
}
