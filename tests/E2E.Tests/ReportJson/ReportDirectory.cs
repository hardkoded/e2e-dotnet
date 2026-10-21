// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Tests.ReportJson;

/// <summary>
/// .NET-only: the output directory a test hands to its session so it can read <c>report.json</c> afterwards, where upstream's
/// <c>runProject</c> returns the report its runner wrote. The document is written when the session is disposed.
/// </summary>
internal sealed class ReportDirectory : IDisposable
{
    public string Output { get; } = Directory.CreateTempSubdirectory("e2e-report-").FullName;

    /// <summary>The results directory to give the session; <c>report.json</c> goes beside it.</summary>
    public string Results => Path.Combine(Output, "results");

    public void Dispose() => Directory.Delete(Output, recursive: true);

    /// <summary>The document, parsed and checked against the report-1 schema (upstream's <c>assertValidReport</c>).</summary>
    public JsonNode Read()
    {
        var document = JsonNode.Parse(File.ReadAllText(Path.Combine(Output, "report.json")))!;
        ReportSchema.AssertValidReport(document);
        return document;
    }

    /// <summary>The run of the document.</summary>
    public JsonNode Run() => Read()["run"]!;

    /// <summary>The attempt that ran last, of the only test in the document.</summary>
    public JsonNode LastAttempt() => Run()["results"]!.AsArray().Single()!["attempts"]!.AsArray().Last()!;

    /// <summary>The first step the last attempt recorded for <paramref name="api"/>.</summary>
    public JsonNode Step(string api) => LastAttempt()["steps"]!.AsArray().First(step => (string?)step!["api"] == api)!;
}
