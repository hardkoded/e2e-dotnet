// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.Explore.ExploreHarness;
using Explorer = E2E.Explore;

namespace E2E.Tests.Explore;

/// <summary>
/// .NET-only: what <c>--trace</c>, <c>--video</c>, and <c>--ai-trace</c> of <c>e2e explore</c> do to the files an exploration leaves.
/// Upstream reads them from the run's report and artifacts; the port has neither, so these read the files. Upstream's only
/// <c>it</c> for the flags, the retry-mode notice, is in <see cref="E2eExploreTests"/>.
/// </summary>
public sealed class ExploreRecordingTests
{
    private static ExploreModel Finishing() => new(_ => ExploreModel.Finish("Looked."), _ => ExploreModel.Done("passed", "unused"));

    private static IEnumerable<string> Pages(string root) => Files(root).Where(file => Path.GetFileName(file) == "trace.md");

    [Fact]
    public async Task Writes_no_trace_page_of_a_passing_exploration_by_default()
    {
        var root = TempRoot();
        await Explorer.ExploreAsync(Options(root, Finishing().Model, []));

        Assert.Empty(Pages(root));
    }

    [Fact]
    public async Task Writes_a_trace_page_of_a_passing_exploration_with_trace_on()
    {
        var root = TempRoot();
        await Explorer.ExploreAsync(Options(root, Finishing().Model, [], trace: RecordingMode.On));

        Assert.Single(Pages(root));
    }

    [Fact]
    public async Task Leaves_no_trace_page_for_a_verdict_its_findings_already_tell()
    {
        var plans = 0;
        var model = new ExploreModel(
            _ => ++plans == 1 ? ExploreModel.Step("Counter", "Look at the counter") : ExploreModel.Finish("One defect."),
            call => call.Turn == 1 ? ExploreModel.ReportFinding(ExploreModel.CounterFinding()) : ExploreModel.Done("passed", "Looked"));
        var root = TempRoot();
        var outcome = await Explorer.ExploreAsync(Options(root, model.Model, [], trace: RecordingMode.On));

        Assert.Equal(1, outcome.ExitCode);
        Assert.Empty(Pages(root));
    }

    [Fact]
    public async Task Says_nothing_of_a_video_the_engine_cannot_record_unless_the_flag_asked_for_it()
    {
        var quiet = new List<string>();
        await Explorer.ExploreAsync(Options(TempRoot(), Finishing().Model, quiet));
        Assert.Empty(quiet);

        var asked = new List<string>();
        await Explorer.ExploreAsync(Options(TempRoot(), Finishing().Model, asked, video: RecordingMode.On));
        Assert.Equal(["video records only on targets whose engine can record it; target \"web\" (engine " + ExploreHarness.Engine().Platform + ") records no video"], asked);
    }

    [Fact]
    public void Says_to_pass_trace_on_when_a_retry_mode_would_keep_nothing()
    {
        var notices = Explorer.RecordingNotices(RecordingMode.OnAllRetries, new ResolvedRecording(RecordingMode.Off, RecordingSource.Default), recordsVideo: true, "web", "web");

        Assert.Equal(["trace: 'on-all-retries' records retries only, and explore runs its goal once, so no traces will be recorded; pass --trace on"], notices);
    }

    [Fact]
    public async Task Records_every_model_call_next_to_the_results_directory_with_ai_trace()
    {
        var root = TempRoot();
        await Explorer.ExploreAsync(Options(root, Finishing().Model, [], aiTrace: true));

        Assert.Contains(Files(root), file => Path.GetFileName(file).StartsWith("ai-trace-", StringComparison.Ordinal) && file.EndsWith(".json", StringComparison.Ordinal));
    }
}
