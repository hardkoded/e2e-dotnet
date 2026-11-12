// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using static E2E.Cli.Tests.ReportList.ListReporterKit;

namespace E2E.Cli.Tests.ReportList;

/// <summary>
/// Upstream: packages/e2e/tests/unit/report-list.test.ts, describe('recording model mismatch'). Each <c>it.each</c> upstream runs with and without a
/// live window, and so does each test here. Not ported: <c>keeps serial retry notices in their file block among multiple files</c>, since the port
/// has no serial groups. The cases of <c>keeps distinct recordings with the same label</c> that upstream names <c>entry=%s</c> are kept as the
/// pair of <c>hasEntry</c> and <c>live</c>. The port's step records the
/// recording model as one label (<see cref="CacheInfo.RecordedBy"/>), where upstream's has a provider and a model.
/// </summary>
public sealed class RecordingModelMismatchTests
{
    private static CacheInfo Replayed(string? recordedBy, string? entry = null) =>
        new() { Mode = "self-finalized", ReplayedActions = 1, TotalActions = 1, RecordedBy = recordedBy, Entry = entry };

    private static List<string> Notices(Capture output) => [.. output.Lines.Where(line => line.Contains("recorded with", StringComparison.Ordinal))];

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Keeps_distinct_recordings_with_the_same_label(bool live, bool hasEntry)
    {
        var output = new Capture { Raw = _ => { } };
        var reporter = PlainReporter(output, live: live);
        reporter.Handle(RunStarted(model: "example/B"));
        reporter.Handle(Plan(("tests/case.e2e.ts", 1)));
        var steps = new[] { 0, 1 }.Select(index => AgentStepRecord("pay", Replayed("example/A", hasEntry ? $"recording-{index}" : null), index: index)).ToList();
        reporter.Handle(Finished(Result(ResultStatus.Flaky, attempts: [Attempt(steps, status: AttemptStatus.Failed), Attempt(steps, id: "attempt-2")])));
        Assert.Equal(2, Notices(output).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stays_quiet_for_a_per_call_pinned_agent(bool live)
    {
        var output = new Capture { Raw = _ => { } };
        var reporter = PlainReporter(output, live: live);
        reporter.Handle(RunStarted(model: "example/B"));
        reporter.Handle(Plan(("tests/case.e2e.ts", 1)));
        var cached = AgentStepRecord("pay", Replayed("example/A"), agent: "buyer");
        reporter.Handle(Finished(Result(ResultStatus.Passed, agent: "default", attempts: [Attempt([cached])])));
        Assert.Empty(Notices(output));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Deduplicates_retries_inside_the_file_block(bool live)
    {
        var output = new Capture { Raw = _ => { } };
        var reporter = PlainReporter(output, live: live);
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
    [InlineData(false, "example/B", "example/A", "default", true)]
    [InlineData(false, "example/A", "example/A", "default", false)]
    [InlineData(false, "example/B", null, "default", false)]
    [InlineData(false, null, "example/A", "default", false)]
    [InlineData(false, "example/B", "example/A", "pinned", false)]
    [InlineData(false, "example/B", "example/A\n\u001b[31m", "default", true)]
    [InlineData(true, "example/B", "example/A", "default", true)]
    [InlineData(true, "example/A", "example/A", "default", false)]
    [InlineData(true, "example/B", null, "default", false)]
    [InlineData(true, null, "example/A", "default", false)]
    [InlineData(true, "example/B", "example/A", "pinned", false)]
    [InlineData(true, "example/B", "example/A\n\u001b[31m", "default", true)]
    public void Prints_only_known_mismatches_and_preserves_the_header(bool live, string? configured, string? recorded, string agent, bool expected)
    {
        var output = new Capture { Raw = _ => { } };
        var reporter = PlainReporter(output, live: live);
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
