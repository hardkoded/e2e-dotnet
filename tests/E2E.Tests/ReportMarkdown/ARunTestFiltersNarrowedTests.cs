// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Internal;
using static E2E.Tests.ReportMarkdown.MarkdownKit;

namespace E2E.Tests.ReportMarkdown;

/// <summary>Upstream's <c>describe('a run test filters narrowed')</c> (<c>tests/unit/report-markdown.test.ts</c>).</summary>
public sealed class ARunTestFiltersNarrowedTests
{
    private const string Base = "0123456789abcdef0123456789abcdef01234567";

    private static string Render(Report1Document document) => MarkdownReport.RenderMarkdownReport(document);

    [Fact]
    public void Counts_what_the_filters_left_out_in_the_headline_names_each_filter_and_folds_what_they_left_out_with_their_reasons()
    {
        var results = new[] { Passing, LeftOut("pays", "areas", "the diff misses billing"), LeftOut("refunds", "jev", "Jev: 4% likely affected") };
        var filters = new[] { Record(excluded: 1, note: "src/auth changed"), Record("jev", 2, 1) };

        var body = Render(Page(results: results, filters: filters));

        Assert.Equal(
            string.Join(
                '\n',
                "### 🟢 e2e: 1 passed, 2 left out by filters",
                "",
                "**Filter** `areas` kept 2 of 3 tests, left out 1 (src/auth changed)  \n**Filter** `jev` kept 1 of 2 tests, left out 1",
                "",
                string.Join(
                    '\n',
                    "<details>",
                    "<summary>2 tests left out by filters</summary>",
                    "",
                    "| Test | Filter | Reason |",
                    "| --- | --- | --- |",
                    "| tests/billing.e2e.ts › pays | `areas` | the diff misses billing |",
                    "| tests/billing.e2e.ts › refunds | `jev` | Jev: 4% likely affected |",
                    "</details>"),
                "",
                "<details>\n<summary>All 1 test in 1 file</summary>\n\n|  | Test | Time |\n| --- | --- | --- |\n| 🟢 | **tests/smoke.e2e.ts** · 1 passed | 850ms |\n| 🟢 | opens the app | 850ms |\n</details>",
                "",
                "<sub>Tested by [e2e](https://e2e.tester.army) 0.9.0 · 8.4s · web</sub>",
                ""),
            body);
    }

    [Fact]
    public void Counts_the_tests_left_out_once_on_a_run_of_several_targets_or_repeats_as_the_filter_line_does_with_a_row_per_result_naming_its_target()
    {
        static ReportResult Ran(string title, string target) => Named(title, ResultStatus.Passed, file: "tests/billing.e2e.ts", target: target);
        var results = new[] { "web", "web-2" }.SelectMany(target => new[] { Ran("alpha", target), LeftOut("beta", "areas", "unaffected", target), LeftOut("gamma", "areas", "unaffected", target) }).ToList();

        var body = Render(Page(results: results, targets: [Target(), Target("web-2", 1)], filters: [Record(candidates: 3, excluded: 2)]));

        Assert.StartsWith("### 🟢 e2e: 2 passed, 2 left out by filters\n", body);
        Assert.Contains("**Filter** `areas` kept 1 of 3 tests, left out 2\n", body);
        Assert.Contains("<summary>2 tests left out by filters</summary>", body);
        Assert.Contains("| tests/billing.e2e.ts (web) › beta | `areas` | unaffected |\n| tests/billing.e2e.ts (web) › gamma |", body);
        Assert.Contains("| tests/billing.e2e.ts (web-2) › gamma | `areas` | unaffected |", body);

        var repeated = Render(Page(results: [.. new[] { 0, 1 }.Select(repeat => LeftOut("beta", "areas", "unaffected") with { Repeat = repeat })], filters: [Record(candidates: 1, excluded: 1)]));
        Assert.Contains("### 🟢 e2e: no tests selected, 1 left out by filters\n", repeated);
        Assert.Contains("<summary>1 test left out by filters</summary>", repeated);
        Assert.Contains("| tests/billing.e2e.ts › beta | `areas` | unaffected |\n| tests/billing.e2e.ts › beta (repeat #1) | `areas` | unaffected |", repeated);
    }

    [Fact]
    public void Reads_a_filter_that_kept_every_test_or_left_every_test_out_and_one_that_failed_and_so_left_nothing_out_by_its_note()
    {
        var failed = Record("owners", 1, 0, "failed, so it leaves nothing out: CODEOWNERS not found", FilterOutcome.Failed);

        var kept = Render(Page(results: [Passing], filters: [Record(candidates: 1, excluded: 0, note: "every test reaches src/app.ts"), failed]));

        Assert.Contains("### 🟢 e2e: 1 passed\n", kept);
        Assert.Contains("**Filter** `areas` kept all 1 test (every test reaches src/app.ts)  \n**Filter** `owners` kept all 1 test (failed, so it leaves nothing out: CODEOWNERS not found)\n", kept);
        Assert.DoesNotContain("left out by filters", kept);
        Assert.Contains("**Filter** `areas` left out all 1 test\n", Render(Page(results: [LeftOut("pays", "areas", "unaffected")], filters: [Record(candidates: 1, excluded: 1)])));
    }

    [Fact]
    public void Counts_what_a_filter_left_out_from_the_results_so_a_test_its_record_counts_that_ran_anyway_reads_as_kept()
    {
        var body = Render(Page(results: [Passing], filters: [Record(candidates: 2, excluded: 1)]));

        Assert.Contains("**Filter** `areas` kept all 2 tests\n", body);
        Assert.DoesNotContain("left out by filters", body);
    }

    [Fact]
    public void Escapes_what_a_filter_wrote_so_a_reason_or_a_note_cannot_break_the_table_or_open_a_block()
    {
        var body = Render(Page(results: [LeftOut("pays", "areas", "a | b\n# heading <img src=x>")], filters: [Record(candidates: 1, excluded: 1, note: "note | with pipe")]));

        var table = body.Split('\n').Where(line => line.StartsWith("| tests/billing.e2e.ts", StringComparison.Ordinal)).ToList();
        Assert.Single(table);
        Assert.Equal(5, Regex.Split(table[0], @"(?<!\\)\|").Length);
        Assert.DoesNotContain("<img", body);
        Assert.DoesNotMatch(new Regex("^# heading", RegexOptions.Multiline), body);
        Assert.Contains("note \\| with pipe", body);
    }

    [Fact]
    public void Names_the_change_the_filters_judged_above_their_lines()
    {
        var body = Render(Page(results: [Passing, LeftOut("pays", "areas", "unaffected")], changes: new ReportChanges(Base, 12), filters: [Record(candidates: 2, excluded: 1)]));

        Assert.Contains("\n**Change** 12 files changed since `0123456`  \n**Filter** `areas` kept 1 of 2 tests, left out 1\n", body);
        Assert.Contains("\n**Change** 1 file changed since `0123456`  \n", Render(Page(results: [Passing], changes: new ReportChanges(Base, 1), filters: [Record(candidates: 1, excluded: 0)])));
    }

    [Theory]
    [InlineData(null, null, "no tests to judge")]
    [InlineData(Base, 0, "no files changed since 0123456")]
    public void Says_why_no_filter_ran_whatever_the_reason(string? @base, int? files, string skipped)
    {
        Assert.Contains("\n**Filters** none ran: " + skipped + "\n", Render(Page(results: [Passing], changes: new ReportChanges(@base, files, skipped))));
    }

    [Fact]
    public void Caps_the_left_out_table_at_50_rows_pointing_at_reportjson_for_the_rest()
    {
        var results = Enumerable.Range(0, 55).Select(index => LeftOut("t" + index.ToString("D2", System.Globalization.CultureInfo.InvariantCulture), "areas", "unaffected")).ToList();

        var body = Render(Page(results: results, filters: [Record(candidates: 55, excluded: 55)]));

        Assert.Contains("### 🟢 e2e: no tests selected, 55 left out by filters\n", body);
        Assert.Contains("<summary>55 tests left out by filters</summary>", body);
        Assert.Contains("| tests/billing.e2e.ts › t49 |", body);
        Assert.DoesNotContain("› t50", body);
        Assert.Contains("| and 5 more in report.json |  |  |\n</details>", body);
    }
}
