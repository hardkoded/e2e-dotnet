// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine.Protocol;

namespace E2E.Tests.EngineProtocol;

/// <summary>.NET-only: JavaScript's <c>\d</c>, <c>\w</c> and <c>\b</c> are ASCII only, so the wire decoder must not match Unicode (#2235). Upstream has no such <c>it</c>.</summary>
public sealed class WireRegexAsciiShorthandsTests
{
    private static System.Text.RegularExpressions.Regex Decode(string source)
    {
        var node = new JsonObject
        {
            ["kind"] = "regexp",
            ["source"] = source,
            ["flags"] = "",
        };
        return WireCodec.DecodeTextPattern(node).Match.Pattern!;
    }

    [Theory]
    [InlineData(@"^\d$", "5", true)]
    [InlineData(@"^\d$", "١", false)]
    [InlineData(@"^\w+$", "ab_9", true)]
    [InlineData(@"^\w+$", "café", false)]
    [InlineData(@"^[\d]+$", "١", false)]
    [InlineData(@"^[\d]+$", "12", true)]
    [InlineData(@"^\D$", "١", true)]
    [InlineData(@"^\W$", "é", true)]
    [InlineData(@"\bcat\b", "a cat!", true)]
    [InlineData(@"\bcat\b", "écat", true)]
    [InlineData(@"a\B", "ab", true)]
    [InlineData(@"^\\d$", "\\d", true)]
    public void Matches_like_JavaScript(string source, string input, bool expected) =>
        Assert.Equal(expected, Decode(source).IsMatch(input));

    [Fact]
    public void Refuses_a_negated_shorthand_inside_a_character_class() =>
        Assert.Throws<JsonException>(() => Decode(@"[\D]"));
}
