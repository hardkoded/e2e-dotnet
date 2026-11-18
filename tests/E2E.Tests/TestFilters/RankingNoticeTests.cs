// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.Select;
using static E2E.Tests.Select.SelectKit;
using Filters = E2E.TestFilters;

namespace E2E.Tests.TestFilters;

/// <summary>
/// Upstream's <c>describe('rankingNotice')</c> (<c>tests/unit/test-filters.test.ts</c>). Every <c>it</c> is partial: it builds its collection
/// by hand where upstream collects <c>test()</c> declarations with <c>collectModule</c>, and its config by hand where upstream calls
/// <c>resolveConfig</c>.
/// </summary>
public sealed class RankingNoticeTests
{
    private static string IdOf(Selection selection, string title) =>
        TestFiltersKit.IdOf(Filters.FilterCandidates(selection), title);

    [Fact]
    public void Partial_Says_how_many_of_the_tests_the_selection_runs_were_scored_counting_each_test_once_across_targets_when_the_scores_tell_them_apart()
    {
        var col = CollectionOf(
            "tests/a.e2e.ts",
            new Decl("a"),
            new Decl("b"),
            new Decl("c") { Tags = ["slow"] },
            new Decl("d") { Skip = true }
        );
        var cfg = ConfigOf(targets: [Web("web"), Web("web-2")]);
        var selection = Selector.Select(col, cfg, new SelectionFilters { ExcludeTags = ["slow"] });
        var candidates = Filters.FilterCandidates(selection);
        var (a, b) = (TestFiltersKit.IdOf(candidates, "a"), TestFiltersKit.IdOf(candidates, "b"));
        Assert.Equal(["a", "b"], candidates.Select(TestFiltersKit.TitleOf));

        Assert.Equal(
            "ranked 1 of 2 tests by relevance, likeliest first",
            Filters.RankingNotice(selection, new Dictionary<string, double> { [a] = 1 })
        );
        Assert.Equal(
            "ranked 2 of 2 tests by relevance, likeliest first",
            Filters.RankingNotice(selection, new Dictionary<string, double> { [a] = 0.8, [b] = 0.1 })
        );
        Assert.Null(Filters.RankingNotice(selection, new Dictionary<string, double>()));

        // A score for a test the selection does not run counts for nothing: a slow test, a skip, a test no file has.
        var slow = col.Tests.First(entry => entry.Title == "c").Id;
        var skipped = col.Tests.First(entry => entry.Title == "d").Id;
        Assert.Null(
            Filters.RankingNotice(
                selection,
                new Dictionary<string, double>
                {
                    [slow] = 1,
                    [skipped] = 1,
                    ["tests/z.e2e.ts::gone"] = 1,
                }
            )
        );
    }

    [Fact]
    public void Partial_Says_nothing_when_the_scores_leave_every_test_in_one_band_since_the_order_is_the_one_without_them()
    {
        var selection = Selector.Select(CollectionOf("tests/a.e2e.ts", new Decl("a"), new Decl("b")), ConfigOf());
        var (a, b) = (IdOf(selection, "a"), IdOf(selection, "b"));

        // 0.8 and 0.9 are both in the top band, and so are 0 and 0.24 in the lowest.
        Assert.Null(Filters.RankingNotice(selection, new Dictionary<string, double> { [a] = 0.8, [b] = 0.9 }));
        Assert.Null(Filters.RankingNotice(selection, new Dictionary<string, double> { [a] = 0, [b] = 0.24 }));

        // One test scored 0.5 beside one unscored is two bands.
        Assert.Equal(
            "ranked 1 of 2 tests by relevance, likeliest first",
            Filters.RankingNotice(selection, new Dictionary<string, double> { [a] = 0.5 })
        );

        // A single test has nothing to rank against.
        var alone = Selector.Select(CollectionOf("tests/a.e2e.ts", new Decl("only")), ConfigOf());
        Assert.Null(Filters.RankingNotice(alone, new Dictionary<string, double> { [IdOf(alone, "only")] = 1 }));
    }
}
