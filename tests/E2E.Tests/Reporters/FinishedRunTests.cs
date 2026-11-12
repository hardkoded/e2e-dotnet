// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using E2E.Reports;
using E2E.Tests.ScreenTree;

namespace E2E.Tests.Reporters;

/// <summary>.NET-only (upstream's runner builds <c>FinishedRun</c> from the run it owns): the trace map and the AI trace path a reporter is given.</summary>
[Collection(nameof(ConsoleCollection))]
public sealed class FinishedRunTests : IDisposable
{
    private readonly ReporterRunKit _kit = new();

    public void Dispose()
    {
        _kit.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Maps_each_test_that_kept_a_trace_to_its_page_from_the_project_root()
    {
        await _kit.PassAsync("keeps a trace");
        await using (var failing = await _kit.StartAsync("fails and keeps a trace", trace: RecordingMode.RetainOnFailure))
        {
            failing.Complete(new TestException("TEST_FAILED", "nope"));
        }

        await using (var untraced = await _kit.StartAsync("keeps none", trace: RecordingMode.Off))
        {
            untraced.Complete();
        }

        var finished = await _kit.FinishedAsync();

        Assert.Equal(2, finished.Traces.Count);
        foreach (var (resultId, page) in finished.Traces)
        {
            Assert.Contains(finished.Report.Run.Results, result => result.Id == resultId);
            Assert.False(Path.IsPathRooted(page));
            Assert.Equal("trace.md", Path.GetFileName(page));
            Assert.True(File.Exists(Path.Combine(finished.ProjectRoot, page)), page);
        }

        Assert.DoesNotContain(AttemptReport.ResultId(null, "keeps none"), finished.Traces.Keys);
    }

    [Fact]
    public async Task Drops_the_page_of_a_test_whose_later_run_keeps_none()
    {
        await _kit.PassAsync("keeps then drops");
        var kept = (await _kit.FinishedAsync()).Traces;
        Assert.Contains(AttemptReport.ResultId("E2E.Tests.Reporters.ReporterRun", "keeps then drops"), kept.Keys);

        await using (var untraced = await _kit.StartAsync("keeps then drops", trace: RecordingMode.Off))
        {
            untraced.Complete();
        }

        Assert.Empty((await _kit.FinishedAsync()).Traces);
    }

    [Fact]
    public async Task Names_the_AI_trace_file_the_process_wrote_and_none_when_it_wrote_nothing()
    {
        await _kit.PassAsync();
        Assert.Null((await _kit.FinishedAsync()).AiTracePath);

        var path = Internal.AiTrace.PathFor(_kit.ProjectRoot);
        Assert.Null(Internal.AiTrace.WrittenPath(_kit.ProjectRoot));
        var recorder = new AiTraceRecorder();
        recorder.OnStart(new AiTraceStartEvent("call", "ai.generateText"));
        recorder.OnStepStart(new AiTraceStepStartEvent("call", 0, "test", "model", null, []));
        recorder.OnEnd(new AiTraceEndEvent("call"));
        Internal.AiTrace.Collect(recorder, path);

        var finished = await _kit.FinishedAsync();

        Assert.Equal(path, finished.AiTracePath);
        Assert.True(File.Exists(finished.AiTracePath));
    }
}
