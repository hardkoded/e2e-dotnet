// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace E2E;

/// <summary>
/// The words every reporter uses for an exploration record: severity grades, why a run ended, how its steps fared, and the
/// order findings are read in. Upstream's <c>report/explore-text.ts</c>, so a wording change lands in every reporter at once.
/// </summary>
internal static class ExploreText
{
    /// <summary>Upstream's <c>SEVERITY_WORDS</c>: 5 is critical, 1 is trivial.</summary>
    public static string SeverityWord(int severity) => severity switch
    {
        5 => "critical",
        4 => "high",
        3 => "medium",
        2 => "low",
        _ => "trivial",
    };

    /// <summary>Upstream's <c>ENDED_TEXT</c>.</summary>
    public static string EndedText(ReportExploreEnded ended) => ended switch
    {
        ReportExploreEnded.Finished => "the agent covered the goal",
        ReportExploreEnded.StepLimit => "the step limit was reached",
        ReportExploreEnded.Time => "the time budget ran out",
        ReportExploreEnded.Stuck => "steps kept failing",
        _ => "the run was cut short",
    };

    /// <summary>Issues before warnings, the most severe first, then in the order reported.</summary>
    public static IReadOnlyList<ReportExploreFinding> OrderFindings(IEnumerable<ReportExploreFinding> findings) =>
        [.. findings
            .OrderBy(finding => finding.Kind == ReportExploreFindingKind.Warning ? 1 : 0)
            .ThenByDescending(finding => finding.Severity)
            .ThenBy(finding => finding.Index)];

    /// <summary>`3 passed`, `1 failed`, `2 ended at their limit`, `1 blocked` over the steps, zero counts left out, each with its status for coloring.</summary>
    public static IReadOnlyList<(ReportExploreStepStatus Status, string Text)> StepCountParts(IEnumerable<ReportExploreStep> steps)
    {
        var counts = steps.GroupBy(step => step.Status).ToDictionary(group => group.Key, group => group.Count());
        List<(ReportExploreStepStatus, string)> parts = [];
        void Add(ReportExploreStepStatus status, string text)
        {
            if (counts.TryGetValue(status, out var count))
            {
                parts.Add((status, count.ToString(CultureInfo.InvariantCulture) + " " + text));
            }
        }

        Add(ReportExploreStepStatus.Passed, "passed");
        Add(ReportExploreStepStatus.Failed, "failed");
        Add(ReportExploreStepStatus.Exhausted, "ended at their limit");
        Add(ReportExploreStepStatus.Blocked, "blocked");
        return parts;
    }
}
