// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Reports;

namespace E2E.Tests.LastRun;

/// <summary>
/// Upstream's helpers in <c>last-run.test.ts</c> (<c>document</c>, <c>report</c>, <c>reportFile</c>): report documents whose results each name a test by
/// <c>id</c> as their test id and title, in <c>tests/a.e2e.ts</c> on target <c>web</c> as agent <c>default</c>, selected, with no attempts, unless a result
/// says otherwise. Built as JSON, as upstream's are, and read into the typed <see cref="Report"/> the way a run reads them.
/// </summary>
internal static class LastRunDocuments
{
    /// <summary>Upstream's <c>row</c>: the defaults, then the result's own fields; a field set to null is absent.</summary>
    public static JsonObject Row(JsonObject result)
    {
        var id = result["id"]!.DeepClone();
        var row = new JsonObject
        {
            ["testId"] = id.DeepClone(),
            ["titlePath"] = new JsonArray(id.DeepClone()),
            ["file"] = "tests/a.e2e.ts",
            ["targetId"] = "web",
            ["agent"] = "default",
            ["selected"] = true,
            ["attempts"] = new JsonArray(),
        };
        Spread(row, result);
        return row;
    }

    /// <summary>Upstream's <c>document</c>: <paramref name="run"/> adds or overrides run fields; a <c>carried</c> there gets the same result defaults.</summary>
    public static JsonObject Document(IEnumerable<JsonObject> results, IEnumerable<JsonObject>? errors = null, JsonObject? run = null)
    {
        var part = new JsonObject
        {
            ["results"] = new JsonArray([.. results.Select(result => (JsonNode)Row(result))]),
            ["errors"] = new JsonArray([.. (errors ?? []).Select(error => (JsonNode)error.DeepClone())]),
            ["serialGroups"] = new JsonArray(),
            ["targets"] = new JsonArray(new JsonObject { ["id"] = "web" }, new JsonObject { ["id"] = "mobile" }),
        };
        if (run is not null)
        {
            Spread(part, run);
            if (run["carried"] is JsonObject carried)
            {
                var withDefaults = new JsonObject { ["serialGroups"] = new JsonArray(), ["errors"] = new JsonArray() };
                Spread(withDefaults, carried);
                withDefaults["results"] = new JsonArray([.. carried["results"]!.AsArray().Select(result => (JsonNode)Row(result!.AsObject()))]);
                part["carried"] = withDefaults;
            }
        }

        return new JsonObject { ["schemaVersion"] = "report-1", ["run"] = part };
    }

    /// <summary>Upstream's <c>report</c>: <see cref="Document"/> as the text of a report file.</summary>
    public static string ReportText(IEnumerable<JsonObject> results, IEnumerable<JsonObject>? errors = null, JsonObject? run = null) =>
        Document(results, errors, run).ToJsonString();

    /// <summary>The typed form of a document, the way <c>--last-failed</c> reads one.</summary>
    public static Report Typed(JsonObject document) => ReportSerializer.Read(document);

    /// <summary>Upstream's <c>reportFile</c>: writes <paramref name="content"/> as the report file of a fresh directory and returns its path. The directory goes with <paramref name="roots"/>.</summary>
    public static string ReportFile(ICollection<string> roots, string content)
    {
        var dir = Directory.CreateTempSubdirectory("e2e-last-run-").FullName;
        roots.Add(dir);
        var file = Path.Combine(dir, "report.json");
        File.WriteAllText(file, content);
        return file;
    }

    /// <summary>A result with the given fields.</summary>
    public static JsonObject Result(params (string Name, JsonNode? Value)[] fields)
    {
        var result = new JsonObject();
        foreach (var (name, value) in fields)
        {
            result[name] = value?.DeepClone();
        }

        return result;
    }

    /// <summary>A result the run left out with a filter.</summary>
    public static JsonObject LeftOut(string id, string reason = "title does not match --grep") =>
        Result(("id", id), ("status", "skipped"), ("selected", false), ("skip", Result(("cause", "filtered"), ("reason", reason))));

    /// <summary>The id <c>--last-failed</c> names a test on the default target and agent by.</summary>
    public static string IdOf(string testId) => E2E.Internal.Ids.ResultId(testId, "web", "default");

    // `{ ...target, ...source }`: a source field overrides, and one set to null is dropped, as `JSON.stringify` drops undefined.
    private static void Spread(JsonObject target, JsonObject source)
    {
        foreach (var (name, value) in source)
        {
            if (value is null)
            {
                target.Remove(name);
            }
            else
            {
                target[name] = value.DeepClone();
            }
        }
    }
}
