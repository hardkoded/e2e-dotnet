// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.RoutePattern;

/// <summary><c>describe('toWaitPattern')</c> of upstream's <c>route-pattern.test.ts</c>.</summary>
public sealed class ToWaitPatternTests
{
    [Fact]
    public void Returns_a_predicate_as_is_and_validates_every_other_pattern_as_a_route_pattern()
    {
        Func<PageResponse, bool> predicate = response => response.Status == 201;
        Assert.Same(predicate, E2E.Internal.Routes.ToWaitPattern("browser.waitForResponse", predicate));
        var matcher = new RouteMatcher { Method = "post", Url = "**/a" };
        var expected = E2E.Internal.Routes.ToRoutePattern("browser.route", matcher);
        var actual = Assert.IsType<E2E.Internal.NormalizedRoutePattern>(
            E2E.Internal.Routes.ToWaitPattern("browser.waitForRequest", matcher)
        );
        Assert.Equal(expected.Method, actual.Method);
        Assert.Equal(expected.Url, actual.Url);
    }

    [Fact]
    public void Names_the_predicate_among_the_shapes_it_takes()
    {
        var error = Assert.Throws<TestException>(() => E2E.Internal.Routes.ToWaitPattern("browser.waitForRequest", 42));
        Assert.Equal(
            "browser.waitForRequest pattern must be a string, RegExp, { method, url }, or a predicate function",
            error.Message
        );
    }
}
