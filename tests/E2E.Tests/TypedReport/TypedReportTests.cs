// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Tests.TypedReport;

/// <summary>
/// .NET-only: upstream's <c>Report</c> is the object its document is built from, where the port reads the typed report from the document.
/// These tests check that the read loses nothing the typed report has a member for.
/// </summary>
public sealed class TypedReportTests
{
    [Fact]
    public void Tells_every_field_of_the_valid_fixture_that_the_port_reports()
    {
        var document = (JsonObject)ReportSchema.ReadJson("fixtures", "report-v1.valid.json");
        var report = ReportSerializer.Read(document);

        // The port has no serial groups and no carried block, and the typed report leaves out the stack and the extensions of a document.
        var run = document["run"]!.AsObject();
        run.Remove("serialGroups");
        run.Remove("carried");
        Strip(document);

        var difference = FirstDifference(document, JsonNode.Parse(ReportSerializer.Write(report)), "$");
        Assert.True(difference.Length == 0, difference);
    }

    [Fact]
    public void Reads_the_values_of_the_valid_fixture_as_their_types()
    {
        var report = ReportSerializer.Read((JsonObject)ReportSchema.ReadJson("fixtures", "report-v1.valid.json"));

        Assert.Equal("report-1", report.SchemaVersion);
        Assert.Equal(ReportRunStatus.Failed, report.Run.Status);
        Assert.Contains(report.Run.Results, result => result is { Status: Reports.ResultStatus.Skipped, Skip.Cause: Reports.SkipCause.Explicit });
        Assert.Contains(report.Run.Results.SelectMany(result => result.Attempts).SelectMany(attempt => attempt.Steps), step => step.Kind == Reports.StepKind.Agent);
    }

    private static string FirstDifference(JsonNode? expected, JsonNode? actual, string path)
    {
        if (JsonNode.DeepEquals(expected, actual))
        {
            return "";
        }

        if (expected is JsonObject left && actual is JsonObject right)
        {
            foreach (var name in left.Select(pair => pair.Key).Union(right.Select(pair => pair.Key)))
            {
                var found = FirstDifference(left[name], right[name], path + "." + name);
                if (found.Length > 0)
                {
                    return found;
                }
            }
        }

        if (expected is JsonArray first && actual is JsonArray second && first.Count == second.Count)
        {
            for (var index = 0; index < first.Count; index++)
            {
                var found = FirstDifference(first[index], second[index], path + "[" + index + "]");
                if (found.Length > 0)
                {
                    return found;
                }
            }
        }

        return $"{path}: expected {expected?.ToJsonString()} but was {actual?.ToJsonString()}";
    }

    private static void Strip(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                obj.Remove("extensions");
                obj.Remove("stack");
                obj.Remove("serialGroupId");
                foreach (var child in obj.Select(pair => pair.Value).ToList())
                {
                    Strip(child);
                }

                break;
            case JsonArray array:
                foreach (var child in array)
                {
                    Strip(child);
                }

                break;
        }
    }
}
