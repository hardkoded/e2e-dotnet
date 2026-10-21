// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using static E2E.Kernel.Tests.Kernel.KernelFixtures;

namespace E2E.Kernel.Tests.Kernel;

/// <summary><c>describe('record')</c> of upstream <c>kernel.test.ts</c>: the Kernel replay of a leased browser.</summary>
public sealed class KernelRecordTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("e2e-kernel-replay-");

    private static ProviderRecordContext RecordContext() => new() { RunId = "run-1", TargetName = "web", AttemptId = "a1", Env = Env };

    public void Dispose() => _dir.Delete(recursive: true);

    [Fact]
    public async Task Records_a_Kernel_replay_of_the_browser_and_saves_it_as_replay_mp4_in_the_directory_it_is_given_PARTIAL_a_call_has_no_signal_of_the_step_it_serves_to_compare()
    {
        var fake = new FakeKernel();
        var before = DateTimeOffset.UtcNow;

        var recording = await Provider(fake).RecordAsync(Lease(), RecordContext(), CancellationToken.None);

        Assert.NotNull(recording);
        Assert.True(recording.StartedAt >= before.AddMilliseconds(-1));
        var result = await recording.StopAsync(new ProviderRecordingStopContext { Dir = _dir.FullName }, CancellationToken.None);
        Assert.Equal("replay.mp4", result.File);
        Assert.Equal("mp4 bytes", await File.ReadAllTextAsync(Path.Combine(_dir.FullName, "replay.mp4")));
        Assert.Equal(["startReplay", "stopReplay", "downloadReplay"], fake.Calls.Select(call => call.Route));
        Assert.All(fake.Calls, call => Assert.Equal("b1", call.Id));
        AssertJson(new JsonObject(), fake.Calls[0].Body);
    }

    [Fact]
    public async Task Retries_a_failed_download_without_stopping_the_replay_a_second_time()
    {
        var fake = new FakeKernel { DownloadFailures = 1 };
        var recording = await Provider(fake).RecordAsync(Lease(), RecordContext(), CancellationToken.None);
        var stop = new ProviderRecordingStopContext { Dir = _dir.FullName };

        var failure = await Assert.ThrowsAsync<KernelApiException>(() => recording!.StopAsync(stop, CancellationToken.None));
        Assert.Contains("503 replay still processing", failure.Message, StringComparison.Ordinal);
        var result = await recording!.StopAsync(stop, CancellationToken.None);

        Assert.Equal("replay.mp4", result.File);
        Assert.Equal(["startReplay", "stopReplay", "downloadReplay", "downloadReplay"], fake.Calls.Select(call => call.Route));
    }

    [Fact]
    public async Task Does_not_record_with_replay_false_or_for_a_headless_browser_which_Kernel_cannot_replay()
    {
        var fake = new FakeKernel();

        Assert.Null(await Provider(fake, new KernelOptions { Replay = false }).RecordAsync(Lease(), RecordContext(), CancellationToken.None));
        Assert.Null(await Provider(fake, new KernelOptions { Headless = true }).RecordAsync(Lease(), RecordContext(), CancellationToken.None));
        Assert.NotNull(await Provider(fake, new KernelOptions { Headless = false }).RecordAsync(Lease(), RecordContext(), CancellationToken.None));
    }

    [Fact]
    public async Task Passes_the_replay_options_to_the_replay_never_to_Kernel_as_a_create_browser_field()
    {
        var fake = new FakeKernel();
        var provider = Provider(fake, new KernelOptions { ReplayParams = new KernelReplayParams { Framerate = 20, MaxDurationInSeconds = 300 } });

        await provider.AcquireAsync(Request(), CancellationToken.None);
        var body = Assert.IsType<JsonObject>(Assert.Single(fake.Of("create")).Body);
        Assert.DoesNotContain(body, pair => pair.Key.Contains("replay", StringComparison.OrdinalIgnoreCase));

        await provider.RecordAsync(Lease(), RecordContext(), CancellationToken.None);
        var start = Assert.Single(fake.Of("startReplay"));
        AssertJson(new JsonObject { ["framerate"] = 20, ["max_duration_in_seconds"] = 300 }, start.Body);
        Assert.Single(fake.Of("create"));
    }
}
