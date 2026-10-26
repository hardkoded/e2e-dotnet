// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using IdHelpers = E2E.Internal.Ids;
using JsonNumbers = System.Text.Json.Nodes.JsonValue;

namespace E2E.Tests.Ids;

/// <summary>
/// Upstream's <c>describe('canonicalJson (RFC 8785)')</c> (<c>tests/unit/ids.test.ts</c>). Partial: "drops undefined object members
/// and nullifies undefined array items" is not ported, since a <see cref="JsonNode"/> has no undefined (a missing member is simply
/// absent).
/// </summary>
public sealed class CanonicalJsonRfc8785Tests
{
    [Fact]
    public void Sorts_object_keys_by_UTF_16_code_units()
    {
        Assert.Equal("{\"a\":2,\"b\":1}", IdHelpers.CanonicalJson(new JsonObject { ["b"] = 1, ["a"] = 2 }));

        // U+E000 is one code unit above the surrogate pair of U+10000, which a code point order would put last.
        Assert.Equal("{\"\ud800\udc00\":1,\"\ue000\":2}", IdHelpers.CanonicalJson(new JsonObject { ["\ue000"] = 2, ["\ud800\udc00"] = 1 }));
    }

    [Fact]
    public void Rejects_non_finite_numbers()
    {
        Assert.ThrowsAny<Exception>(() => IdHelpers.CanonicalJson(JsonNumbers.Create(double.NaN)));
        Assert.ThrowsAny<Exception>(() => IdHelpers.CanonicalJson(JsonNumbers.Create(double.PositiveInfinity)));
    }

    // No upstream it: the port writes a number by hand, so the ECMAScript spelling RFC 8785 requires is pinned here.
    [Theory]
    [InlineData(1e-7, "1e-7")]
    [InlineData(0.000001, "0.000001")]
    [InlineData(1.5, "1.5")]
    [InlineData(-0.25, "-0.25")]
    [InlineData(1e21, "1e+21")]
    [InlineData(1e20, "100000000000000000000")]
    [InlineData(123456789.125, "123456789.125")]
    [InlineData(9007199254740993.0, "9007199254740992")]
    [InlineData(5e-324, "5e-324")]
    [InlineData(-9223372036854775808.0, "-9223372036854776000")]
    public void Writes_a_number_as_ECMAScript_does(double value, string expected) =>
        Assert.Equal(expected, IdHelpers.CanonicalJson(JsonNumbers.Create(value)));

    // No upstream it: the smallest long is past 2^53, so it takes the double path and does not overflow an absolute value.
    [Fact]
    public void Writes_the_smallest_long_as_a_double_does() =>
        Assert.Equal("-9223372036854776000", IdHelpers.CanonicalJson(JsonNumbers.Create(long.MinValue)));

    // No upstream it: an integer is written as digits whatever its CLR type.
    [Fact]
    public void Writes_an_integer_as_digits_whatever_its_type()
    {
        Assert.Equal("[3,3,3,3]", IdHelpers.CanonicalJson(new JsonArray(JsonNumbers.Create(3), JsonNumbers.Create(3L), JsonNumbers.Create(3.0), JsonNumbers.Create(3m))));
    }

    // No upstream it: a control character is escaped as JSON.stringify does, and a character above ASCII is not.
    [Fact]
    public void Escapes_a_string_as_JSON_stringify_does()
    {
        Assert.Equal("\"a\\n\\u0001\\\"\\\\caf\u00e9/\"", IdHelpers.CanonicalJson(JsonNumbers.Create("a\n\u0001\"\\caf\u00e9/")));
    }
}
