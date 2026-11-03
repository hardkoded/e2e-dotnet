// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Internal;
using E2E.Tests.Traces;
using static E2E.Tests.LastRun.LastRunDocuments;

namespace E2E.Tests.LastRun;

/// <summary>
/// .NET-only (upstream's runner reads the report, selects, and writes the rerun's report in one process it owns): the host's <c>--last-failed</c> flow, from
/// <c>E2E_LAST_FAILED</c> through the selection to <c>report.json</c> and <c>FinishedRun.LastRun</c>.
/// </summary>
public sealed class LastFailedRunTests : IDisposable
{
    private const string Namespace = "App.Tests";
    private const string Class = "RerunTests";

    private readonly List<string> _dirs = [];

    public void Dispose()
    {
        foreach (var dir in _dirs)
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private E2EConfig Project()
    {
        var dir = Directory.CreateTempSubdirectory("e2e-last-failed-").FullName;
        _dirs.Add(dir);
        return E2EConfig.Parse("{}", Directory.ResolveLinkTarget(dir, true)?.FullName ?? dir, _ => null);
    }

    private static string ReportPath(E2EConfig config) => Path.Combine(config.Output, "report.json");

    private static DiscoveredTest Found(string method) => new($"{Namespace}.{Class}.{method}", Namespace, Class, method, []);

    private static string TestIdOf(string method) => AttemptReport.TestId($"{Namespace}.{Class}", method);

    private static JsonObject Row(E2EConfig config, string method, string status, params (string Name, JsonNode? Value)[] extra) =>
        Result([("id", method), ("testId", TestIdOf(method)), ("targetId", config.Target.Name), ("status", status), .. extra]);

    private static void WritePrevious(E2EConfig config, params JsonObject[] results)
    {
        Directory.CreateDirectory(config.Output);
        File.WriteAllText(ReportPath(config), ReportText(results));
    }

    [Fact]
    public void Asks_for_a_rerun_with_the_option_or_with_E2E_LAST_FAILED_set_to_1_or_true()
    {
        Assert.True(HostTestFilters.ResolveLastFailed(true, _ => null));
        Assert.True(HostTestFilters.ResolveLastFailed(false, name => name == "E2E_LAST_FAILED" ? "1" : null));
        Assert.True(HostTestFilters.ResolveLastFailed(false, _ => " TRUE "));
        Assert.False(HostTestFilters.ResolveLastFailed(false, _ => "0"));
        Assert.False(HostTestFilters.ResolveLastFailed(false, _ => ""));
        Assert.False(HostTestFilters.ResolveLastFailed(false, _ => null));
    }

    /// <summary>Partial: the port has no runner to run twice; this asks the host's selection, which is what the runner's second run does with the report.</summary>
    [Fact]
    public async Task Partial_Runs_only_what_the_previous_run_did_not_pass_under_last_failed_and_needs_a_report_to_read()
    {
        var config = Project();
        DiscoveredTest[] tests = [Found("passes"), Found("fails"), Found("also_passes")];

        var missing = await Assert.ThrowsAsync<ConfigurationException>(() => HostTestFilters.ExcludeAsync(config, [], null, tests, _ => { }, CancellationToken.None, lastFailed: true));
        Assert.Equal("NO_LAST_RUN", missing.Code);
        Assert.Contains(ReportPath(config), missing.Message, StringComparison.Ordinal);

        WritePrevious(config, Row(config, "passes", "passed"), Row(config, "fails", "failed"), Row(config, "also_passes", "passed"));
        var selection = await HostTestFilters.ExcludeAsync(config, [], null, tests, _ => { }, CancellationToken.None, lastFailed: true);

        Assert.Equal(
            new Dictionary<string, string> { [tests[0].Key] = "did not fail in the last run", [tests[2].Key] = "did not fail in the last run" },
            selection.Excluded);
        Assert.All(selection.Results, result =>
        {
            Assert.False(result.Selected);
            Assert.Equal(ResultStatus.Skipped, result.Status);
            Assert.Equal("did not fail in the last run", result.Skip!.Reason);
        });
        Assert.Equal([TestIdOf("passes"), TestIdOf("also_passes")], selection.Results.Select(result => result.TestId));
    }

    /// <summary>Partial: the port has no runner; this asks the host's selection with a filter in the config, which the second run must not call.</summary>
    [Fact]
    public async Task Partial_Reruns_what_last_failed_owes_without_asking_the_filters_so_a_filter_cannot_turn_a_real_failure_green()
    {
        var config = Project();
        DiscoveredTest[] tests = [Found("alpha"), Found("beta"), Found("gamma")];
        var calls = 0;
        var drifting = new Filter(() => calls++);
        WritePrevious(
            config,
            Row(config, "alpha", "passed"),
            Row(config, "beta", "failed"),
            Row(config, "gamma", "skipped", ("selected", false), ("skip", Result(("cause", "filtered"), ("reason", "Jev: 5% likely affected"), ("filter", "jev")))));
        var notices = new List<string>();

        var selection = await HostTestFilters.ExcludeAsync(config, [drifting], "main", tests, notices.Add, CancellationToken.None, lastFailed: true);

        Assert.Equal(0, calls);
        Assert.Equal([tests[0].Key, tests[2].Key], selection.Excluded.Keys);
        Assert.Equal(new ReportChanges(Skipped: "--last-failed"), selection.Changes);
        Assert.Empty(selection.Filters);
        Assert.Equal(["--last-failed: the tests the last run owes run without filters"], notices);
    }

    [Fact]
    public async Task Carries_what_the_run_before_owed_until_a_session_runs_it_and_hands_that_report_to_the_reporters()
    {
        // The run before: one test failed, one passed. Its report is the one the rerun reads.
        var first = Project();
        var target = new ReportTarget(first.Target.Name, 0, first.Target.Platform, BuildReport.DescribeTarget(null));
        var failed = ReportBuilders.Result(id: new string('a', 64), titlePath: ["fails"], attempts: [ReportBuilders.Attempt(AttemptStatus.Failed)]) with { Status = ResultStatus.Failed, TestId = TestIdOf("fails"), TargetId = target.Id };
        var passed = ReportBuilders.Result(id: new string('b', 64), titlePath: ["passes"], attempts: [ReportBuilders.Attempt()]) with { TestId = TestIdOf("passes"), TargetId = target.Id };
        var runBefore = Guid.CreateVersion7().ToString();
        foreach (var result in new[] { failed, passed })
        {
            await ReportFile.WriteAsync(new Report1Document(new ReportRun(runBefore, "1.0.0", [target], [result])), first.ProjectRoot, Path.Combine(first.Output, "results"), text => text);
        }

        var config = Project();
        Directory.CreateDirectory(config.Output);
        File.Copy(ReportPath(first), ReportPath(config));
        DiscoveredTest[] tests = [Found("fails"), Found("passes")];

        // The rerun starts: the test it owes is carried until a session runs it, and the report stays schema-valid.
        var selection = await HostTestFilters.ExcludeAsync(config, [], null, tests, _ => { }, CancellationToken.None, lastFailed: true);
        await HostTestFilters.WriteReportAsync(config, selection, CancellationToken.None);
        var started = JsonNode.Parse(File.ReadAllText(ReportPath(config)))!;
        ReportSchema.AssertValidReport(started);
        var carried = started["run"]!["carried"]!;
        Assert.Equal([TestIdOf("fails")], carried["results"]!.AsArray().Select(result => (string)result!["testId"]!));
        Assert.Equal("failed", (string)carried["results"]![0]!["status"]!);
        Assert.Empty(carried["serialGroups"]!.AsArray());
        Assert.Equal(0, (int)started["run"]!["summary"]!["selected"]!);

        // A session runs it: its own result says so now, and nothing is carried.
        var rerun = failed with { Status = ResultStatus.Passed, Attempts = [ReportBuilders.Attempt()] };
        await ReportFile.WriteAsync(new Report1Document(new ReportRun(AttemptReport.RunId, "1.0.0", [target], [rerun])), config.ProjectRoot, Path.Combine(config.Output, "results"), text => text);
        var finishedDocument = JsonNode.Parse(File.ReadAllText(ReportPath(config)))!;
        ReportSchema.AssertValidReport(finishedDocument);
        Assert.Null(finishedDocument["run"]!["carried"]);

        var finished = (await ReportFile.FinishedRunsAsync()).Single(run => run.ReportPath == ReportPath(config));
        Assert.Equal(runBefore, finished.LastRun!.Run.Id);
        Assert.Equal(ReportRunStatus.Failed, finished.LastRun.Run.Status);
    }

    private sealed class Filter(Action called) : ITestFilter
    {
        public string Name => "jev";

        public Task<TestFilterResult> FilterAsync(IReadOnlyList<TestFilterCandidate> tests, TestFilterContext context)
        {
            called();
            return Task.FromResult(new TestFilterResult { Exclude = tests.ToDictionary(test => test.Id, _ => "Jev: 5% likely affected") });
        }
    }
}
