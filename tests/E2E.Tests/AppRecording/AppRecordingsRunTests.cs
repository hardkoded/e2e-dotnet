// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Reports;

namespace E2E.Tests.AppRecording;

/// <summary>
/// <c>describe('app recordings')</c> of upstream <c>tests/integration/app-recording.test.ts</c>: <c>app.startRecording</c> /
/// <c>app.stopRecording</c> on the real web engine. A span becomes a video under <c>recordings/</c> linked from its step, a page
/// replaced mid-span gives one video per page, a span left running is kept at the attempt's end, and a span is refused while the
/// test's video records, which still records as before. Partial: the port has no runner, so each upstream test is one session
/// whose report is read, and the bodies are run by hand.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class AppRecordingsRunTests : IDisposable
{
    // The output directory holds the results and the run's report.json beside them.
    private readonly string _results = Path.Combine(Directory.CreateTempSubdirectory("e2e-app-recording-run-").FullName, "results");

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_results)!, recursive: true);

    /// <summary>Runs one test body and returns its one attempt, which must have passed with a complete cleanup.</summary>
    private async Task<(Reports.ReportAttempt Attempt, IReadOnlyList<(string Path, string? Step)> Videos)> RunAsync(string appUrl, string title, ResolvedRecording video, Func<App, Task> body)
    {
        var session = await E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = new WebEngine(),
            BaseUrl = appUrl,
            TestTitle = title,
            Video = video,
            ResultsDirectory = _results,
            ProjectRoot = _results,
            CacheEnabled = false,
        });
        await body(session.App);
        session.Complete();
        await session.DisposeAsync();
        var result = Assert.Single(session.Report()!.Run.Results);
        var attempt = Assert.Single(result.Attempts);
        Assert.Equal(Reports.AttemptStatus.Passed, attempt.Status);
        Assert.Equal(Reports.AttemptCleanup.Complete, attempt.Cleanup);
        var videos = attempt.Artifacts
            .Where(artifact => artifact.Kind == ArtifactKind.Video)
            .Select(artifact => (artifact.Path!, attempt.Steps.FirstOrDefault(step => step.Artifacts.Contains(artifact.Id))?.Api))
            .ToList();
        return (attempt, videos);
    }

    private static readonly ResolvedRecording Off = new(RecordingMode.Off, RecordingSource.Default);

    [Fact]
    public async Task Records_a_span_into_recordings_and_links_it_from_the_stop_step()
    {
        using var app = await FixtureApp.StartAsync();
        var (_, videos) = await RunAsync(app.Url, "records one span", Off, async fixture =>
        {
            await fixture.OpenAsync();
            await fixture.StartRecordingAsync(new RecordingOptions { Name = "span" });
            await Task.Delay(300);
            var recorded = await fixture.StopRecordingAsync();
            Assert.Single(recorded);
            Assert.EndsWith("recordings" + Path.DirectorySeparatorChar + "001-span.webm", Assert.IsType<RecordedVideoFile>(recorded[0]).Path, StringComparison.Ordinal);
        });

        var video = Assert.Single(videos);
        Assert.EndsWith("recordings/001-span.webm", video.Path, StringComparison.Ordinal);
        Assert.Equal("app.stopRecording", video.Step);
        var file = Path.Combine(_results, video.Path);
        Assert.True(File.Exists(file) && new FileInfo(file).Length > 0);
    }

    [Fact]
    public async Task Returns_one_video_per_page_when_the_page_is_replaced_mid_span()
    {
        using var app = await FixtureApp.StartAsync();
        var (_, videos) = await RunAsync(app.Url, "one video per page", Off, async fixture =>
        {
            await fixture.OpenAsync();
            await fixture.StartRecordingAsync(new RecordingOptions { Name = "restart" });
            await fixture.RestartAsync();
            var recorded = await fixture.StopRecordingAsync();
            Assert.Equal(["001-restart.webm", "001-restart-part2.webm"], recorded.Select(video => Path.GetFileName(Assert.IsType<RecordedVideoFile>(video).Path)));
        });

        Assert.Equal(["001-restart.webm", "001-restart-part2.webm"], videos.Select(video => Path.GetFileName(video.Path)));
    }

    [Fact]
    public async Task Keeps_a_span_left_running_when_the_attempt_ends()
    {
        using var app = await FixtureApp.StartAsync();
        var (_, videos) = await RunAsync(app.Url, "left running", Off, async fixture =>
        {
            await fixture.OpenAsync();
            await fixture.StartRecordingAsync();
        });

        var video = Assert.Single(videos);
        Assert.EndsWith("recordings/001-recording.webm", video.Path, StringComparison.Ordinal);
        Assert.Null(video.Step);
    }

    [Fact]
    public async Task Refuses_a_span_while_the_tests_video_records_and_keeps_that_video()
    {
        using var app = await FixtureApp.StartAsync();
        var (_, videos) = await RunAsync(app.Url, "refused while the video records", new ResolvedRecording(RecordingMode.On, RecordingSource.Test), async fixture =>
        {
            await fixture.OpenAsync();
            var refusal = await Assert.ThrowsAsync<ConfigurationException>(() => fixture.StartRecordingAsync());
            Assert.Equal("UNSUPPORTED_CAPABILITY", refusal.Code);
        });

        var video = Assert.Single(videos);
        Assert.EndsWith("video/video.webm", video.Path, StringComparison.Ordinal);
        Assert.Null(video.Step);
    }
}
