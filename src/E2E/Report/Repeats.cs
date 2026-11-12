// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace E2E;

/// <summary>One run of a repeated test. <paramref name="Code"/> is the error code of a run that did not pass.</summary>
internal sealed record RepeatRun(int Repeat, string Status, string? Code = null);

/// <summary>Every run of one test, target, and agent, in repeat order.</summary>
/// <param name="Label">What the group is called.</param>
/// <param name="Runs">The runs in repeat order.</param>
/// <param name="Passed">Runs that passed on their first attempt; a flaky run needed a retry and does not count.</param>
/// <param name="Unstable">Whether a run that reached a verdict did not pass: a flake. A run the run interrupted, or never started, says nothing about the test.</param>
internal sealed record RepeatGroup(string Label, IReadOnlyList<RepeatRun> Runs, int Passed, bool Unstable);

/// <summary>
/// The <c>--repeat-each</c> tally the list and markdown reporters print: every run of one test, target, and agent side by side, so a
/// flake reads as <c>3/5 passed</c> with the runs that did not, instead of five results to count. Upstream's <c>report/repeats.ts</c>.
/// </summary>
internal static class Repeats
{
    /// <summary>Statuses of a run that reached no verdict: stopped by the run, or never started.</summary>
    private static readonly HashSet<string> Undecided = new(["interrupted", "skipped"], StringComparer.Ordinal);

    /// <summary>Groups runs by key, keeping only tests that ran more than once and did not skip every run, in first-seen order.</summary>
    public static List<RepeatGroup> RepeatGroups<T>(IEnumerable<T> items, Func<T, (string Key, string Label, RepeatRun Run)> describe)
    {
        List<string> order = [];
        Dictionary<string, (string Label, List<RepeatRun> Runs)> groups = new(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var (key, label, run) = describe(item);
            if (!groups.TryGetValue(key, out var group))
            {
                order.Add(key);
                groups[key] = group = (label, []);
            }

            group.Runs.Add(run);
        }

        return [.. order
            .Select(key => groups[key])
            .Where(group => group.Runs.Count > 1 && group.Runs.Any(run => run.Status != "skipped"))
            .Select(group =>
            {
                var runs = group.Runs.OrderBy(run => run.Repeat).ToList();
                return new RepeatGroup(
                    group.Label,
                    runs,
                    runs.Count(run => run.Status == "passed"),
                    runs.Any(run => run.Status != "passed" && !Undecided.Contains(run.Status)));
            })];
    }

    /// <summary><c>2 of 3 tests passed all 5 runs</c>; the run count is the most any test had.</summary>
    public static string RepeatSummary(IReadOnlyList<RepeatGroup> groups)
    {
        var stable = groups.Count(group => group.Passed == group.Runs.Count);
        var runs = groups.Max(group => group.Runs.Count);
        return stable.ToString(CultureInfo.InvariantCulture) + " of " + groups.Count.ToString(CultureInfo.InvariantCulture) + " test" + (groups.Count == 1 ? "" : "s") + " passed all " + runs.ToString(CultureInfo.InvariantCulture) + " runs";
    }

    /// <summary>A run that did not pass, in a word: its error code, <c>flaky</c> with the code its retry recovered from, or why it reached no verdict.</summary>
    private static string OutcomeOf(RepeatRun run)
    {
        if (run.Status == "flaky")
        {
            return run.Code is null ? "flaky" : "flaky (" + run.Code + ")";
        }

        return Undecided.Contains(run.Status) ? run.Status : run.Code ?? run.Status;
    }

    /// <summary><c>3/5 passed · repeat 0 ASSERTION_FAILED · repeat 2 flaky · repeat 4 interrupted</c>, naming each run that did not pass.</summary>
    public static string RepeatLine(RepeatGroup group)
    {
        var misses = group.Runs
            .Where(run => run.Status != "passed")
            .Select(run => "repeat " + run.Repeat.ToString(CultureInfo.InvariantCulture) + " " + OutcomeOf(run));
        return string.Join(" · ", [group.Passed.ToString(CultureInfo.InvariantCulture) + "/" + group.Runs.Count.ToString(CultureInfo.InvariantCulture) + " passed", .. misses]);
    }
}
