// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.NUnit.Tests.TestStep;

/// <summary>What the tests of <c>tests/integration/test-step.test.ts</c> read from a run's <c>report.json</c> (upstream's <c>resultByTitle</c>).</summary>
internal static class TestStepKit
{
    public static JsonNode Report(Type fixture) => JsonNode.Parse(File.ReadAllText(TestStepResults.ReportOf(fixture)))!;

    public static JsonNode ResultByTitle(JsonNode report, string method) =>
        report["run"]!["results"]!
            .AsArray()
            .Single(found =>
                ((string?)found!["titlePath"]!.AsArray().Last())!.EndsWith("." + method, StringComparison.Ordinal)
            )!;

    public static JsonArray StepsOf(JsonNode result) => result["attempts"]![0]!["steps"]!.AsArray();

    /// <summary><c>[index, kind, api, label, parent, phase, status]</c> of every step: a label is told only for a <c>step</c>, and is <c>...</c> for any other.</summary>
    public static List<(
        int Index,
        string Kind,
        string Api,
        string Label,
        int? Parent,
        string? Phase,
        string Status
    )> Told(JsonNode result) =>
        [
            .. StepsOf(result)
                .Select(step =>
                    (
                        (int)step!["index"]!,
                        (string)step["kind"]!,
                        (string)step["api"]!,
                        (string)step["label"]! == "" ? ""
                        : (string)step["kind"]! == "step" ? (string)step["label"]!
                        : "...",
                        (int?)step["parent"],
                        (string?)step["phase"],
                        (string)step["status"]!
                    )
                ),
        ];
}
