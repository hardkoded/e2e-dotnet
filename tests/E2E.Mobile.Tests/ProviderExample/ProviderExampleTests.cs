// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Docs.Examples.Mobile;
using E2E.Mobile.Lease;

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
}
