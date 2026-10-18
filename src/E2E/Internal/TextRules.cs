// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.RegularExpressions;

namespace E2E.Internal;

/// <summary>
/// Whitespace and string matching used by locators. Exact string matches are
/// case-sensitive after whitespace is collapsed. Inexact matches are
/// case-insensitive substrings, which is how <c>exact: false</c> behaves upstream.
/// </summary>
internal static partial class TextRules
{
    /// <summary>
    /// App text as one line, as upstream's <c>collapseText</c> reads it: whitespace collapsed, and a control
    /// character becomes U+FFFD, so app text cannot put it in a prompt.
    /// </summary>
    public static string Collapse(string text) =>
        string.Concat(Normalize(text).Select(character => char.IsControl(character) ? '�' : character));

    public static string Normalize(string text)
    {
        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts).Trim();
    }

    public static bool Matches(string? actual, string expected, bool exact)
    {
        if (actual is null)
        {
            return false;
        }

        var left = Normalize(actual);
        var right = Normalize(expected);
        if (exact)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }

        return left.Contains(right, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The text matcher rule. A pattern is tested anywhere in the subject in both modes. A string
    /// equals, or is contained by, the subject, folding case when <paramref name="ignoreCase"/> is true.
    /// <paramref name="normalize"/> collapses whitespace on both sides first.
    /// </summary>
    public static bool Compare(string actual, TextMatch expected, bool contains, bool normalize, bool? ignoreCase)
    {
        var subject = normalize ? Normalize(actual) : actual;
        if (expected.Pattern is not null)
        {
            return expected.Pattern.IsMatch(subject);
        }

        var text = normalize ? Normalize(expected.Text ?? "") : expected.Text ?? "";
        var comparison = ignoreCase == true ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return contains ? subject.Contains(text, comparison) : string.Equals(subject, text, comparison);
    }

    /// <summary>
    /// Caps prose at <paramref name="maxChars"/>, marking the cut with an ellipsis. A cut never splits a surrogate pair.
    /// </summary>
    public static string Bound(string text, int maxChars)
    {
        if (text.Length <= maxChars)
        {
            return text;
        }

        var end = maxChars - 1;
        if (end > 0 && char.IsHighSurrogate(text[end - 1]))
        {
            end--;
        }

        return text[..end] + "…";
    }

    /// <summary>
    /// Upstream's <c>sanitizeText</c> (<c>internal/errors.ts</c>): strips ANSI escape sequences, then every C0 and C1
    /// control character except tab and newline becomes U+FFFD. Libraries color their own messages; left in, the color codes
    /// reach a report as literal text.
    /// </summary>
    public static string SanitizeText(string text) => ControlCharacters().Replace(AnsiSequences().Replace(text, ""), "�");

    /// <summary>Upstream's <c>truncateUtf8</c>: cuts the text so its UTF-8 encoding fits <paramref name="maxBytes"/>, never inside a character.</summary>
    public static string TruncateUtf8(string text, int maxBytes)
    {
        if (Encoding.UTF8.GetByteCount(text) <= maxBytes)
        {
            return text;
        }

        var bytes = 0;
        var end = 0;
        while (end < text.Length)
        {
            // A surrogate pair is one character: the cut never lands between its halves.
            var width = char.IsHighSurrogate(text[end]) && end + 1 < text.Length && char.IsLowSurrogate(text[end + 1]) ? 2 : 1;
            var size = Encoding.UTF8.GetByteCount(text.AsSpan(end, width));
            if (bytes + size > maxBytes)
            {
                break;
            }

            bytes += size;
            end += width;
        }

        return text[..end];
    }

    /// <summary>
    /// Upstream's <c>withHint</c>: appends a remedy to a message, as a clause after a one-line message and on its own line after a
    /// multi-line one, so the hint never dangles off a sentence that already ended.
    /// </summary>
    public static string WithHint(string message, string hint)
    {
        if (hint.Length == 0)
        {
            return message;
        }

        var text = message.TrimEnd();
        return text + (text.Contains('\n', StringComparison.Ordinal) ? "\n" : "; ") + hint;
    }

    [GeneratedRegex("\\u001B\\[[0-?]*[ -/]*[@-~]|\\u001B\\][^\\u0007\\u001B]*(?:\\u0007|\\u001B\\\\)|\\u009B[0-?]*[ -/]*[@-~]|\\u001B[@-Z\\\\-_]")]
    private static partial Regex AnsiSequences();

    [GeneratedRegex("[\\u0000-\\u0008\\u000b-\\u001f\\u007f-\\u009f]")]
    private static partial Regex ControlCharacters();
}
