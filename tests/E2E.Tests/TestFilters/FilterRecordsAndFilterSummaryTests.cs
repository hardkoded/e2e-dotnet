// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.TestFilters.TestFiltersKit;
using Filters = E2E.TestFilters;

namespace E2E.Tests.TestFilters;

/// <summary>Upstream's <c>describe('filterRecords and filterSummary')</c> (<c>tests/unit/test-filters.test.ts</c>).</summary>
public sealed class FilterRecordsAndFilterSummaryTests
{
    private static FilterOf Leaving(string name, string[] titles, string? note = null) =>
        new(name, (tests, _) => Task.FromResult(new TestFilterResult
        {
            Exclude = tests.Where(entry => titles.Contains(TitleOf(entry))).ToDictionary(entry => entry.Id, _ => "unaffected"),
            Note = note,
        }));

    /// <summary>
    /// Partial: the port has no <c>collect</c> and <c>select</c>, so the final selection is built by hand: two targets over the tests
    /// <c>step 1</c>, <c>step 2</c>, <c>pays</c>, and <c>refunds</c>, with no serial group, so <c>step 1</c> is not named by the first filter
    /// (upstream names it, and its serial group takes it back).
    /// </summary>
    [Fact]
    public async Task Partial_counts_distinct_tests_the_final_selection_leaves_out_once_across_targets_each_filter_on_its_own_input_with_no_summary_for_one_that_failed_or_timed_out()
    {
        var candidates = CandidatesOf("step 1", "step 2", "pays", "refunds");
        var boom = new FilterOf("boom", (_, _) => throw new InvalidOperationException("kaboom"));
        var applied = await Filters.ApplyTestFilters([Leaving("areas", ["pays"], "src/todos changed"), Leaving("jev", ["refunds"]), boom], candidates, Scope().Scope);

        // The final selection: every test on two targets, each left-out pair carrying its filter's name.
        var results = candidates
            .SelectMany(candidate => new[] { "web", "web-2" }.Select(_ => (candidate.Id, Skip: applied.Excluded.TryGetValue(candidate.Id, out var left) ? new ReportSkip("filtered", left.Reason, left.Filter) : null)))
            .ToList();
        var records = Filters.FilterRecords(applied.Decisions, results);
        Assert.Equal(
            [
                new ReportFilter("areas", 4, 1, "src/todos changed"),
                new ReportFilter("jev", 3, 1),
                new ReportFilter("boom", 2, 0, "failed, so it leaves nothing out: kaboom", FilterOutcome.Failed),
            ],
            records);
        Assert.Equal(["areas kept 3 of 4 tests, left out 1 (src/todos changed)", "jev kept 2 of 3 tests, left out 1", null], records.Select(Filters.FilterSummary));
        Assert.Null(Filters.FilterSummary(new ReportFilter("slow", 2, 0, Outcome: FilterOutcome.TimedOut)));
    }
}
