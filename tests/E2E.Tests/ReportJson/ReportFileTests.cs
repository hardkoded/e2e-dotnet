// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using E2E.Engine;
using E2E.Internal;
using E2E.Tests.Traces;

namespace E2E.Tests.ReportJson;

/// <summary>
/// .NET-only: <c>report.json</c> as the sessions of a process write it. Upstream builds the document once, when its runner ends the run;
/// a test framework runs the port's attempts as sessions, so each one adds its attempt and the file is written again.
/// </summary>
public sealed class ReportFileTests : IDisposable
{
    private readonly string _output = Directory.CreateTempSubdirectory("e2e-report-file-").FullName;

    public void Dispose()
    {
        Directory.Delete(_output, recursive: true);
        GC.SuppressFinalize(this);
    }

    private string Results => Path.Combine(_output, "results");

    private JsonNode Read()
    {
        var document = JsonNode.Parse(File.ReadAllText(Path.Combine(_output, "report.json")))!;
        ReportSchema.AssertValidReport(document);
        return document;
    }

    [Fact]
    public async Task Writes_a_valid_document_after_every_attempt_that_tells_the_run_so_far()
    {
        await using (var failing = await StartAsync("shop > pays", 1))
        {
            await failing.App.OpenAsync("/");
            failing.Complete(new TestException("TEST_FAILED", "nope"));
        }

        var afterFailure = Read()["run"]!;
        Assert.Equal("failed", (string)afterFailure["status"]!);
        Assert.Equal(1, (int)afterFailure["exitCode"]!);
        Assert.Equal(1, (int)afterFailure["summary"]!["failed"]!);
        Assert.Equal("TEST_FAILED", (string)afterFailure["results"]![0]!["attempts"]![0]!["error"]!["code"]!);

        await using (var passing = await StartAsync("shop > pays", 2))
        {
            await passing.App.OpenAsync("/");
            passing.Complete();
        }

        await using (var other = await StartAsync("shop > browses", 1))
        {
            await other.App.OpenAsync("/");
            other.Complete();
        }

        var run = Read()["run"]!;
        Assert.Equal("passed", (string)run["status"]!);
        Assert.Equal(2, (int)run["summary"]!["selected"]!);
        Assert.Equal(1, (int)run["summary"]!["flaky"]!);
        Assert.Equal(1, (int)run["summary"]!["passed"]!);
        Assert.Equal(2, run["results"]!.AsArray().Select(result => (string)result!["id"]!).Distinct().Count());
        var flaky = run["results"]!.AsArray().Single(result => (string)result!["status"]! == "flaky")!;
        Assert.Equal(["failed", "passed"], flaky["attempts"]!.AsArray().Select(attempt => (string)attempt!["status"]!));
        Assert.Equal("document", (string)run["targets"]![0]!["platform"]!);
        Assert.Equal("http://127.0.0.1:4100", (string)run["targets"]![0]!["baseOrigin"]!);
    }

    [Fact]
    public async Task Writes_the_document_when_the_mode_keeps_no_trace_and_the_test_passes()
    {
        await using (var session = await StartAsync("quiet > passes", 1, RecordingMode.Off))
        {
            await session.App.OpenAsync("/");
            session.Complete();
        }

        var run = Read()["run"]!;
        Assert.Equal("passed", (string)run["status"]!);
        Assert.Equal(1, (int)run["summary"]!["passed"]!);
        Assert.False(File.Exists(Path.Combine(Results, AttemptReport.ResultSegmentOf(null, "quiet > passes"), "trace.md")));
    }

    [Fact]
    public async Task Tells_a_skipped_test_with_its_reason()
    {
        await using (var session = await StartAsync("later > skips", 1))
        {
            session.Complete(new SkipException("not on this platform"));
        }

        var result = Read()["run"]!["results"]![0]!;
        Assert.Equal("skipped", (string)result["status"]!);
        Assert.Equal("not on this platform", (string)result["skip"]!["reason"]!);
        Assert.Null(result["attempts"]![0]!["error"]);
    }

    [Fact]
    public async Task Redacts_the_secrets_of_a_test_from_every_text_of_its_result()
    {
        var report = new Report1Document(new ReportRun(
            Guid.CreateVersion7().ToString(),
            "1.0.0",
            [new ReportTarget("web", 0, "web", BuildReport.DescribeTarget(null))],
            [ReportBuilders.Result(id: new string('a', 64), attempts: [ReportBuilders.Attempt(AttemptStatus.Failed, ReportBuilders.Error(message: "wrong password hunter2"))]) with { Status = ResultStatus.Failed }]));

        await ReportFile.WriteAsync(report, _output, Results, text => text.Replace("hunter2", "<secret:password>", StringComparison.Ordinal));

        var json = File.ReadAllText(Path.Combine(_output, "report.json"));
        Assert.DoesNotContain("hunter2", json, StringComparison.Ordinal);
        Assert.Contains("wrong password <secret:password>", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("test", 1, "Failed")]
    [InlineData("configuration", 2, "Error")]
    [InlineData("infrastructure", 3, "Error")]
    [InlineData("internal", 4, "Error")]
    public void Exits_with_the_category_of_the_error_of_a_failed_result_when_that_is_worse_than_a_test_failure(string category, int exitCode, string status)
    {
        var failed = ReportBuilders.Result(attempts: [ReportBuilders.Attempt(AttemptStatus.Failed, ReportBuilders.Error() with { Category = category })]) with { Status = ResultStatus.Failed };

        Assert.Equal((Enum.Parse<ReportRunStatus>(status), exitCode), ReportFile.StatusOf([failed]));
    }

    [Fact]
    public void Exits_with_the_worst_exit_code_of_the_results()
    {
        var configuration = ReportBuilders.Result(id: new string('a', 64), attempts: [ReportBuilders.Attempt(AttemptStatus.Failed, ReportBuilders.Error() with { Category = "configuration" })]) with { Status = ResultStatus.Failed };
        var failed = ReportBuilders.Result(id: new string('b', 64), attempts: [ReportBuilders.Attempt(AttemptStatus.Failed)]) with { Status = ResultStatus.Failed };
        var interrupted = ReportBuilders.Result(id: new string('c', 64), attempts: [ReportBuilders.Attempt(AttemptStatus.Interrupted)]) with { Status = ResultStatus.Interrupted };

        Assert.Equal((ReportRunStatus.Error, 2), ReportFile.StatusOf([failed, configuration]));
        Assert.Equal((ReportRunStatus.Interrupted, 130), ReportFile.StatusOf([configuration, interrupted]));
        Assert.Equal((ReportRunStatus.Passed, 0), ReportFile.StatusOf([]));
    }

    [Fact]
    public async Task Asks_for_the_commit_once_per_project_root_however_many_sessions_ask_and_asks_again_after_a_failure()
    {
        var root = Path.Combine(_output, "project");
        var asked = 0;
        var info = new VcsInfo("abc123", "main", false);
        Task<VcsInfo?> Detect(string _)
        {
            Interlocked.Increment(ref asked);
            return Task.FromResult<VcsInfo?>(info);
        }

        var all = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => ReportFile.DetectVcs(root, Detect))));

        Assert.All(all, found => Assert.Same(info, found));
        Assert.Equal(1, asked);

        var failing = Path.Combine(_output, "failing");
        var attempts = 0;
        Task<VcsInfo?> Fail(string _) => ++attempts == 1 ? Task.FromException<VcsInfo?>(new IOException("no git")) : Task.FromResult<VcsInfo?>(info);

        await Assert.ThrowsAsync<IOException>(() => ReportFile.DetectVcs(failing, Fail));
        Assert.Same(info, await ReportFile.DetectVcs(failing, Fail));
        Assert.Equal(2, attempts);
    }

    [Fact]
    public void Reads_the_size_and_digest_of_an_artifact_from_its_file_and_trusts_only_a_log_to_hold_no_secret()
    {
        Directory.CreateDirectory(Path.Combine(Results, "t", "attempt-1"));
        File.WriteAllText(Path.Combine(Results, "t", "attempt-1", "screen.txt"), "abc");
        File.WriteAllBytes(Path.Combine(Results, "t", "attempt-1", "shot.png"), [1, 2]);

        var log = ReportWire.Artifact(new ReportArtifact("screen-0", "log", "t/attempt-1/screen.txt"), Results, null);
        var shot = ReportWire.Artifact(new ReportArtifact("shot-0", "screenshot", "t/attempt-1/shot.png"), Results, "step-3");
        var gone = ReportWire.Artifact(new ReportArtifact("screen-1", "log", "t/attempt-1/missing.txt"), Results, null);

        Assert.Equal(("complete", 3L), ((string)log["redaction"]!, (long)log["size"]!));
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", (string)log["sha256"]!);
        Assert.Equal(("incomplete", 2L, "step"), ((string)shot["redaction"]!, (long)shot["size"]!, (string)shot["producer"]!["kind"]!));
        Assert.Equal("step-3", (string)shot["producer"]!["stepId"]!);
        Assert.Equal("incomplete", (string)gone["redaction"]!);
        Assert.Null(gone["path"]);
    }

    [Fact]
    public void Keeps_the_lists_of_a_result_within_the_schemas_counts_and_unique_where_it_asks()
    {
        var screen = new StepScreen { Nodes = 3, Since = 0, Changes = [.. Enumerable.Range(0, 20).Select(index => "added button " + index)] };
        var step = ReportBuilders.Step(screen: screen);
        var result = ReportBuilders.Result(id: new string('b', 64), attempts: [ReportBuilders.Attempt(steps: [step], appLog: [.. Enumerable.Range(0, 400).Select(index => new AppLogRecord(AppLogSource.Console, AppLogLevel.Info, "line " + index, ReportBuilders.ReportAt))])]) with { Tags = ["smoke", "smoke"] };
        var report = new Report1Document(new ReportRun(Guid.CreateVersion7().ToString(), "1.0.0", [new ReportTarget("web", 0, "web", BuildReport.DescribeTarget(null))], [result]));

        var wire = ReportWire.Document(BuildReport.Build(new BuildReportOptions { RunId = report.Run.Id, StartedAt = ReportBuilders.ReportAt, Status = ReportRunStatus.Passed, ExitCode = 0, Results = [result], ReportTargets = report.Run.Targets }), "results");

        ReportSchema.AssertValidReport(wire);
        Assert.Equal(12, wire["run"]!["results"]![0]!["attempts"]![0]!["steps"]![0]!["screen"]!["changes"]!.AsArray().Count);
    }

    [Fact]
    public void Cuts_text_at_the_schemas_bound_without_splitting_a_surrogate_pair()
    {
        // "😀" is two UTF-16 code units; a cut between them would leave a lone surrogate.
        var cut = ReportWire.Clip("a😀b", 2);

        Assert.Equal("a", cut);
        Assert.Equal("a😀", ReportWire.Clip("a😀b", 3));
        Assert.Equal("abc", ReportWire.Clip("abc", 3));

        // A lone surrogate cannot be written as JSON text; it reads as the replacement character, with a valid pair beside it kept.
        Assert.Equal("a\uFFFDb😀\uFFFD", ReportWire.Clip("a\uD800b😀\uDC00", 20));
    }

    private Task<E2ESession> StartAsync(string title, int attempt, RecordingMode mode = RecordingMode.RetainOnFailure) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new ReportEngine(),
            TestTitle = title,
            BaseUrl = "http://127.0.0.1:4100",
            ActionTimeout = TimeSpan.FromMilliseconds(300),
            ProjectRoot = ProjectRoot(),
            ResultsDirectory = Results,
            CacheEnabled = false,
            Trace = new ResolvedRecording(mode, RecordingSource.Target),
            Attempt = attempt,
        });

    private static string ProjectRoot([CallerFilePath] string testFile = "") => Path.GetDirectoryName(Path.GetDirectoryName(testFile))!;

    private sealed class ReportEngine : IEngine
    {
        public string Platform => "document";

        public string Version => "test";

        public EngineCapabilities Capabilities => EngineCapabilities.Observation | EngineCapabilities.Actions | EngineCapabilities.Location;

        public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) => Task.FromResult<IEngineSession>(new Session());
    }

    private sealed class Session : IEngineSession
    {
        public string Route => "/";

        public Task OpenAsync(string url, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken) => Task.FromResult(new Observation
        {
            Route = "/",
            Roots = [new SemanticNode { Ref = "root", Role = "document", Children = [new SemanticNode { Ref = "submit", Role = "button", Name = "Submit" }] }],
        });

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task PressAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
