// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.TraceTemplate;

/// <summary>
/// "projects the value for the model and lists the pointer for the cache, at any depth" is not ported: the port has no
/// <c>validateParams</c>, and a <c>unique()</c> value is templated at the top level of the params only.
/// </summary>
public sealed class UniqueThroughValidateParamsTests
{
    /// <summary>Partial port: a value that is not a string cannot be built in C#, so only the blank and placeholder cases remain.</summary>
    [Fact]
    public void Partial_Refuses_a_blank_value_and_one_that_spells_a_placeholder()
    {
        Assert.Contains("non-empty string", Assert.Throws<TestException>(() => Values.Unique("")).Message, StringComparison.Ordinal);
        Assert.Contains("non-empty string", Assert.Throws<TestException>(() => Values.Unique("  ")).Message, StringComparison.Ordinal);
        Assert.Contains("placeholder", Assert.Throws<TestException>(() => Values.Unique("x {{param:/y}}")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Escapes_keys_the_JSON_Pointer_way_so_two_shapes_never_share_a_pointer()
    {
        Assert.Equal("/a~1b", CacheKeys.ParamPointer("", "a/b"));
        Assert.Equal("/x/a~0b", CacheKeys.ParamPointer("/x", "a~b"));
    }

    [Fact]
    public void Round_trips_a_key_that_contains_a_closing_brace()
    {
        var recorded = new Dictionary<string, object?> { ["a}b"] = Values.Unique("Acme Corp") };
        var templated = CacheKeys.Template("tap \"Acme Corp\"", recorded);

        Assert.Equal("tap \"{{param:/a}b}}\"", templated);
        Assert.Equal("tap \"Globex\"", CacheKeys.Detemplate(templated, new Dictionary<string, object?> { ["a}b"] = Values.Unique("Globex") }));
    }
}
