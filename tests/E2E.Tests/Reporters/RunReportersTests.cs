// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Reports;
using E2E.Tests.ScreenTree;

namespace E2E.Tests.Reporters;

/// <summary>.NET-only: a reporter that honors its token (upstream's <c>withAbort</c> rejects a promise, which a C# task cannot be made to ignore).</summary>
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
            await E2E.Reporters.RunReportersAsync([new HonoringReporter()], finished, TimeSpan.FromMilliseconds(100), CancellationToken.None);
        }
        finally
        {
            Console.SetError(original);
        }

        Assert.Contains("e2e: reporter \"honoring\" did not finish within 100ms", stderr.ToString(), StringComparison.Ordinal);
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
