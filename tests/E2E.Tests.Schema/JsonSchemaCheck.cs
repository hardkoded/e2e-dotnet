// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using Json.Schema;

namespace E2E.Tests;

/// <summary>
/// Compiles a JSON Schema document and answers whether a value satisfies it (the Ajv instance of upstream's
/// <c>agent-judgment-schema.test.ts</c> and <c>schema-fixtures.test.ts</c>). Like <see cref="ReportSchema"/>, it keeps JsonSchema.Net
/// and its top-level <c>Json</c> namespace out of the test projects.
/// </summary>
public sealed class JsonSchemaCheck
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, JsonSchemaCheck> Files = new(StringComparer.Ordinal);

    private readonly JsonSchema _schema;

    private JsonSchemaCheck(JsonSchema schema) => _schema = schema;

    /// <summary>Compiles the schema file <paramref name="name"/> under the <c>schema</c> directory.</summary>
    public static JsonSchemaCheck FromFile(string name) =>
        // A schema's $id is registered once per process, so each file compiles once.
        Files.GetOrAdd(name, file => FromText(File.ReadAllText(ReportSchema.SchemaPath(file))));

    public static JsonSchemaCheck FromText(string schema) => new(JsonSchema.FromText(schema));

    /// <summary>True when the value satisfies the schema. Format is asserted, as upstream's Ajv does with ajv-formats.</summary>
    public bool IsValid(JsonNode? value) =>
        _schema.Evaluate(System.Text.Json.JsonSerializer.SerializeToElement(value), new EvaluationOptions { OutputFormat = OutputFormat.List, RequireFormatValidation = true }).IsValid;
}
