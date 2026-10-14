// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.Routes;

/// <summary>
/// No upstream <c>it</c>: a .NET regex <c>$</c> also matches before a final line feed, which a JavaScript one does not.
/// </summary>
public sealed class RouteHeaderNameTests
{
    [Theory]
    [InlineData("X-Foo\n")]
    [InlineData("X-Foo\r\n")]
    [InlineData("x a")]
    [InlineData("")]
    public void Rejects_a_header_name_outside_the_token_grammar(string name)
    {
        Assert.Equal("has an invalid header name: \"" + name + "\"", RouteOptions.HeaderProblem(name, "b"));
    }

    [Fact]
    public void Accepts_a_token_header_name()
    {
        Assert.Null(RouteOptions.HeaderProblem("X-Foo", "b"));
    }
}
