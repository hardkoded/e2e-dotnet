// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Report;
using E2E.Reports;
using static E2E.Cli.Tests.ReportList.ListReporterKit;

namespace E2E.Cli.Tests.ReportList;

/// <summary>
/// Upstream: packages/e2e/tests/unit/report-list.test.ts, the <c>it</c> that sit directly in describe('ListReporter'). The control characters
/// of <c>never lets control characters through error codes, phases, categories, or paths</c> reach a category and a phase upstream; the port's
/// are enums, which cannot hold them, so that test is <c>Partial_</c>: it checks the codes, messages, and paths.
/// </summary>
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

    [Fact]
    public void Partial_Never_lets_control_characters_through_error_codes_or_paths()
    {
        var raw = new Raw();
        var reporter = new global::E2E.Cli.Report.ListReporter(raw, new ListReporterOptions { Live = false, Colors = false });
        const string esc = "\u001b[2J";
        reporter.Handle(RunStarted());
        reporter.Handle(Plan(($"tests/{esc}a.e2e.ts", 1)));
        reporter.Handle(Finished(Result(
            ResultStatus.Failed,
            file: $"tests/{esc}a.e2e.ts",
            attempts: [Attempt(error: new ReportError($"CODE{esc}", $"msg{esc}"), status: E2E.Internal.AttemptStatus.Failed)])));
        reporter.Handle(new RunErrorEvent(new Reports.ReportError { Category = ErrorCategory.Infrastructure, Code = $"X{esc}", Message = "m", Phase = ErrorPhase.Launch }));
        reporter.Handle(RunFinished(ReportRunStatus.Failed, 1, $"/project/{esc}/report.json", $"{esc}t.json"));
        Assert.True(raw.Lines.Count > 10);
        Assert.DoesNotContain(raw.Lines, line => line.Any(char.IsControl));
    }

    [Fact]
    public void Sanitizes_control_characters_in_titles_and_bounds_long_fields()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(Plan(("tests/a.e2e.ts", 1)));
        reporter.Handle(Finished(Result(ResultStatus.Passed, file: "tests/a.e2e.ts", title: ["bad\u0007title\u001b[31m"])));
        Assert.DoesNotContain("\u0007", string.Join('\n', output.Lines), StringComparison.Ordinal);
        var longOutput = new Capture();
        var longReporter = PlainReporter(longOutput);
        longReporter.Handle(Plan(("tests/a.e2e.ts", 1)));
        longReporter.Handle(Finished(Result(ResultStatus.Passed, file: "tests/a.e2e.ts", title: [new string('x', 20_000)])));
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(longOutput.Lines[1]) < 10_000);
    }

    private sealed class Raw : IListReporterOutput
    {
        public List<string> Lines { get; } = [];

        public void Write(string line) => Lines.Add(line);
    }
}
