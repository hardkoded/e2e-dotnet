// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Engine;
using E2E.Internal;
using static E2E.Agent;

namespace E2E;

/// <summary>One node of a page: what it reads with clock-like values masked, and as it is.</summary>
internal readonly record struct PageLine(string Masked, string Exact);

/// <summary>What a screen reads, a line per node that carries a name or text.</summary>
internal sealed record PageReading(IReadOnlyList<PageLine> Lines);

/// <summary>
/// Upstream's <c>agent/reading.ts</c>: reading a screen for a text the agent pages toward. Which node reads it, and
/// what the screen reads as a whole, for telling a page that moved from one that did not. Upstream's <c>nodeReading</c>
/// is <see cref="NodeReading"/>, its <c>readPage</c> is <see cref="ReadPage"/>, its <c>pageMoved</c> is <see cref="PageMoved"/>, and
/// its <c>pageLanded</c> is <see cref="PageLanded"/>. Upstream's <c>normalizeReading</c> is <see cref="TextRules.Normalize"/>, which
/// <see cref="TextRules.Matches"/> applies.
/// </summary>
internal static class Reading
{
    /// <summary>
    /// The innermost visible node whose redacted name or text contains <paramref name="text"/>. With a
    /// <paramref name="scope"/>, the list being paged, only its descendants count. The port keeps the raw nodes, so the
    /// <paramref name="redactor"/> is applied to each field as it is read (see <see cref="RedactField"/>).
    /// </summary>
    public static SemanticNode? NodeReading(Observation observation, string text, Redactor redactor, SemanticNode? scope = null) =>
        Innermost(scope is null ? observation.Roots : scope.Children, text, redactor);

    /// <summary>
    /// What a screen reads and where, for telling a page that moved from one that did not. Name and text both count: a
    /// windowed list's rows keep one name and one slot on the screen while the text in them changes, and that is the list
    /// moving. Each line is kept masked and as it is, since a clock-like value is both a clock ticking and a time picker's
    /// row.
    /// </summary>
    public static PageReading ReadPage(Observation observation)
    {
        var lines = new List<PageLine>();
        foreach (var node in LocatorResolver.Walk(observation.Roots))
        {
            if (node.Name is null && node.Text is null)
            {
                continue;
            }

            var at = node.Rect is null ? "" : Round(node.Rect.X) + "," + Round(node.Rect.Y);
            var exact = (node.Name ?? "") + "|" + (node.Text ?? "") + "@" + at;
            lines.Add(new PageLine(AgentObservation.MaskClocks(exact), exact));
        }

        return new PageReading(lines);
    }

    // JavaScript's Math.round: halves round up.
    private static string Round(double value) => ((long)Math.Floor(value + 0.5)).ToString(CultureInfo.InvariantCulture);

    // Whether two pages read the same lines, clock-like values masked.
    private static bool SameLines(PageReading before, PageReading after) =>
        before.Lines.Count == after.Lines.Count && before.Lines.Zip(after.Lines).All(pair => string.Equals(pair.First.Masked, pair.Second.Masked, StringComparison.Ordinal));

    /// <summary>
    /// How many clock-like values changing at once are a list turning over and not clocks ticking: one clock ticks beside a
    /// screen, while a time picker or a timetable changes every row it shows. Several clocks ticking together (world clocks)
    /// read as a list that moves, which only costs the end-of-list check its early exit, as before.
    /// </summary>
    private const int TurnoverClocks = 2;

    /// <summary>
    /// Whether the screen moved between two looks, for ending a scroll at the end of its list. A different set of lines is
    /// a page that moved, whatever clock-like values read. When only those differ, one changing is a clock ticking beside a
    /// list that has not moved, and several are the list's own rows (a time picker, a timetable) turning over. A clock needs
    /// a line beside it to tick beside: on a page of one line, a pager or carousel whose only text goes <c>10:30</c> to
    /// <c>11:00</c>, that line is the page. The page is the whole screen, so a carousel with any other named node beside it
    /// (a title, a button) still reads as a clock ticking; telling the two apart takes more than the screen can say.
    /// </summary>
    public static bool PageMoved(PageReading before, PageReading after)
    {
        if (!SameLines(before, after))
        {
            return true;
        }

        var ticked = 0;
        for (var at = 0; at < before.Lines.Count; at++)
        {
            if (!string.Equals(before.Lines[at].Exact, after.Lines[at].Exact, StringComparison.Ordinal))
            {
                ticked++;
            }
        }

        return ticked >= TurnoverClocks || (ticked > 0 && before.Lines.Count < 2);
    }

    /// <summary>
    /// The share of a page's lines that must be new for a swipe to have turned it. Lines are keyed by position as well as
    /// text, so this share only does work on a windowed list, whose rows keep fixed slots: a page turn replaces most of
    /// them, while a counter or a status line beside a stale list replaces a line or two. On a list whose rows move (a
    /// native list, an unwindowed web list) any movement changes nearly every line, and the share is met by the first pixel
    /// scrolled. A third leaves room for chrome as large as the list itself, and a page with more takes the look that
    /// proves it holds still, which only costs the read this saves.
    /// </summary>
    private const double LandedNewShare = 1.0 / 3;

    /// <summary>The fewest new lines a landed page shows. A third of a short screen is one line, and one changing line is a counter, not a page turn.</summary>
    private const int LandedMinNewLines = 2;

    // How many lines of `after` the page before did not read, clock-like values masked.
    private static int NewLines(PageReading before, PageReading after)
    {
        var unmatched = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in before.Lines)
        {
            unmatched[line.Masked] = unmatched.GetValueOrDefault(line.Masked) + 1;
        }

        var fresh = 0;
        foreach (var line in after.Lines)
        {
            var left = unmatched.GetValueOrDefault(line.Masked);
            if (left > 0)
            {
                unmatched[line.Masked] = left - 1;
            }
            else
            {
                fresh++;
            }
        }

        return fresh;
    }

    /// <summary>
    /// Whether a capture taken after paging is the page the swipe landed on, without a second read to confirm it: a third
    /// or more of what it reads is new (two lines at least), clock-like values aside, and it is not sparser than the page
    /// before. A capture that reads the same may be the screen before the scroll rendered, or the end of the list; one that
    /// differs by a line or two may be that screen with a counter ticking beside it; a sparser one may be a windowed list
    /// between the scroll and the rows it draws. Only those need another look. A page whose rows differ by a clock-like
    /// value alone is not vouched for here either, since a clock ticking beside the stale screen reads just like it: it
    /// takes the look that proves it holds still, while <see cref="PageMoved"/> keeps it paging. The page before is the
    /// only baseline, so a page that was itself still drawing is not caught.
    /// </summary>
    public static bool PageLanded(PageReading before, PageReading after)
    {
        if (after.Lines.Count < before.Lines.Count || SameLines(before, after))
        {
            return false;
        }

        return NewLines(before, after) >= Math.Max(LandedMinNewLines, (int)Math.Ceiling(after.Lines.Count * LandedNewShare));
    }

    private static SemanticNode? Innermost(IReadOnlyList<SemanticNode> nodes, string text, Redactor redactor)
    {
        foreach (var node in nodes)
        {
            if (node.States.Hidden || node.States.AriaHidden)
            {
                continue;
            }

            var inner = Innermost(node.Children, text, redactor);
            if (inner is not null)
            {
                return inner;
            }

            if (TextRules.Matches(RedactField(node.Name, ObservationLimits.Name, redactor), text, exact: false) || TextRules.Matches(RedactField(node.Text, ObservationLimits.Text, redactor), text, exact: false))
            {
                return node;
            }
        }

        return null;
    }
}
