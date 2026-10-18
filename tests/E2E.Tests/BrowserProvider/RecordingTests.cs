// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.BrowserProvider;

/// <summary>
/// <c>describe('recording')</c> of upstream <c>browser-provider.test.ts</c>, on a real Chromium the provider leases. Upstream
/// drives a scripted page; the port records the real page, so "records the screencast when the provider does not record"
/// checks a real <c>video.webm</c>. Not ported: "rejects a record that is not a function" (the interface has the method) and
/// "refuses, at the start and named, a recording without a start time and a stop" (a <see cref="ProviderRecording"/> always has
/// both; a provider that returns none does not record, and the engine records the screencast).
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class RecordingTests : IAsyncLifetime
{
    private static readonly CancellationToken None = CancellationToken.None;

    private readonly List<string> _made = [];
    private readonly List<WebEngine> _workers = [];
    private RemoteChrome _remote = null!;

    public async Task InitializeAsync() => _remote = await RemoteChrome.LaunchAsync();

    public async Task DisposeAsync()
    {
        foreach (var worker in _workers)
        {
            await worker.FinishAsync(Runner.FinishInfo(), None);
        }

        await _remote.DisposeAsync();
        foreach (var dir in _made)
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Records_the_attempt_through_the_provider_instead_of_the_screencast_as_one_segment_in_the_attempt_directory()
    {
        var cloud = new RecordingCloud();
        var (session, artifactsDir) = await RecordingWorker(cloud);
        await session.StartVideoAsync(None);
        Assert.Equal([("lease-0", "a1", "run-1", "web")], cloud.Recorded);
        var segments = await session.StopVideoAsync(None);
        Assert.Equal([new VideoFile { Path = "video/replay.mp4", StartedAt = "2026-09-28T10:00:00.000Z" }], segments);
        Assert.Equal("mp4", await File.ReadAllTextAsync(Path.Combine(artifactsDir, "video", "replay.mp4"), None));
        Assert.False(File.Exists(Path.Combine(artifactsDir, "video", "video.webm")));
        await session.DisposeAsync();
        Assert.Equal(["lease-0"], cloud.Stopped);
    }

    [Fact]
    public async Task Turns_a_recording_the_provider_keeps_into_a_link_segment_with_nothing_written_locally()
    {
        var cloud = new RecordingCloud
        {
            Stop = _ => Task.FromResult(new ProviderRecordingResult { Url = "https://recordings.example/r1.mp4", MediaType = "video/mp4" }),
        };
        var (session, artifactsDir) = await RecordingWorker(cloud);
        await session.StartVideoAsync(None);
        Assert.Equal(
            [new VideoLink { Url = "https://recordings.example/r1.mp4", MediaType = "video/mp4", StartedAt = "2026-09-28T10:00:00.000Z" }],
            await session.StopVideoAsync(None));
        Assert.False(File.Exists(Path.Combine(artifactsDir, "video", "video.webm")));
        await session.DisposeAsync();
    }

    [Fact]
    public async Task Records_the_screencast_when_the_provider_does_not_record()
    {
        var (session, artifactsDir) = await RecordingWorker(new ToyCloud());
        await session.StartVideoAsync(None);
        var segments = await session.StopVideoAsync(None);
        Assert.Equal(["video/video.webm"], segments.Select(segment => segment is VideoFile file ? file.Path : ((VideoLink)segment).Url));
        Assert.True(File.Exists(Path.Combine(artifactsDir, "video", "video.webm")));
        await session.DisposeAsync();
    }

    [Fact]
    public async Task Stops_a_recording_the_attempt_never_collected_when_the_attempt_ends()
    {
        var cloud = new RecordingCloud();
        var (session, _) = await RecordingWorker(cloud);
        await session.StartVideoAsync(None);
        await session.DisposeAsync();
        Assert.Equal(["lease-0"], cloud.Stopped);
    }

    [Fact]
    public async Task Stops_a_recording_that_started_after_the_attempt_ended_and_keeps_nothing_of_it()
    {
        var resume = new TaskCompletionSource();
        var started = new TaskCompletionSource();
        var cloud = new RecordingCloud
        {
            BeforeRecord = async () =>
            {
                started.SetResult();
                await resume.Task;
            },
        };
        var (session, _) = await RecordingWorker(cloud);
        var starting = session.StartVideoAsync(None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(30), None);
        await session.DisposeAsync();
        resume.SetResult();
        Assert.Equal("CANCELLED", (await Assert.ThrowsAsync<EngineException>(() => starting)).Code);
        Assert.Equal(["lease-0"], cloud.Stopped);
    }

    [Fact]
    public async Task Reports_a_recording_that_failed_to_start_once_the_attempt_was_cancelled_as_CANCELLED_not_a_provider_failure()
    {
        var cancelling = new ToyCloud { OnRecord = (lease, context, token) => throw new InvalidOperationException("aborted by the signal") };
        var (_, env) = await Runner.PreparedAsync(cancelling, 1);
        var leases = new LeasedBrowsers(cancelling);
        leases.Init(Runner.InitInfo(0, env));
        using var controller = new CancellationTokenSource();
        await controller.CancelAsync();
        Assert.Equal("CANCELLED", (await Assert.ThrowsAsync<EngineException>(() => leases.Recorder("a1")(controller.Token))).Code);
        Assert.Equal("ENGINE_FAILURE", (await Assert.ThrowsAsync<EngineException>(() => leases.Recorder("a1")(None))).Code);
    }

    [Fact]
    public async Task Shares_a_stop_that_timed_out_and_is_still_in_flight_so_ending_the_attempt_never_stops_a_recording_twice()
    {
        var stops = 0;
        var finish = new TaskCompletionSource();
        var stopping = new TaskCompletionSource();
        var cloud = new RecordingCloud
        {
            Stop = async dir =>
            {
                Interlocked.Increment(ref stops);
                stopping.TrySetResult();
                await finish.Task;
                await File.WriteAllTextAsync(Path.Combine(dir, "replay.mp4"), "mp4", None);
                return new ProviderRecordingResult { File = "replay.mp4" };
            },
        };
        var (session, _) = await RecordingWorker(cloud);
        await session.StartVideoAsync(None);

        // The runner's cleanup budget runs out while the provider is still stopping; the close then waits for that same stop.
        using var budget = new CancellationTokenSource();
        var collecting = session.StopVideoAsync(budget.Token);
        await stopping.Task.WaitAsync(TimeSpan.FromSeconds(30), None);
        await budget.CancelAsync();
        Assert.Equal("CANCELLED", (await Assert.ThrowsAsync<EngineException>(() => collecting)).Code);
        var ending = session.DisposeAsync().AsTask();
        finish.SetResult();
        await ending;
        Assert.Equal(1, stops);
    }

    [Fact]
    public async Task Keeps_a_recording_whose_stop_failed_so_ending_the_attempt_stops_it_once_more()
    {
        var failures = 1;
        var cloud = new RecordingCloud { BeforeStop = () => failures-- > 0 ? throw new InvalidOperationException("download interrupted") : Task.CompletedTask };
        var (session, _) = await RecordingWorker(cloud);
        await session.StartVideoAsync(None);
        var failed = await Assert.ThrowsAsync<EngineException>(() => session.StopVideoAsync(None));
        Assert.Contains("download interrupted", failed.Message, StringComparison.Ordinal);
        await session.DisposeAsync();
        Assert.Equal(["lease-0"], cloud.Stopped);
    }

    [Fact]
    public async Task Names_the_provider_when_a_recording_cannot_start_and_refuses_a_stop_that_names_no_file_it_wrote()
    {
        var (failing, _) = await RecordingWorker(new RecordingCloud { FailStart = true });
        var refused = await Assert.ThrowsAsync<EngineException>(() => failing.StartVideoAsync(None));
        Assert.Equal("ENGINE_FAILURE", refused.Code);
        Assert.Contains("browser provider \"toy-cloud\" could not start recording browser lease-0: recordings unavailable", refused.Message, StringComparison.Ordinal);
        await failing.DisposeAsync();

        foreach (var report in new[] { "../escape.mp4", "missing.mp4" })
        {
            var (session, _) = await RecordingWorker(new RecordingCloud { Report = report });
            await session.StartVideoAsync(None);
            var error = await Assert.ThrowsAsync<EngineException>(() => session.StopVideoAsync(None));
            Assert.Equal("ENGINE_FAILURE", error.Code);
            Assert.Contains("browser provider \"toy-cloud\" recording lease lease-0 finished without naming a file it wrote", error.Message, StringComparison.Ordinal);
            await session.DisposeAsync();
        }
    }

    /// <summary>A worker holding slot 0's lease on the real browser, and attempt <c>a1</c> started in a fresh artifact directory.</summary>
    private async Task<(IEngineSession Session, string ArtifactsDir)> RecordingWorker(ToyCloud cloud)
    {
        cloud.OnAcquire = (request, token) => Task.FromResult<BrowserLease>(new ToyLease { Id = "lease-0", CdpEndpoint = _remote.Endpoint });
        var worker = new WebEngine(new WebEngineOptions { Browser = cloud });
        _workers.Add(worker);
        var prepared = await worker.PrepareAsync(Runner.PrepareInfo(1), None);
        worker.Init(Runner.InitInfo(0, Runner.Widen(prepared.Env)));
        var artifactsDir = Directory.CreateTempSubdirectory("e2e-provider-recording-").FullName;
        _made.Add(artifactsDir);
        var session = await worker.StartAsync(new EngineStartOptions { AttemptId = "a1", ArtifactsDir = artifactsDir, ActionTimeout = TimeSpan.FromSeconds(10) }, None);
        return (session, artifactsDir);
    }

    /// <summary>A provider that records: its stop writes <c>replay.mp4</c>, or whatever name it is told to report.</summary>
    private sealed class RecordingCloud : ToyCloud
    {
        public RecordingCloud()
        {
            OnRecord = async (lease, context, token) =>
            {
                Recorded.Add((lease.Id, context.AttemptId, context.RunId, context.TargetName));
                if (BeforeRecord is { } before)
                {
                    await before();
                }

                if (FailStart)
                {
                    throw new InvalidOperationException("recordings unavailable");
                }

                return new ToyRecording("2026-09-28T10:00:00.000Z", async dir =>
                {
                    if (BeforeStop is { } beforeStop)
                    {
                        await beforeStop();
                    }

                    if (Stop is { } custom)
                    {
                        return await custom(dir);
                    }

                    Stopped.Add(lease.Id);
                    await File.WriteAllTextAsync(Path.Combine(dir, "replay.mp4"), "mp4", None);
                    return new ProviderRecordingResult { File = Report ?? "replay.mp4" };
                });
            };
        }

        public List<(string Lease, string Attempt, string Run, string Target)> Recorded { get; } = [];

        public List<string> Stopped { get; } = [];

        public bool FailStart { get; init; }

        public string? Report { get; init; }

        /// <summary>Waits before the provider answers the start.</summary>
        public Func<Task>? BeforeRecord { get; init; }

        /// <summary>Runs before the stop does anything, as a stop that fails.</summary>
        public Func<Task>? BeforeStop { get; init; }

        /// <summary>Replaces the stop.</summary>
        public Func<string, Task<ProviderRecordingResult>>? Stop { get; init; }
    }
}
