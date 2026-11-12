// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using static E2E.Cli.Tests.ReportList.ListReporterKit;

namespace E2E.Cli.Tests.ReportList;

/// <summary>Upstream: packages/e2e/tests/unit/report-list.test.ts, describe('screenshots row').</summary>
public sealed class ScreenshotsRowTests
{
    private static StepRecord Step(string api, params string[] artifacts)
    {
        var step = new StepRecord { Id = api, Index = 0, Kind = StepKind.App, Api = api, Label = string.Empty, StartedAt = DateTimeOffset.UnixEpoch, DurationMs = 1 };
        step.Artifacts.AddRange(artifacts);
        return step;
    }

    private static ReportArtifact Shot(string id, string file) => new(id, "screenshot", file);

    // The port has no serial groups, so the serial half of upstream's `it` is not ported.
    [Fact]
    public void Partial_Names_each_app_screenshot_file_and_leaves_out_assertion_and_failure_screenshots()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(RunStarted());
        reporter.Handle(Plan(("tests/case.e2e.ts", 2)));
        var taken = Attempt([Step("app.screenshot", "a:0"), Step("agent.assert", "a:1")]) with
        {
            Artifacts = [Shot("a:0", "case/attempt-1/screenshots/001-plan.png"), Shot("a:1", "case/attempt-1/screenshots/002-assert.png"), Shot("a:2", "case/attempt-1/failure.png")],
        };
        reporter.Handle(Finished(Result(ResultStatus.Passed, declarationIndex: 1, attempts: [taken])));
        reporter.Handle(RunFinished());
        Assert.Equal(["Screenshots  .e2e/artifacts/case/attempt-1/screenshots/001-plan.png"], output.Lines.Where(line => line.Contains(".png", StringComparison.Ordinal)));
    }

    [Fact]
    public void Counts_the_files_past_the_first_ten()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(RunStarted());
        reporter.Handle(Plan(("tests/case.e2e.ts", 1)));
        var ids = Enumerable.Range(0, 12).Select(index => $"a:{index}").ToArray();
        var attempt = Attempt([Step("app.screenshot", ids)]) with { Artifacts = [.. ids.Select((id, index) => Shot(id, $"s/{index}.png"))] };
        reporter.Handle(Finished(Result(ResultStatus.Passed, attempts: [attempt])));
        reporter.Handle(RunFinished());
        var row = output.Lines.FindIndex(line => line.StartsWith("Screenshots", StringComparison.Ordinal));
        Assert.Equal(
            [
                "Screenshots  .e2e/artifacts/s/0.png",
                .. Enumerable.Range(0, 9).Select(index => $"             .e2e/artifacts/s/{index + 1}.png"),
                "             and 2 more (the report lists every artifact)",
            ],
            output.Lines.Skip(row).Take(11));
    }
}
