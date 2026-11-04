// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Cli.Report;

/// <summary>
/// Renders one agent step and its replay notices for the list reporter (upstream's <c>report/list-steps.ts</c>): the same finished step prints
/// in a CI log, in a file block, and in the live window, each at its own indent, so nothing here knows where the line lands.
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

    /// <summary>A finished model turn.</summary>
    private const string FModel = "•";

    /// <summary>A tool call the model made.</summary>
    private const string FTool = "›";

    private const string FInput = "↑";

    private const string FOutput = "↓";

    /// <summary>Columns the <c> · </c> between a turn's timing and its reasoning excerpt takes.</summary>
    private const int ReasoningSeparatorWidth = 3;

    /// <summary>A narrower clip than this hides more than it says; the excerpt is dropped instead.</summary>
    private const int MinReasoningColumns = 24;

    /// <summary>Longest tool call act the live window names, in bytes.</summary>
    private const int MaxActBytes = 60;

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

    /// <summary>
    /// <c>(↑in ↓out)</c> for one model call, the total when only that is known, or nothing when the provider reported no usage (upstream's <c>tokenSplit</c>).
    /// </summary>
    private static string TokenSplit(Reports.StepEvent stepEvent)
    {
        if (stepEvent.InputTokens is { } input && stepEvent.OutputTokens is { } output)
        {
            return $" ({FInput}{Format.FormatTokens(input)} {FOutput}{Format.FormatTokens(output)})";
        }

        return stepEvent.Count is > 0 and var count ? $" ({Format.FormatTokens(count)} tokens)" : string.Empty;
    }

    /// <summary>
    /// One model turn or tool call under the current step (upstream's <c>eventLine</c>). A tool call reads as the act it performed when the
    /// engine described it, else as the tool name (<c>tool:</c> prefixes come from executor tool accounting). A model turn the model reasoned its
    /// way through appends an excerpt of that reasoning, clipped to the row: why it acted, not just that it did.
    /// </summary>
    public static string EventLine(Colors pc, Reports.StepEvent stepEvent, int? maxWidth = null)
    {
        if (stepEvent.Kind == Reports.StepEventKind.Model)
        {
            var head = $"{FModel} Thinking ({Format.FormatTime(TimeSpan.FromMilliseconds(stepEvent.DurationMs))}){TokenSplit(stepEvent)}";
            var reasoning = ReasoningExcerpt(stepEvent.Reasoning, Format.VisibleWidth(head), maxWidth);
            return pc.Dim(reasoning is null ? head : $"{head} · {reasoning}");
        }

        var name = TextRules.SanitizeText(stepEvent.Name ?? "engine");
        name = name.StartsWith("tool:", StringComparison.Ordinal) ? name["tool:".Length..] : name;
        var act = stepEvent.Detail is null ? name : TextRules.SanitizeText(TextRules.Collapse(stepEvent.Detail));
        var failed = stepEvent.Status == Reports.StepEventStatus.Passed ? string.Empty : $" {pc.Red(Format.Cross)}";
        return $"{pc.Dim($"{FTool} {TextRules.TruncateUtf8(act, MaxActBytes)} ({Format.FormatTime(TimeSpan.FromMilliseconds(stepEvent.DurationMs))})")}{failed}";
    }

    /// <summary>The one-line form of a turn's reasoning for its Thinking row, or null when the row has no room worth reading at (upstream's <c>reasoningExcerpt</c>).</summary>
    private static string? ReasoningExcerpt(string? reasoning, int headWidth, int? maxWidth)
    {
        if (reasoning is null)
        {
            return null;
        }

        var room = (maxWidth ?? Format.TerminalColumns()) - headWidth - ReasoningSeparatorWidth;
        return room < MinReasoningColumns ? null : Format.FitColumns(TextRules.Collapse(reasoning), room);
    }
}
