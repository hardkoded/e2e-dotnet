// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using E2E.Tests.Traces;

namespace E2E.Tests.RunReport;

/// <summary>
/// .NET-only: the port's test framework runs each attempt as its own session, so <see cref="E2E.RunReport"/> collects the attempts
/// of a test into the one result upstream's runner keeps.
/// </summary>
public sealed class RunReportTests
{
    private static Report1Document Single(string id, AttemptStatus status) =>
        ReportBuilders.Document(ReportBuilders.Result(id: id, attempts: [ReportBuilders.Attempt(status)]));

    private static (Report1Document Report, bool Traced, Func<string, string> Redact) Add(Report1Document report, string root, int attempt, bool kept, Func<string, string>? redact = null) =>
        E2E.RunReport.Add(report, root, attempt, kept, redact ?? (text => text));

    private static string Root() => "root-" + Guid.NewGuid().ToString("N");

    [Fact]
    public void Collects_the_attempts_of_a_test_in_order_and_reads_a_pass_after_a_failure_as_flaky()
    {
        var root = Root();

        var (first, _, _) = Add(Single("a", AttemptStatus.Failed), root, 1, kept: true);
        var (second, traced, _) = Add(Single("a", AttemptStatus.Passed), root, 2, kept: false);

        var result = Assert.Single(second.Run.Results);
        Assert.Equal(ResultStatus.Failed, Assert.Single(first.Run.Results).Status);
        Assert.Equal([AttemptStatus.Failed, AttemptStatus.Passed], result.Attempts.Select(attempt => attempt.Status));
        Assert.Equal(ResultStatus.Flaky, result.Status);
        Assert.True(traced);
    }

    [Fact]
    public void Starts_a_test_over_at_its_first_attempt_so_a_repeat_does_not_extend_the_one_before()
    {
        var root = Root();
        Add(Single("a", AttemptStatus.Failed), root, 1, kept: true);

        var (report, traced, _) = Add(Single("a", AttemptStatus.Passed), root, 1, kept: false);

        var result = Assert.Single(report.Run.Results);
        Assert.Single(result.Attempts);
        Assert.Equal(ResultStatus.Passed, result.Status);
        Assert.False(traced);
    }

    [Fact]
    public void Keeps_two_tests_and_two_results_directories_apart()
    {
        var root = Root();
        Add(Single("a", AttemptStatus.Failed), root, 1, kept: true);

        Assert.Single(Assert.Single(Add(Single("b", AttemptStatus.Failed), root, 2, kept: true).Report.Run.Results).Attempts);
        Assert.Single(Assert.Single(Add(Single("a", AttemptStatus.Failed), Root(), 2, kept: true).Report.Run.Results).Attempts);
    }

    [Fact]
    public void Redacts_the_page_of_a_test_with_the_redactors_of_all_its_attempts()
    {
        var root = Root();
        Add(Single("a", AttemptStatus.Failed), root, 1, kept: true, text => text.Replace("one", "<one>", StringComparison.Ordinal));

        var (_, _, redact) = Add(Single("a", AttemptStatus.Passed), root, 2, kept: false, text => text.Replace("two", "<two>", StringComparison.Ordinal));

        Assert.Equal("<one> <two>", redact("one two"));
    }

    [Fact]
    public void Drops_a_test_when_an_attempt_is_skipped_after_a_failure()
    {
        var root = Root();
        Add(Single("a", AttemptStatus.Failed), root, 1, kept: true);
        Add(Single("a", AttemptStatus.Skipped), root, 2, kept: false);

        var (report, _, _) = Add(Single("a", AttemptStatus.Failed), root, 2, kept: false);

        Assert.Single(Assert.Single(report.Run.Results).Attempts);
    }

    [Fact]
    public void Drops_a_test_once_an_attempt_passes_so_the_run_holds_only_the_tests_still_failing()
    {
        var root = Root();
        Add(Single("a", AttemptStatus.Failed), root, 1, kept: true);
        Add(Single("a", AttemptStatus.Passed), root, 2, kept: false);

        var (report, _, _) = Add(Single("a", AttemptStatus.Failed), root, 2, kept: false);

        Assert.Single(Assert.Single(report.Run.Results).Attempts);
    }

    [Fact]
    public void Reads_the_final_attempt_as_the_verdict_of_a_test_that_did_not_pass()
    {
        (AttemptStatus Final, ResultStatus Expected)[] cases =
        [
            (AttemptStatus.Failed, ResultStatus.Failed),
            (AttemptStatus.TimedOut, ResultStatus.TimedOut),
            (AttemptStatus.Interrupted, ResultStatus.Interrupted),
            (AttemptStatus.Skipped, ResultStatus.Skipped),
        ];
        foreach (var (final, expected) in cases)
        {
            Assert.Equal(expected, E2E.RunReport.StatusOf([ReportBuilders.Attempt(AttemptStatus.Failed), ReportBuilders.Attempt(final)]));
        }
    }
}
