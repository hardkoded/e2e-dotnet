// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Cli.Tests.ReportList.ListReporterKit;

namespace E2E.Cli.Tests.ReportList;

/// <summary>Upstream: packages/e2e/tests/unit/report-list.test.ts, the <c>it</c> that sits directly in describe('ListReporter').</summary>
public sealed class ListReporterTests
{
    [Fact]
    public void Titles_the_later_runs_of_a_repeated_test_and_keeps_each_run_its_own_line()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(RunStarted());
        reporter.Handle(Plan(("tests/checkout.e2e.ts", 3)));
        output.Lines.Clear();
        foreach (var repeat in new[] { 0, 1, 2 })
        {
            reporter.Handle(Finished(Result(repeat == 1 ? ResultStatus.Failed : ResultStatus.Passed, id: "t1", repeat: repeat, file: "tests/checkout.e2e.ts", title: ["pays"], attempts: [Attempt() with { DurationMs = 80 }])));
        }

        var block = string.Join('\n', output.Lines);
        Assert.Contains("pays 80ms", block);
        Assert.Contains("pays (repeat #1)", block);
        Assert.Contains("pays (repeat #2) 80ms", block);
    }
}
