// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E;

/// <summary>
/// What a text matcher compares against: a string or a <see cref="System.Text.RegularExpressions.Regex"/>.
/// Converts implicitly from both, as upstream <c>TextMatch</c> is <c>string | RegExp</c>.
/// </summary>
public sealed class TextMatch
{
    private TextMatch(string? text, Regex? pattern)
    {
        Text = text;
        Pattern = pattern;
    }

    /// <summary>The string to compare, or null for a pattern.</summary>
    public string? Text { get; }

    /// <summary>The pattern to test, or null for a string.</summary>
    public Regex? Pattern { get; }

    public static implicit operator TextMatch(string text) => FromString(text);

    public static implicit operator TextMatch(Regex pattern) => FromRegex(pattern);

    public static TextMatch FromString(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new TextMatch(text, null);
    }

    public static TextMatch FromRegex(Regex pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return new TextMatch(null, pattern);
    }

    /// <summary>
    /// The match under <c>ignoreCase</c>: <c>true</c> adds <see cref="RegexOptions.IgnoreCase"/> to a pattern,
    /// <c>false</c> removes it, and null keeps it. A string is returned as it is; the comparison folds its case.
    /// </summary>
    internal TextMatch WithIgnoreCase(bool? ignoreCase)
    {
        if (Pattern is null || ignoreCase is not bool fold)
        {
            return this;
        }

        var options = fold ? Pattern.Options | RegexOptions.IgnoreCase : Pattern.Options & ~RegexOptions.IgnoreCase;
        return options == Pattern.Options ? this : new TextMatch(null, new Regex(Pattern.ToString(), options, Pattern.MatchTimeout));
    }

    /// <summary>The match as a failure message prints it.</summary>
    internal string Describe(bool? ignoreCase)
    {
        if (Pattern is not null)
        {
            return "/" + Pattern + "/" + ((Pattern.Options & RegexOptions.IgnoreCase) != 0 ? "i" : "");
        }

        return "\"" + Text + "\"" + (ignoreCase == true ? " (ignoring case)" : "");
    }
}
