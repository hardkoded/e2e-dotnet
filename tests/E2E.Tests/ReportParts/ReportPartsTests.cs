// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;
using E2E.Tests.Traces;
using static E2E.Tests.ReportStatus.ReportStatusBuilders;

namespace E2E.Tests.ReportParts;

/// <summary>
/// The run-level blocks of <c>report/build.ts</c>. Upstream reaches them only through whole runs (the runner's integration tests), which
/// the port has none of, so these tests call them directly; they are .NET-only.
/// </summary>
public sealed class ReportPartsTests
{
    private sealed class FakeEngine(EngineCapabilities capabilities) : IEngine
    {
        public string Platform => "web";

        public string Version => "1.2.3";

        public EngineCapabilities Capabilities => capabilities;

        public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    [Fact]
    public void Describes_a_target_without_an_engine_as_having_no_capabilities()
    {
        var described = BuildReport.DescribeTarget(null);

        Assert.Equal(new ReportEngine("none", "unversioned", 1), described.Engine);
        Assert.Empty(described.Capabilities);
        Assert.Empty(described.ArtifactCapabilities);
        Assert.False(described.StateCapability);
    }

    [Fact]
    public void Describes_an_engine_by_its_sorted_capabilities_and_what_it_can_capture()
    {
        var described = BuildReport.DescribeTarget(new FakeEngine(EngineCapabilities.Observation | EngineCapabilities.Actions | EngineCapabilities.Screenshot | EngineCapabilities.Video | EngineCapabilities.TapModifiers));

        Assert.Equal(new ReportEngine("web", "1.2.3", 1), described.Engine);
        Assert.Equal(["actions", "observation", "screenshot", "tapModifiers", "video"], described.Capabilities);
        Assert.Equal(["screenshot", "video"], described.ArtifactCapabilities);
    }

    [Fact]
    public void Counts_only_the_selected_results_by_status_and_every_result_that_ran()
    {
        ReportResult Result(ResultStatus status, bool selected = true, int attempts = 1) =>
            ReportBuilders.Result(status: status, attempts: [.. Enumerable.Repeat(ReportBuilders.Attempt(), attempts)]) with { Selected = selected };

        var summary = BuildReport.ComputeSummary(
        [
            Result(ResultStatus.Passed),
            Result(ResultStatus.Flaky, attempts: 2),
            Result(ResultStatus.TimedOut),
            Result(ResultStatus.Interrupted),
            Result(ResultStatus.Skipped, attempts: 0),
            Result(ResultStatus.Failed, selected: false),
        ]);

        Assert.Equal(new ReportSummary(6, 5, 5, 1, 1, 1, 1, 1), summary);
    }

    [Fact]
    public void Adds_up_what_the_steps_used()
    {
        var first = ReportBuilders.Step(0);
        first.Metrics = new StepMetrics { ModelCalls = 2, ActionSteps = 3, ContextBytes = 100, LedgerBytes = 40 };
        first.Model = new StepModelInfo { InputTokens = 10, OutputTokens = 5, CacheReadTokens = 4, EstimatedCostUsd = 0.5 };
        var second = ReportBuilders.Step(1);
        second.Metrics = new StepMetrics { ModelCalls = 1, ActionSteps = 7, ContextBytes = 60, LedgerBytes = 90 };
        second.Model = new StepModelInfo { InputTokens = 1, OutputTokens = 1, EstimatedCostUsd = 0.25 };
        var attempt = ReportBuilders.Attempt(steps: [first, second]) with { Artifacts = [new ReportArtifact("a", "video", Size: 10), new ReportArtifact("b", "download", Size: 5)] };

        var usage = BuildReport.ComputeUsage([ReportBuilders.Result(attempts: [attempt])], 1);

        Assert.Equal(
            new ReportUsage { DiscoveredResults = 1, MaxAgentContextBytes = 100, MaxLedgerBytes = 90, ArtifactBytes = 15, Downloads = 1, ModelTokens = 17, ModelCachedTokens = 4, MaxModelCallsInStep = 2, MaxActionStepsInStep = 7, EstimatedCostUsd = 0.75 },
            usage);
    }

    [Fact]
    public void Reports_the_origin_of_an_http_app_url_without_credentials()
    {
        TargetConfig Target(string url) => new() { Name = "web", Platform = "web", App = new AppConfig { Url = url } };
        var provenance = BuildReport.DescribeTarget(null);

        Assert.Equal("https://staging.example.com", BuildReport.SerializeTarget(Target("https://user:secret@staging.example.com/app"), 0, provenance).BaseOrigin);
        Assert.Equal("http://localhost:3000", BuildReport.SerializeTarget(Target("http://localhost:3000/x"), 0, provenance).BaseOrigin);
        Assert.Equal("http://[::1]:3000", BuildReport.SerializeTarget(Target("http://[::1]:3000/x"), 0, provenance).BaseOrigin);
        Assert.Null(BuildReport.SerializeTarget(Target("file:///tmp/x.html"), 0, provenance).BaseOrigin);
    }

    [Fact]
    public void Names_the_test_file_for_a_result_with_no_recorded_declaration()
    {
        var result = ReportBuilders.Result(file: "tests/auth.e2e.ts") with { Source = new SourceLocation("unknown", 0, 0) };

        var document = BuildReport.Build(new BuildReportOptions { RunId = "run-1", StartedAt = DateTimeOffset.UnixEpoch, Status = ReportRunStatus.Passed, ExitCode = 0, Results = [result] });

        Assert.Equal(new SourceLocation("tests/auth.e2e.ts", 1, 1), document.Run.Results[0].Source);
    }

    [Fact]
    public async Task Gives_no_answer_for_a_git_command_outside_a_repository_or_without_a_directory()
    {
        var outside = Directory.CreateTempSubdirectory("e2e-git-").FullName;
        try
        {
            Assert.Null(await Git.RunAsync(outside, ["rev-parse", "HEAD"]));
            Assert.Null(await Git.RunAsync(Path.Combine(outside, "missing"), ["rev-parse", "HEAD"]));
        }
        finally
        {
            Directory.Delete(outside);
        }
    }

    [Fact]
    public async Task Gives_no_answer_when_git_does_not_answer_within_its_budget()
    {
        Assert.Null(await Git.RunAsync(Path.GetTempPath(), ["--version"], budget: TimeSpan.Zero));
    }

    [Fact]
    public async Task Kills_git_and_gives_no_answer_when_it_outlasts_its_budget()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var started = DateTimeOffset.UtcNow;

        // An alias that sleeps stands for a git that never answers.
        Assert.Null(await Git.RunAsync(Path.GetTempPath(), ["-c", "alias.slow=!sleep 20", "slow"], budget: TimeSpan.FromMilliseconds(300)));
        Assert.True(DateTimeOffset.UtcNow - started < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Surfaces_a_cancelled_token_instead_of_no_answer()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Git.RunAsync(Path.GetTempPath(), ["--version"], cancelled.Token));
    }
}
