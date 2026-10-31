// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace E2E.Cli.Report;

/// <summary>
/// Terminal colors. Upstream's <c>Colors</c> is a picocolors instance; this is the part the exploration view uses, and it
/// writes plain text when colors are off.
/// </summary>
internal sealed class Colors(bool enabled)
{
    public bool Enabled { get; } = enabled;

    public string Bold(string text) => Wrap(text, 1, 22);

    public string Dim(string text) => Wrap(text, 2, 22);

    public string Inverse(string text) => Wrap(text, 7, 27);

    public string Red(string text) => Wrap(text, 31, 39);

    public string Green(string text) => Wrap(text, 32, 39);

    public string Yellow(string text) => Wrap(text, 33, 39);

    public string Cyan(string text) => Wrap(text, 36, 39);

    public string Gray(string text) => Wrap(text, 90, 39);

    public string Black(string text) => Wrap(text, 30, 39);

    public string BgRed(string text) => Wrap(text, 41, 49);

    /// <summary>Bright yellow, cyan, green, and magenta backgrounds, by the target's declaration order (upstream's <c>BADGE_COLORS</c>); GitHub Actions renders plain yellow as dark brown.</summary>
    public string BgBright(int index, string text) => Wrap(text, (index % 4) switch { 0 => 103, 1 => 106, 2 => 102, _ => 105 }, 49);

    /// <summary>Black on cyan, as the list reporter's target badge.</summary>
    public string Badge(string text) => Enabled ? Wrap(Wrap(text, 46, 49), 30, 39) : text;

    private string Wrap(string text, int open, int close) =>
        Enabled ? "\u001b[" + open.ToString(CultureInfo.InvariantCulture) + "m" + text + "\u001b[" + close.ToString(CultureInfo.InvariantCulture) + "m" : text;
}

/// <summary>Where <see cref="Format.Rule(string, RuleAlign)"/> puts its text.</summary>
internal enum RuleAlign
{
    Center,
    Right,
}

/// <summary>
/// Pure presentation helpers shared by the terminal reporters: glyphs, durations, and widths. Upstream's <c>report/format.ts</c>,
/// the part the exploration view uses and the cache tally of the run summary row.
/// </summary>
internal static partial class Format
{
    /// <summary>Glyphs shared by the list reporter's permanent lines.</summary>
    public const string Pointer = "❯";

    public const string Check = "✓";

    public const string Cross = "×";

    /// <summary>Every untrusted field is capped here before it reaches the terminal.</summary>
    private const int MaxFieldBytes = 8192;

    private const char LongDash = '⎯';

    /// <summary>Width of the summary's right-aligned title column (<c>Test Files</c>).</summary>
    private const int TitleWidth = 11;

    /// <summary>
    /// Sanitized and bounded: the one form untrusted text takes on the terminal. Tabs become spaces, since a tab's width depends on
    /// the column it lands in.
    /// </summary>
    public static string Bounded(string text)
    {
        var clean = Program.Clean(text);
        if (Encoding.UTF8.GetByteCount(clean) > MaxFieldBytes)
        {
            // Whole characters only, in one pass.
            int bytes = 0, end = 0;
            foreach (var rune in clean.EnumerateRunes())
            {
                if (bytes + rune.Utf8SequenceLength > MaxFieldBytes)
                {
                    break;
                }

                bytes += rune.Utf8SequenceLength;
                end += rune.Utf16SequenceLength;
            }

            clean = clean[..end];
        }

        return clean.Replace('\t', ' ');
    }

    /// <summary>vitest's duration format: whole milliseconds up to a second, two decimals above.</summary>
    public static string FormatTime(TimeSpan duration)
    {
        var ms = duration.TotalMilliseconds;
        return ms > 1_000
            ? (ms / 1_000).ToString("F2", CultureInfo.InvariantCulture) + "s"
            : Math.Round(ms, MidpointRounding.AwayFromZero).ToString("F0", CultureInfo.InvariantCulture) + "ms";
    }

    /// <summary>Wall-clock <c>HH:MM:SS</c> for the summary's <c>Start at</c> row.</summary>
    public static string FormatClock(DateTimeOffset time) => time.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>The trace cache's part in a run's steps, by step (upstream's <c>CacheTally</c>).</summary>
    public sealed class CacheTally
    {
        public int Replayed { get; set; }

        public int HandedOff { get; set; }

        public int Missed { get; set; }
    }

    /// <summary>Upstream's <c>emptyCacheTally</c>.</summary>
    public static CacheTally EmptyCacheTally() => new();

    /// <summary>Upstream's <c>addCacheTally</c>.</summary>
    public static void AddCacheTally(CacheTally into, CacheTally counts)
    {
        into.Replayed += counts.Replayed;
        into.HandedOff += counts.HandedOff;
        into.Missed += counts.Missed;
    }

    /// <summary>Upstream's <c>stepsCacheTally</c>: the trace cache's part in several step lists, by step. A step with no cache detail counts nowhere.</summary>
    public static CacheTally StepsCacheTally(IEnumerable<IEnumerable<StepRecord>> stepLists)
    {
        var counts = EmptyCacheTally();
        foreach (var step in stepLists.SelectMany(steps => steps))
        {
            switch (step.Cache?.Mode)
            {
                case "self-finalized":
                    counts.Replayed++;
                    break;
                case "agent-concluded":
                    counts.HandedOff++;
                    break;
                case "missed":
                    counts.Missed++;
                    break;
            }
        }

        return counts;
    }

    /// <summary>
    /// Upstream's <c>cacheText</c>: <c>9 replayed · 2 handed off · 4 missed</c>, zero counts left out, or null when no step went
    /// through the trace cache.
    /// </summary>
    public static string? CacheText(Colors pc, CacheTally counts)
    {
        List<string> parts = [];
        if (counts.Replayed > 0)
        {
            parts.Add(pc.Green(counts.Replayed.ToString(CultureInfo.InvariantCulture) + " replayed"));
        }

        if (counts.HandedOff > 0)
        {
            parts.Add(pc.Yellow(counts.HandedOff.ToString(CultureInfo.InvariantCulture) + " handed off"));
        }

        if (counts.Missed > 0)
        {
            parts.Add(counts.Missed.ToString(CultureInfo.InvariantCulture) + " missed");
        }

        return parts.Count == 0 ? null : string.Join(pc.Dim(" · "), parts);
    }

    /// <summary>One padded summary title, right-aligned like vitest's <c>Test Files</c> column.</summary>
    public static string PadTitle(Colors pc, string title) => pc.Dim(title.PadLeft(TitleWidth) + "  ");

    /// <summary>
    /// The dashes of a full-width rule around <paramref name="text"/>, centered. Uncolored, so the caller colors the dashes and the
    /// text independently.
    /// </summary>
    public static (string Before, string After) Rule(string text, int columns)
    {
        var width = VisibleWidth(text);
        var after = Math.Max(0, (int)Math.Ceiling((columns - width) / 2.0));
        var before = Math.Max(0, columns - width - after);
        return (new string(LongDash, before), new string(LongDash, after));
    }

    /// <summary>Printed width of a line in text elements, ANSI sequences excluded. Wide East Asian characters count as one.</summary>
    public static int VisibleWidth(string text) => new StringInfo(Ansi().Replace(text, string.Empty)).LengthInTextElements;

    /// <summary>Clips plain text to <paramref name="columns"/> text elements with the ellipsis inside the budget; untouched when it fits.</summary>
    public static string FitColumns(string text, int columns)
    {
        var info = new StringInfo(text);
        return info.LengthInTextElements <= columns ? text : info.SubstringByTextElements(0, Math.Max(0, columns - 1)) + "…";
    }

    /// <summary>The terminal width; a terminal without a size (or none) gives 80 (upstream's <c>terminalColumns</c>).</summary>
    public static int TerminalColumns()
    {
        try
        {
            return Console.IsOutputRedirected ? 80 : Console.WindowWidth is var width and > 0 ? width : 80;
        }
        catch (Exception ex) when (ex is IOException or PlatformNotSupportedException or InvalidOperationException)
        {
            return 80;
        }
    }

    /// <summary>A span as <c>11m 23s</c>. Rounds to whole seconds first, so 119.5 s is <c>2m 0s</c> and never <c>1m 60s</c> (upstream's <c>formatMinutes</c>).</summary>
    public static string FormatMinutes(TimeSpan span)
    {
        var total = (long)Math.Round(span.TotalSeconds, MidpointRounding.AwayFromZero);
        return (total / 60).ToString(CultureInfo.InvariantCulture) + "m " + (total % 60).ToString(CultureInfo.InvariantCulture) + "s";
    }

    /// <summary>
    /// Upstream's <c>durationText</c>: the summary's <c>Duration</c> cell, the seconds, then dimmed in parentheses the same span in minutes
    /// once it has one and the startup split when a process started.
    /// </summary>
    public static string DurationText(Colors pc, TimeSpan elapsed, TimeSpan startup)
    {
        List<string> aside = [];
        if (elapsed.TotalMilliseconds >= 60_000)
        {
            aside.Add(FormatMinutes(elapsed));
        }

        if (startup > TimeSpan.Zero)
        {
            aside.Add("startup " + FormatTime(startup));
        }

        var time = FormatTime(elapsed);
        return aside.Count == 0 ? time : time + " " + pc.Dim("(" + string.Join(", ", aside) + ")");
    }

    /// <summary>Compact token count: plain under a thousand, <c>12.4k</c>, then <c>4.2M</c> (upstream's <c>formatTokens</c>).</summary>
    public static string FormatTokens(long count) =>
        count < 1_000
            ? count.ToString(CultureInfo.InvariantCulture)
            : count < 1_000_000
                ? (count / 1_000.0).ToString("F1", CultureInfo.InvariantCulture) + "k"
                : (count / 1_000_000.0).ToString("F1", CultureInfo.InvariantCulture) + "M";

    /// <summary>USD with enough precision for sub-cent model calls (upstream's <c>formatCost</c>).</summary>
    public static string FormatCost(double costUsd) => "$" + costUsd.ToString(costUsd < 0.1 ? "F4" : "F2", CultureInfo.InvariantCulture);

    /// <summary>
    /// Upstream's <c>rule</c>: the dashes of a full-width rule around <paramref name="text"/>, centered, or with the text pushed to the
    /// right edge as vitest draws <c>[1/3]</c> markers.
    /// </summary>
    public static (string Before, string After) Rule(string text, RuleAlign align)
    {
        var columns = TerminalColumns();
        if (align == RuleAlign.Center)
        {
            return Rule(text, columns);
        }

        var width = VisibleWidth(text);
        var after = 1;
        var before = Math.Max(0, columns - width - after);
        return (new string(LongDash, before), new string(LongDash, after));
    }

    /// <summary>The counts of results by bucket (upstream's <c>Counters</c>).</summary>
    public sealed class Counters
    {
        public int Passed { get; set; }

        public int Failed { get; set; }

        public int Interrupted { get; set; }

        public int Flaky { get; set; }

        public int Skipped { get; set; }

        public int Total { get; set; }

        /// <summary>Counts one more in <paramref name="bucket"/>, a value of <see cref="StatusBucket"/>.</summary>
        public void Add(string bucket)
        {
            switch (bucket)
            {
                case "passed":
                    Passed++;
                    break;
                case "failed":
                    Failed++;
                    break;
                case "interrupted":
                    Interrupted++;
                    break;
                case "flaky":
                    Flaky++;
                    break;
                default:
                    Skipped++;
                    break;
            }
        }
    }

    /// <summary>Upstream's <c>emptyCounters</c>.</summary>
    public static Counters EmptyCounters() => new();

    /// <summary>
    /// Upstream's <c>statusBucket</c>: the counter a result status lands in. A timeout is a failure; an interrupted test was stopped by the
    /// run, not by its own verdict, so it counts on its own.
    /// </summary>
    public static string StatusBucket(ResultStatus status) =>
        status switch
        {
            ResultStatus.TimedOut or ResultStatus.Failed => "failed",
            ResultStatus.Interrupted => "interrupted",
            ResultStatus.Flaky => "flaky",
            ResultStatus.Skipped => "skipped",
            _ => "passed",
        };

    /// <summary>Upstream's <c>tally</c>: counts results by bucket; <c>Total</c> is the number of items.</summary>
    public static Counters Tally(IEnumerable<ResultStatus> statuses)
    {
        var counters = EmptyCounters();
        foreach (var status in statuses)
        {
            counters.Add(StatusBucket(status));
            counters.Total++;
        }

        return counters;
    }

    /// <summary>Upstream's <c>fileOutcome</c>: failed if any test did, else interrupted if any was, skipped if all were, else passed.</summary>
    public static string FileOutcome(Counters counters) =>
        counters.Failed > 0 ? "failed"
        : counters.Interrupted > 0 ? "interrupted"
        : counters.Total > 0 && counters.Skipped == counters.Total ? "skipped"
        : "passed";

    /// <summary>Upstream's <c>stateString</c>: <c>2 failed | 1 interrupted | 10 passed | 1 flaky (14)</c> in vitest's colors and order.</summary>
    public static string StateString(Colors pc, Counters counters)
    {
        List<string> parts = [];
        if (counters.Failed > 0)
        {
            parts.Add(pc.Bold(pc.Red(counters.Failed.ToString(CultureInfo.InvariantCulture) + " failed")));
        }

        if (counters.Interrupted > 0)
        {
            parts.Add(pc.Bold(pc.Yellow(counters.Interrupted.ToString(CultureInfo.InvariantCulture) + " interrupted")));
        }

        parts.Add(pc.Bold(pc.Green(counters.Passed.ToString(CultureInfo.InvariantCulture) + " passed")));
        if (counters.Flaky > 0)
        {
            parts.Add(pc.Yellow(counters.Flaky.ToString(CultureInfo.InvariantCulture) + " flaky"));
        }

        if (counters.Skipped > 0)
        {
            parts.Add(pc.Gray(counters.Skipped.ToString(CultureInfo.InvariantCulture) + " skipped"));
        }

        return string.Join(pc.Dim(" | "), parts) + pc.Gray(" (" + counters.Total.ToString(CultureInfo.InvariantCulture) + ")");
    }

    /// <summary>Model usage across steps (upstream's <c>AiUsage</c>).</summary>
    public sealed class AiUsage
    {
        public long Calls { get; set; }

        /// <summary>Input plus output tokens.</summary>
        public long Tokens { get; set; }

        /// <summary>Input tokens alone; the denominator of the cached share.</summary>
        public long InputTokens { get; set; }

        /// <summary>Input tokens the provider served from its prompt cache; part of <see cref="InputTokens"/>.</summary>
        public long CachedTokens { get; set; }

        public double? CostUsd { get; set; }
    }

    /// <summary>Upstream's <c>emptyUsage</c>.</summary>
    public static AiUsage EmptyUsage() => new();

    /// <summary>Upstream's <c>addUsage</c>.</summary>
    public static void AddUsage(AiUsage into, AiUsage usage)
    {
        into.Calls += usage.Calls;
        into.Tokens += usage.Tokens;
        into.InputTokens += usage.InputTokens;
        into.CachedTokens += usage.CachedTokens;
        if (usage.CostUsd is { } cost)
        {
            into.CostUsd = (into.CostUsd ?? 0) + cost;
        }
    }

    /// <summary>Upstream's <c>stepsUsage</c>: the model usage of several step lists summed.</summary>
    public static AiUsage StepsUsage(IEnumerable<IEnumerable<StepRecord>> stepLists)
    {
        var usage = EmptyUsage();
        foreach (var step in stepLists.SelectMany(steps => steps))
        {
            if (step.Model is not { } model)
            {
                continue;
            }

            usage.Calls += model.Calls;
            usage.Tokens += model.InputTokens + model.OutputTokens;
            usage.InputTokens += model.InputTokens;
            usage.CachedTokens += model.CacheReadTokens ?? 0;
            if (model.EstimatedCostUsd is { } cost)
            {
                usage.CostUsd = (usage.CostUsd ?? 0) + cost;
            }
        }

        return usage;
    }

    /// <summary>Upstream's <c>sumUsage</c>: the model usage of several items summed.</summary>
    public static AiUsage SumUsage(IEnumerable<AiUsage> items)
    {
        var total = EmptyUsage();
        foreach (var item in items)
        {
            AddUsage(total, item);
        }

        return total;
    }

    /// <summary>
    /// Upstream's <c>usageText</c>: tokens, cached share, and cost, <c>12.4k tokens · 38% cached · $0.01</c>, or null when no model was
    /// used. The cached share appears only when the provider served some of the input from its prompt cache.
    /// </summary>
    public static string? UsageText(AiUsage usage)
    {
        if (usage.Calls == 0)
        {
            return null;
        }

        var cached = usage.CachedTokens > 0 && usage.InputTokens > 0
            ? " · " + Math.Round((double)usage.CachedTokens / usage.InputTokens * 100, MidpointRounding.AwayFromZero).ToString("F0", CultureInfo.InvariantCulture) + "% cached"
            : string.Empty;
        var cost = usage.CostUsd is { } costUsd ? " · " + FormatCost(costUsd) : string.Empty;
        return FormatTokens(usage.Tokens) + " tokens" + cached + cost;
    }

    /// <summary>Upstream's <c>aiSegment</c>: <see cref="UsageText"/> labeled <c>ai …</c> for lines where nothing else names it.</summary>
    public static string? AiSegment(AiUsage usage) => UsageText(usage) is { } text ? "ai " + text : null;

    /// <summary>
    /// Upstream's <c>stepModelLabel</c>: <c>provider/model</c>, the form the run header names the configured model in; null when the record
    /// lacks either half, so a step an executor reported without provenance never prints as <c>undefined/undefined</c>.
    /// </summary>
    public static string? StepModelLabel(StepModelInfo model) =>
        string.IsNullOrEmpty(model.Provider) || string.IsNullOrEmpty(model.Model) ? null : model.Provider + "/" + model.Model;

    /// <summary>Upstream's <c>ModelTally</c>: the models a run's steps reported, <c>provider/model</c> to the calls each answered.</summary>
    public sealed class ModelTally
    {
        private readonly Dictionary<string, int> _calls = new(StringComparer.Ordinal);

        public IEnumerable<KeyValuePair<string, int>> Entries => _calls;

        public void Add(string label, int calls) => _calls[label] = _calls.GetValueOrDefault(label) + calls;
    }

    /// <summary>Upstream's <c>addModelTally</c>.</summary>
    public static void AddModelTally(ModelTally into, ModelTally counts)
    {
        foreach (var (label, calls) in counts.Entries)
        {
            into.Add(label, calls);
        }
    }

    /// <summary>Upstream's <c>stepsModelTally</c>: the models several step lists reported, with their call counts.</summary>
    public static ModelTally StepsModelTally(IEnumerable<IEnumerable<StepRecord>> stepLists)
    {
        var counts = new ModelTally();
        foreach (var step in stepLists.SelectMany(steps => steps))
        {
            if (step.Model is { } model && StepModelLabel(model) is { } label)
            {
                counts.Add(label, model.Calls);
            }
        }

        return counts;
    }

    /// <summary>
    /// Upstream's <c>modelsText</c>: the models that answered, most calls first. One model by its label alone, since the row's call total is
    /// its count, several as <c>gateway/openai/gpt-6-luna-fast (22 calls) · typesafe-ai/jev (1 call)</c>; null when no step reported a model.
    /// </summary>
    public static string? ModelsText(ModelTally counts)
    {
        var sorted = counts.Entries.OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key, StringComparer.InvariantCulture).ToList();
        return sorted.Count switch
        {
            0 => null,
            1 => Bounded(sorted[0].Key),
            _ => string.Join(" · ", sorted.Select(entry => Bounded(entry.Key) + " (" + entry.Value.ToString(CultureInfo.InvariantCulture) + (entry.Value == 1 ? " call)" : " calls)"))),
        };
    }

    /// <summary>Upstream's <c>repeatSuffix</c>: the title suffix of a repeat-each run past the first, <c> (repeat #2)</c>; empty for the first run.</summary>
    public static string RepeatSuffix(int repeat) => repeat == 0 ? string.Empty : " (repeat #" + repeat.ToString(CultureInfo.InvariantCulture) + ")";

    [GeneratedRegex("\u001b\\[[0-9;]*m")]
    private static partial Regex Ansi();
}
