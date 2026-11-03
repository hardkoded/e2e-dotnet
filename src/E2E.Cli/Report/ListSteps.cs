// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Cli.Report;

/// <summary>
/// Renders one agent step and its replay notices for the list reporter (upstream's <c>report/list-steps.ts</c>): the same finished step prints
/// in a CI log, in a file block, and in the live window, each at its own indent, so nothing here knows where the line lands. Not ported:
/// <c>eventLine</c>, which only the live window prints.
/// </summary>
internal static class ListSteps
{
    /// <summary>Step labels stay one glanceable line; the report holds the full text.</summary>
    private const int MaxStepLabelColumns = 72;

    /// <summary>Recording model labels stay on one bounded terminal line.</summary>
    private const int MaxRecordedModelColumns = 120;

    /// <summary>A clipped step line keeps at least this much of its label, however narrow the terminal.</summary>
    private const int MinStepLabelColumns = 24;

    /// <summary>Columns a step line spends around its label: the quotes and the space before the tail.</summary>
    private const int LabelChrome = 3;

    /// <summary>One-line, quoted step label; <paramref name="maxColumns"/> clips it further to fit a row (upstream's <c>quotedLabel</c>).</summary>
    public static string QuotedLabel(string label, int maxColumns = MaxStepLabelColumns)
    {
        var max = Math.Min(MaxStepLabelColumns, Math.Max(MinStepLabelColumns, maxColumns));
        return $"\"{Format.FitColumns(TextRules.Collapse(label), max)}\"";
    }

    /// <summary>
    /// One finished agent step: outcome glyph, api, quoted label, then duration, model calls, and a non-passed status (upstream's <c>stepLine</c>).
    /// <paramref name="context"/> is the text before the api, naming the test when the line prints away from it; <paramref name="maxWidth"/> is the
    /// printed width the line may take, so the label is clipped and the tail stays on the row.
    /// </summary>
    public static string StepLine(Colors pc, FinishedStep step, string context = "", int? maxWidth = null)
    {
        var passed = step.Status == StepStatus.Passed;
        var glyph = passed ? pc.Green(Format.Check) : pc.Red(Format.Cross);
        var calls = step.ModelCalls > 0 ? $" · {step.ModelCalls} model call{(step.ModelCalls == 1 ? string.Empty : "s")}" : string.Empty;
        var outcome = passed ? string.Empty : " " + StepNames.Of(step.Status);
        var head = $"{glyph} {context}{pc.Dim(step.Api)} ";
        var tail = pc.Dim($"{Format.FormatTime(TimeSpan.FromMilliseconds(step.DurationMs))}{calls}{outcome}");
        var room = maxWidth is { } width ? width - Format.VisibleWidth(head) - Format.VisibleWidth(tail) - LabelChrome : MaxStepLabelColumns;
        return $"{head}{QuotedLabel(step.Label, room)} {tail}";
    }

    /// <summary>
    /// Recording mismatches under one test, deduplicated across its attempts (upstream's <c>replayNotices</c>). A step that ran as another agent than
    /// the configured one, that replayed nothing, or whose recording came from the configured model is no mismatch. The port's step records the
    /// recording model as one label (<see cref="CacheInfo.RecordedBy"/>), so the notice's <c>Source</c> is that label.
    /// </summary>
    public static List<ReplayNotice> ReplayNotices(IEnumerable<IReadOnlyList<StepRecord>> attempts, ConfiguredModel? configured)
    {
        if (configured is null)
        {
            return [];
        }

        var notices = new Dictionary<(string Entry, string Source), ReplayNotice>();
        foreach (var steps in attempts)
        {
            foreach (var step in steps)
            {
                if (step.Agent is not null && step.Agent != configured.Agent)
                {
                    continue;
                }

                if (step.Cache is not { RecordedBy: { } source, ReplayedActions: > 0 } cache || source == configured.Label)
                {
                    continue;
                }

                notices[(cache.Entry ?? $"{step.Index}\n{step.Api}\n{step.Label}", source)] = new ReplayNotice(step.Api, step.Label, source);
            }
        }

        return [.. notices.Values];
    }

    /// <summary>A recording mismatch under its test in the file block (upstream's <c>replayLine</c>).</summary>
    public static string ReplayLine(Colors pc, ReplayNotice notice) =>
        $"{pc.Dim(notice.Api)} {QuotedLabel(notice.Label)} {pc.Dim($"replayed · recorded with {Format.FitColumns(TextRules.Collapse(notice.Source), MaxRecordedModelColumns)}")}";
}
