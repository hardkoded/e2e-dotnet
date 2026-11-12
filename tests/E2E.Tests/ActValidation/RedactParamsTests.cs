// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Internal;

namespace E2E.Tests.ActValidation;

/// <summary>
/// Upstream's <c>redactParams</c> is <c>ActParams.RedactParams</c>, and its <c>validateParams</c> is <c>ActParams.ValidateParams</c>.
/// </summary>
public sealed class RedactParamsTests
{
    private const string Secret = "sk_param_4Rt9Zq";

    private static string Redact(string text) => text.Replace(Secret, "<secret:probe>", StringComparison.Ordinal);

    [Fact]
    public void Redacts_keys_and_leaves_and_moves_unique_templates_with_their_leaves_so_the_cache_key_slots_them()
    {
        var (projected, templates) = ActParams.ValidateParams(
            new Dictionary<string, object?>
            {
                ["note"] = Secret,
                ["account " + Secret] = new Dictionary<string, object?>
                {
                    ["name"] = Values.Unique("E2E " + Secret + " 42"),
                    ["tags"] = new object[] { Secret, 7 },
                },
            },
            "agent.act");
        Assert.Equal([new ParamTemplate("/account " + Secret + "/name", "E2E " + Secret + " 42")], templates);

        var redacted = ActParams.RedactParams(projected!, templates, Redact, "agent.act");

        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("{\"note\":\"<secret:probe>\",\"account <secret:probe>\":{\"name\":\"E2E <secret:probe> 42\",\"tags\":[\"<secret:probe>\",7]}}"),
            redacted.Params));
        Assert.Equal([new ParamTemplate("/account <secret:probe>/name", "E2E <secret:probe> 42")], redacted.Templates);
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("{\"note\":\"<secret:probe>\",\"account <secret:probe>\":{\"name\":\"{{param:/account <secret:probe>/name}}\",\"tags\":[\"<secret:probe>\",7]}}"),
            CacheKeys.TemplateParams(redacted.Params, redacted.Templates)));
    }

    [Fact]
    public void Keeps_a_proto_key_as_an_own_property()
    {
        var (projected, templates) = ActParams.ValidateParams(
            new Dictionary<string, object?> { ["__proto__"] = Secret, ["note"] = "x" },
            "agent.act");

        var redacted = ActParams.RedactParams(projected!, templates, Redact, "agent.act");

        Assert.True(redacted.Params.ContainsKey("__proto__"));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("{\"__proto__\":\"<secret:probe>\",\"note\":\"x\"}"), redacted.Params));
    }

    [Fact]
    public void Refuses_two_keys_that_redact_alike_instead_of_dropping_one()
    {
        var (projected, templates) = ActParams.ValidateParams(
            new Dictionary<string, object?>
            {
                ["nested"] = new Dictionary<string, object?> { ["id " + Secret] = 1, ["id <secret:probe>"] = 2 },
            },
            "agent.act");

        var error = Assert.Throws<TestException>(() => ActParams.RedactParams(projected!, templates, Redact, "agent.act"));

        Assert.Equal("INVALID_ARGUMENT", error.Code);
        Assert.Equal("agent.act params has two keys that read \"id <secret:probe>\" once secret values are redacted; a key cannot be told apart by a secret", error.Message);
    }
}
