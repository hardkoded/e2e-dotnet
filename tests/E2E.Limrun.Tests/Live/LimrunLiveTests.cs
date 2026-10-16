// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json.Nodes;
using E2E.Mobile;

namespace E2E.Limrun.Tests.Live;

/// <summary>A fact that runs only when <c>LIMRUN_API_KEY</c> is set: it uses a real Limrun account and creates a real device.</summary>
public sealed class LimrunLiveFactAttribute : FactAttribute
{
    public LimrunLiveFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LIMRUN_API_KEY")))
        {
            Skip = "LIMRUN_API_KEY is not set";
        }
    }
}

/// <summary>
/// The provider against Limrun itself and the real agent-device daemon. The fakes in this project cannot see what the
/// daemon does with a command's lease scope, so a client that left part of it out passed every other test. The .NET port
/// has no upstream test for it.
/// </summary>
public sealed class LimrunLiveTests
{
    [LimrunLiveFact]
    [Trait("TestCategory", "Limrun")]
    public async Task Boots_snapshots_and_screenshots_a_real_ios_simulator_and_terminates_it_on_release()
    {
        var key = Environment.GetEnvironmentVariable("LIMRUN_API_KEY")!;
        var env = new Dictionary<string, string?>
        {
            ["LIMRUN_API_KEY"] = key,
            ["PATH"] = Environment.GetEnvironmentVariable("PATH"),
            ["HOME"] = Environment.GetEnvironmentVariable("HOME"),
        };
        using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        var provider = new LimrunSimulators(new LimrunSimulatorsOptions { HardTimeout = "10m" }, LimrunHost.Instance, http, static (delay, token) => Task.Delay(delay, token));
        var screenshot = Path.Combine(Path.GetTempPath(), $"e2e-limrun-live-{Guid.NewGuid():N}.png");
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        DeviceLease? lease = null;
        string id;
        try
        {
            lease = await provider.AcquireAsync(
                new DeviceRequest
                {
                    Platform = MobilePlatform.Ios,
                    RunId = $"live-{Guid.NewGuid():N}"[..13],
                    TargetName = "live",
                    Slot = 0,
                    Slots = 1,
                    AgentDeviceVersion = AgentDeviceVersion.Pinned,
                    ProjectRoot = Directory.GetCurrentDirectory(),
                    Env = env,
                    Log = _ => { },
                },
                budget.Token);

            // Every one of these fails with UNAUTHORIZED when the command's meta leaves out part of the lease scope.
            var client = DeviceClientConfig.For("e2e-live-0", new DeviceConnection { Client = lease.Client }).Connect();
            await client.CallAsync(DeviceMethods.DevicesBoot, new JsonObject { ["platform"] = "ios", ["udid"] = lease.DeviceId }, budget.Token);
            var snapshot = await client.CallAsync(DeviceMethods.CaptureSnapshot, new JsonObject(), budget.Token);
            Assert.NotEmpty(Assert.IsType<JsonObject>(snapshot)["nodes"]!.AsArray());
            await client.CallAsync(DeviceMethods.CaptureScreenshot, new JsonObject { ["path"] = screenshot }, budget.Token);
            Assert.True(new FileInfo(screenshot).Length > 0);
        }
        finally
        {
            File.Delete(screenshot);
            id = lease?.Id ?? string.Empty;
            if (lease is not null)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromMinutes(1));
                await provider.ReleaseAsync(lease, new DeviceReleaseContext { RunId = "live", TargetName = "live", Env = env, Log = _ => { } }, cleanup.Token);
            }
        }

        var state = "gone";
        try
        {
            state = (await new LimrunApi(http, key, new Uri("https://api.limrun.com")).GetAsync(MobilePlatform.Ios, id, CancellationToken.None)).State;
        }
        catch (LimrunApiException cause) when (cause.Status == HttpStatusCode.NotFound)
        {
        }

        Assert.Contains(state, new[] { "terminated", "gone" });
    }
}
