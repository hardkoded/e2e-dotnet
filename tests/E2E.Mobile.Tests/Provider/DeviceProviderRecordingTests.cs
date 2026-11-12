// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Mobile.Tests.Provider;

/// <remarks>
/// Upstream's <c>describe('device provider recording')</c> of <c>provider.test.ts</c>. Upstream's worker is the engine here, which keeps the device of
/// its slot across the attempts that run on it: <c>startAttempt</c> is <see cref="Harness.Boot"/> (with the attempt's id), <c>endAttempt</c> is
/// <see cref="MobileEngine.EndAttemptAsync"/>, and <c>dispose</c> with a <c>reuseSignal</c> is the internal <c>DisposeAsync</c> overload.
/// </remarks>
public sealed class DeviceProviderRecordingTests : IDisposable
{
    private static readonly DateTimeOffset StartedAt = DateTimeOffset.Parse("2026-09-28T10:00:00.000Z", System.Globalization.CultureInfo.InvariantCulture);

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);

    private readonly string _artifactsDir = Directory.CreateTempSubdirectory("e2e-device-provider-").FullName;

    public void Dispose() => Directory.Delete(_artifactsDir, recursive: true);

    private Dictionary<string, string?> _workerEnv = [];

    private string VideoDir => Path.Combine(_artifactsDir, "video");

    private string VideoPath => Path.Combine(VideoDir, "video.mp4");

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
    private async Task<(Harness Worker, DeviceSession Session)> LeasedWorker(IDeviceProvider provider, string attemptId = "a1")
    {
        var runner = Over(provider);
        var result = await runner.Engine.PrepareAsync(runner.PrepareInfo(1, env: Token), CancellationToken.None);
        var worker = Over(provider);
        var env = Harness.WorkerEnv(result).Concat(Token).ToDictionary(pair => pair.Key, pair => pair.Value);
        _workerEnv = env;
        return (worker, await worker.Boot("ios", 0, env, _artifactsDir, attemptId: attemptId));
    }

    private static int Records(Harness h) => h.Fake.Methods().Count(method => method == DeviceMethods.RecordingRecord);

    private static IEnumerable<string> Dirs(RecordingCloud cloud) => cloud.Stopped.Select(stop => stop.Dir);

    private static Func<ProviderRecordingStopContext, Task<ProviderRecordingResult>> Fails(string message) =>
        _ => Task.FromException<ProviderRecordingResult>(new InvalidOperationException(message));

    private static readonly ProviderRecordingResult Link = new() { Url = "https://toy.example/a1.mp4", MediaType = "video/mp4" };

    /// <summary>The native recorder of a fake device: a start while one runs fails, and a stop fails while <paramref name="failStop"/> holds.</summary>
    private static JsonNode? NativeRecording(JsonObject? args, ref bool active, bool failStop)
    {
        if (args!["action"]!.GetValue<string>() == "start")
        {
            if (active)
            {
                throw new InvalidOperationException("recording already active");
            }

            active = true;
        }
        else
        {
            if (failStop)
            {
                throw new InvalidOperationException("native recorder busy");
            }

            active = false;
        }

        return new JsonObject();
    }

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
        await worker.Engine.EndAttemptAsync(Budget, CancellationToken.None);
        Assert.Empty(cloud.Stopped);
        Assert.Equal(0, Records(worker));
    }

    [Fact]
    public async Task Cancels_a_start_whose_budget_ran_out_and_stops_the_recording_that_came_up_anyway_at_attempt_end()
    {
        var arrive = new TaskCompletionSource();
        var cloud = new RecordingCloud(start: (_, _) => arrive.Task);
        var (worker, session) = await LeasedWorker(cloud);
        using var budget = new CancellationTokenSource();
        var starting = session.StartVideoAsync(budget.Token);
        await budget.CancelAsync();
        var failure = await Assert.ThrowsAsync<EngineException>(() => starting);
        Assert.Equal("CANCELLED", failure.Code);
        arrive.SetResult();
        await worker.Engine.EndAttemptAsync(Budget, CancellationToken.None);
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

    [Fact]
    public async Task Stops_a_provider_recording_without_closing_its_leased_session_when_the_worker_is_disposed_mid_attempt()
    {
        var cloud = new RecordingCloud();
        var (worker, session) = await LeasedWorker(cloud);
        await session.StartVideoAsync(CancellationToken.None);
        using var reuse = new CancellationTokenSource();
        await worker.Engine.DisposeAsync(Budget, reuse.Token, CancellationToken.None);
        Assert.Equal([VideoDir], Dirs(cloud));
        Assert.Equal([DeviceMethods.DevicesBoot], worker.Fake.Methods());
    }

    [Fact]
    public async Task Keeps_a_reusable_leased_session_when_the_provider_recording_still_cannot_stop_during_disposal()
    {
        var cloud = new RecordingCloud(stop: Fails("stop unavailable"));
        var (worker, session) = await LeasedWorker(cloud);
        await session.StartVideoAsync(CancellationToken.None);
        var failure = await Assert.ThrowsAsync<EngineException>(() => session.StopVideoAsync(CancellationToken.None));
        Assert.Contains("stop unavailable", failure.Message, StringComparison.Ordinal);
        await worker.Engine.EndAttemptAsync(Budget, CancellationToken.None);
        using var reuse = new CancellationTokenSource();
        await worker.Engine.DisposeAsync(Budget, reuse.Token, CancellationToken.None);
        Assert.Equal(Enumerable.Repeat(VideoDir, 3), Dirs(cloud));
        Assert.Equal([DeviceMethods.DevicesBoot], worker.Fake.Methods());
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

    [Fact]
    public async Task Finishes_a_retained_provider_recording_before_the_next_attempt_records_in_the_same_worker()
    {
        var stopped = new TaskCompletionSource<ProviderRecordingResult>();
        var reached = new TaskCompletionSource();
        var events = new List<string>();
        var stops = 0;
        var cloud = new RecordingCloud(
            start: (_, context) =>
            {
                events.Add("record");
                return Task.CompletedTask;
            },
            stop: async _ =>
            {
                if (++stops < 3)
                {
                    throw new InvalidOperationException("stop unavailable");
                }

                reached.TrySetResult();
                var result = await stopped.Task;
                events.Add("stopped");
                return result;
            });
        var (worker, session) = await LeasedWorker(cloud, "a1");
        await session.StartVideoAsync(CancellationToken.None);
        await Assert.ThrowsAsync<EngineException>(() => session.StopVideoAsync(CancellationToken.None));
        await worker.Engine.EndAttemptAsync(Budget, CancellationToken.None);
        var nextDir = Path.Combine(_artifactsDir, "retry");
        var starting = worker.Boot("ios", 0, _workerEnv, nextDir, attemptId: "a2");
        try
        {
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // The next attempt does not start until the retained recording is stopped.
            Assert.Equal(Enumerable.Repeat(VideoDir, 3), Dirs(cloud));
            Assert.Single(cloud.Recorded);
            Assert.False(starting.IsCompleted);
        }
        finally
        {
            stopped.SetResult(Link);
        }

        var next = await starting;
        await next.StartVideoAsync(CancellationToken.None);
        Assert.Equal(["record", "stopped", "record"], events);
        await next.StopVideoAsync(CancellationToken.None);
        Assert.Equal(Path.Combine(nextDir, "video"), cloud.Stopped[^1].Dir);
        Assert.Equal(0, Records(worker));
    }

    [Fact]
    public async Task Blocks_the_next_attempt_while_its_retained_provider_recording_cannot_stop_and_preserves_its_owner()
    {
        var unavailable = true;
        var cloud = new RecordingCloud(stop: _ => unavailable
            ? Task.FromException<ProviderRecordingResult>(new InvalidOperationException("stop unavailable"))
            : Task.FromResult(Link));
        var (worker, session) = await LeasedWorker(cloud, "a1");
        await session.StartVideoAsync(CancellationToken.None);
        await Assert.ThrowsAsync<EngineException>(() => session.StopVideoAsync(CancellationToken.None));
        await worker.Engine.EndAttemptAsync(Budget, CancellationToken.None);
        var nextDir = Path.Combine(_artifactsDir, "retry");
        var blocked = await Assert.ThrowsAsync<EngineException>(() => worker.Boot("ios", 0, _workerEnv, nextDir, attemptId: "a2"));
        Assert.Contains("stop unavailable", blocked.Message, StringComparison.Ordinal);
        Assert.Single(cloud.Recorded);
        Assert.Equal(Enumerable.Repeat(VideoDir, 3), Dirs(cloud));

        unavailable = false;
        var next = await worker.Boot("ios", 0, _workerEnv, nextDir, attemptId: "a2");
        Assert.Equal(Enumerable.Repeat(VideoDir, 4), Dirs(cloud));
        await next.StartVideoAsync(CancellationToken.None);
        await next.StopVideoAsync(CancellationToken.None);
        Assert.Equal(["a1", "a2"], cloud.Recorded.Select(recorded => recorded.Context.AttemptId));
    }

    [Fact]
    public async Task Does_not_begin_another_attempt_or_record_when_a_retained_provider_stop_wait_is_cancelled()
    {
        var stopped = new TaskCompletionSource<ProviderRecordingResult>();
        var reached = new TaskCompletionSource();
        var stops = 0;
        var cloud = new RecordingCloud(stop: _ =>
        {
            if (++stops < 3)
            {
                return Task.FromException<ProviderRecordingResult>(new InvalidOperationException("stop unavailable"));
            }

            reached.TrySetResult();
            return stopped.Task;
        });
        var (worker, session) = await LeasedWorker(cloud);
        await session.StartVideoAsync(CancellationToken.None);
        await Assert.ThrowsAsync<EngineException>(() => session.StopVideoAsync(CancellationToken.None));
        await worker.Engine.EndAttemptAsync(Budget, CancellationToken.None);
        using var budget = new CancellationTokenSource();
        var starting = worker.Boot("ios", 0, _workerEnv, Path.Combine(_artifactsDir, "retry"), cancellationToken: budget.Token, attemptId: "a2");
        try
        {
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await budget.CancelAsync();
            var cancelled = await Assert.ThrowsAsync<EngineException>(() => starting);
            Assert.Equal(EngineErrorCodes.Cancelled, cancelled.Code);
            Assert.Single(cloud.Recorded);
            Assert.Equal(Enumerable.Repeat(VideoDir, 3), Dirs(cloud));
        }
        finally
        {
            stopped.SetResult(Link);
        }
    }

    [Fact]
    public async Task Resets_a_leased_session_whose_native_recording_still_cannot_stop_after_endAttempt()
    {
        var (worker, session) = await LeasedWorker(new PlainCloud());
        var active = false;
        worker.Fake.Respond(DeviceMethods.RecordingRecord, args => NativeRecording(args, ref active, failStop: true));
        worker.Fake.Respond(DeviceMethods.SessionsClose, _ =>
        {
            active = false;
            return new JsonObject();
        });
        await session.StartVideoAsync(CancellationToken.None);
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => session.StopVideoAsync(CancellationToken.None));
        Assert.Contains("native recorder busy", failure.Message, StringComparison.Ordinal);
        await worker.Engine.EndAttemptAsync(Budget, CancellationToken.None);
        using var reuse = new CancellationTokenSource();
        await worker.Engine.DisposeAsync(Budget, reuse.Token, CancellationToken.None);
        Assert.Equal(DeviceMethods.SessionsClose, worker.Fake.Methods()[^1]);

        var replacement = Over(new PlainCloud());
        var replacementActive = false;
        replacement.Fake.Respond(DeviceMethods.RecordingRecord, args => NativeRecording(args, ref replacementActive, failStop: false));
        var booted = await replacement.Boot("ios", 0, _workerEnv, _artifactsDir, attemptId: "a2");
        await booted.StartVideoAsync(CancellationToken.None);
        Assert.Empty(await booted.StopVideoAsync(CancellationToken.None));
        JsonAssert.Args("""{"platform":"ios","device":"sim-0","action":"stop"}""", replacement.Fake.LastArgs(DeviceMethods.RecordingRecord));
    }

    [Fact]
    public async Task Resets_an_unconfirmed_native_recording_before_another_attempt_in_the_same_worker()
    {
        var (worker, session) = await LeasedWorker(new PlainCloud());
        worker.Fake.Respond(DeviceMethods.RecordingRecord, args => args!["action"]!.GetValue<string>() == "stop"
            ? throw new InvalidOperationException("native recorder busy")
            : new JsonObject());
        await session.StartVideoAsync(CancellationToken.None);
        await worker.Engine.EndAttemptAsync(Budget, CancellationToken.None);
        var next = await worker.Boot("ios", 0, _workerEnv, _artifactsDir, attemptId: "a2");
        await next.StartVideoAsync(CancellationToken.None);
        Assert.Equal(
            [DeviceMethods.DevicesBoot, DeviceMethods.RecordingRecord, DeviceMethods.RecordingRecord, DeviceMethods.SessionsClose, DeviceMethods.RecordingRecord],
            worker.Fake.Methods());
    }

    [Fact]
    public async Task Fails_the_next_attempt_when_its_native_recording_reset_cannot_close_the_session()
    {
        var (worker, session) = await LeasedWorker(new PlainCloud());
        worker.Fake.Respond(DeviceMethods.RecordingRecord, args => args!["action"]!.GetValue<string>() == "stop"
            ? throw new InvalidOperationException("native recorder busy")
            : new JsonObject());
        worker.Fake.Respond(DeviceMethods.SessionsClose, _ => throw new InvalidOperationException("reset failed"));
        await session.StartVideoAsync(CancellationToken.None);
        await worker.Engine.EndAttemptAsync(Budget, CancellationToken.None);
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => worker.Boot("ios", 0, _workerEnv, _artifactsDir, attemptId: "a2"));
        Assert.Contains("reset failed", failure.Message, StringComparison.Ordinal);
        Assert.Equal(
            [DeviceMethods.DevicesBoot, DeviceMethods.RecordingRecord, DeviceMethods.RecordingRecord, DeviceMethods.SessionsClose],
            worker.Fake.Methods());
    }

    [Fact]
    public async Task Keeps_an_unconfirmed_native_recording_after_its_stop_wait_was_cancelled()
    {
        var (worker, session) = await LeasedWorker(new PlainCloud());
        var stopping = new TaskCompletionSource();
        var stopped = new TaskCompletionSource<JsonNode?>();
        worker.Fake.RespondAsync(DeviceMethods.RecordingRecord, (args, _) =>
        {
            if (args!["action"]!.GetValue<string>() != "stop")
            {
                return Task.FromResult<JsonNode?>(new JsonObject());
            }

            stopping.TrySetResult();
            return stopped.Task;
        });
        await session.StartVideoAsync(CancellationToken.None);
        using var budget = new CancellationTokenSource();
        var pending = session.StopVideoAsync(budget.Token);
        await stopping.Task;
        await budget.CancelAsync();
        var cancelled = await Assert.ThrowsAsync<EngineException>(() => pending);
        Assert.Equal(EngineErrorCodes.Cancelled, cancelled.Code);
        await worker.Engine.EndAttemptAsync(TimeSpan.Zero, CancellationToken.None);
        using var reuse = new CancellationTokenSource();
        try
        {
            await worker.Engine.DisposeAsync(Budget, reuse.Token, CancellationToken.None);
            Assert.Equal(DeviceMethods.SessionsClose, worker.Fake.Methods()[^1]);
        }
        finally
        {
            stopped.SetResult(new JsonObject());
        }
    }

    [Fact]
    public async Task Preserves_a_session_after_native_stop_succeeded_even_when_moving_its_artifact_fails()
    {
        var (worker, session) = await LeasedWorker(new PlainCloud());
        var elsewhere = Path.Combine(_artifactsDir, "elsewhere.mp4");
        worker.Fake.Respond(DeviceMethods.RecordingRecord, args =>
        {
            if (args!["action"]!.GetValue<string>() != "stop")
            {
                return new JsonObject();
            }

            File.WriteAllText(elsewhere, "video");
            Directory.CreateDirectory(VideoPath);
            return new JsonObject { ["outPath"] = elsewhere };
        });
        await session.StartVideoAsync(CancellationToken.None);
        await Assert.ThrowsAnyAsync<Exception>(() => session.StopVideoAsync(CancellationToken.None));
        await worker.Engine.EndAttemptAsync(Budget, CancellationToken.None);
        using var reuse = new CancellationTokenSource();
        await worker.Engine.DisposeAsync(Budget, reuse.Token, CancellationToken.None);
        Assert.Equal([DeviceMethods.DevicesBoot, DeviceMethods.RecordingRecord, DeviceMethods.RecordingRecord], worker.Fake.Methods());
    }

    [Fact]
    public async Task Clears_a_confirmed_provider_stop_before_rejecting_its_invalid_artifact()
    {
        var stops = 0;
        var cloud = new RecordingCloud(stop: _ => Task.FromResult(++stops == 1
            ? new ProviderRecordingResult { File = "missing.mp4" }
            : new ProviderRecordingResult { Url = "https://toy.example/replay.mp4", MediaType = "video/mp4" }));
        var (worker, session) = await LeasedWorker(cloud, "a1");
        await session.StartVideoAsync(CancellationToken.None);
        var failure = await Assert.ThrowsAsync<EngineException>(() => session.StopVideoAsync(CancellationToken.None));
        Assert.Contains("finished without naming a file", failure.Message, StringComparison.Ordinal);
        await worker.Engine.EndAttemptAsync(Budget, CancellationToken.None);
        var next = await worker.Boot("ios", 0, _workerEnv, _artifactsDir, attemptId: "a2");
        await next.StartVideoAsync(CancellationToken.None);
        Assert.Equal<VideoSegment>(
            [new VideoLink { Url = "https://toy.example/replay.mp4", MediaType = "video/mp4", StartedAt = "2026-09-28T10:00:00.000Z" }],
            await next.StopVideoAsync(CancellationToken.None));
        using var reuse = new CancellationTokenSource();
        await worker.Engine.DisposeAsync(Budget, reuse.Token, CancellationToken.None);
        Assert.Equal(2, cloud.Stopped.Count);
        Assert.Equal([DeviceMethods.DevicesBoot], worker.Fake.Methods());
    }

    [Fact]
    public async Task Retries_an_unconfirmed_provider_stop_after_endAttempt_during_disposal()
    {
        var stops = 0;
        var cloud = new RecordingCloud(stop: _ => ++stops < 3
            ? Task.FromException<ProviderRecordingResult>(new InvalidOperationException("stop unavailable"))
            : Task.FromResult(Link));
        var (worker, session) = await LeasedWorker(cloud);
        await session.StartVideoAsync(CancellationToken.None);
        await Assert.ThrowsAsync<EngineException>(() => session.StopVideoAsync(CancellationToken.None));
        await worker.Engine.EndAttemptAsync(Budget, CancellationToken.None);
        using var reuse = new CancellationTokenSource();
        await worker.Engine.DisposeAsync(Budget, reuse.Token, CancellationToken.None);
        Assert.Equal(Enumerable.Repeat(VideoDir, 3), Dirs(cloud));
        Assert.Equal([DeviceMethods.DevicesBoot], worker.Fake.Methods());
    }

    [Fact]
    public async Task Shares_a_pending_provider_stop_and_closes_on_reuse_revocation_before_the_stop_resolves()
    {
        var stopping = new TaskCompletionSource();
        var stopped = new TaskCompletionSource<ProviderRecordingResult>();
        var cloud = new RecordingCloud(stop: _ =>
        {
            stopping.TrySetResult();
            return stopped.Task;
        });
        var (worker, session) = await LeasedWorker(cloud);
        await session.StartVideoAsync(CancellationToken.None);
        using var budget = new CancellationTokenSource();
        var pending = session.StopVideoAsync(budget.Token);
        await stopping.Task;
        await budget.CancelAsync();
        var cancelled = await Assert.ThrowsAsync<EngineException>(() => pending);
        Assert.Equal(EngineErrorCodes.Cancelled, cancelled.Code);
        await worker.Engine.EndAttemptAsync(TimeSpan.Zero, CancellationToken.None);
        using var reuse = new CancellationTokenSource();
        var disposing = worker.Engine.DisposeAsync(Budget, reuse.Token, CancellationToken.None);
        try
        {
            // Revoked before the stop resolves: the session closes at once, and the stop is not sent a second time.
            reuse.Cancel();
            Assert.Equal(DeviceMethods.SessionsClose, worker.Fake.Methods()[^1]);
            Assert.Equal([VideoDir], Dirs(cloud));
        }
        finally
        {
            stopped.SetResult(Link);
            await disposing;
        }
    }
}
