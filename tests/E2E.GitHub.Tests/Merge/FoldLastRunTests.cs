// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Reports;
using static E2E.GitHub.Tests.Fixtures;
using Folding = E2E.GitHub.Merge;

namespace E2E.GitHub.Tests.Merge;

/// <summary>
/// Upstream's <c>describe('foldLastRun')</c> (<c>packages/github/tests/unit/merge.test.ts</c>, tester-army/e2e <c>50fa7af</c>): a rerun folded into the run before it,
/// and what the page shows for each kind of test. Not ported, since the port has no serial groups: <c>joins a rerun serial group's attempts to the run before's...</c>,
/// <c>shows a carried serial member's group, where its attempts live</c>, and <c>counts a serial member executed when its body ran in any group attempt...</c>.
/// </summary>
public sealed class FoldLastRunTests
{
    private static readonly ReportAttempt FailedAttempt = Attempt(AttemptStatus.Failed, error: Error("ASSERTION_FAILED", "no cart"));

    private static readonly ReportAttempt InterruptedAttempt = Attempt(
        AttemptStatus.Interrupted,
        error: new ReportError { Category = ErrorCategory.Interrupted, Code = "INTERRUPTED", Message = "run interrupted in phase test" });

    private static readonly SkipInfo LeftOut = new() { Cause = SkipCause.Filtered, Reason = "did not fail in the last run" };

    private static SkipInfo Grepped => new() { Cause = SkipCause.Filtered, Reason = "title does not match --grep" };

    private static readonly Report FirstPass = ReportOf(
        ReportRunStatus.Failed,
        [
            Result("steady", ResultStatus.Passed, attempts: [Attempt(durationMs: 500)]),
            Result("recovers", ResultStatus.Failed, attempts: [FailedAttempt, FailedAttempt]),
            Result("still broken", ResultStatus.Failed, attempts: [FailedAttempt]),
            Result("never wanted", ResultStatus.Skipped, skip: new SkipInfo { Cause = SkipCause.Explicit, Reason = "later" }),
        ]);

    private static readonly Report Rerun = ReportOf(
        ReportRunStatus.Failed,
        [
            Result("steady", ResultStatus.Skipped, selected: false, skip: LeftOut),
            Result("recovers", ResultStatus.Passed, attempts: [Attempt(durationMs: 900)]),
            Result("still broken", ResultStatus.Failed, attempts: [FailedAttempt]),
            Result("never wanted", ResultStatus.Skipped, selected: false, skip: LeftOut),
            Result("new since", ResultStatus.Passed, attempts: [Attempt()]),
        ]);

    private static Dictionary<string, ReportResult> ByTitle(Report document)
    {
        var entries = new Dictionary<string, ReportResult>(StringComparer.Ordinal);
        foreach (var entry in document.Run.Results)
        {
            entries[$"{entry.TitlePath[0]}#{entry.Repeat}"] = entry;
        }

        return entries;
    }

    private static string Page(Report document) => MarkdownReport.RenderMarkdownReport(document);

    [Fact]
    public void Leaves_the_rerun_as_it_is_when_the_run_before_was_an_exploration()
    {
        var explored = ReportOf(
            ReportRunStatus.Failed,
            [Result("Explore checkout", ResultStatus.Failed, attempts: [FailedAttempt])],
            explore: new ReportExplore("Explore checkout", new ExploreBudgets(8, TimeSpan.FromMilliseconds(600_000)), ReportExploreEnded.Finished, [], [], null));
        Assert.Same(Rerun, Folding.FoldLastRun(Rerun, explored));
    }

    [Fact]
    public void Carries_the_tests_the_rerun_left_out_marks_a_recovered_test_flaky_with_both_runs_attempts_and_keeps_a_still_failing_one_failed()
    {
        var folded = Folding.FoldLastRun(Rerun, FirstPass);
        var entries = ByTitle(folded);
        Assert.Equal(ResultStatus.Passed, entries["steady#0"].Status);
        Assert.True(entries["steady#0"].Selected);
        Assert.Single(entries["steady#0"].Attempts);
        Assert.Equal(ResultStatus.Flaky, entries["recovers#0"].Status);
        Assert.True(entries["recovers#0"].Selected);
        Assert.Equal([AttemptStatus.Failed, AttemptStatus.Failed, AttemptStatus.Passed], entries["recovers#0"].Attempts.Select(entry => entry.Status));
        Assert.Equal(ResultStatus.Failed, entries["still broken#0"].Status);
        Assert.Equal(2, entries["still broken#0"].Attempts.Count);

        // An explicit skip stays what the run before said it was; a test the run before never had is the rerun's.
        Assert.Equal(ResultStatus.Skipped, entries["never wanted#0"].Status);
        Assert.Equal(SkipCause.Explicit, entries["never wanted#0"].Skip!.Cause);
        Assert.Equal(ResultStatus.Passed, entries["new since#0"].Status);
        Assert.True(entries["new since#0"].Selected);
        Assert.Equal(new ReportSummary { Discovered = 5, Selected = 5, Executed = 4, Passed = 2, Flaky = 1, Failed = 1, Interrupted = 0, Skipped = 1 }, folded.Run.Summary);

        // The rerun decides the outcome; the start is the run before's, so the duration spans both.
        Assert.Equal(ReportRunStatus.Failed, folded.Run.Status);
        Assert.Equal(FirstPass.Run.StartedAt, folded.Run.StartedAt);
        Assert.Equal(Rerun.Run.FinishedAt, folded.Run.FinishedAt);
    }

    [Fact]
    public void Folds_the_repeats_a_plain_rerun_ran_once_into_its_one_result_and_carries_the_repeats_of_a_test_it_left_out()
    {
        var before = ReportOf(
            ReportRunStatus.Failed,
            [
                Result("wobbly", ResultStatus.Passed, attempts: [Attempt()], repeat: 0),
                Result("wobbly", ResultStatus.Failed, attempts: [FailedAttempt], repeat: 1),
                Result("wobbly", ResultStatus.Failed, attempts: [FailedAttempt], repeat: 2),
                Result("solid", ResultStatus.Passed, attempts: [Attempt()], repeat: 0),
                Result("solid", ResultStatus.Passed, attempts: [Attempt()], repeat: 1),
            ]);
        var once = ReportOf(results:
        [
            Result("wobbly", ResultStatus.Passed, attempts: [Attempt()]),
            Result("solid", ResultStatus.Skipped, selected: false, skip: LeftOut),
        ]);
        var folded = Folding.FoldLastRun(once, before);
        var entries = ByTitle(folded);
        Assert.Equal(3, folded.Run.Results.Count);
        Assert.Equal(ResultStatus.Flaky, entries["wobbly#0"].Status);
        Assert.Equal([AttemptStatus.Passed, AttemptStatus.Failed, AttemptStatus.Failed, AttemptStatus.Passed], entries["wobbly#0"].Attempts.Select(entry => entry.Status));
        Assert.False(entries.ContainsKey("wobbly#1"));
        Assert.Equal(ResultStatus.Passed, entries["solid#0"].Status);
        Assert.True(entries["solid#0"].Selected);
        Assert.Equal(ResultStatus.Passed, entries["solid#1"].Status);
        Assert.True(entries["solid#1"].Selected);
        Assert.Equal(3, folded.Run.Summary.Discovered);
        Assert.Equal(2, folded.Run.Summary.Passed);
        Assert.Equal(1, folded.Run.Summary.Flaky);
        Assert.Equal(0, folded.Run.Summary.Failed);
        Assert.Equal(ReportRunStatus.Passed, folded.Run.Status);
    }

    [Fact]
    public void Turns_the_page_red_when_a_filter_kept_a_failed_test_out_of_the_rerun_since_the_page_lists_that_failure()
    {
        var before = ReportOf(
            ReportRunStatus.Failed,
            [Result("A", ResultStatus.Failed, attempts: [FailedAttempt]), Result("B", ResultStatus.Failed, attempts: [FailedAttempt])]);
        var onlyB = ReportOf(results:
        [
            Result("A", ResultStatus.Skipped, selected: false, skip: Grepped),
            Result("B", ResultStatus.Passed, attempts: [Attempt()]),
        ]);
        var folded = Folding.FoldLastRun(onlyB, before);
        Assert.Equal(ResultStatus.Failed, ByTitle(folded)["A#0"].Status);
        Assert.True(ByTitle(folded)["A#0"].Selected);
        Assert.Equal(ResultStatus.Flaky, ByTitle(folded)["B#0"].Status);
        Assert.Equal(ReportRunStatus.Failed, folded.Run.Status);
        Assert.Equal(1, folded.Run.ExitCode);
        Assert.Contains("### 🔴 e2e: 1 failed, 1 flaky", Page(folded), StringComparison.Ordinal);
    }

    [Fact]
    public void Counts_an_interrupted_test_apart_from_failures_one_that_passes_on_the_rerun_is_not_flaky_and_a_carried_one_keeps_the_page_from_reading_green()
    {
        var before = ReportOf(
            ReportRunStatus.Interrupted,
            [Result("A", ResultStatus.Interrupted, attempts: [InterruptedAttempt]), Result("B", ResultStatus.Interrupted, attempts: [InterruptedAttempt])]);
        var onlyA = ReportOf(results:
        [
            Result("A", ResultStatus.Passed, attempts: [Attempt()]),
            Result("B", ResultStatus.Skipped, selected: false, skip: Grepped),
        ]);
        var folded = Folding.FoldLastRun(onlyA, before);
        Assert.Equal(ResultStatus.Passed, ByTitle(folded)["A#0"].Status);
        Assert.Equal(ResultStatus.Interrupted, ByTitle(folded)["B#0"].Status);
        Assert.True(ByTitle(folded)["B#0"].Selected);
        Assert.Equal(new ReportSummary { Discovered = 2, Selected = 2, Executed = 2, Passed = 1, Failed = 0, Interrupted = 1, Flaky = 0, Skipped = 0 }, folded.Run.Summary);
        Assert.Equal(ReportRunStatus.Passed, folded.Run.Status);
        Assert.Contains("### ⏹️ e2e: 1 interrupted, 1 passed", Page(folded), StringComparison.Ordinal);

        // The runner carries B, still owed; a carried interrupted test reached no verdict, so it does not red the page.
        var carryingB = ReportOf(results: onlyA.Run.Results, carried: new ReportCarried { Results = [before.Run.Results[1]] });
        var foldedCarried = Folding.FoldLastRun(carryingB, before);
        Assert.Equal(ResultStatus.Interrupted, ByTitle(foldedCarried)["B#0"].Status);
        Assert.True(ByTitle(foldedCarried)["B#0"].Selected);
        Assert.Equal(ReportRunStatus.Passed, foldedCarried.Run.Status);
        Assert.Equal(0, foldedCarried.Run.ExitCode);
        Assert.Contains("### ⏹️ e2e: 1 interrupted, 1 passed", Page(foldedCarried), StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_the_first_pass_s_failure_when_the_rerun_of_that_test_is_interrupted()
    {
        var before = ReportOf(ReportRunStatus.Failed, [Result("A", ResultStatus.Failed, attempts: [FailedAttempt])]);
        var cut = ReportOf(ReportRunStatus.Interrupted, [Result("A", ResultStatus.Interrupted, attempts: [InterruptedAttempt])]);
        var folded = Folding.FoldLastRun(cut, before);
        Assert.Equal(ResultStatus.Failed, ByTitle(folded)["A#0"].Status);
        Assert.Equal(1, folded.Run.Summary.Failed);
        Assert.Equal(0, folded.Run.Summary.Interrupted);
        Assert.Contains("### 🔴 e2e: 1 failed", Page(folded), StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_the_page_red_while_the_rerun_carries_a_hook_failure_or_a_failed_test_it_did_not_run_again_with_their_evidence()
    {
        var hook = new ReportError
        {
            Category = ErrorCategory.Test,
            Code = "HOOK_FAILED",
            Message = "afterAll failed: teardown broke",
            Phase = ErrorPhase.AfterAll,
            ScopeId = "file",
            Scope = new HookScope { File = "tests/a.e2e.ts", TargetId = "web", TitlePath = [] },
        };
        var evidence = Attempt(artifacts: [ArtifactKind.Screenshot]);
        var inHookScope = Result("A", ResultStatus.Passed, file: "tests/a.e2e.ts", attempts: [evidence]);
        var before = ReportOf(ReportRunStatus.Failed, [inHookScope, Result("B", ResultStatus.Failed, attempts: [FailedAttempt])], errors: [hook]);
        var onlyB = ReportOf(
            results:
            [
                Result("A", ResultStatus.Skipped, file: "tests/a.e2e.ts", selected: false, skip: Grepped),
                Result("B", ResultStatus.Passed, attempts: [Attempt()]),
            ],
            carried: new ReportCarried { Results = [inHookScope], Errors = [hook] });
        var folded = Folding.FoldLastRun(onlyB, before);
        Assert.Same(inHookScope, ByTitle(folded)["A#0"]);
        Assert.Equal(ResultStatus.Flaky, ByTitle(folded)["B#0"].Status);
        Assert.Equal([hook], folded.Run.Errors);
        Assert.Equal(ReportRunStatus.Failed, folded.Run.Status);
        Assert.Equal(1, folded.Run.ExitCode);
        var page = Page(folded);
        Assert.Contains("### 🔴 e2e: 1 flaky, 1 passed", page, StringComparison.Ordinal);
        Assert.Contains("HOOK_FAILED", page, StringComparison.Ordinal);

        // A carried test that failed reds the page on its own.
        var carriedFailure = Result("C", ResultStatus.Failed, attempts: [FailedAttempt]);
        var withFailure = ReportOf(
            results: [Result("C", ResultStatus.Skipped, selected: false, skip: LeftOut), Result("B", ResultStatus.Passed, attempts: [Attempt()])],
            carried: new ReportCarried { Results = [carriedFailure] });
        Assert.Equal(
            ReportRunStatus.Failed,
            Folding.FoldLastRun(withFailure, ReportOf(results: [Result("B", ResultStatus.Skipped, selected: false, skip: LeftOut)])).Run.Status);

        // So does one the failure limit stopped before it ever ran.
        var neverRan = Result("D", ResultStatus.Skipped, skip: new SkipInfo { Cause = SkipCause.FailureLimit, Reason = "run stopped after 1 failure (--max-failures 1)" });
        var withLimit = ReportOf(
            results: [Result("D", ResultStatus.Skipped, selected: false, skip: LeftOut), Result("B", ResultStatus.Passed, attempts: [Attempt()])],
            carried: new ReportCarried { Results = [neverRan] });
        var limited = Folding.FoldLastRun(withLimit, ReportOf(results: [Result("B", ResultStatus.Skipped, selected: false, skip: LeftOut)])).Run;
        Assert.Equal(ReportRunStatus.Failed, limited.Status);
        Assert.Equal(1, limited.ExitCode);
    }

    [Fact]
    public void Reads_a_test_the_run_before_carried_by_the_row_it_carried_so_a_rerun_that_runs_it_at_last_folds_in_its_first_failure()
    {
        var firstFailure = Result("A", ResultStatus.Failed, attempts: [FailedAttempt]);
        var narrowed = ReportOf(
            results:
            [
                Result("A", ResultStatus.Skipped, selected: false, skip: Grepped),
                Result("B", ResultStatus.Passed, attempts: [Attempt()]),
            ],
            carried: new ReportCarried { Results = [firstFailure] });
        var atLast = ReportOf(results:
        [
            Result("A", ResultStatus.Passed, attempts: [Attempt()]),
            Result("B", ResultStatus.Skipped, selected: false, skip: LeftOut),
        ]);
        var folded = Folding.FoldLastRun(atLast, narrowed);
        Assert.Equal(ResultStatus.Flaky, ByTitle(folded)["A#0"].Status);
        Assert.Equal([AttemptStatus.Failed, AttemptStatus.Passed], ByTitle(folded)["A#0"].Attempts.Select(entry => entry.Status));
        Assert.Equal(ResultStatus.Passed, ByTitle(folded)["B#0"].Status);
        Assert.True(ByTitle(folded)["B#0"].Selected);
        Assert.Equal(ReportRunStatus.Passed, folded.Run.Status);
    }

    [Fact]
    public void Keeps_the_filters_and_the_change_of_the_run_before_beside_the_tests_its_filters_left_out_since_a_last_failed_rerun_runs_no_filter()
    {
        const string Base = "0123456789abcdef0123456789abcdef01234567";
        var before = ReportOf(
            ReportRunStatus.Failed,
            [
                Result("alpha", ResultStatus.Failed, attempts: [FailedAttempt]),
                Result("beta", ResultStatus.Passed, attempts: [Attempt()]),
                Result("gamma", ResultStatus.Skipped, selected: false, skip: new SkipInfo { Cause = SkipCause.Filtered, Reason = "Jev: 5% likely affected", Filter = "jev" }),
            ],
            filters: [new ReportFilter { Name = "jev", Candidates = 3, Excluded = 1 }],
            changes: new ReportChanges { Base = Base, Files = 4 });
        var lastFailed = ReportOf(
            results:
            [
                Result("alpha", ResultStatus.Passed, attempts: [Attempt()]),
                Result("beta", ResultStatus.Skipped, selected: false, skip: LeftOut),
                Result("gamma", ResultStatus.Skipped, selected: false, skip: LeftOut),
            ],
            changes: new ReportChanges { Skipped = "--last-failed" });
        var folded = Folding.FoldLastRun(lastFailed, before);
        Assert.Same(before.Run.Filters, folded.Run.Filters);
        Assert.Same(before.Run.Changes, folded.Run.Changes);
        Assert.False(ByTitle(folded)["gamma#0"].Selected);
        Assert.Equal("jev", ByTitle(folded)["gamma#0"].Skip!.Filter);
        var page = Page(folded);
        Assert.Contains(", 1 left out by filters\n", page, StringComparison.Ordinal);
        Assert.Contains("**Filter** `jev` kept 2 of 3 tests, left out 1\n", page, StringComparison.Ordinal);

        // A first pass that ran no filter folds to no run.filters and no run.changes, not even the rerun's `--last-failed` record.
        var unfiltered = Folding.FoldLastRun(lastFailed, ReportOf(ReportRunStatus.Failed, [.. before.Run.Results.Take(2)]));
        Assert.Null(unfiltered.Run.Filters);
        Assert.Null(unfiltered.Run.Changes);
    }

    [Fact]
    public void Counts_only_the_tests_still_left_out_on_the_page_when_the_rerun_ran_one_the_run_before_s_filter_left_out()
    {
        var before = ReportOf(
            ReportRunStatus.Failed,
            [
                Result("sign in", ResultStatus.Failed, attempts: [FailedAttempt]),
                Result("member b", ResultStatus.Skipped, selected: false, skip: new SkipInfo { Cause = SkipCause.Filtered, Reason = "unaffected", Filter = "pages" }),
                Result("guest page", ResultStatus.Passed, attempts: [Attempt()]),
            ],
            filters: [new ReportFilter { Name = "pages", Candidates = 2, Excluded = 1 }],
            changes: new ReportChanges { Base = "0123456789abcdef0123456789abcdef01234567", Files = 2 });
        var lastFailed = ReportOf(
            results:
            [
                Result("sign in", ResultStatus.Passed, attempts: [Attempt()]),
                Result("member b", ResultStatus.Passed, attempts: [Attempt()]),
                Result("guest page", ResultStatus.Skipped, selected: false, skip: LeftOut),
            ],
            changes: new ReportChanges { Skipped = "--last-failed" });
        var folded = Folding.FoldLastRun(lastFailed, before);
        Assert.True(ByTitle(folded)["member b#0"].Selected);
        Assert.Equal(ResultStatus.Passed, ByTitle(folded)["member b#0"].Status);
        var page = Page(folded);
        Assert.Contains("**Filter** `pages` kept all 2 tests\n", page, StringComparison.Ordinal);
        Assert.DoesNotContain("left out by filters", page, StringComparison.Ordinal);
    }

    [Fact]
    public void Leaves_the_rerun_alone_when_the_run_before_is_another_projects()
    {
        var other = ReportOf(ReportRunStatus.Failed, FirstPass.Run.Results, projectId: "dev.example.other");
        Assert.Same(Rerun, Folding.FoldLastRun(Rerun, other));
    }
}
