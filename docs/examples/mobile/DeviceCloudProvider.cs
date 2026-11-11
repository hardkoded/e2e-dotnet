// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Mobile;
using E2E.Mobile.Lease;

namespace E2E.Docs.Examples.Mobile;

/// <summary>What <see cref="DeviceCloudProvider.CloudDevices"/> leases: a device of a cloud agent-device itself speaks to.</summary>
/// <param name="Provider">agent-device's name for the cloud.</param>
/// <param name="StateDir">The directory the run's leases keep their state in: each run's leases share <c>&lt;StateDir&gt;/&lt;runId&gt;</c>.</param>
public sealed record CloudDevicesOptions(string Provider, string StateDir);

/// <summary>
/// Upstream's <c>docs/examples/mobile/device-cloud-provider.ts</c>, which exports <c>cloudDevices</c>. C# has no free function, so
/// the holder is a static class (.NET-only name).
/// </summary>
public static class DeviceCloudProvider
{
    /// <summary>
    /// Leases devices from a device cloud agent-device itself speaks to. Each worker slot gets its own agent-device daemon, whose
    /// runtime for the cloud starts the session when the lease is allocated and ends it on release. The cloud picks the device.
    /// <c>Provider</c> is agent-device's name for the cloud. <paramref name="leasesFactory"/> (.NET-only) makes the leases, so a test
    /// can stand in for the service.
    /// </summary>
    public static IDeviceProvider CloudDevices(CloudDevicesOptions options, Func<AgentDeviceLeaseOptions, IAgentDeviceLeases>? leasesFactory = null)
    {
        var leases = (leasesFactory ?? (leaseOptions => new AgentDeviceLeases(leaseOptions)))(
            new AgentDeviceLeaseOptions { Provider = options.Provider, Label = options.Provider, StateDir = options.StateDir });
        return new CloudDeviceProvider(options, leases);
    }

    private sealed class CloudDeviceProvider(CloudDevicesOptions options, IAgentDeviceLeases leases) : IDeviceProvider
    {
        public string Name => options.Provider;

        public Task<DeviceLease> AcquireAsync(DeviceRequest request, CancellationToken cancellationToken) =>
            leases.AcquireAsync(
                request,
                new AgentDeviceLeaseRequest
                {
                    // The daemon's runtime reads the cloud's credentials from the run's environment.
                    Env = new Dictionary<string, string>(),
                    Summary = request.Platform.WireName(),
                },
                cancellationToken);

        public Task ReleaseAsync(DeviceLease lease, DeviceReleaseContext context, CancellationToken cancellationToken) =>
            leases.ReleaseAsync(lease, context, cancellationToken);
    }
}
