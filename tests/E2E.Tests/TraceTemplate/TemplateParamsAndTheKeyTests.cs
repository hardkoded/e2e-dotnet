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
    /// <summary>
    /// Partial port of "puts a placeholder where each unique value was and leaves everything else literal": a secret keys as
    /// <c>&lt;secret:name&gt;</c> in the port, not as upstream's <c>{ kind, name, purpose }</c>, and a missing params object keys as
    /// null, not as the empty object (so <c>act(x)</c> and <c>act(x, {})</c> are two entries).
    /// </summary>
    [Fact]
    public void Partial_Puts_a_placeholder_where_each_unique_value_was_and_leaves_everything_else_literal()
    {
        var projected = CacheKeys.Project(new Dictionary<string, object?>
        {
            ["name"] = Values.Unique("E2E abc"),
            ["qty"] = 2,
            ["s"] = Secret.Create("admin", "hunter2-pw", "password"),
            ["owner"] = new Dictionary<string, object?> { ["tags"] = new object[] { "vip", Values.Unique("new") } },
        });

        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("{\"name\":\"{{param:/name}}\",\"qty\":2,\"s\":\"<secret:admin>\",\"owner\":{\"tags\":[\"vip\",\"{{param:/owner/tags/1}}\"]}}"),
            projected));
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
