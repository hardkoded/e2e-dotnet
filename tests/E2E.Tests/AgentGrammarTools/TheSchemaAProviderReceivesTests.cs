// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;

namespace E2E.Tests.AgentGrammarTools;

// Upstream reads the schema twice: as the AI SDK sends it, and as zod alone emits it. The port has one JSON Schema, the one the model
// request carries as the tool's parameters, so each check reads it once. zod's output also carries a $schema keyword, which the port's
// hand-written schemas do not.
public sealed class TheSchemaAProviderReceivesTests
{
    private static IReadOnlyList<ModelTool> FullGrammar() =>
        AgentTools.GrammarToolDefinitions(
            EngineCapabilities.Keyboard | EngineCapabilities.Scroll | EngineCapabilities.History | EngineCapabilities.Resize | EngineCapabilities.Upload | EngineCapabilities.TapModifiers,
            null,
            EngineActionKinds.Locator,
            ["tap", "hover", "dragTo"],
            dismissKeyboard: true);

    [Fact]
    public void Carries_additionalProperties_false_for_every_grammar_tool_and_complete_step_from_the_schema_itself()
    {
        IEnumerable<ModelTool> tools = [.. FullGrammar(), AgentTools.CreateVerdictTool().Tool];
        foreach (var tool in tools)
        {
            var closed = tool.Parameters.TryGetProperty("additionalProperties", out var additional) ? additional : (JsonElement?)null;
            Assert.True(closed?.ValueKind == JsonValueKind.False, tool.Name + " as sent");
        }
    }

    [Fact]
    public void Sends_complete_step_as_the_plain_object_the_errorCode_rule_is_its_description_never_a_keyword_or_a_refinement()
    {
        var schema = AgentTools.CreateVerdictTool().Tool.Parameters;
        Assert.Equal(["additionalProperties", "properties", "required", "type"], schema.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Contains("a passed step carries none", schema.GetProperty("properties").GetProperty("code").GetProperty("description").GetString(), StringComparison.Ordinal);
    }
}
