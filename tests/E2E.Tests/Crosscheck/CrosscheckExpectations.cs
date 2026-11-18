// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace E2E.Tests.Crosscheck;

/// <summary>
/// The committed state of the cross-check (upstream's <c>expectations.ts</c>): one line per known disagreement, each with the
/// reason it is kept. A run fails on a disagreement the file does not list and on a listed one that no longer happens, so a fix
/// removes its line and a regression adds one, both in review. <c>E2E_CROSSCHECK_UPDATE=1</c> rewrites the file from the run,
/// keeping every surviving reason and marking new lines <c>TODO</c>, which fails until someone writes the reason.
/// Line format, fields separated by <c> | </c>: <c>page | oracle field | role "name" | ours &lt;value&gt; | theirs &lt;value&gt; | reason</c>.
/// </summary>
public static class CrosscheckExpectations
{
    public const string TodoReason = "TODO: say why this disagreement is kept, or fix it";

    private const string Separator = " | ";

    /// <summary>The line of one disagreement without its reason; equal keys are the same disagreement (upstream's <c>keyOf</c>).</summary>
    public static string KeyOf(string page, Disagreement disagreement) =>
        string.Join(
                Separator,
                page,
                $"{disagreement.Oracle} {disagreement.Field}",
                disagreement.Node,
                $"ours {disagreement.Ours}",
                $"theirs {disagreement.Theirs}"
            )
            .Replace("\n", " ", StringComparison.Ordinal);

    /// <summary>Every listed key with its reason; comment and blank lines are skipped (upstream's <c>readExpectations</c>).</summary>
    public static Dictionary<string, string> ReadExpectations(string file)
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllText(file).Split('\n'))
        {
            if (line.Trim().Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var cut = line.LastIndexOf(Separator, StringComparison.Ordinal);
            expected[line[..cut]] = line[(cut + Separator.Length)..];
        }

        return expected;
    }

    /// <summary>Expected keys of the pages that ran which did not happen, and seen keys the file does not list (upstream's <c>diffExpectations</c>).</summary>
    public static (List<string> Unexpected, List<string> Stale) DiffExpectations(
        IReadOnlyDictionary<string, string> expected,
        IReadOnlySet<string> seen,
        IReadOnlySet<string> pages
    )
    {
        var unexpected = seen.Where(key => !expected.ContainsKey(key)).Order(StringComparer.Ordinal).ToList();
        var stale = expected
            .Keys.Where(key => pages.Contains(PageOf(key)) && !seen.Contains(key))
            .Order(StringComparer.Ordinal)
            .ToList();
        return (unexpected, stale);
    }

    /// <summary>
    /// Rewrites the file: lines of pages that did not run are kept as they are, lines of pages that ran are replaced by what they
    /// showed (upstream's <c>writeExpectations</c>).
    /// </summary>
    public static void WriteExpectations(
        string file,
        string header,
        IReadOnlyDictionary<string, string> expected,
        IReadOnlySet<string> seen,
        IReadOnlySet<string> pages
    )
    {
        var lines = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, reason) in expected)
        {
            if (!pages.Contains(PageOf(key)))
            {
                lines[key] = reason;
            }
        }

        foreach (var key in seen)
        {
            lines[key] = expected.TryGetValue(key, out var reason) ? reason : TodoReason;
        }

        var body = lines
            .OrderBy(line => line.Key, StringComparer.Ordinal)
            .Select(line => $"{line.Key}{Separator}{line.Value}");
        File.WriteAllText(file, $"{header}\n{string.Join('\n', body)}\n", new UTF8Encoding(false));
    }

    /// <summary>The page a key belongs to: everything before its first separator (upstream's <c>pageOf</c>).</summary>
    public static string PageOf(string key) => key[..key.IndexOf(Separator, StringComparison.Ordinal)];
}
