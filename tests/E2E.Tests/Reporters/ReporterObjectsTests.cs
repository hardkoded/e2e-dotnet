// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Internal;
using E2E.Reports;
using E2E.Tests.ScreenTree;

namespace E2E.Tests.Reporters;

/// <summary>
/// Upstream's <c>describe('reporter objects')</c> (<c>tests/integration/reporters.test.ts</c>). The port has no runner: a run is what the run fixtures drive,
/// so each test starts a run, runs a passing test as a session over a fake engine, and ends the run (see <see cref="ReporterRunKit"/>). Not ported: the
/// <c>scalar</c> reporter of the third <c>it</c>, whose <c>onRunFinished</c> answers with something other than rows (untyped JavaScript); a C# reporter
/// returns a list of <see cref="ReporterSummaryRow"/> or nothing, so the case cannot be written.
/// </summary>
[Collection(nameof(ConsoleCollection))]
public sealed class ReporterObjectsTests : IDisposable
{
    private readonly ReporterRunKit _kit = new();

    public void Dispose()
    {
        _kit.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Receives_the_event_stream_and_the_finished_run_whose_artifact_paths_resolve_and_leaves_no_work_behind()
    {
        var recording = new RecordingReporter();

        var (finished, _) = await _kit.RunAsync([recording], Runner.ReporterTimeout);

        Assert.False(AbandonedWork.LeftWorkBehind);
        Assert.IsType<RunStartedEvent>(recording.Events[0].Fact);
        Assert.IsType<RunFinishedEvent>(recording.Events[^1].Fact);
        Assert.Contains(recording.Events, runEvent => runEvent.Fact is TestFinishedEvent);
        // The process counts its events from the first, so a reporter sees a run of consecutive numbers from wherever it began listening.
        Assert.Equal(Enumerable.Range(0, recording.Events.Count).Select(offset => recording.Events[0].Seq + offset), recording.Events.Select(runEvent => runEvent.Seq));

        var run = recording.Finished.Single(each => each.ArtifactsRoot == _kit.Results);
        Assert.Equal(ReportRunStatus.Passed, run.Status);
        Assert.Equal(0, run.ExitCode);
        Assert.Equal(finished.Report.Run.Id, run.Report.Run.Id);

        // The document a reporter uploads is the file on disk.
        var onDisk = JsonNode.Parse(File.ReadAllText(run.ReportPath))!;
        Assert.Equal((string)onDisk["run"]!["id"]!, run.Report.Run.Id);
        Assert.Equal(Path.Combine(_kit.ProjectRoot, "report.json"), run.ReportPath);
        Assert.Equal(_kit.ProjectRoot, run.ProjectRoot);
        Assert.Equal(_kit.Results, run.ArtifactsRoot);
        var artifacts = run.Report.Run.Results.SelectMany(result => result.Attempts).SelectMany(attempt => attempt.Artifacts).ToList();
        Assert.NotEmpty(artifacts);
        foreach (var artifact in artifacts)
        {
            Assert.True(File.Exists(Path.Combine(run.ArtifactsRoot, artifact.Path!)));
        }
    }

    [Fact]
    public async Task Never_calls_a_reporter_when_the_run_was_forced_to_stop_before_reporting_so_it_leaves_no_work_behind()
    {
        var recording = new RecordingReporter { Label = "uploading" };
        using var force = new CancellationTokenSource();
        await force.CancelAsync();

        var (_, stderr) = await _kit.RunAsync([recording], Runner.ReporterTimeout, force.Token);

        Assert.Empty(recording.Finished);
        Assert.False(AbandonedWork.LeftWorkBehind);
        Assert.Contains("e2e: reporter \"uploading\" abandoned: the run was forced to stop", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Warns_on_stderr_for_a_throwing_hanging_or_malformed_reporter_leaves_the_outcome_alone_and_records_the_hanging_one_as_work_left_behind()
    {
        var hanging = new HangingReporter();
        var malformed = new RecordingReporter
        {
            Label = "malformed",
            Rows = [new ReporterSummaryRow("", "https://example.test"), new ReporterSummaryRow("ok", "https://example.test/ok")],
        };
        var loud = new LoudReporter();
        var quiet = new RecordingReporter { Label = "quiet" };

        // The host's own sink: upstream's `runOptions.onEvent`.
        using var host = E2E.RunEvents.Subscribe(new RunEventSubscriber("onEvent", _ => throw new InvalidOperationException("host bang")));
        var (finished, stderr) = await _kit.RunAsync([new ThrowingReporter(), hanging, malformed, loud, quiet], TimeSpan.FromMilliseconds(200));

        Assert.Equal(ReportRunStatus.Passed, finished.Status);
        Assert.Equal(0, finished.ExitCode);

        // The budget running out cancels the reporter's own token, so it can stop its work.
        Assert.True(hanging.Token.IsCancellationRequested);
        Assert.True(AbandonedWork.LeftWorkBehind);
        Assert.Contains("e2e: reporter \"throwing\" failed: boom", stderr, StringComparison.Ordinal);
        Assert.Contains("e2e: reporter \"hanging\" did not finish within 200ms", stderr, StringComparison.Ordinal);
        Assert.Contains("e2e: reporter \"malformed\" returned 1 row(s) without a label and text; dropped", stderr, StringComparison.Ordinal);
        const string loudLine = "e2e: reporter \"loud\" threw on run-started: bang; ignoring it for the rest of the run";
        Assert.Equal(2, stderr.Split(loudLine).Length);
        Assert.Contains("e2e: reporter \"onEvent\" threw on run-started: host bang; ignoring it for the rest of the run", stderr, StringComparison.Ordinal);

        // The quarantine is per sink: the reporter beside the loud one still sees the whole run.
        var seen = quiet.Events.Select(runEvent => runEvent.Fact).ToList();
        Assert.IsType<RunStartedEvent>(seen[0]);
        Assert.Contains(seen, fact => fact is TestFinishedEvent);
        Assert.IsType<RunFinishedEvent>(seen[^1]);
    }

    [Fact]
    public async Task Abandons_a_reporter_when_the_run_is_forced_to_stop()
    {
        using var force = new CancellationTokenSource();

        // The second Ctrl-C lands while the upload is in flight.
        var uploading = new HangingReporter { Label = "uploading", OnStart = () => force.CancelAfter(50) };

        var (finished, stderr) = await _kit.RunAsync([uploading], TimeSpan.FromSeconds(30), force.Token);

        Assert.Equal(ReportRunStatus.Passed, finished.Status);
        Assert.Equal(0, finished.ExitCode);
        Assert.Contains("e2e: reporter \"uploading\" abandoned: the run was forced to stop", stderr, StringComparison.Ordinal);
    }

    private sealed class RecordingReporter : Reporter
    {
        public override string Name => Label;

        public string Label { get; init; } = "recording";

        public List<RunEvent> Events { get; } = [];

        public List<FinishedRun> Finished { get; } = [];

        public IReadOnlyList<ReporterSummaryRow> Rows { get; init; } = [new("Results", "https://example.test/runs/1")];

        public override void OnEvent(RunEvent runEvent) => Events.Add(runEvent);

        public override Task<IReadOnlyList<ReporterSummaryRow>?> OnRunFinishedAsync(FinishedRun run, CancellationToken cancellationToken)
        {
            Finished.Add(run);
            return Task.FromResult<IReadOnlyList<ReporterSummaryRow>?>(Rows);
        }
    }

    private sealed class LoudReporter : Reporter
    {
        public override string Name => "loud";

        public override void OnEvent(RunEvent runEvent) => throw new InvalidOperationException("bang");
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
