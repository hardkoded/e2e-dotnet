// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Internal;

namespace E2E.Tests.TraceTemplate;

/// <summary>
/// Upstream's <c>validateParams</c> is <c>ActParams.ValidateParams</c>.
/// </summary>
public sealed class UniqueThroughValidateParamsTests
{
    [Fact]
    public void Projects_the_value_for_the_model_and_lists_the_pointer_for_the_cache_at_any_depth()
    {
        var (projected, templates) = ActParams.ValidateParams(new Dictionary<string, object?>
        {
            ["name"] = Values.Unique("E2E abc Company"),
            ["plan"] = "pro",
            ["qty"] = 2,
            ["owner"] = new Dictionary<string, object?> { ["email"] = Values.Unique("a@b.test"), ["tags"] = new object[] { "vip", Values.Unique("new-customer") } },
            ["a/b"] = Values.Unique("slash"),
        }, "agent.act");

        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("{\"name\":\"E2E abc Company\",\"plan\":\"pro\",\"qty\":2,\"owner\":{\"email\":\"a@b.test\",\"tags\":[\"vip\",\"new-customer\"]},\"a/b\":\"slash\"}"),
            projected));
        Assert.Equal(
            [
                new ParamTemplate("/name", "E2E abc Company"),
                new ParamTemplate("/owner/email", "a@b.test"),
                new ParamTemplate("/owner/tags/1", "new-customer"),
                new ParamTemplate("/a~1b", "slash"),
            ],
            templates);
    }

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
        Assert.Equal("/tags/0", CacheKeys.ParamPointer("/tags", 0));
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
