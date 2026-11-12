// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Tests.AgentJudgmentSchema;

/// <summary>
/// <c>describe('agent-judgment-2')</c> of <c>tests/unit/agent-judgment-schema.test.ts</c>. <c>ValidateJudgmentResponse</c> and
/// <c>schema/agent-judgment-v2.schema.json</c> give the same answer for every input. The schema wins on divergence, so a case that
/// fails here is a runtime bug. The provider-facing <c>JudgmentSchema</c>, the copy the model is asked to fill, is held to the same answers.
/// </summary>
public sealed class AgentJudgment2Tests
{
    private static readonly JsonSchemaCheck WireSchema = JsonSchemaCheck.FromFile("agent-judgment-v2.schema.json");

    private static readonly JsonSchemaCheck ProviderSchema = JsonSchemaCheck.FromText(AgentProtocol.JudgmentSchema.GetRawText());

    private const string Astral = "\U0001F600";

    private const string LoneSurrogate = "\uD83D";

    private static JsonObject Judgment(string? key = null, JsonNode? value = null, bool remove = false)
    {
        var record = new JsonObject
        {
            ["protocolVersion"] = "agent-judgment-2",
            ["verdict"] = "holds",
            ["explanation"] = "the dashboard is visible",
        };
        if (remove)
        {
            record.Remove(key!);
        }
        else if (key is not null)
        {
            record[key] = value;
        }

        return record;
    }

    private static string Repeat(string text, int count) => string.Concat(Enumerable.Repeat(text, count));

    private static readonly (string Name, Func<JsonNode?> Value, bool Accepted)[] Cases =
    [
        ("the valid fixture", () => ReportSchema.ReadJson("fixtures", "agent-judgment-v2.valid.json"), true),
        ("the invalid fixture", () => ReportSchema.ReadJson("fixtures", "agent-judgment-v2.invalid.json"), false),
        ("a fails verdict", () => Judgment("verdict", "fails"), true),
        ("an inconclusive verdict", () => Judgment("verdict", "inconclusive"), true),
        ("an empty explanation", () => Judgment("explanation", ""), true),
        ("an explanation of 8192 characters", () => Judgment("explanation", Repeat("x", 8192)), true),
        ("an explanation of 8192 astral code points", () => Judgment("explanation", Repeat(Astral, 8192)), true),
        ("an explanation of 8193 characters", () => Judgment("explanation", Repeat("x", 8193)), false),
        ("an explanation of 8193 astral code points", () => Judgment("explanation", Repeat(Astral, 8193)), false),
        ("an explanation of 8192 lone surrogates", () => Judgment("explanation", Repeat(LoneSurrogate, 8192)), true),
        ("an explanation of 8193 lone surrogates", () => Judgment("explanation", Repeat(LoneSurrogate, 8193)), false),
        ("an extra key", () => Judgment("confidence", 0.9), false),
        ("a verdict outside the enum", () => Judgment("verdict", "true"), false),
        ("a boolean verdict", () => Judgment("verdict", true), false),
        ("a null verdict", () => Judgment("verdict", null), false),
        ("a missing verdict", () => Judgment("verdict", remove: true), false),
        ("a missing explanation", () => Judgment("explanation", remove: true), false),
        ("a missing protocolVersion", () => Judgment("protocolVersion", remove: true), false),
        ("a non-string explanation", () => Judgment("explanation", 42), false),
        ("the agent-judgment-1 shape", () => new JsonObject { ["protocolVersion"] = "agent-judgment-1", ["result"] = true, ["explanation"] = "yes" }, false),
        ("null", () => null, false),
    ];

    public static TheoryData<string> CaseNames => [.. Cases.Select(each => each.Name)];

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void The_wire_schema_the_provider_schema_and_the_runtime_agree(string name)
    {
        var (_, value, accepted) = Cases.Single(each => each.Name == name);

        Assert.True(accepted == WireSchema.IsValid(value()), "wire schema");
        Assert.True(accepted == ProviderSchema.IsValid(value()), "provider schema");
        Assert.True(accepted == AgentProtocol.ValidateJudgmentResponse(value()).Ok, "runtime");
    }
}
