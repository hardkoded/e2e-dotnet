// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Tests.Select;
using static E2E.Tests.Select.SelectKit;
using Filters = E2E.TestFilters;

namespace E2E.Tests.TestFilters;

/// <summary>
/// Upstream's <c>describe('filterCandidates')</c> (<c>tests/unit/test-filters.test.ts</c>). Every <c>it</c> is partial: it builds its collection
/// by hand where upstream collects <c>test()</c> declarations with <c>collectModule</c>, and its config by hand where upstream calls
/// <c>resolveConfig</c>.
/// </summary>
public sealed class FilterCandidatesTests
{
    [Fact]
    public void Partial_Hands_over_each_test_the_selection_runs_once_in_report_order_by_id_file_title_path_and_tags()
    {
        var col = CollectionOf(
            "tests/a.e2e.ts",
            new Decl("sign in") { Sessions = ["member"] },
            new Decl("signed in") { Session = "member", Tags = ["auth"] },
            new Decl("pays") { Describe = ["checkout"], DescribeTags = ["billing"], Tags = ["smoke"] },
            new Decl("later") { Skip = true },
            new Decl("on ios only") { Platforms = ["ios"] },
            new Decl("needs a device") { Requires = ["device"] },
            new Decl("slow") { Tags = ["slow"] });
        var cfg = ConfigOf(targets: [Web("web", StateEngine), Web("web-2", StateEngine)]);
        var candidates = Filters.FilterCandidates(Selector.Select(col, cfg, new SelectionFilters { ExcludeTags = ["slow"] }));

        // Two targets, one candidate per test; the setup, the skip, the tests no target runs are never offered.
        Assert.Equal(["signed in", "pays"], candidates.Select(TestFiltersKit.TitleOf));
        var pays = candidates[1];
        Assert.Equal(col.Tests.First(entry => entry.Title == "pays").Id, pays.Id);
        Assert.Equal("tests/a.e2e.ts", pays.File);
        Assert.Equal(["checkout", "pays"], pays.TitlePath);
        Assert.Equal(["billing", "smoke"], pays.Tags);
        Assert.Null(pays.SetupFile);
    }

    [Fact]
    public void Partial_Orders_candidates_by_file_then_declaration_whichever_target_runs_them_first()
    {
        var a = CollectionOf("tests/a.e2e.ts", new Decl("a ios only") { Platforms = ["ios"] }, new Decl("a anywhere"));
        var b = CollectionOf("tests/b.e2e.ts", new Decl("b anywhere"), new Decl("b ios only") { Platforms = ["ios"] });
        var cfg = ConfigOf(targets: [Web("web"), Web("ios", platform: "ios")]);
        Assert.Equal(
            ["tests/a.e2e.ts::a%20ios%20only", "tests/a.e2e.ts::a%20anywhere", "tests/b.e2e.ts::b%20anywhere", "tests/b.e2e.ts::b%20ios%20only"],
            Filters.FilterCandidates(Selector.Select(Merge(a, b), cfg)).Select(candidate => candidate.Id));
    }

    [Fact]
    public void Partial_Names_the_file_of_the_setup_test_that_saves_each_candidate_session_and_none_for_a_test_without_one()
    {
        var setups = CollectionOf("tests/auth.setup.e2e.ts", new Decl("sign in") { Sessions = ["member", "admin"] });
        var pages = CollectionOf(
            "tests/pages.e2e.ts",
            new Decl("member page") { Session = "member" },
            new Decl("admin page") { Session = "admin", Tags = ["slow"] },
            new Decl("guest page"));
        var selection = Selector.Select(Merge(setups, pages), ConfigOf(targets: [Web("web", StateEngine)]), new SelectionFilters { ExcludeTags = ["slow"] });
        Assert.Equal(
            [("member page", (string?)"tests/auth.setup.e2e.ts"), ("guest page", null)],
            Filters.FilterCandidates(selection).Select(candidate => (candidate.TitlePath[^1], candidate.SetupFile)));
    }
}
