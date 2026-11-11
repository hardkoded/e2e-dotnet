// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Docs.Examples.Mobile;
using E2E.Mobile.Lease;
using E2E.Mobile.Tests.Lease;

namespace E2E.Mobile.Tests.ProviderExample;

public sealed class ProviderExampleTests
{
    private static readonly CloudDevicesOptions Options = new("cloud", "iPhone 16", "18", "cloud://APP");

    private static DeviceRequest Request(IReadOnlyDictionary<string, string?> env) => new()
    {
        Platform = MobilePlatform.Ios,
        RunId = "run-1",
        TargetName = "ios",
        Slot = 0,
        Slots = 1,
        AgentDeviceVersion = "0.21.36",
        ProjectRoot = "/project",
        Env = env,
        Log = _ => { },
    };

    private sealed class RecordingLeases : IAgentDeviceLeases
    {
        public List<(DeviceRequest Request, AgentDeviceLeaseRequest Lease)> Acquired { get; } = [];

        public List<DeviceLease> Released { get; } = [];

        public Task<DeviceLease> AcquireAsync(DeviceRequest request, AgentDeviceLeaseRequest lease, CancellationToken cancellationToken)
        {
            Acquired.Add((request, lease));
            return Task.FromResult(new DeviceLease { Id = "lease-1" });
        }

        public Task ReleaseAsync(DeviceLease lease, CancellationToken cancellationToken)
        {
            Released.Add(lease);
            return Task.CompletedTask;
        }
    }

    // Partial: upstream also checks that release hands the leases the release context. The port's IAgentDeviceLeases.ReleaseAsync takes the lease alone
    // (the context is only the provider's), so only the lease it releases is checked.
    [Fact]
    public async Task Partial_Leases_with_the_token_the_selectors_and_a_summary_and_releases_through_the_same_leases()
    {
        var leases = new RecordingLeases();
        AgentDeviceLeaseOptions? made = null;
        var cloud = DeviceCloudProvider.CloudDevices(Options, leaseOptions =>
        {
            made = leaseOptions;
            return leases;
        });
        Assert.Equal(new AgentDeviceLeaseOptions { Provider = "cloud", Label = "cloud" }, made);

        var request = Request(new Dictionary<string, string?> { ["CLOUD_TOKEN"] = "token" });
        var lease = await cloud.AcquireAsync(request, CancellationToken.None);

        var (acquiredRequest, acquired) = Assert.Single(leases.Acquired);
        Assert.Same(request, acquiredRequest);
        Assert.Equal(new Dictionary<string, string> { ["CLOUD_TOKEN"] = "token" }, acquired.Env);
        Assert.Equal(new LeaseSelectors { Device = "iPhone 16", ProviderOsVersion = "18", ProviderApp = "cloud://APP" }, acquired.Selectors);
        Assert.Equal("iPhone 16, ios 18", acquired.Summary);

        await cloud.ReleaseAsync(lease, new DeviceReleaseContext { RunId = "run-1", TargetName = "ios", Env = request.Env, Log = request.Log }, CancellationToken.None);
        Assert.Same(lease, Assert.Single(leases.Released));
    }

    [Fact]
    public async Task Leases_nothing_without_a_token()
    {
        var leases = new RecordingLeases();
        var cloud = DeviceCloudProvider.CloudDevices(Options, _ => leases);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => cloud.AcquireAsync(Request(new Dictionary<string, string?>()), CancellationToken.None));
        Assert.Equal("CLOUD_TOKEN is not set", error.Message);
        Assert.Empty(leases.Acquired);
    }

    // Partial: upstream's worker client holds the lease scope only. The port's client also holds the selectors and the platform, which its worker
    // resolves the device from (see AgentDeviceLeasesTests). The checks of the allocation, the scope, and the release are upstream's.
    [Fact]
    public async Task Partial_Hands_a_retained_cloud_grant_to_the_worker_and_releases_the_allocated_scope()
    {
        using var host = new FakeLeaseHost();
        var cloud = DeviceCloudProvider.CloudDevices(Options, leaseOptions => new AgentDeviceLeases(leaseOptions, host));
        var request = Request(new Dictionary<string, string?> { ["CLOUD_TOKEN"] = "token" });

        var lease = await cloud.AcquireAsync(request, CancellationToken.None);

        JsonAssert.Args(
            """{"tenant":"cloud","runId":"run-1","leaseBackend":"ios-instance","leaseProvider":"cloud","ttlMs":60000,"retainOnClose":true,"platform":"ios","target":"mobile","device":"iPhone 16","providerOsVersion":"18","providerApp":"cloud://APP"}""",
            host.Starts[0].Request.Allocate);
        var stateDirectory = host.Starts[0].Request.StateDirectory;
        Assert.Equal("lease-1", lease.Id);
        JsonAssert.Args(
            $$"""{"stateDir":{{JsonValue.Create(stateDirectory)!.ToJsonString()}},"tenant":"cloud","runId":"run-1","leaseId":"lease-1","leaseBackend":"ios-instance","leaseProvider":"cloud","platform":"ios","target":"mobile","device":"iPhone 16","providerOsVersion":"18","providerApp":"cloud://APP"}""",
            lease.Client);
        Assert.Empty(host.Stops);

        await cloud.ReleaseAsync(lease, new DeviceReleaseContext { RunId = "run-1", TargetName = "ios", Env = request.Env, Log = request.Log }, CancellationToken.None);
        var stop = Assert.Single(host.Stops);
        Assert.Equal((stateDirectory, "lease-1"), (stop.StateDirectory, stop.LeaseId));
    }

    [Fact]
    public async Task Releases_a_declined_retention_grant_before_rejecting_acquisition_without_keeping_it_for_release()
    {
        using var host = new FakeLeaseHost();
        host.NextStarts.Enqueue(_ => Task.FromResult(new DriverGrant("lease-1", "cloud", "run-1", false)));
        var cloud = DeviceCloudProvider.CloudDevices(Options, leaseOptions => new AgentDeviceLeases(leaseOptions, host));
        var request = Request(new Dictionary<string, string?> { ["CLOUD_TOKEN"] = "token" });

        // The stop is awaited before the rejection, so by the time it arrives the release has finished.
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => cloud.AcquireAsync(request, CancellationToken.None));
        Assert.Contains("retainOnClose", error.Message, StringComparison.Ordinal);
        Assert.Single(host.Stops);

        await cloud.ReleaseAsync(new DeviceLease { Id = "lease-1" }, new DeviceReleaseContext { RunId = "run-1", TargetName = "ios", Env = request.Env, Log = request.Log }, CancellationToken.None);
        Assert.Single(host.Stops);
    }

    /// <summary>.NET-only: the declined grant of <see cref="Releases_a_declined_retention_grant_before_rejecting_acquisition_without_keeping_it_for_release"/> with the port's message, which names the cloud. The same stop is checked in <c>AgentDeviceLeasesTests</c>.</summary>
    [Fact]
    public async Task Stops_the_daemon_when_a_declined_retention_grant_names_the_cloud_in_its_message()
    {
        using var host = new FakeLeaseHost();
        host.NextStarts.Enqueue(_ => Task.FromResult(new DriverGrant("lease-1", "cloud", "run-1", false)));
        var cloud = DeviceCloudProvider.CloudDevices(Options, leaseOptions => new AgentDeviceLeases(leaseOptions, host));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => cloud.AcquireAsync(Request(new Dictionary<string, string?> { ["CLOUD_TOKEN"] = "token" }), CancellationToken.None));

        Assert.Equal("agent-device did not acknowledge retainOnClose. Closing the engine session could end the cloud lease before run-end release", error.Message);
        Assert.Single(host.Stops);
    }
}
