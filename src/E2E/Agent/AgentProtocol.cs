// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E;

/// <summary>
/// The three answers a judgment can give (upstream's <c>JudgmentVerdict</c>). <see cref="Holds"/> and <see cref="Fails"/> are
/// product verdicts. <see cref="Inconclusive"/> says the observation does not carry enough evidence to decide either way; the
/// runner treats it as a failure, never as a pass, because a judge that guesses when the screen is silent is how a broken flow
/// stays green.
/// </summary>
internal enum JudgmentVerdict
{
    Holds,
    Fails,
    Inconclusive,
}

/// <summary>An <c>agent-judgment-2</c> response that passed <see cref="AgentProtocol.ValidateJudgmentResponse"/> (upstream's <c>JudgmentResponse</c>).</summary>
internal sealed record JudgmentResponse(JudgmentVerdict Verdict, string Explanation)
{
    public const string ProtocolVersion = "agent-judgment-2";
}

/// <summary>Upstream's <c>ProtocolValidation&lt;T&gt;</c>: a value that matched the grammar, or the issue that says why not.</summary>
internal readonly record struct ProtocolValidation<T>(bool Ok, T? Value, string? Issue)
{
    public static ProtocolValidation<T> Success(T value) => new(true, value, null);

    public static ProtocolValidation<T> Fail(string issue) => new(false, default, issue);
}

/// <summary>
/// Upstream's <c>agent/protocol.ts</c>: the closed <c>agent-protocol-1</c> response grammar of a judgment. It mirrors
/// <c>schema/agent-judgment-v2.schema.json</c>; the schema wins on any divergence, and <c>AgentJudgmentSchema/AgentJudgment2Tests</c>
/// holds both to the same answers. Validation is runner-owned: a response that does not match exactly is a policy error before
/// any engine dispatch. Upstream's extract envelope (<c>extractSchema</c>, <c>validateExtractResponse</c>) is not ported: the port
/// extracts through its <c>extract</c> tool.
/// </summary>
internal static class AgentProtocol
{
    private const int ExplanationMaxLength = 8192;

    /// <summary>The name a provider is asked to give the judgment structured output (upstream's <c>schemaName</c> of the judgment call).</summary>
    public const string JudgmentSchemaName = "agent-judgment-2";

    private static readonly string[] JudgmentVerdicts = ["holds", "fails", "inconclusive"];

    /// <summary>
    /// The provider schema of a judgment. The explanation comes before the verdict on purpose: providers that emit structured
    /// output in schema order then make the model reason before it decides, and a verdict written first was seen to contradict
    /// the reasoning that followed it.
    /// </summary>
    public static JsonElement JudgmentSchema { get; } = JsonSerializer.SerializeToElement(
        new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = new JsonArray("protocolVersion", "explanation", "verdict"),
            ["properties"] = new JsonObject
            {
                ["protocolVersion"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray(JudgmentResponse.ProtocolVersion) },
                ["explanation"] = new JsonObject { ["type"] = "string", ["maxLength"] = ExplanationMaxLength },
                ["verdict"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray([.. JudgmentVerdicts.Select(verdict => (JsonNode)JsonValue.Create(verdict)!)]) },
            },
        });

    public static ProtocolValidation<JudgmentResponse> ValidateJudgmentResponse(JsonNode? value)
    {
        var record = AsClosedRecord(value, ["protocolVersion", "verdict", "explanation"]);
        if (record is null)
        {
            return ProtocolValidation<JudgmentResponse>.Fail("response is not an agent-judgment-2 object");
        }

        if (!string.Equals(StringOf(record["protocolVersion"]), JudgmentResponse.ProtocolVersion, StringComparison.Ordinal))
        {
            return ProtocolValidation<JudgmentResponse>.Fail("unknown protocolVersion");
        }

        var index = Array.IndexOf(JudgmentVerdicts, StringOf(record["verdict"]));
        if (index < 0)
        {
            return ProtocolValidation<JudgmentResponse>.Fail("verdict must be \"holds\", \"fails\", or \"inconclusive\"");
        }

        var explanation = AsBoundedString(record["explanation"], 0, ExplanationMaxLength);
        if (explanation is null)
        {
            return ProtocolValidation<JudgmentResponse>.Fail("explanation must be a bounded string");
        }

        return ProtocolValidation<JudgmentResponse>.Success(new JudgmentResponse((JudgmentVerdict)index, explanation));
    }

    /// <summary>The text of a string value, or null for anything else.</summary>
    private static string? StringOf(JsonNode? node) =>
        node is JsonValue scalar && scalar.TryGetValue<string>(out var text) ? text : null;

    /// <summary>Accepts an object whose keys are exactly within the allowed set.</summary>
    private static JsonObject? AsClosedRecord(JsonNode? value, string[] allowed)
    {
        if (value is not JsonObject record)
        {
            return null;
        }

        return record.Any(pair => !allowed.Contains(pair.Key, StringComparer.Ordinal)) ? null : record;
    }

    private static string? AsBoundedString(JsonNode? value, int min, int max)
    {
        var text = StringOf(value);
        if (text is null)
        {
            return null;
        }

        var length = CodePointLength(text);
        return length < min || length > max ? null : text;
    }

    /// <summary>
    /// Counts code points, the unit JSON Schema <c>maxLength</c> measures. <c>Length</c> counts UTF-16 units, so an explanation
    /// with astral characters would fail here while the schema accepts it. <see cref="System.Text.Rune"/> enumeration pairs
    /// surrogates and counts a lone one as a single code point, as Ajv's <c>ucs2length</c> does.
    /// </summary>
    private static int CodePointLength(string value)
    {
        var count = 0;
        foreach (var _ in value.EnumerateRunes())
        {
            count++;
        }

        return count;
    }
}
