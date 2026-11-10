// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace WebBenchmark.E2ETests;

/// <summary>
/// Ported from upstream's <c>apps/web-benchmark/tests/support.ts</c>: the spellings the deterministic suites share. Only the helpers the
/// ported suites call are here; <c>centerOf</c> comes with the suite that needs it.
/// </summary>
public static class Support
{
    /// <summary>
    /// Waits until <paramref name="target"/> shows <paramref name="pattern"/>, then returns the match, the way a person copies a code off the
    /// screen.
    /// </summary>
    public static async Task<string> ReadCodeAsync(Locator target, Regex pattern)
    {
        await Expect.That(target).ToContainTextAsync(pattern);
        var match = pattern.Match(await target.TextContentAsync() ?? "");
        if (!match.Success)
        {
            throw new InvalidOperationException($"the text no longer matches {pattern}");
        }

        return match.Value;
    }

    /// <summary>What <paramref name="act"/> threw or rejected with, null when it succeeded; the caller pins its <c>Code</c>.</summary>
    public static async Task<Exception?> FailureAsync(Func<Task> act)
    {
        try
        {
            await act();
            return null;
        }
        catch (Exception error)
        {
            return error;
        }
    }

    /// <summary>
    /// The first <paramref name="sibling"/> element after the node whose own text is <paramref name="label"/>: a Screen query names a node by
    /// its own text or accessible name and cannot step to a neighbour, so a control whose only truthful name is the label text beside it has
    /// no semantic handle.
    /// </summary>
    public static Locator SiblingOf(Browser browser, string label, string sibling) =>
        browser.Locator($"xpath=//*[normalize-space(text())={XpathLiteral(label)}]/following-sibling::{sibling}[1]");

    /// <summary>Quotes <paramref name="text"/> as an XPath string literal, through <c>concat()</c> when it holds both quote kinds.</summary>
    private static string XpathLiteral(string text)
    {
        if (!text.Contains('"'))
        {
            return $"\"{text}\"";
        }

        if (!text.Contains('\''))
        {
            return $"'{text}'";
        }

        return "concat(\"" + string.Join("\", '\"', \"", text.Split('"')) + "\")";
    }
}
