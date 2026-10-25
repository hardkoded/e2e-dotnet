// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>How many tests a run's test filters left out, counted one way everywhere (upstream's <c>report/left-out.ts</c>).</summary>
internal static class LeftOut
{
    /// <summary>How many tests each test filter left out of <paramref name="results"/>, by its name; a test left out on several targets, agents, or repeats counts once.</summary>
    public static Dictionary<string, int> LeftOutTests(IEnumerable<(string TestId, ReportSkip? Skip)> results)
    {
        var tests = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var (testId, skip) in results)
        {
            if (skip?.Filter is { } filter)
            {
                if (!tests.TryGetValue(filter, out var ids))
                {
                    tests[filter] = ids = new HashSet<string>(StringComparer.Ordinal);
                }

                ids.Add(testId);
            }
        }

        return tests.ToDictionary(pair => pair.Key, pair => pair.Value.Count, StringComparer.Ordinal);
    }
}
