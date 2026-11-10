// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E;
using E2E.Engine;

namespace WebBenchmark.E2ETests.Fixtures;

/// <summary>
/// Ported from upstream's <c>apps/web-benchmark/tests/support.ts</c>: the spellings the deterministic suites share. Reading a code off the page,
/// the centre of a node, the error a step failed with, and the control beside a visible label.
/// </summary>
public static class Support
{
    /// <summary>
    /// Waits until <paramref name="target"/> shows <paramref name="pattern"/>, then returns the match, the way a person copies a code off the
    /// screen. Upstream's <c>readCode</c>.
    /// </summary>
    public static async Task<string> ReadCodeAsync(Locator target, Regex pattern)
    {
        await Expect.That(target).ToContainTextAsync(pattern);
        var match = pattern.Match(await target.TextContentAsync() ?? string.Empty);
        if (!match.Success)
        {
            throw new InvalidOperationException($"the text no longer matches {pattern}");
        }

        return match.Value;
    }

    /// <summary>The centre of a node's box, the point a pixel-driven gesture aims for; no box is an error. Upstream's <c>centerOf</c>.</summary>
    public static async Task<Point> CenterOfAsync(Locator node)
    {
        var box = await node.BoundingBoxAsync() ?? throw new InvalidOperationException("the node has no box");
        return new Point(box.X + box.Width / 2, box.Y + box.Height / 2);
    }

    /// <summary>What <paramref name="act"/> threw, null when it succeeded; the caller pins its type or message. Upstream's <c>failure</c>.</summary>
    public static async Task<Exception?> FailureAsync(Func<Task> act)
    {
        try
        {
            await act();
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception error)
        {
            return error;
        }
    }

    /// <summary>
    /// The first <paramref name="sibling"/> element after the node whose own text is <paramref name="label"/>: a Screen query names a node by
    /// its own text or accessible name and cannot step to a neighbour, so a control whose only truthful name is the label text beside it has no
    /// semantic handle. Upstream's <c>siblingOf</c>.
    /// </summary>
    public static Locator SiblingOf(Browser browser, string label, string sibling) =>
        browser.Locator($"xpath=//*[normalize-space(text())={XPathLiteral(label)}]/following-sibling::{sibling}[1]");

    /// <summary>Quotes <paramref name="text"/> as an XPath string literal, through <c>concat()</c> when it holds both quote kinds. Upstream's <c>xpathLiteral</c>.</summary>
    private static string XPathLiteral(string text)
    {
        if (!text.Contains('"'))
        {
            return $"\"{text}\"";
        }

        if (!text.Contains('\''))
        {
            return $"'{text}'";
        }

        return $"concat(\"{string.Join("\", '\"', \"", text.Split('"'))}\")";
    }
}
