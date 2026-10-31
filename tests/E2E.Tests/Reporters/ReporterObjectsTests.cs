// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Reports;
using E2E.Tests.ScreenTree;
using E2E.Tests.Traces;

namespace E2E.Tests.Reporters;

/// <summary>
/// Upstream's <c>describe('reporter objects')</c> (<c>tests/integration/reporters.test.ts</c>). Each test is partial: the port has no runner, so the
/// reporters run through <see cref="E2E.Reporters.RunReportersAsync"/> on the run <see cref="ReportFile"/> wrote, with no event stream and no
/// <c>leftWorkBehind</c> flag.
/// </summary>
[Collection(nameof(ConsoleCollection))]
public sealed class ReporterObjectsTests : IDisposable
{
    private readonly string _output = Directory.CreateTempSubdirectory("e2e-reporters-").FullName;

    public void Dispose()
    {
        Directory.Delete(_output, recursive: true);
        GC.SuppressFinalize(this);
    }

    private string Results => Path.Combine(_output, "results");

    private async Task<FinishedRun> FinishedAsync()
    {
        var report = new Report1Document(new ReportRun(
            Guid.CreateVersion7().ToString(),
            "1.0.0",
            [new ReportTarget("web", 0, "web", BuildReport.DescribeTarget(null))],
            [ReportBuilders.Result(id: new string('a', 64), attempts: [ReportBuilders.Attempt(E2E.Internal.AttemptStatus.Passed)])]));
        await ReportFile.WriteAsync(report, _output, Results, text => text);
        return (await ReportFile.FinishedRunsAsync()).Single(run => run.ArtifactsRoot == Results);
    }

    private static async Task<(IReadOnlyList<ReporterSummaryRow> Rows, string Stderr)> RunAsync(IReadOnlyList<Reporter> reporters, FinishedRun finished, TimeSpan timeout, CancellationToken force = default)
    {
        var stderr = new StringWriter();
        var original = Console.Error;
        Console.SetError(stderr);
        try
        {
            return (await E2E.Reporters.RunReportersAsync(reporters, finished, timeout, force), stderr.ToString());
        }
        finally
        {
            Console.SetError(original);
        }
    }

    [Fact]
    public async Task Partial_Receives_the_finished_run_whose_artifact_paths_resolve()
    {
        var finished = await FinishedAsync();
        var recording = new RecordingReporter();

        var (rows, _) = await RunAsync([recording], finished, E2E.Reporters.ReporterTimeout);

        var run = Assert.IsType<FinishedRun>(recording.Finished);
        Assert.Equal(ReportRunStatus.Passed, run.Status);
        Assert.Equal(0, run.ExitCode);
        Assert.Equal(["https://example.test/runs/1"], rows.Select(row => row.Text));
        Assert.Equal("Results", Assert.Single(rows).Label);

        // The document a reporter uploads is the file on disk.
        var onDisk = JsonNode.Parse(File.ReadAllText(run.ReportPath))!;
        Assert.Equal((string)onDisk["run"]!["id"]!, run.Report.Run.Id);
        Assert.Equal(Path.Combine(_output, "report.json"), run.ReportPath);
        Assert.Equal(_output, run.ProjectRoot);
        Assert.Equal(Results, run.ArtifactsRoot);
        foreach (var artifact in run.Report.Run.Results.SelectMany(result => result.Attempts).SelectMany(attempt => attempt.Artifacts))
        {
            Assert.True(File.Exists(Path.Combine(run.ArtifactsRoot, artifact.Path!)));
        }
    }

    [Fact]
    public async Task Partial_Never_calls_a_reporter_when_the_run_was_forced_to_stop_before_reporting()
    {
        var finished = await FinishedAsync();
        var recording = new RecordingReporter { Label = "uploading" };
        using var force = new CancellationTokenSource();
        await force.CancelAsync();

        var (_, stderr) = await RunAsync([recording], finished, E2E.Reporters.ReporterTimeout, force.Token);

        Assert.Null(recording.Finished);
        Assert.Contains("e2e: reporter \"uploading\" abandoned: the run was forced to stop", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Partial_Warns_on_stderr_for_a_throwing_hanging_or_malformed_reporter_and_leaves_the_outcome_alone()
    {
        var finished = await FinishedAsync();
        var hanging = new HangingReporter();
        var malformed = new RecordingReporter
        {
            Label = "malformed",
            Rows = [new ReporterSummaryRow("", "https://example.test"), new ReporterSummaryRow("ok", "https://example.test/ok")],
        };

        var (rows, stderr) = await RunAsync([new ThrowingReporter(), hanging, malformed], finished, TimeSpan.FromMilliseconds(200));

        // The budget running out cancels the reporter's own token, so it can stop its work.
        Assert.True(hanging.Token.IsCancellationRequested);
        Assert.Equal(["https://example.test/ok"], rows.Select(row => row.Text));
        Assert.Contains("e2e: reporter \"throwing\" failed: boom", stderr, StringComparison.Ordinal);
        Assert.Contains("e2e: reporter \"hanging\" did not finish within 200ms", stderr, StringComparison.Ordinal);
        Assert.Contains("e2e: reporter \"malformed\" returned 1 row(s) without a label and text; dropped", stderr, StringComparison.Ordinal);
        Assert.Equal(ReportRunStatus.Passed, finished.Status);
        Assert.Equal(0, finished.ExitCode);
    }

    [Fact]
    public async Task Partial_Abandons_a_reporter_when_the_run_is_forced_to_stop()
    {
        var finished = await FinishedAsync();
        using var force = new CancellationTokenSource();
        var uploading = new HangingReporter { Label = "uploading", OnStart = () => force.CancelAfter(50) };

        var (_, stderr) = await RunAsync([uploading], finished, TimeSpan.FromSeconds(30), force.Token);

        Assert.Contains("e2e: reporter \"uploading\" abandoned: the run was forced to stop", stderr, StringComparison.Ordinal);
        Assert.Equal(ReportRunStatus.Passed, finished.Status);
    }

    private sealed class RecordingReporter : Reporter
    {
        public override string Name => Label;

        public string Label { get; init; } = "recording";

        public FinishedRun? Finished { get; private set; }

        public IReadOnlyList<ReporterSummaryRow> Rows { get; init; } = [new("Results", "https://example.test/runs/1")];

        public override Task<IReadOnlyList<ReporterSummaryRow>?> OnRunFinishedAsync(FinishedRun run, CancellationToken cancellationToken)
        {
            Finished = run;
            return Task.FromResult<IReadOnlyList<ReporterSummaryRow>?>(Rows);
        }
    }

    private sealed class ThrowingReporter : Reporter
    {
        public override string Name => "throwing";

        public override Task<IReadOnlyList<ReporterSummaryRow>?> OnRunFinishedAsync(FinishedRun run, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");
    }

    private sealed class HangingReporter : Reporter
    {
        public override string Name => Label;

        public string Label { get; init; } = "hanging";

        public CancellationToken Token { get; private set; }

        public Action? OnStart { get; init; }

        public override Task<IReadOnlyList<ReporterSummaryRow>?> OnRunFinishedAsync(FinishedRun run, CancellationToken cancellationToken)
        {
            Token = cancellationToken;
            OnStart?.Invoke();
            return new TaskCompletionSource<IReadOnlyList<ReporterSummaryRow>?>().Task;
        }
    }
}
