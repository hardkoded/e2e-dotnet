// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using static E2E.Cli.Tests.ReportList.ListReporterKit;

namespace E2E.Cli.Tests.ReportList;

/// <summary>
/// Upstream: packages/e2e/tests/unit/report-list.test.ts, describe('recording model mismatch'). Each <c>it.each</c> upstream runs with and without a
/// live window; the port has no live window, so each test here is the <c>live=false</c> case, hence the <c>Partial_</c> prefix. Not ported:
/// <c>keeps serial retry notices in their file block among multiple files</c>, since the port has no serial groups. The port's step records the
/// recording model as one label (<see cref="CacheInfo.RecordedBy"/>), where upstream's has a provider and a model.
/// </summary>
public sealed class RecordingModelMismatchTests
{
    private static CacheInfo Replayed(string? recordedBy, string? entry = null) =>
        new() { Mode = "self-finalized", ReplayedActions = 1, TotalActions = 1, RecordedBy = recordedBy, Entry = entry };

    private static List<string> Notices(Capture output) => [.. output.Lines.Where(line => line.Contains("recorded with", StringComparison.Ordinal))];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Partial_Keeps_distinct_recordings_with_the_same_label(bool hasEntry)
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(RunStarted(model: "example/B"));
        reporter.Handle(Plan(("tests/case.e2e.ts", 1)));
        var steps = new[] { 0, 1 }.Select(index => AgentStepRecord("pay", Replayed("example/A", hasEntry ? $"recording-{index}" : null), index: index)).ToList();
        reporter.Handle(Finished(Result(ResultStatus.Flaky, attempts: [Attempt(steps, status: AttemptStatus.Failed), Attempt(steps, id: "attempt-2")])));
        Assert.Equal(2, Notices(output).Count);
    }

    [Fact]
    public void Partial_Stays_quiet_for_a_per_call_pinned_agent()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(RunStarted(model: "example/B"));
        reporter.Handle(Plan(("tests/case.e2e.ts", 1)));
        var cached = AgentStepRecord("pay", Replayed("example/A"), agent: "buyer");
        reporter.Handle(Finished(Result(ResultStatus.Passed, agent: "default", attempts: [Attempt([cached])])));
        Assert.Empty(Notices(output));
    }

    [Fact]
    public void Partial_Deduplicates_retries_inside_the_file_block()
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(RunStarted(model: "example/B"));
        reporter.Handle(Plan(("tests/case.e2e.ts", 1)));
        var cached = AgentStepRecord("pay", Replayed("example/A"));
        reporter.Handle(Finished(Result(ResultStatus.Flaky, attempts: [Attempt([cached], status: AttemptStatus.Failed), Attempt([cached], id: "attempt-2")])));
        var notices = Notices(output);
        Assert.Single(notices);
        Assert.True(output.Lines.IndexOf(notices[0]) > output.Lines.FindIndex(line => line.Contains("tests/case.e2e.ts", StringComparison.Ordinal)));
        Assert.StartsWith("     ", notices[0], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("example/B", "example/A", "default", true)]
    [InlineData("example/A", "example/A", "default", false)]
    [InlineData("example/B", null, "default", false)]
    [InlineData(null, "example/A", "default", false)]
    [InlineData("example/B", "example/A", "pinned", false)]
    [InlineData("example/B", "example/A\n\u001b[31m", "default", true)]
    public void Partial_Prints_only_known_mismatches_and_preserves_the_header(string? configured, string? recorded, string agent, bool expected)
    {
        var output = new Capture();
        var reporter = PlainReporter(output);
        reporter.Handle(RunStarted(model: configured));
        reporter.Handle(Plan(("tests/case.e2e.ts", 1)));
        var cached = AgentStepRecord("pay", Replayed(recorded));
        reporter.Handle(Finished(Result(ResultStatus.Passed, agent: agent, attempts: [Attempt([cached])])));
        var replays = Notices(output);
        Assert.Equal(expected ? 1 : 0, replays.Count);
        if (expected)
        {
            Assert.Contains("replayed · recorded with example/A", replays[0], StringComparison.Ordinal);
        }

        Assert.DoesNotContain("configured model", string.Join('\n', output.Lines), StringComparison.Ordinal);
    }
}
