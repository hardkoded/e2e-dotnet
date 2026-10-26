// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Mobile.Tests.Provider;

/// <remarks>
/// Upstream's <c>describe('device provider recording')</c> of <c>provider.test.ts</c>. The port's session is one attempt and
/// closes its agent-device session when it ends, so the harness's <c>endAttempt</c> is the session's <c>DisposeAsync</c>.
/// </remarks>
public sealed class DeviceProviderRecordingTests : IDisposable
{
    private static readonly DateTimeOffset StartedAt = DateTimeOffset.Parse("2026-09-28T10:00:00.000Z", System.Globalization.CultureInfo.InvariantCulture);

    private readonly string _artifactsDir = Directory.CreateTempSubdirectory("e2e-device-provider-").FullName;

    public void Dispose() => Directory.Delete(_artifactsDir, recursive: true);

    private Dictionary<string, string?> _workerEnv = [];

    private string VideoDir => Path.Combine(_artifactsDir, "video");

    /// <summary>A lease with a field of the provider's own: it stays behind, and a recording gets only the declared ones.</summary>
    private sealed class KeptLease : DeviceLease
    {
        public string Session { get; init; } = "";
    }

    /// <summary>A recording a scripted provider's service made: each stop answers what the test scripts, or writes <c>replay.mp4</c>.</summary>
    private sealed class ScriptedRecording(Func<ProviderRecordingStopContext, Task<ProviderRecordingResult>>? stop, List<ProviderRecordingStopContext> stopped) : ProviderRecording
    {
        public override DateTimeOffset StartedAt => DeviceProviderRecordingTests.StartedAt;

        public override Task<ProviderRecordingResult> StopAsync(ProviderRecordingStopContext context, CancellationToken cancellationToken)
        {
            stopped.Add(context);
            if (stop is not null)
            {
                return stop(context);
            }

            File.WriteAllText(Path.Combine(context.Dir, "replay.mp4"), "mp4 bytes");
            return Task.FromResult(new ProviderRecordingResult { File = "replay.mp4" });
        }
    }

    /// <summary>A scripted provider whose service records: <c>start</c> answers record, and each <c>stop</c> of a started recording answers what it resolves to.</summary>
    private sealed class RecordingCloud(
        Func<DeviceLease, ProviderRecordContext, Task>? start = null,
        Func<ProviderRecordingStopContext, Task<ProviderRecordingResult>>? stop = null,
        bool records = true) : IDeviceProvider
    {
        public string Name => "toy-cloud";

        public List<(DeviceLease Lease, ProviderRecordContext Context)> Recorded { get; } = [];

        public List<ProviderRecordingStopContext> Stopped { get; } = [];

        public Task<DeviceLease> AcquireAsync(DeviceRequest request, CancellationToken cancellationToken) =>
            Task.FromResult<DeviceLease>(new KeptLease
            {
                Id = $"lease-{request.Slot}",
                Daemon = new DeviceDaemon($"https://{request.Slot}.example"),
                Device = $"sim-{request.Slot}",
                Session = "kept by the provider",
            });

        public Task ReleaseAsync(DeviceLease lease, DeviceReleaseContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task<ProviderRecording?> RecordAsync(DeviceLease lease, ProviderRecordContext context, CancellationToken cancellationToken)
        {
            if (!records)
            {
                return null;
            }

            Recorded.Add((lease, context));
            if (start is not null)
            {
                await start(lease, context).ConfigureAwait(false);
            }

            return new ScriptedRecording(stop, Stopped);
        }
    }

    /// <summary>A provider with no recording of its own: it leaves <c>RecordAsync</c> out, as upstream's does by leaving out <c>record</c>.</summary>
    private sealed class PlainCloud : IDeviceProvider
    {
        public string Name => "toy-cloud";

        public Task<DeviceLease> AcquireAsync(DeviceRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new DeviceLease { Id = $"lease-{request.Slot}", Daemon = new DeviceDaemon($"https://{request.Slot}.example"), Device = $"sim-{request.Slot}" });

        public Task ReleaseAsync(DeviceLease lease, DeviceReleaseContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static Harness Over(IDeviceProvider provider) => Harness.Create(options => options with { Provider = provider });

    private static readonly Dictionary<string, string?> Token = new() { ["DEVICE_SERVICE_TOKEN"] = "t" };

    /// <summary>Prepares one leased slot in the runner, then boots a worker on it from the environment prepare returned.</summary>
    private async Task<(Harness Worker, DeviceSession Session)> LeasedWorker(IDeviceProvider provider)
    {
        var runner = Over(provider);
        var result = await runner.Engine.PrepareAsync(runner.PrepareInfo(1, env: Token), CancellationToken.None);
        var worker = Over(provider);
        var env = Harness.WorkerEnv(result).Concat(Token).ToDictionary(pair => pair.Key, pair => pair.Value);
        _workerEnv = env;
        return (worker, await worker.Boot("ios", 0, env, _artifactsDir));
    }

    private static int Records(Harness h) => h.Fake.Methods().Count(method => method == DeviceMethods.RecordingRecord);

    [Fact]
    public async Task Records_through_the_provider_instead_of_agent_device_from_the_worker_with_the_lease_id_and_the_declared_lease_fields()
    {
        var cloud = new RecordingCloud();
        var (worker, session) = await LeasedWorker(cloud);
        await session.StartVideoAsync(CancellationToken.None);

        // The lease as it traveled to the worker: the id rode the binding, the provider's own field stayed behind.
        var (lease, context) = Assert.Single(cloud.Recorded);
        Assert.IsNotType<KeptLease>(lease);
        Assert.Equal(("lease-0", "sim-0", new DeviceDaemon("https://0.example")), (lease.Id, lease.Device, lease.Daemon));
        Assert.Null(lease.DeviceId);
        Assert.Null(lease.Client);
        Assert.Null(lease.InstalledApp);
        Assert.Equal(("run-1", "ios"), (context.RunId, context.TargetName));
        Assert.False(string.IsNullOrEmpty(context.AttemptId));
        Assert.Equal("t", context.Env["DEVICE_SERVICE_TOKEN"]);
        var segments = await session.StopVideoAsync(CancellationToken.None);
        Assert.Equal<VideoSegment>([new VideoFile { Path = "video/replay.mp4", StartedAt = "2026-09-28T10:00:00.000Z" }], segments);
        Assert.Equal([VideoDir], cloud.Stopped.Select(stop => stop.Dir));
        Assert.True(File.Exists(Path.Combine(VideoDir, "replay.mp4")));
        await session.DisposeAsync();

        // agent-device recorded nothing, and nothing is left for the end of the attempt to stop.
        Assert.Equal(0, Records(worker));
        Assert.Single(cloud.Stopped);
    }

    [Fact]
    public async Task Reports_a_recording_the_service_keeps_as_a_link()
    {
        var cloud = new RecordingCloud(stop: _ => Task.FromResult(new ProviderRecordingResult { Url = "https://toy.example/replays/lease-0.mp4", MediaType = "video/mp4" }));
        var (_, session) = await LeasedWorker(cloud);
        await session.StartVideoAsync(CancellationToken.None);
        Assert.Equal<VideoSegment>(
            [new VideoLink { Url = "https://toy.example/replays/lease-0.mp4", MediaType = "video/mp4", StartedAt = "2026-09-28T10:00:00.000Z" }],
            await session.StopVideoAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Keeps_the_agent_device_recording_for_a_provider_without_record()
    {
        var (worker, session) = await LeasedWorker(new PlainCloud());
        await session.StartVideoAsync(CancellationToken.None);
        JsonAssert.Args(
            $$"""{"platform":"ios","device":"sim-0","action":"start","path":{{JsonValue.Create(Path.Combine(VideoDir, "video.mp4"))!.ToJsonString()}},"quality":"medium","recordingScope":"device"}""",
            worker.Fake.LastArgs(DeviceMethods.RecordingRecord));
        await session.StopVideoAsync(CancellationToken.None);
        Assert.Equal(2, Records(worker));
    }

    [Fact]
    public async Task Keeps_the_agent_device_recording_for_a_provider_that_records_nothing_of_this_device()
    {
        var cloud = new RecordingCloud(records: false);
        var (worker, session) = await LeasedWorker(cloud);
        await session.StartVideoAsync(CancellationToken.None);
        Assert.Equal(1, Records(worker));
        await session.StopVideoAsync(CancellationToken.None);
        Assert.Equal(2, Records(worker));
        Assert.Empty(cloud.Stopped);
    }

    [Fact]
    public async Task Keeps_the_agent_device_recording_for_a_worker_without_a_lease_when_no_prepare_ran()
    {
        var cloud = new RecordingCloud();
        var worker = Over(cloud);
        var session = await worker.Boot("ios", 0, _workerEnv, _artifactsDir);
        await session.StartVideoAsync(CancellationToken.None);
        Assert.Empty(cloud.Recorded);
        Assert.Equal(1, Records(worker));
    }

    [Fact]
    public async Task Names_the_provider_and_the_lease_when_record_fails_and_has_nothing_to_stop_at_attempt_end()
    {
        var cloud = new RecordingCloud(start: (_, _) => throw new InvalidOperationException("recording quota exceeded"));
        var (worker, session) = await LeasedWorker(cloud);
        var failure = await Assert.ThrowsAsync<EngineException>(() => session.StartVideoAsync(CancellationToken.None));
        Assert.Equal("ENGINE_FAILURE", failure.Code);
        Assert.Equal("device provider \"toy-cloud\" could not start recording: recording quota exceeded for lease lease-0", failure.Message);
        await session.DisposeAsync();
        Assert.Empty(cloud.Stopped);
        Assert.Equal(0, Records(worker));
    }

    [Fact]
    public async Task Cancels_a_start_whose_budget_ran_out_and_stops_the_recording_that_came_up_anyway_at_attempt_end()
    {
        var arrive = new TaskCompletionSource();
        var cloud = new RecordingCloud(start: (_, _) => arrive.Task);
        var (_, session) = await LeasedWorker(cloud);
        using var budget = new CancellationTokenSource();
        var starting = session.StartVideoAsync(budget.Token);
        await budget.CancelAsync();
        var failure = await Assert.ThrowsAsync<EngineException>(() => starting);
        Assert.Equal("CANCELLED", failure.Code);
        arrive.SetResult();
        await session.DisposeAsync();
        Assert.Single(cloud.Stopped);
    }

    [Fact]
    public async Task Never_asks_the_provider_to_record_for_an_operation_already_cancelled()
    {
        var cloud = new RecordingCloud();
        var (_, session) = await LeasedWorker(cloud);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var failure = await Assert.ThrowsAsync<EngineException>(() => session.StartVideoAsync(cancelled.Token));
        Assert.Equal("CANCELLED", failure.Code);
        Assert.Empty(cloud.Recorded);
    }

    // PARTIAL: upstream's worker keeps its leased session when it is disposed (reuseSignal), so the test checks that no agent-device
    // command but the boot went out. The port's session is one attempt: it stops the recording and ends its agent-device session.
    [Fact]
    public async Task Partial_Stops_a_provider_recording_when_the_worker_is_disposed_mid_attempt()
    {
        var cloud = new RecordingCloud();
        var (worker, session) = await LeasedWorker(cloud);
        await session.StartVideoAsync(CancellationToken.None);
        await session.DisposeAsync();
        Assert.Equal([VideoDir], cloud.Stopped.Select(stop => stop.Dir));
        Assert.Equal(0, Records(worker));
    }

    [Fact]
    public async Task Keeps_a_recording_whose_stop_failed_so_endAttempt_stops_it_once_more()
    {
        var failures = 1;
        var cloud = new RecordingCloud(stop: context =>
        {
            if (failures-- > 0)
            {
                throw new InvalidOperationException("replay not ready");
            }

            File.WriteAllText(Path.Combine(context.Dir, "replay.mp4"), "mp4 bytes");
            return Task.FromResult(new ProviderRecordingResult { File = "replay.mp4" });
        });
        var (worker, session) = await LeasedWorker(cloud);
        await session.StartVideoAsync(CancellationToken.None);
        var failure = await Assert.ThrowsAsync<EngineException>(() => session.StopVideoAsync(CancellationToken.None));
        Assert.Equal("ENGINE_FAILURE", failure.Code);
        Assert.Equal("device provider \"toy-cloud\" recording lease lease-0 could not finish: replay not ready", failure.Message);
        Assert.Single(cloud.Stopped);
        await session.DisposeAsync();
        Assert.Equal(2, cloud.Stopped.Count);

        // Stopped for good: the next attempt starts with nothing recording.
        var next = await worker.Boot("ios", 0, _workerEnv, _artifactsDir);
        await next.DisposeAsync();
        Assert.Equal(2, cloud.Stopped.Count);
    }

    // PARTIAL: upstream ends the attempt and then disposes the worker, which keeps its session for reuse, and checks that only
    // the boot reached agent-device. The port's one session ends once: its close stops nothing more once the stop was confirmed.
    [Fact]
    public async Task Partial_Clears_a_confirmed_provider_stop_before_rejecting_its_invalid_artifact()
    {
        var stops = 0;
        var cloud = new RecordingCloud(stop: _ => Task.FromResult(++stops == 1
            ? new ProviderRecordingResult { File = "missing.mp4" }
            : new ProviderRecordingResult { Url = "https://toy.example/replay.mp4", MediaType = "video/mp4" }));
        var (worker, session) = await LeasedWorker(cloud);
        await session.StartVideoAsync(CancellationToken.None);
        var failure = await Assert.ThrowsAsync<EngineException>(() => session.StopVideoAsync(CancellationToken.None));
        Assert.Contains("finished without naming a file", failure.Message, StringComparison.Ordinal);
        await session.DisposeAsync();
        Assert.Single(cloud.Stopped);

        var next = await worker.Boot("ios", 0, _workerEnv, _artifactsDir);
        await next.StartVideoAsync(CancellationToken.None);
        Assert.Equal<VideoSegment>(
            [new VideoLink { Url = "https://toy.example/replay.mp4", MediaType = "video/mp4", StartedAt = "2026-09-28T10:00:00.000Z" }],
            await next.StopVideoAsync(CancellationToken.None));
        await next.DisposeAsync();
        Assert.Equal(2, cloud.Stopped.Count);
        Assert.Equal(0, Records(worker));
    }
}
