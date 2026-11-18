// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using E2E.Reports;

namespace E2E.HtmlReporter.Tests.Reporter;

/// <summary>Shared inputs of upstream's <c>reporter.test.ts</c>: the fixture report and the template a page is filled from.</summary>
internal static class ReporterFixtures
{
    public static readonly string Template =
        $"<!doctype html><script id=\"e2e-report\" type=\"application/json\">{Payload.Placeholder}</script>";

    public static JsonObject ReadReport() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "report-v1.valid.json")))!.AsObject();

    public static ReportPayload PayloadOf(JsonNode report, ReportMedia? media = null) =>
        new(
            report,
            media ?? new ReportMedia(HtmlReporterMedia.None),
            ".e2e/results",
            new Dictionary<string, string>(),
            new Dictionary<string, IReadOnlyDictionary<string, string>>()
        );

    /// <summary>The payload the page would parse back out of a filled template.</summary>
    public static JsonObject ParsePayload(string page)
    {
        var match = Regex.Match(
            page,
            "<script id=\"e2e-report\" type=\"application/json\">(.*?)</script>",
            RegexOptions.Singleline
        );
        return match.Success
            ? JsonNode.Parse(match.Groups[1].Value)!.AsObject()
            : throw new InvalidOperationException("no payload script in the page");
    }
}
