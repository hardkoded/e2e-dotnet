// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace E2E.Cli.Report;

/// <summary>
/// The running part of the list reporter's live window (upstream's <c>report/running-tree.ts</c>): each running file, its tests with a ticking
/// clock, each test's finished steps, and its current step with the model turns and tool calls so far and the turn in flight. Padded to the
/// bottom of the screen, so the summary sits there and never moves as calls come and go.
/// </summary>
internal sealed class RunningTree(Colors pc, Func<string, string> badge)
{
    private const string FDownRight = "↳";

    private const string FTreeMiddle = "├──";

    private const string FTreeEnd = "└──";

    /// <summary>
    /// Spinner for the turn in flight: an asterisk blooming and folding back. Centered dingbats, unlike six-dot braille, which sits in the
    /// top-left of its cell.
    /// </summary>
    private static readonly string[] SpinnerFrames = ["·", "✢", "✳", "✶", "✻", "✽", "✻", "✶", "✳", "✢"];

    /// <summary>Frames each spinner glyph holds; the bloom reads better slower than the shimmer.</summary>
    private const int SpinnerHoldFrames = 2;

    /// <summary>Frames the shimmer rests after each sweep across the status word.</summary>
    private const int ShimmerRestFrames = 6;

    /// <summary>Indentation of a running test's detail (steps, calls) under its row.</summary>
    private const string DetailIndent = "       ";

    /// <summary>Rows the window spends outside the running tests and the summary: its blank lines, the <c>… more running</c> marker, and a margin above the prompt.</summary>
    private const int ChromeRows = 6;

    /// <summary>Blank rows framing the block: above the running area, between it and the summary, and under the summary.</summary>
    public const int FrameRows = 3;

    /// <summary>
    /// Rows the running area keeps once the log has grown down to it: one file row, one test row, its current step, the calls the window
    /// shows, the turn in flight, and a few finished steps. Until then the area reaches the bottom of the screen; from then on it never shrinks
    /// below what it once needed, so the summary stays put instead of jumping as calls come and go.
    /// </summary>
    private const int ReservedRowsFloor = 14;

    /// <summary>Most recent calls of a step the window shows; older ones fold into the <c>… earlier calls</c> marker so a tall terminal does not fill with model turns.</summary>
    private const int MaxEvents = 6;

    /// <summary>High-water mark of the running area's rows; the window pads up to it.</summary>
    private int _reservedRows = ReservedRowsFloor;

    /// <summary>
    /// A bright highlight sweeping over <paramref name="word"/> one character per frame, resting a few frames between sweeps: the status word
    /// reads as alive without moving (upstream's <c>shimmer</c>).
    /// </summary>
    private static string Shimmer(Colors pc, string word, int frame)
    {
        var chars = word.EnumerateRunes().Select(rune => rune.ToString()).ToList();
        var head = frame % (chars.Count + ShimmerRestFrames);
        return string.Concat(chars.Select((letter, index) => Math.Abs(index - head) switch
        {
            0 => pc.Bold(pc.White(letter)),
            1 => pc.White(letter),
            _ => pc.Dim(letter),
        }));
    }

    /// <summary>
    /// Pads the running area down to the bottom of the screen: to <paramref name="fill"/>, the rows left under the log once the summary has its
    /// own, or to <paramref name="reserved"/> when the log has grown past that point and the block scrolls it instead (upstream's <c>padToScreen</c>).
    /// </summary>
    public static void PadToScreen(List<string> lines, int reserved, int fill)
    {
        var height = Math.Max(reserved, fill);
        while (lines.Count < height)
        {
            lines.Add(string.Empty);
        }
    }

    /// <summary><c>… N earlier things</c> folding rows that do not fit (upstream's <c>foldMarker</c>).</summary>
    private static string FoldMarker(Colors pc, int count, string noun) => pc.Dim($"… {count} earlier {noun}{(count == 1 ? string.Empty : "s")}");

    /// <summary>
    /// The window's rows: running files and tests with their detail, then the summary. The block must fit the screen, because repainting more
    /// rows than the terminal has breaks the cursor-up erase: rows left after the summary go to the running tests, each test's detail shares what
    /// remains once every test has its own row, and tests that still do not fit fold into one <c>more running</c> marker. <paramref name="room"/>
    /// is the screen under the permanent log; the block pads out to it so the summary sits at the bottom.
    /// </summary>
    public List<string> Render(IReadOnlyList<RunningTest> running, IReadOnlyList<string> summary, long now, int room, SetupInFlight? setup = null)
    {
        var active = new List<(FileGroup Group, List<RunningTest> Tests)>();
        foreach (var test in running)
        {
            var entry = active.FindIndex(candidate => candidate.Group == test.Group);
            if (entry == -1)
            {
                active.Add((test.Group, [test]));
            }
            else
            {
                active[entry].Tests.Add(test);
            }
        }

        var capacity = Format.TerminalRows() - summary.Count - ChromeRows;
        var budget = capacity;
        var perTest = Math.Max(0, (int)Math.Floor((budget - active.Count - running.Count) / (double)Math.Max(1, running.Count)));
        var maxEvents = Math.Min(MaxEvents, perTest);
        List<string> lines = [];
        var hidden = 0;
        if (setup is not null && budget > 0)
        {
            lines.Add($"{pc.Bold(pc.Yellow($" {Format.Pointer} "))}{pc.Dim(setup.Verb)} {setup.Subject} {Clock(now - setup.StartedMs)}");
            budget--;
        }

        foreach (var (group, tests) in active)
        {
            if (budget < 2)
            {
                hidden += tests.Count;
                continue;
            }

            var progress = pc.Dim($" {group.Lines.Count}/{(group.Planned is { } planned ? planned.ToString(CultureInfo.InvariantCulture) : "?")}");
            lines.Add($"{pc.Bold(pc.Yellow($" {Format.Pointer} "))}{badge(group.Target)} {Format.Bounded(group.File)}{progress}");
            budget--;
            for (var index = 0; index < tests.Count; index++)
            {
                if (budget < 1)
                {
                    hidden++;
                    continue;
                }

                var test = tests[index];
                var glyph = index == tests.Count - 1 ? FTreeEnd : FTreeMiddle;
                lines.Add($"{pc.Bold(pc.Yellow($"   {glyph} "))}{test.Title} {Clock(now - test.StartedMs)}");
                budget--;
                var detail = DetailRows(test, maxEvents, Math.Min(perTest, budget), now);
                lines.AddRange(detail);
                budget -= detail.Count;
            }
        }

        if (hidden > 0)
        {
            lines.Add(pc.Dim($"   … {hidden} more running"));
        }

        _reservedRows = Math.Min(capacity, Math.Max(_reservedRows, lines.Count));
        PadToScreen(lines, _reservedRows, room - FrameRows - summary.Count);
        return [string.Empty, .. lines, string.Empty, .. summary, string.Empty];
    }

    /// <summary>The one ticking clock of a running test, on its row.</summary>
    private string Clock(long elapsedMs) => pc.Bold(pc.Yellow(Format.FormatTime(TimeSpan.FromMilliseconds(Math.Max(0, elapsedMs)))));

    /// <summary>
    /// Everything under a running test's row: its finished steps, then the current step. The current step has priority; finished steps take
    /// the rows left and the oldest fold into one marker.
    /// </summary>
    private List<string> DetailRows(RunningTest test, int maxEvents, int budget, long now)
    {
        var current = StepRows(test, maxEvents, budget, now);
        var room = budget - current.Count;
        var maxWidth = Format.TerminalColumns() - LiveWindow.WidthMargin - DetailIndent.Length;
        var rendered = test.Steps.Select(step => ListSteps.StepLine(pc, step, maxWidth: maxWidth)).ToList();
        List<string> finished = [];
        if (room > 0)
        {
            var folded = rendered.Count - room + 1;
            finished = folded > 1 ? [FoldMarker(pc, folded, "step"), .. rendered.Skip(folded)] : rendered;
        }

        return [.. finished.Concat(current).Select(row => DetailIndent + row)];
    }

    /// <summary>
    /// The current step of a running pair and, for an agent step, its calls and the work in flight, at most <paramref name="budget"/> rows. Older
    /// calls fold into one marker. No clock here: the test row above carries the one clock, so the eye has a single moving number per running test.
    /// </summary>
    private List<string> StepRows(RunningTest test, int maxEvents, int budget, long now)
    {
        if (test.Current is not { } current)
        {
            return [];
        }

        List<string> rows = [pc.Dim($"{FDownRight} {current.Api} {ListSteps.QuotedLabel(current.Label)}")];
        if (current.Kind != Reports.StepKind.Agent)
        {
            return [.. rows.Take(budget)];
        }

        var overflow = current.Events.Count - maxEvents;
        if (overflow > 0)
        {
            rows.Add("  " + FoldMarker(pc, overflow, "call"));
        }

        var maxWidth = Format.TerminalColumns() - LiveWindow.WidthMargin - DetailIndent.Length - 2;
        foreach (var stepEvent in overflow > 0 ? current.Events.Skip(overflow) : current.Events)
        {
            rows.Add("  " + ListSteps.EventLine(pc, stepEvent, maxWidth));
        }

        rows.Add("  " + WaitingRow(pc, now, WaitingWord(current)));
        return [.. rows.Take(budget)];
    }

    /// <summary>
    /// The status word for a running agent step: <c>Replaying</c> while the trace cache has it with no model in the loop; <c>Observing</c> or
    /// <c>Acting</c> while the step announced a screen read or an action and no event has ended it yet (a device snapshot or tap takes seconds);
    /// otherwise the model has the turn and the word is <c>Thinking</c> (upstream's <c>waitingWord</c>).
    /// </summary>
    private static string WaitingWord(CurrentStep current) =>
        current.Replaying ? "Replaying" : current.Activity switch
        {
            Reports.StepActivity.Observe => "Observing",
            Reports.StepActivity.Action => "Acting",
            _ => "Thinking",
        };

    /// <summary>The work in flight: a spinner and a shimmering status word (upstream's <c>waitingRow</c>).</summary>
    private static string WaitingRow(Colors pc, long now, string word)
    {
        var frame = (int)(now / LiveWindow.RepaintIntervalMs);
        var spinner = pc.Cyan(SpinnerFrames[frame / SpinnerHoldFrames % SpinnerFrames.Length]);
        return $"{spinner} {Shimmer(pc, word, frame)}";
    }
}
