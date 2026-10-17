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

    /// <summary>Black on cyan, as the list reporter's target badge.</summary>
    public string Badge(string text) => Enabled ? Wrap(Wrap(text, 46, 49), 30, 39) : text;

    private string Wrap(string text, int open, int close) =>
        Enabled ? "\u001b[" + open.ToString(CultureInfo.InvariantCulture) + "m" + text + "\u001b[" + close.ToString(CultureInfo.InvariantCulture) + "m" : text;
}

/// <summary>
/// Pure presentation helpers shared by the terminal reporters: glyphs, durations, and widths. Upstream's <c>report/format.ts</c>,
/// the part the exploration view uses.
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
            var end = clean.Length;
            while (end > 0 && Encoding.UTF8.GetByteCount(clean.AsSpan(0, end)) > MaxFieldBytes)
            {
                end--;
            }

            if (end > 0 && char.IsHighSurrogate(clean[end - 1]))
            {
                end--;
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

    [GeneratedRegex("\u001b\\[[0-9;]*m")]
    private static partial Regex Ansi();
}
