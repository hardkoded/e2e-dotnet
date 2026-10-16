// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.BrowserProvider;

/// <summary>
/// <c>describe('recording')</c> of upstream <c>browser-provider.test.ts</c>. The port has no video: no <c>startVideo</c> or
/// <c>stopVideo</c>, no screencast, and no <c>ProviderVideo</c> (<c>provider-video.ts</c>) to wrap a recording in. So the three
/// tests that reach a recording through them check the part the port has, the start through <c>LeasedBrowsers.Recorder</c> and
/// the stop through <c>StopProviderRecordingAsync</c>. Not ported:
/// "rejects a record that is not a function" (the interface has the method),
/// "records the screencast when the provider does not record" (no screencast),
/// "stops a recording the attempt never collected when the attempt ends",
/// "stops a recording that started after the attempt ended, and keeps nothing of it",
/// "shares a stop that timed out and is still in flight, so ending the attempt never stops a recording twice", and
/// "keeps a recording whose stop failed, so ending the attempt stops it once more" (all four are <c>ProviderVideo</c>), and
/// "refuses, at the start and named, a recording without a start time and a stop" (a <see cref="ProviderRecording"/> always has
/// both; a provider that returns none does not record, and the engine records the screencast).
/// </summary>
public sealed class RecordingTests : IDisposable
{
    private static readonly CancellationToken None = CancellationToken.None;

    private readonly List<string> _made = [];

    public void Dispose()
    {
        foreach (var dir in _made)
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Records_the_attempt_through_the_provider_instead_of_the_screencast_as_one_segment_in_the_attempt_directory_PARTIAL_no_video_lifecycle_or_screencast()
    {
        var cloud = new ToyCloud();
        var recorded = new List<(string Lease, string Attempt, string Run, string Target)>();
        var stopped = new List<string>();
        cloud.OnRecord = (lease, context, token) =>
        {
            recorded.Add((lease.Id, context.AttemptId, context.RunId, context.TargetName));
            return Task.FromResult<E2E.Engine.ProviderRecording?>(new ToyRecording("2026-09-28T10:00:00.000Z", async dir =>
            {
                stopped.Add(lease.Id);
                await File.WriteAllTextAsync(Path.Combine(dir, "replay.mp4"), "mp4", None);
                return new ProviderRecordingResult { File = "replay.mp4" };
            }));
        };
        var (artifactsDir, started) = await RecordingWorker(cloud);
        Assert.Equal([("lease-0", "a1", "run-1", "web")], recorded);
        var segment = await ProviderRecordings.StopProviderRecordingAsync(started.Recording, Target(artifactsDir, started), None);
        Assert.Equal(new VideoFile { Path = "video/replay.mp4", StartedAt = "2026-09-28T10:00:00.000Z" }, segment);
        Assert.Equal("mp4", await File.ReadAllTextAsync(Path.Combine(artifactsDir, "video", "replay.mp4"), None));
        Assert.Equal(["lease-0"], stopped);
    }

    [Fact]
    public async Task Turns_a_recording_the_provider_keeps_into_a_link_segment_with_nothing_written_locally_PARTIAL_no_screencast_to_compare_with()
    {
        var cloud = new ToyCloud
        {
            OnRecord = (lease, context, token) => Task.FromResult<E2E.Engine.ProviderRecording?>(
                new ToyRecording("2026-09-28T10:00:00.000Z", _ => Task.FromResult(new ProviderRecordingResult { Url = "https://recordings.example/r1.mp4", MediaType = "video/mp4" }))),
        };
        var (artifactsDir, started) = await RecordingWorker(cloud);
        var segment = await ProviderRecordings.StopProviderRecordingAsync(started.Recording, Target(artifactsDir, started), None);
        Assert.Equal(new VideoLink { Url = "https://recordings.example/r1.mp4", MediaType = "video/mp4", StartedAt = "2026-09-28T10:00:00.000Z" }, segment);
        Assert.Empty(Directory.GetFiles(Path.Combine(artifactsDir, "video")));
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

    /// <summary>Partial: the start and the stop run through <c>Recorder</c> and <c>StopProviderRecordingAsync</c>, not <c>startVideo</c> and <c>stopVideo</c>.</summary>
    [Fact]
    public async Task Names_the_provider_when_a_recording_cannot_start_and_refuses_a_stop_that_names_no_file_it_wrote_PARTIAL_it_runs_through_Recorder_not_startVideo()
    {
        var failing = new ToyCloud { OnRecord = (lease, context, token) => throw new InvalidOperationException("recordings unavailable") };
        var (_, env) = await Runner.PreparedAsync(failing, 1);
        var leases = new LeasedBrowsers(failing);
        leases.Init(Runner.InitInfo(0, env));
        var refused = await Assert.ThrowsAsync<EngineException>(() => leases.Recorder("a1")(None));
        Assert.Equal("ENGINE_FAILURE", refused.Code);
        Assert.Contains("browser provider \"toy-cloud\" could not start recording browser lease-0: recordings unavailable", refused.Message, StringComparison.Ordinal);

        foreach (var report in new[] { "../escape.mp4", "missing.mp4" })
        {
            var cloud = new ToyCloud
            {
                OnRecord = (lease, context, token) => Task.FromResult<E2E.Engine.ProviderRecording?>(new ToyRecording("2026-09-28T10:00:00.000Z", async dir =>
                {
                    await File.WriteAllTextAsync(Path.Combine(dir, "replay.mp4"), "mp4", None);
                    return new ProviderRecordingResult { File = report };
                })),
            };
            var (artifactsDir, started) = await RecordingWorker(cloud);
            var error = await Assert.ThrowsAsync<EngineException>(() => ProviderRecordings.StopProviderRecordingAsync(started.Recording, Target(artifactsDir, started), None));
            Assert.Equal("ENGINE_FAILURE", error.Code);
            Assert.Contains("browser provider \"toy-cloud\" recording lease lease-0 finished without naming a file it wrote", error.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>A worker holding slot 0's lease, and the recording the provider started for attempt <c>a1</c> in a fresh artifact directory.</summary>
    private async Task<(string ArtifactsDir, LeaseRecording Started)> RecordingWorker(ToyCloud cloud)
    {
        var (_, env) = await Runner.PreparedAsync(cloud, 1);
        var leases = new LeasedBrowsers(cloud);
        leases.Init(Runner.InitInfo(0, env));
        var artifactsDir = Directory.CreateTempSubdirectory("e2e-provider-recording-").FullName;
        _made.Add(artifactsDir);
        var started = await leases.Recorder("a1")(None);
        return (artifactsDir, started!);
    }

    private static ProviderRecordingTarget Target(string artifactsDir, LeaseRecording started) =>
        new() { ArtifactsDir = artifactsDir, Provider = started.Provider, LeaseId = started.LeaseId };
}
