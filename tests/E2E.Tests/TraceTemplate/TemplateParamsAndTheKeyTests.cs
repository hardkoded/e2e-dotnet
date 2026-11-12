// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Tests.TraceTemplate;

/// <summary>
/// Upstream's <c>templateParams</c> is <see cref="CacheKeys.TemplateParams"/>, applied to the serialized params by
/// <see cref="CacheKeys.Project"/>.
/// </summary>
public sealed class TemplateParamsAndTheKeyTests
{
    [Fact]
    public void Puts_a_placeholder_where_each_unique_value_was_and_leaves_everything_else_literal()
    {
        var projected = CacheKeys.Project(new Dictionary<string, object?>
        {
            ["name"] = Values.Unique("E2E abc"),
            ["qty"] = 2,
            ["s"] = Secret.Create("admin", "hunter2-pw", "password"),
            ["owner"] = new Dictionary<string, object?> { ["tags"] = new object[] { "vip", Values.Unique("new") } },
        });

        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("{\"name\":\"{{param:/name}}\",\"qty\":2,\"s\":{\"kind\":\"secret\",\"name\":\"admin\",\"purpose\":\"password\"},\"owner\":{\"tags\":[\"vip\",\"{{param:/owner/tags/1}}\"]}}"),
            projected));
        Assert.True(JsonNode.DeepEquals(new JsonObject(), CacheKeys.Project(null)));
    }

    private static string Key(string name, string plan, bool marked = true) => CacheKeys.ParamsDigest(new Dictionary<string, object?>
    {
        ["name"] = marked ? Values.Unique(name) : name,
        ["plan"] = plan,
    });

    [Fact]
    public void Keys_two_calls_apart_only_by_what_they_did_not_mark()
    {
        Assert.Equal(Key("E2E abc", "pro"), Key("E2E xyz", "pro"));
        Assert.NotEqual(Key("E2E abc", "pro"), Key("E2E abc", "starter"));

        // Marking is part of the shape: the same values, marked and unmarked, are two entries.
        Assert.NotEqual(Key("E2E abc", "pro"), Key("E2E abc", "pro", marked: false));
    }
}
