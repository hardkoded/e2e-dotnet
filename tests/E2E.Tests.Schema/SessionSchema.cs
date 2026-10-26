// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace E2E.Tests;

/// <summary>
/// Validates session envelopes against the session-1 wire schema in <c>schema/</c> (upstream's <c>tests/helpers/session-schema.ts</c>).
/// The schema library is a test dependency only, as in <see cref="ReportSchema"/>.
/// </summary>
public static class SessionSchema
{
    private static readonly Lazy<JsonSchema> Schema = new(() => JsonSchema.FromText(File.ReadAllText(ReportSchema.SchemaPath("session-v1.schema.json"))));

    /// <summary>True when the document satisfies the canonical session-1 schema.</summary>
    public static bool IsValid(JsonNode? document) => Evaluate(document).IsValid;

    /// <summary>Asserts the document is schema-valid session-1; throws with details otherwise.</summary>
    public static void AssertValidSessionEnvelope(JsonNode? document)
    {
        var results = Evaluate(document);
        if (!results.IsValid)
        {
            var details = (results.Details ?? []).SelectMany(detail => (detail.Errors ?? new Dictionary<string, string>()).Select(error => detail.InstanceLocation + " " + error.Key + ": " + error.Value));
            throw new InvalidOperationException("session-1 schema violation:\n" + string.Join('\n', details));
        }
    }

    private static EvaluationResults Evaluate(JsonNode? document) =>
        // Format is asserted, as upstream's Ajv does with ajv-formats.
        Schema.Value.Evaluate(JsonSerializer.SerializeToElement(document), new EvaluationOptions { OutputFormat = OutputFormat.List, RequireFormatValidation = true });
}
