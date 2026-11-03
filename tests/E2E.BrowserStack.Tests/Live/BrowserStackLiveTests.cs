// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Mobile;

namespace E2E.BrowserStack.Tests.Live;

/// <summary>
/// A fact that runs only with a real BrowserStack account: <c>BROWSERSTACK_USERNAME</c>, <c>BROWSERSTACK_ACCESS_KEY</c>, and
/// <c>BROWSERSTACK_APP</c> (a <c>bs://</c> id of an Android build already uploaded to the account). It leases a real device.
/// </summary>
public sealed class BrowserStackLiveFactAttribute : FactAttribute
{
    public BrowserStackLiveFactAttribute()
    {
        var missing = new[] { "BROWSERSTACK_USERNAME", "BROWSERSTACK_ACCESS_KEY", "BROWSERSTACK_APP" }.Where(name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))).ToArray();
        if (missing.Length > 0)
        {
            Skip = $"{string.Join(", ", missing)} not set";
        }
    }
}

/// <summary>
/// The provider against BrowserStack itself and the real agent-device daemon. The fakes in this project cannot see what the daemon does
/// with the allocate params, so a client that left a selector out passed every other test. The .NET port has no upstream test for it.
/// Never run in CI: no account.
/// </summary>
public sealed class BrowserStackLiveTests
{
    [BrowserStackLiveFact]
    [Trait("TestCategory", "BrowserStack")]
    public async Task Leases_a_real_android_device_takes_a_snapshot_and_ends_the_session_on_release()
    {
        var env = new Dictionary<string, string?>
        {
            ["BROWSERSTACK_USERNAME"] = Environment.GetEnvironmentVariable("BROWSERSTACK_USERNAME"),
            ["BROWSERSTACK_ACCESS_KEY"] = Environment.GetEnvironmentVariable("BROWSERSTACK_ACCESS_KEY"),
            ["PATH"] = Environment.GetEnvironmentVariable("PATH"),
            ["HOME"] = Environment.GetEnvironmentVariable("HOME"),
        };
        var provider = new BrowserStackDevices(new BrowserStackDevicesOptions
        {
            Device = Environment.GetEnvironmentVariable("BROWSERSTACK_DEVICE") ?? "Google Pixel 9",
            OsVersion = Environment.GetEnvironmentVariable("BROWSERSTACK_OS_VERSION") ?? "16.0",
            App = Environment.GetEnvironmentVariable("BROWSERSTACK_APP")!,
            AppiumVersion = "3.2.0",
        });
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        DeviceLease? lease = null;
        try
        {
            lease = await provider.AcquireAsync(
                new DeviceRequest
                {
                    Platform = MobilePlatform.Android,
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

            var client = DeviceClientConfig.For("e2e-live-0", new DeviceConnection { Client = lease.Client }).Connect();
            var snapshot = await client.CallAsync(DeviceMethods.CaptureSnapshot, new JsonObject(), budget.Token);
            Assert.NotEmpty(Assert.IsType<JsonObject>(snapshot)["nodes"]!.AsArray());
        }
        finally
        {
            if (lease is not null)
            {
                // The release throws unless agent-device confirms it ended the BrowserStack session.
                using var cleanup = new CancellationTokenSource(TimeSpan.FromMinutes(1));
                await provider.ReleaseAsync(lease, new DeviceReleaseContext { RunId = "live", TargetName = "live", Env = env, Log = _ => { } }, cleanup.Token);
            }
        }
    }
}
