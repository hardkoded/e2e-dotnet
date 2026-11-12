// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Cli.Tests.ReportList.ListReporterKit;

namespace E2E.Cli.Tests.ReportList;

/// <summary>Upstream: packages/e2e/tests/unit/report-list.test.ts, describe('reporter rows').</summary>
public sealed class ReporterRowsTests
{
    [Fact]
    public void Prints_each_row_in_the_summary_layout_then_a_blank_line()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Rows([("JUnit", ".e2e/junit.xml"), ("Results", "https://example.test/runs/1")]);
        Assert.Equal(3, output.Lines.Count);
        Assert.Equal("      JUnit  .e2e/junit.xml", output.Lines[0]);
        Assert.Matches(@"^ +Results {2}https://example\.test/runs/1\z", output.Lines[1]);
        Assert.Equal(string.Empty, output.Lines[2]);
    }

    [Fact]
    public void Prints_nothing_for_no_rows()
    {
        var output = new Capture();
        PlainReporter(output).Rows([]);
        Assert.Empty(output.Lines);
    }
}
