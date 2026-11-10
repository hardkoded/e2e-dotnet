// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E;
using E2E.Engine;

namespace WebBenchmark.E2ETests.Fixtures;

/// <summary>
/// Upstream's <c>apps/web-benchmark/tests/support.ts</c>: spellings the deterministic suite shares. Reading a code off the page, the centre
/// of a node, the error a step failed with, and the control beside a visible label.
/// </summary>
public static class Support
{
    /// <summary>Waits until <paramref name="target"/> shows <paramref name="pattern"/>, then returns the match, the way a person copies a code off the screen.</summary>
    public static async Task<string> ReadCode(Locator target, Regex pattern)
    {
        await Expect.That(target).ToContainTextAsync(pattern);
        var match = pattern.Match(await target.TextContentAsync() ?? "");
        if (!match.Success)
        {
            throw new InvalidOperationException($"the text no longer matches {pattern}");
        }

        return match.Value;
    }

    /// <summary>The centre of a node's box, the point a pixel-driven gesture aims for; no box is an error.</summary>
    public static async Task<Point> CenterOf(Locator node)
    {
        var box = await node.BoundingBoxAsync() ?? throw new InvalidOperationException("the node has no box");
        return new Point(box.X + box.Width / 2, box.Y + box.Height / 2);
    }

    /// <summary>What <paramref name="act"/> threw, null when it succeeded; the caller pins its type or message.</summary>
    public static async Task<Exception?> Failure(Func<Task> act)
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
