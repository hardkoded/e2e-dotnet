// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace E2E.Cli.Report;

/// <summary>
/// Failure-location presentation (upstream's <c>report/code-frame.ts</c>): renders a small annotated code frame around the failing line,
/// in vitest's frame style. Upstream's <c>userFrame</c> is <c>E2E.Internal.Source.Locate</c>, in the core assembly.
/// </summary>
internal static class CodeFrame
{
    /// <summary>Lines of context on each side of the failing line.</summary>
    private const int FrameRange = 2;

    /// <summary>A source line longer than this is minified output; the frame would be noise.</summary>
    private const int MaxFrameLineChars = 200;

    /// <summary>
    /// The failing line with two lines of context on each side and a column caret, gutter-numbered like vitest (<c>  28| code</c>),
    /// each line clipped to <paramref name="maxChars"/> (by default what fits the terminal). Empty when the file cannot be read or the
    /// line is not in it. Upstream's <c>codeFrame</c>.
    /// </summary>
    public static IReadOnlyList<string> Render(string file, int line, int column, Colors pc, int? maxChars = null)
    {
        ArgumentNullException.ThrowIfNull(pc);
        var limit = maxChars ?? Math.Max(20, Format.TerminalColumns() - 10);
        string[] lines;
        try
        {
            lines = File.ReadAllText(file).Split(["\r\n", "\n"], StringSplitOptions.None);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        var index = line - 1;
        if (index < 0 || index >= lines.Length)
        {
            return [];
        }

        var start = Math.Max(0, index - FrameRange);
        var end = Math.Min(lines.Length - 1, index + FrameRange);
        string Gutter(int? number = null) => pc.Gray((number?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).PadLeft(3) + "|");
        List<string> rows = [];
        for (var i = start; i <= end; i++)
        {
            var raw = Format.Bounded(lines[i]);
            if (raw.Length > MaxFrameLineChars)
            {
                return [];
            }

            var trimmed = raw.TrimEnd();
            var text = Format.FitColumns(trimmed, limit);
            rows.Add(Gutter(i + 1) + (text.Length == 0 ? string.Empty : " " + text));
            if (i == index)
            {
                var source = lines[i];
                var prefix = Format.Bounded(source[..Math.Min(source.Length, Math.Max(0, column - 1))]);
                var width = Format.VisibleWidth(prefix);

                // A clipped-away location has no visible token to annotate.
                var room = text == trimmed ? limit : Format.VisibleWidth(text) - 1;
                if (width < room)
                {
                    rows.Add(Gutter() + new string(' ', width + 1) + pc.Red("^"));
                }
            }
        }

        return rows;
    }
}
