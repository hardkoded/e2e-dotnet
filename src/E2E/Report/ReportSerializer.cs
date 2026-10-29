// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using E2E.Reports;

namespace E2E;

/// <summary>
/// Reads a report-1 document (the JSON <see cref="ReportWire"/> writes) into a <see cref="Report"/>. .NET-only: upstream's <c>Report</c> is the object the document is built from, and the port builds the document
/// as JSON, so the typed form is read from it. The document stays the single source of what a report says.
/// </summary>
internal static class ReportSerializer
{
    /// <summary>Camel-case names and enum values in the schema's spelling.</summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    /// <summary>The typed form of <paramref name="document"/>, a document <see cref="ReportWire.Document(Report1Document, IReadOnlyDictionary{string, JsonNode}, string?)"/> wrote.</summary>
    public static Report Read(JsonObject document) =>
        document.Deserialize<Report>(Options) ?? throw new InvalidOperationException("The report document is empty.");

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new ReportExploreConverter() },
        };

        // The schema spells an enum value in kebab case (timed-out), but the hook phases in camel case (beforeEach) and a vision degradation
        // in upper snake case (PIXEL_TAINTED).
        foreach (var type in typeof(Report).Assembly.GetTypes().Where(type => type.IsEnum && (type.Namespace == typeof(Report).Namespace || type == typeof(ReportRunStatus))).Concat([typeof(ReportExploreFindingKind), typeof(ReportExploreStepStatus), typeof(ReportExploreEnded)]))
        {
            var policy = type == typeof(Reports.ErrorPhase) || type == typeof(Reports.StepPhase)
                ? JsonNamingPolicy.CamelCase
                : type == typeof(Reports.VisionDegradation)
                    ? JsonNamingPolicy.SnakeCaseUpper
                    : JsonNamingPolicy.KebabCaseLower;
            options.Converters.Add((JsonConverter)Activator.CreateInstance(typeof(JsonStringEnumConverter<>).MakeGenericType(type), policy, false)!);
        }

        return options;
    }

    /// <summary>
    /// The <c>run.explore</c> block. The public <see cref="ReportExplore"/> keeps its durations as <see cref="TimeSpan"/>, where the document
    /// counts milliseconds.
    /// </summary>
    private sealed class ReportExploreConverter : JsonConverter<ReportExplore>
    {
        public override ReportExplore Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var node = JsonNode.Parse(ref reader)!;
            var budgets = node["budgets"]!;
            return new ReportExplore(
                (string)node["goal"]!,
                new ExploreBudgets((int)budgets["maxSteps"]!, TimeSpan.FromMilliseconds((long)budgets["timeoutMs"]!)),
                node["ended"].Deserialize<ReportExploreEnded>(options),
                [.. node["steps"]!.AsArray().Select(step => new ReportExploreStep(
                    (int)step!["index"]!,
                    (string)step["title"]!,
                    (string)step["instruction"]!,
                    step["status"].Deserialize<ReportExploreStepStatus>(options),
                    step["startedAt"].Deserialize<DateTimeOffset>(options),
                    TimeSpan.FromMilliseconds((long)step["durationMs"]!),
                    (string?)step["summary"],
                    (string?)step["errorCode"]))],
                [.. node["findings"]!.AsArray().Select(finding => new ReportExploreFinding(
                    (string)finding!["id"]!,
                    (int)finding["index"]!,
                    (int?)finding["step"],
                    finding["kind"].Deserialize<ReportExploreFindingKind>(options),
                    (int)finding["severity"]!,
                    (string)finding["title"]!,
                    (string)finding["expected"]!,
                    (string)finding["actual"]!,
                    [.. finding["reproduction"]!.AsArray().Select(line => (string)line!)],
                    finding["reportedAt"].Deserialize<DateTimeOffset>(options),
                    (string?)finding["path"],
                    null,
                    (string?)finding["observationRevision"]))],
                (string?)node["summary"]);
        }

        public override void Write(Utf8JsonWriter writer, ReportExplore value, JsonSerializerOptions options) => throw new NotSupportedException();
    }
}
