// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using static E2E.Tests.ReportMarkdown.MarkdownKit;

namespace E2E.Tests.ReportMarkdown;

/// <summary>Upstream's <c>describe('an interrupted run')</c> (<c>tests/unit/report-markdown.test.ts</c>).</summary>
public sealed class AnInterruptedRunTests
{
    private static readonly ReportResult Cut = Named(
        "cut short",
        ResultStatus.Interrupted,
        attempts: [Attempt(AttemptStatus.Interrupted, error: new ReportError("INTERRUPTED", "run interrupted in phase body"))]);

    private static string Render(Report1Document document) => MarkdownReport.RenderMarkdownReport(document);

    [Fact]
    public void Counts_an_interrupted_test_apart_from_failures_and_gives_it_no_failure_block()
    {
        var body = Render(Page(ReportRunStatus.Interrupted, [Passing, Cut]));

        Assert.StartsWith("### ⏹️ e2e: 1 interrupted, 1 passed\n", body);
        Assert.DoesNotContain("INTERRUPTED", body);
        Assert.Contains("| ⏹️ | cut short |", body);
    }

    [Fact]
    public void Tells_the_failure_an_interrupted_retry_was_cut_short_after()
    {
        var retried = Named(
            "fails then gets cut",
            ResultStatus.Failed,
            attempts:
            [
                Attempt(AttemptStatus.Failed, error: new ReportError("ASSERTION_FAILED", "no cart")),
                Attempt(AttemptStatus.Interrupted, error: new ReportError("INTERRUPTED", "run interrupted in phase body")),
            ]);

        var body = Render(Page(ReportRunStatus.Interrupted, [retried]));

        Assert.StartsWith("### 🔴 e2e: 1 failed\n", body);
        Assert.Contains("**ASSERTION_FAILED**", body);
        Assert.DoesNotContain("**INTERRUPTED**", body);
    }

    [Fact]
    public void Stays_red_when_the_run_failed_before_it_was_stopped()
    {
        var body = Render(Page(ReportRunStatus.Interrupted, [Failing, Cut]));

        Assert.StartsWith("### 🔴 e2e: 1 failed, 1 interrupted\n", body);
    }
}
