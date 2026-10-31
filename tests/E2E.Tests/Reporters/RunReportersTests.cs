// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Reports;
using E2E.Tests.ScreenTree;

namespace E2E.Tests.Reporters;

/// <summary>.NET-only: a reporter that honors its token or blocks (upstream's <c>withAbort</c> rejects a promise, which a C# task cannot be made to ignore).</summary>
[Collection(nameof(ConsoleCollection))]
public sealed class RunReportersTests
{
    [Fact]
    public async Task Reports_a_reporter_that_stops_at_its_token_as_given_up_on_not_as_failed()
    {
        var stderr = new StringWriter();
        var original = Console.Error;
        Console.SetError(stderr);
        try
        {
            var finished = new FinishedRun(new Report(), ReportRunStatus.Passed, 0, "/p", "/p/report.json", "/p/results");
            await Runner.RunReportersAsync([new HonoringReporter()], finished, TimeSpan.FromMilliseconds(100), CancellationToken.None);
        }
        finally
        {
            Console.SetError(original);
        }

        Assert.Contains("e2e: reporter \"honoring\" did not finish within 100ms", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stops_waiting_for_a_reporter_that_blocks_before_it_returns_a_task()
    {
        var stderr = new StringWriter();
        var original = Console.Error;
        Console.SetError(stderr);
        var started = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var finished = new FinishedRun(new Report(), ReportRunStatus.Passed, 0, "/p", "/p/report.json", "/p/results");
            await Runner.RunReportersAsync([new BlockingReporter()], finished, TimeSpan.FromMilliseconds(100), CancellationToken.None);
        }
        finally
        {
            Console.SetError(original);
        }

        Assert.True(started.Elapsed < TimeSpan.FromSeconds(1.5), started.Elapsed.ToString());
        Assert.Contains("e2e: reporter \"blocking\" did not finish within 100ms", stderr.ToString(), StringComparison.Ordinal);
    }

    private sealed class BlockingReporter : Reporter
    {
        public override string Name => "blocking";

        public override Task<IReadOnlyList<ReporterSummaryRow>?> OnRunFinishedAsync(FinishedRun run, CancellationToken cancellationToken)
        {
            Thread.Sleep(2000);
            return Task.FromResult<IReadOnlyList<ReporterSummaryRow>?>(null);
        }
    }

    private sealed class HonoringReporter : Reporter
    {
        public override string Name => "honoring";

        public override async Task<IReadOnlyList<ReporterSummaryRow>?> OnRunFinishedAsync(FinishedRun run, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return null;
        }
    }
}
