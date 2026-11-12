// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Docs.Examples.Mobile;
using E2E.Mobile.Lease;
using E2E.Mobile.Tests.Lease;

namespace E2E.Mobile.Tests.ProviderExample;

/// <summary>
/// Ports <c>packages/mobile/tests/unit/provider-example.test.ts</c>: the example's <c>cloudDevices</c> against a stubbed service. The
/// service is <see cref="FakeLeaseHost"/> (the daemon the lease starts), where upstream stubs <c>agent-device</c>'s client.
/// </summary>
public sealed class ProviderExampleTests
{
    private static readonly string StateDir = Path.Combine(Path.GetTempPath(), "state");

    private static readonly CloudDevicesOptions Options = new("cloud", StateDir);

    private static readonly DeviceReleaseContext ReleaseContext = new() { RunId = "run-1", TargetName = "ios", Env = new Dictionary<string, string?>(), Log = _ => { } };

    private static DeviceRequest Request() => new()
    {
        Platform = MobilePlatform.Ios,
        RunId = "run-1",
        TargetName = "ios",
        Slot = 0,
        Slots = 1,
        AgentDeviceVersion = "0.21.36",
        ProjectRoot = "/project",
        Env = new Dictionary<string, string?>(),
        Log = _ => { },
    };

    [Fact]
    public async Task Hands_a_retained_cloud_grant_to_the_worker_and_releases_the_allocated_scope()
    {
        using var host = new FakeLeaseHost();
        host.NextStarts.Enqueue(_ => Task.FromResult(new DriverGrant("allocated-lease", "allocated-tenant", "run-1", true)));
        var cloud = DeviceCloudProvider.CloudDevices(Options, leaseOptions => new AgentDeviceLeases(leaseOptions, host));

        var lease = await cloud.AcquireAsync(Request(), CancellationToken.None);

        JsonAssert.Args(
            """{"tenant":"cloud","runId":"run-1","leaseBackend":"ios-instance","leaseProvider":"cloud","retainOnClose":true,"platform":"ios","target":"mobile"}""",
            host.Starts[0].Request.Allocate);
        var stateDirectory = Path.Combine(StateDir, "run-1");
        Assert.Equal("allocated-lease", lease.Id);
        JsonAssert.Args(
            $$"""{"stateDir":{{JsonValue.Create(stateDirectory)!.ToJsonString()}},"tenant":"allocated-tenant","runId":"run-1","leaseId":"allocated-lease","leaseBackend":"ios-instance","leaseProvider":"cloud"}""",
            lease.Client);
        Assert.Empty(host.Stops);

        await cloud.ReleaseAsync(lease, ReleaseContext, CancellationToken.None);
        var stop = Assert.Single(host.Stops);
        Assert.Equal((stateDirectory, "allocated-lease"), (stop.StateDirectory, stop.LeaseId));
    }

    [Fact]
    public async Task Releases_a_declined_retention_grant_before_rejecting_acquisition_without_keeping_it_for_release()
    {
        using var host = new FakeLeaseHost();
        host.NextStarts.Enqueue(_ => Task.FromResult(new DriverGrant("allocated-lease", "allocated-tenant", "run-1", false)));
        var stopping = new TaskCompletionSource();
        var finishRelease = new TaskCompletionSource();
        host.Stop = (_, _) =>
        {
            stopping.TrySetResult();
            return finishRelease.Task;
        };
        var cloud = DeviceCloudProvider.CloudDevices(Options, leaseOptions => new AgentDeviceLeases(leaseOptions, host));

        var acquiring = cloud.AcquireAsync(Request(), CancellationToken.None);
        var settled = acquiring.ContinueWith(_ => { }, TaskScheduler.Default);

        // The release runs before the rejection, and the rejection waits for it.
        await stopping.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(acquiring.IsCompleted);
        finishRelease.SetResult();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => acquiring);
        await settled;
        Assert.Contains("retainOnClose", error.Message, StringComparison.Ordinal);
        Assert.Single(host.Stops);

        await cloud.ReleaseAsync(new DeviceLease { Id = "allocated-lease" }, ReleaseContext, CancellationToken.None);
        Assert.Single(host.Stops);
    }

    /// <summary>.NET-only: the declined grant of <see cref="Releases_a_declined_retention_grant_before_rejecting_acquisition_without_keeping_it_for_release"/> with the port's message, which names the cloud. The same stop is checked in <c>AgentDeviceLeasesTests</c>.</summary>
    [Fact]
    public async Task Stops_the_daemon_when_a_declined_retention_grant_names_the_cloud_in_its_message()
    {
        using var host = new FakeLeaseHost();
        host.NextStarts.Enqueue(_ => Task.FromResult(new DriverGrant("lease-1", "cloud", "run-1", false)));
        var cloud = DeviceCloudProvider.CloudDevices(Options, leaseOptions => new AgentDeviceLeases(leaseOptions, host));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => cloud.AcquireAsync(Request(), CancellationToken.None));

        Assert.Equal("agent-device did not acknowledge retainOnClose. Closing the engine session could end the cloud lease before run-end release", error.Message);
        Assert.Single(host.Stops);
    }
}
