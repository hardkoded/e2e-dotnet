// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Internal;

/// <summary>
/// "Did you mean" for diagnostics. A misspelled key, target, file, or fixture is the most common shape of a
/// configuration error, and naming the nearest valid choice turns a dead end into a one-word fix.
/// </summary>
internal static class Suggestions
{
    /// <summary>
    /// The candidate closest to <paramref name="word"/> by edit distance, when one is close enough to be a plausible
    /// typo: within one edit for short words, and within a third of the word's length otherwise. Comparison ignores
    /// case; ties keep candidate order. Returns null when nothing is close, or when the word already matches a
    /// candidate exactly.
    /// </summary>
    public static string? Suggest(string word, IReadOnlyList<string> candidates)
    {
        if (candidates.Contains(word, StringComparer.Ordinal))
        {
            return null;
        }

        var needle = word.ToLowerInvariant();
        var budget = TypoBudget(word);
        string? best = null;
        var bestDistance = int.MaxValue;
        foreach (var candidate in candidates)
        {
            // A case-only difference is distance 0 here and still a fix worth naming.
            var distance = EditDistance(needle, candidate.ToLowerInvariant());
            if (distance > budget)
            {
                continue;
            }

            if (best is null || distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>Whether <paramref name="candidate"/> is close enough to <paramref name="word"/> to be a typo of it, by the budget <see cref="Suggest"/> uses. Ignores case.</summary>
    public static bool IsTypoOf(string word, string candidate)
    {
        var budget = TypoBudget(word);
        if (Math.Abs(word.Length - candidate.Length) > budget)
        {
            return false;
        }

        return EditDistance(word.ToLowerInvariant(), candidate.ToLowerInvariant()) <= budget;
    }

    /// <summary><c>; did you mean "x"?</c> for a message tail, or the empty string.</summary>
    public static string DidYouMean(string word, IReadOnlyList<string> candidates)
    {
        var match = Suggest(word, candidates);
        return match is null ? "" : "; did you mean \"" + match + "\"?";
    }

    /// <summary>The edits a typo may take: one for short words, a third of the length otherwise.</summary>
    private static int TypoBudget(string word)
    {
        return Math.Max(1, word.Length / 3);
    }

    /// <summary>Damerau-Levenshtein distance with adjacent transpositions, the edits typos are made of.</summary>
    private static int EditDistance(string a, string b)
    {
        if (a == b)
        {
            return 0;
        }

        if (a.Length == 0)
        {
            return b.Length;
        }

        if (b.Length == 0)
        {
            return a.Length;
        }

        var rows = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++)
        {
            rows[i, 0] = i;
        }

        for (var j = 0; j <= b.Length; j++)
        {
            rows[0, j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                var value = Math.Min(Math.Min(rows[i - 1, j] + 1, rows[i, j - 1] + 1), rows[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    value = Math.Min(value, rows[i - 2, j - 2] + 1);
                }

                rows[i, j] = value;
            }
        }

        return rows[a.Length, b.Length];
    }
}
