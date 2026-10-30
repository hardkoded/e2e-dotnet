// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using E2E.Internal;

namespace E2E;

/// <summary>
/// Text on its way into markdown: escaped, clipped, one line. Upstream's <c>report/markdown-text.ts</c>, shared by every page that
/// quotes what a test or the app under test wrote, so they all treat it the same way.
/// </summary>
internal static partial class MarkdownText
{
    /// <summary>Text widths, in code points.</summary>
    public const int MaxCellChars = 240;

    public const int MaxPathChars = 200;

    public const int MaxTitleChars = 120;

    public const int MaxIdChars = 64;

    public const int MaxLabelChars = 60;

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// A token GitHub turns into a link on its own: a mention or an email address (<c>@</c> before a letter or digit), an issue
    /// reference (<c>#</c> or <c>GH-</c> before a digit), a URL with a scheme, or a <c>www.</c> host. A code span stops them.
    /// </summary>
    [GeneratedRegex(@"@[A-Za-z0-9]|#\d|\bGH-\d|:\/\/|\bwww\.", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex LinkableToken();

    /// <summary>What opens a block construct at the start of a line: an ordered list marker, or the first character of a heading, a list item, a thematic break, or a setext underline.</summary>
    [GeneratedRegex(@"^(\d{1,9})([.)])(?=\s|\z)|^[#+=_-]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex BlockOpener();

    [GeneratedRegex(@"[\\`*\[\]~|]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex InlineMarkup();

    [GeneratedRegex(@"`+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex BacktickRun();

    [GeneratedRegex(@"[\s()<>]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex DestinationBreak();

    /// <summary>
    /// Text safe inside a table cell, a list item, or at the start of a line: one line, clipped by code point so an emoji at the
    /// cut survives whole, markdown that could open a construct escaped, angle brackets as entities so no HTML gets through, and the
    /// cell separator escaped. A token GitHub would link on its own (a mention, an issue number, a URL) is shown as code, so screen
    /// text from the app under test cannot notify anyone or plant a link in a pull request comment. An underscore stays: inside a
    /// word GitHub never reads it as emphasis, and error codes are full of them.
    /// </summary>
    public static string Cell(string text, int max = MaxCellChars)
    {
        var line = string.Join(
            ' ',
            ReportFormat.Ellipsize(Collapse(text), max).Split(' ').Select(token => LinkableToken().IsMatch(token) ? CodeSpan(token) : EscapeInline(token)));
        return BlockOpener().Replace(
            line,
            match => match.Groups[1].Success ? match.Groups[1].Value + "\\" + match.Groups[2].Value : "\\" + match.Value,
            1);
    }

    /// <summary>A path or label inside backticks: only the cell separator is escaped; a backtick inside lengthens the delimiter.</summary>
    public static string Code(string text, int max = MaxCellChars) => CodeSpan(ReportFormat.Ellipsize(Collapse(text), max));

    /// <summary>A markdown link. A character in the URL that closes the destination or breaks it is percent-encoded.</summary>
    public static string Link(string text, string url) =>
        "[" + text + "](" + DestinationBreak().Replace(Collapse(url), match => "%" + ((int)match.Value[0]).ToString("X2", CultureInfo.InvariantCulture)) + ")";

    /// <summary>A duration the way a page tells it: milliseconds under a second, seconds under a minute, then minutes.</summary>
    public static string FormatDuration(double ms)
    {
        if (ms < 1_000)
        {
            return Math.Round(ms, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + "ms";
        }

        return ms < 60_000 ? (ms / 1_000).ToString("0.0", CultureInfo.InvariantCulture) + "s" : ReportFormat.FormatMinutes(ms);
    }

    /// <summary><c>1 turn</c>, <c>2 turns</c>.</summary>
    public static string Plural(long count, string noun) => count.ToString(CultureInfo.InvariantCulture) + " " + noun + (count == 1 ? "" : "s");

    // Upstream's collapseText: ANSI sequences and control characters gone, whitespace collapsed.
    private static string Collapse(string text) => TextRules.Collapse(TextRules.SanitizeText(text));

    // Inline markdown a token could open, escaped; angle brackets become entities.
    private static string EscapeInline(string text) =>
        InlineMarkup().Replace(text, match => "\\" + match.Value).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);

    // A code span around text that may hold backticks: the delimiter is one backtick longer than the longest run inside and, when
    // longer than one, padded with a space GFM strips, so no character of the text is lost. The cell separator is escaped.
    private static string CodeSpan(string text)
    {
        var longest = BacktickRun().Matches(text).Select(run => run.Length).DefaultIfEmpty(0).Max();
        var fence = new string('`', longest + 1);
        var inner = text.Replace("|", "\\|", StringComparison.Ordinal);
        return longest == 0 ? fence + inner + fence : fence + " " + inner + " " + fence;
    }
}

/// <summary>Upstream's <c>report/format.ts</c>, the parts a trace page and the step table use.</summary>
internal static class ReportFormat
{
    /// <summary>
    /// What one test filter decided of the tests it was given: <c>kept 7 of 42 tests, left out 35</c>, <c>kept all 42 tests</c>, or
    /// <c>left out all 42 tests</c> (upstream's <c>filterDecision</c>, <c>report/format.ts</c>).
    /// </summary>
    public static string FilterDecision(int candidates, int excluded)
    {
        var tests = candidates.ToString(CultureInfo.InvariantCulture) + (candidates == 1 ? " test" : " tests");
        if (excluded == 0)
        {
            return "kept all " + tests;
        }

        return excluded == candidates
            ? "left out all " + tests
            : "kept " + (candidates - excluded).ToString(CultureInfo.InvariantCulture) + " of " + tests + ", left out " + excluded.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Clips to <paramref name="max"/> code points with an ellipsis; untouched when it fits.</summary>
    public static string Ellipsize(string text, int max)
    {
        var runes = text.EnumerateRunes().ToList();
        if (runes.Count <= max)
        {
            return text;
        }

        var clipped = new StringBuilder();
        foreach (var rune in runes.Take(Math.Max(0, max - 1)))
        {
            clipped.Append(rune.ToString());
        }

        return clipped.Append('…').ToString();
    }

    /// <summary>A span as <c>11m 23s</c>. Rounds to whole seconds first, so 119.5 s is <c>2m 0s</c> and never <c>1m 60s</c>.</summary>
    public static string FormatMinutes(double ms)
    {
        var total = (long)Math.Round(ms / 1_000, MidpointRounding.AwayFromZero);
        return (total / 60).ToString(CultureInfo.InvariantCulture) + "m " + (total % 60).ToString(CultureInfo.InvariantCulture) + "s";
    }

    /// <summary>The title suffix of a repeated run past the first: <c> (repeat #2)</c>; empty for the first.</summary>
    public static string RepeatSuffix(int repeat) => repeat == 0 ? "" : " (repeat #" + repeat.ToString(CultureInfo.InvariantCulture) + ")";

    /// <summary>
    /// <c>provider/model</c>; null when the record lacks the model, so a step an executor reported without provenance never prints as
    /// <c>undefined/undefined</c>. <see cref="IAgentModel.Name"/> already spells both halves, so a record without a provider is its model alone.
    /// </summary>
    public static string? StepModelLabel(StepModelInfo model)
    {
        if (string.IsNullOrEmpty(model.Model))
        {
            return null;
        }

        return string.IsNullOrEmpty(model.Provider) ? model.Model : model.Provider + "/" + model.Model;
    }
}
