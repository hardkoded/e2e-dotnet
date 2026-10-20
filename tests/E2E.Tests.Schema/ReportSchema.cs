// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace E2E.Tests;

/// <summary>
/// Validates documents against the report-1 wire schema in <c>schema/</c>, plus the one invariant the schema cannot state: a result id
/// appears once (upstream's <c>tests/helpers/report-schema.ts</c>, with the fixtures' reader of <c>schema-fixtures.test.ts</c>). The schema
/// library, JsonSchema.Net, is a test dependency only; the shipped package validates nothing. It lives in this project so its top-level
/// <c>Json</c> namespace stays out of the test projects.
/// </summary>
public static class ReportSchema
{
    private static readonly Lazy<JsonSchema> Schema = new(() => JsonSchema.FromText(File.ReadAllText(SchemaPath("report-v1.schema.json"))));

    /// <summary>The <c>schema</c> directory at the root of the repository.</summary>
    public static string Root => RootDirectory.Value;

    private static readonly Lazy<string> RootDirectory = new(FindRoot);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "E2E.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("E2E.slnx not found above " + AppContext.BaseDirectory), "schema");
    }

    /// <summary>The path of a file under the <c>schema</c> directory.</summary>
    public static string SchemaPath(params string[] segments) => Path.Combine([Root, .. segments]);

    /// <summary>A fresh copy of a JSON file under the <c>schema</c> directory, which a test may change.</summary>
    public static JsonNode ReadJson(params string[] segments) => JsonNode.Parse(File.ReadAllText(SchemaPath(segments)))!;

    /// <summary>Every failure the schema found in the document, each as <c>(keyword, message, location in the document)</c>; empty when it is valid.</summary>
    public static IReadOnlyList<(string Keyword, string Message, string Location)> Violations(JsonNode? document)
    {
        // Format is asserted, as upstream's Ajv does with ajv-formats.
        var results = Schema.Value.Evaluate(JsonSerializer.SerializeToElement(document), new EvaluationOptions { OutputFormat = OutputFormat.List, RequireFormatValidation = true });
        if (results.IsValid)
        {
            return [];
        }

        IReadOnlyList<(string Keyword, string Message, string Location)> violations = [.. (results.Details ?? []).SelectMany(detail => (detail.Errors ?? new Dictionary<string, string>()).Select(error => (error.Key, error.Value, detail.InstanceLocation.ToString())))];

        // A failure the list does not spell out must still read as one.
        if (violations.Count > 0)
        {
            return violations;
        }

        return [("schema", "the document does not match the schema", "")];
    }

    public static bool IsValid(JsonNode? document) => Violations(document).Count == 0;

    /// <summary>Every message the failed evaluation gave for the keyword <paramref name="keyword"/>.</summary>
    public static IReadOnlyList<string> Errors(JsonNode? document, string keyword) => [.. Violations(document).Where(each => each.Keyword == keyword).Select(each => each.Message)];

    /// <summary>Asserts the document is schema-valid report-1 with unique result ids; throws with details otherwise.</summary>
    public static void AssertValidReport(JsonNode document)
    {
        var violations = Violations(document);
        if (violations.Count > 0)
        {
            throw new InvalidOperationException("report-1 schema violation:\n" + string.Join('\n', violations.Select(each => each.Location + " " + each.Keyword + ": " + each.Message)));
        }

        var ids = document["run"]!["results"]!.AsArray().Select(result => (string)result!["id"]!).ToList();
        var repeated = ids.GroupBy(id => id).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        if (repeated.Count > 0)
        {
            throw new InvalidOperationException("report-1 result ids repeat: " + string.Join(", ", repeated));
        }
    }
}
