// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using E2E.Engine;

namespace E2E.Tests.EngineContract;

/// <summary>
/// <c>describe('video artifacts')</c> of upstream <c>engine-contract.test.ts</c>. Upstream runs a project through the runner
/// over a fake engine and reads the report. The port has neither, so each body runs one or more sessions over a fake
/// <see cref="IEngine"/> and reads <c>E2ESession.Videos</c>, what the session keeps, where the report would register it. Every
/// test is partial for that reason, and says what else it leaves out. Not ported: the three tests of <c>describe('video
/// capability and plan notices')</c>, since the port has no run to notice anything.
/// </summary>
public sealed class VideoArtifactsTests : IDisposable
{
    // The output directory holds the results and the run's report.json beside them.
    private readonly string _results = Path.Combine(Directory.CreateTempSubdirectory("e2e-video-artifacts-").FullName, "results");

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_results)!, recursive: true);

    [Fact]
    public async Task Applies_video_best_effort_an_engine_that_cannot_record_runs_without_one_PARTIAL_the_run_says_nothing()
    {
        var fake = new FakeVideoEngine(video: false);
        var kept = await RunAsync(fake, "taps a node", Run(RecordingMode.On), passes: true);
        Assert.Empty(kept);
        Assert.Equal(1, fake.Started);
    }

    [Fact]
    public async Task Records_every_attempt_for_video_the_segment_is_an_artifact_with_its_start_time_PARTIAL_no_report_to_mark_it_unredacted()
    {
        var fake = new FakeVideoEngine();
        var kept = await RunAsync(fake, "taps a node", Run(RecordingMode.On), passes: true);
        var video = Assert.IsType<VideoFile>(Assert.Single(kept));
        Assert.Matches(@"^video/fake\.webm$", video.Path);
        Assert.True(DateTimeOffset.TryParse(video.StartedAt, System.Globalization.CultureInfo.InvariantCulture, out _));
        var file = Path.Combine(_results, AttemptReport.ResultSegmentOf(null, "taps a node"), "attempt-1", video.Path);
        Assert.Equal(8, new FileInfo(file).Length);
        Assert.Equal(["startVideo", "stopVideo"], fake.Operations);
    }

    [Fact]
    public async Task Keeps_only_failed_attempts_with_retain_on_failure_PARTIAL_no_run_to_pass_and_fail_in_one_report()
    {
        var fake = new FakeVideoEngine();
        var retain = new ResolvedRecording(RecordingMode.RetainOnFailure, RecordingSource.Target);
        Assert.Empty(await RunAsync(fake, "taps a node", retain, passes: true));

        // The passing attempt's recording was written, then removed with its verdict.
        Assert.Empty(Directory.GetFiles(Path.Combine(_results, AttemptReport.ResultSegmentOf(null, "taps a node")), "fake.webm", SearchOption.AllDirectories));
        var kept = Assert.IsType<VideoFile>(Assert.Single(await RunAsync(fake, "fails on purpose", retain, passes: false)));
        Assert.True(File.Exists(Path.Combine(_results, AttemptReport.ResultSegmentOf(null, "fails on purpose"), "attempt-1", kept.Path)));
    }

    [Fact]
    public async Task Fails_the_cleanup_on_an_engine_link_that_is_not_http_s_and_still_keeps_its_other_recordings_PARTIAL_no_report_to_read_the_cleanup_from()
    {
        var fake = new FakeVideoEngine(links: ["file:///tmp/r.mp4", "https://recordings.example/r.mp4"]);
        var session = await StartAsync(fake, "taps a node", new ResolvedRecording(RecordingMode.On, RecordingSource.Target), attempt: 1);
        session.Complete();
        var failure = await Assert.ThrowsAsync<EngineException>(() => session.DisposeAsync().AsTask());
        Assert.Equal("ENGINE_FAILURE", failure.Code);
        Assert.Contains("\"file:///tmp/r.mp4\"", failure.Message, StringComparison.Ordinal);
        Assert.Equal(
            ["https://recordings.example/r.mp4", "video/fake.webm"],
            session.Videos.Select(segment => segment is VideoFile file ? file.Path : ((VideoLink)segment).Url));
    }

    [Fact]
    public async Task Records_only_the_first_retry_with_on_first_retry_and_nothing_for_a_test_that_passes_first_time_PARTIAL_the_attempts_are_started_by_hand()
    {
        var fake = new FakeVideoEngine();
        var mode = new ResolvedRecording(RecordingMode.OnFirstRetry, RecordingSource.Target);
        Assert.Empty(await RunAsync(fake, "taps a node", mode, passes: true));
        Assert.Empty(await RunAsync(fake, "fails on purpose", mode, passes: false, attempt: 1));
        Assert.Single(await RunAsync(fake, "fails on purpose", mode, passes: false, attempt: 2));
    }

    [Fact]
    public async Task Lets_a_tests_own_video_win_over_video_and_a_targets_win_over_the_config_PARTIAL_the_modes_are_resolved_before_the_session_and_tested_in_Config()
    {
        var fake = new FakeVideoEngine();
        Assert.Empty(await RunAsync(fake, "keeps quiet", new ResolvedRecording(RecordingMode.Off, RecordingSource.Test), passes: true));
        Assert.Single(await RunAsync(fake, "follows the run", Run(RecordingMode.On), passes: true));
        Assert.Empty(await RunAsync(fake, "by target", new ResolvedRecording(RecordingMode.Off, RecordingSource.Target), passes: true));
    }

    [Fact]
    public async Task Fails_pre_flight_only_when_a_test_that_runs_would_record_on_an_engine_that_cannot_PARTIAL_a_session_is_refused_before_its_engine_starts_where_a_run_is_before_any_attempt()
    {
        var fake = new FakeVideoEngine(video: false);
        var byTest = await Assert.ThrowsAsync<ConfigurationException>(() =>
            StartAsync(fake, "checkout > records", new ResolvedRecording(RecordingMode.On, RecordingSource.Test), attempt: 1));
        Assert.Equal("UNSUPPORTED_ARTIFACT", byTest.Code);
        Assert.Equal("target \"fake\" (engine fake) cannot record video, and test \"checkout > records\" sets video: 'on'", byTest.Message);

        // A mode a target sets is required of its engine, like a test's.
        var byTarget = await Assert.ThrowsAsync<ConfigurationException>(() =>
            StartAsync(fake, "taps a node", new ResolvedRecording(RecordingMode.RetainOnFailure, RecordingSource.Target), attempt: 1));
        Assert.Equal("target \"fake\" (engine fake) cannot record video, and the target sets video: 'retain-on-failure'", byTarget.Message);
        Assert.Equal(0, fake.Started);

        // on-first-retry never records the first attempt, and a test that opts out asks nothing.
        Assert.Empty(await RunAsync(fake, "taps a node", new ResolvedRecording(RecordingMode.OnFirstRetry, RecordingSource.Target), passes: true));
        Assert.Empty(await RunAsync(fake, "taps a node", new ResolvedRecording(RecordingMode.Off, RecordingSource.Test), passes: true));
    }

    private static ResolvedRecording Run(RecordingMode mode) => new(mode, RecordingSource.Run);

    private Task<E2ESession> StartAsync(FakeVideoEngine fake, string title, ResolvedRecording video, int attempt) =>
        E2ESession.StartAsync(new E2ESessionOptions
        {
            Engine = fake,
            TestTitle = title,
            Video = video,
            Attempt = attempt,
            ResultsDirectory = _results,
            ProjectRoot = _results,
        });

    private async Task<IReadOnlyList<VideoSegment>> RunAsync(FakeVideoEngine fake, string title, ResolvedRecording video, bool passes, int attempt = 1)
    {
        var session = await StartAsync(fake, title, video, attempt);
        session.Complete(passes ? null : new TestException("TEST_FAILED", "nope"));
        await session.DisposeAsync();
        return session.Videos;
    }

    /// <summary>An engine whose session records <c>video/fake.webm</c> into the attempt's directory, and can hand back links.</summary>
    private sealed class FakeVideoEngine(bool video = true, IReadOnlyList<string>? links = null) : IEngine
    {
        public int Started { get; private set; }

        public List<string> Operations { get; } = [];

        public string Platform => "fake";

        public string Version => "test";

        public EngineCapabilities Capabilities => video ? EngineCapabilities.Observation | EngineCapabilities.Video : EngineCapabilities.Observation;

        public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
        {
            Started++;
            var session = DispatchProxy.Create<IEngineSession, SessionProxy>();
            var proxy = (SessionProxy)(object)session;
            proxy.ArtifactsDir = options.ArtifactsDir!;
            proxy.Operations = Operations;
            proxy.Links = links ?? [];
            return Task.FromResult(session);
        }
    }

    public class SessionProxy : DispatchProxy
    {
        internal string ArtifactsDir { get; set; } = "";

        internal List<string> Operations { get; set; } = [];

        internal IReadOnlyList<string> Links { get; set; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case nameof(IEngineSession.StartVideoAsync):
                    Operations.Add("startVideo");
                    return Task.CompletedTask;
                case nameof(IEngineSession.StopVideoAsync):
                    Operations.Add("stopVideo");
                    Directory.CreateDirectory(Path.Combine(ArtifactsDir, "video"));
                    File.WriteAllText(Path.Combine(ArtifactsDir, "video", "fake.webm"), "fake-vid");
                    IReadOnlyList<VideoSegment> segments =
                    [
                        .. Links.Select(url => (VideoSegment)new VideoLink { Url = url, MediaType = "video/mp4", StartedAt = "2026-09-28T10:00:00.000Z" }),
                        new VideoFile { Path = "video/fake.webm", StartedAt = "2026-09-28T10:00:00.000Z" },
                    ];
                    return Task.FromResult(segments);
                case nameof(IAsyncDisposable.DisposeAsync):
                    return default(ValueTask);
                case "get_" + nameof(IEngineSession.CanLocate):
                    return false;
                default:
                    return null;
            }
        }
    }
}
