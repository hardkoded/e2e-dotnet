// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk;

namespace E2E.Decision.Tests;

/// <summary>The helpers the Gateway tool tests use in place of vitest's <c>toMatchObject</c>, <c>asSchema</c> and <c>safeValidateTypes</c>.</summary>
internal static class ProviderToolAssertions
{
    /// <summary>
    /// Vitest's <c>toMatchObject</c> on a provider-executed tool: <c>type</c> is <c>provider</c>, the ID matches, the arguments match,
    /// and <c>isProviderExecuted</c> is true.
    /// </summary>
    public static void AssertProviderExecutedTool(ProviderExecutedTool tool, string id, string argsJson)
    {
        Assert.Equal("provider", tool.Type);
        Assert.Equal(id, tool.Id);
        MatchObject(JsonNode.Parse(argsJson), tool.Args);
        Assert.True(tool.IsProviderExecuted);
    }

    /// <summary>Vitest's <c>toMatchObject</c>: every key of an expected object is in the actual one, and arrays match item for item.</summary>
    public static void MatchObject(JsonNode? expected, JsonNode? actual)
    {
        switch (expected)
        {
            case JsonObject expectedObject:
                var actualObject = Assert.IsType<JsonObject>(actual);
                foreach (var (name, value) in expectedObject)
                {
                    Assert.True(actualObject.ContainsKey(name), "missing property " + name);
                    MatchObject(value, actualObject[name]);
                }

                break;
            case JsonArray expectedArray:
                var actualArray = Assert.IsType<JsonArray>(actual);
                Assert.Equal(expectedArray.Count, actualArray.Count);
                for (var i = 0; i < expectedArray.Count; i++)
                {
                    MatchObject(expectedArray[i], actualArray[i]);
                }

                break;
            default:
                Assert.True(
                    JsonNode.DeepEquals(expected, actual),
                    $"expected {expected?.ToJsonString()} but was {actual?.ToJsonString()}"
                );
                break;
        }
    }

    /// <summary>Upstream's <c>asSchema(schema).jsonSchema</c>.</summary>
    public static JsonObject JsonSchemaOf(FlexibleSchema schema) => SchemaUtils.AsSchema(schema).JsonSchema;

    /// <summary>Upstream's <c>safeValidateTypes({ value, schema: asSchema(schema) })</c>, with the value written as JSON.</summary>
    public static ValidationResult Validate(string json, FlexibleSchema schema) =>
        SchemaUtils.SafeValidateTypes(JsonNode.Parse(json), schema);
}
