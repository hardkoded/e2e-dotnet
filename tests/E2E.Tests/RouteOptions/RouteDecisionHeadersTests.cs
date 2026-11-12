// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.RouteOptions;

/// <summary>Route decision validation that a browser round trip cannot observe, ported from upstream <c>route-options.test.ts</c> (<c>describe('route decision headers')</c>).</summary>
public sealed class RouteDecisionHeadersTests
{
    private static string ResolveUrl(string url) => new Uri(new Uri("http://127.0.0.1/"), url).AbsoluteUri;

    [Fact]
    public void Keeps_a_header_named___proto___as_an_own_header()
    {
        var headers = new Dictionary<string, string> { ["__proto__"] = "kept" };

        var continued = E2E.Internal.RouteOptions.ParseContinue(new RouteContinueOverrides { Headers = headers }, "http://127.0.0.1/echo", ResolveUrl).Headers!;
        var fulfilled = E2E.Internal.RouteOptions.ParseFulfill(new RouteFulfillResponse { Headers = headers }, file => file).Headers!;

        Assert.Equal("kept", continued["__proto__"]);
        Assert.Equal("kept", fulfilled["__proto__"]);
    }
}
