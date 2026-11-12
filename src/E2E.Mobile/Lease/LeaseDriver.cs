// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Mobile.Lease;

/// <summary>One lease allocation from the daemon in <see cref="StateDirectory"/> (upstream's <c>DriverRequest</c>, <c>lease/driver.ts</c>).</summary>
/// <param name="StateDirectory">The state directory of the daemon to allocate from.</param>
/// <param name="Allocate">The allocate call's options: tenant, run, backend, provider, lease life, and the device selectors.</param>
internal sealed record DriverRequest(string StateDirectory, JsonObject Allocate);

/// <summary>What the daemon granted (upstream's <c>DriverGrant</c>).</summary>
internal sealed record DriverGrant(string LeaseId, string TenantId, string RunId, bool RetainOnClose);

/// <summary>
/// Allocates a lease from a daemon (upstream's <c>driver.ts</c>). Upstream runs it in a child process so the daemon gets that
/// process's environment. Here the daemon is its own process already, started with its environment, so this runs in the runner.
/// </summary>
internal static class LeaseDriver
{
    public static async Task<DriverGrant> AllocateAsync(DriverRequest request, DeviceClientFactory connect, CancellationToken cancellationToken)
    {
        var client = connect(LeaseSessions.LeaseSession(request.StateDirectory), new DeviceConnection { Client = new JsonObject { ["stateDir"] = request.StateDirectory } });
        var lease = await client.CallAsync(DeviceMethods.LeasesAllocate, (JsonObject)request.Allocate.DeepClone(), cancellationToken).ConfigureAwait(false) as JsonObject
            ?? throw new InvalidOperationException("agent-device returned no lease");
        return new DriverGrant(
            Required(lease, "leaseId"),
            Required(lease, "tenantId"),
            Required(lease, "runId"),
            lease["retainOnClose"] is JsonValue retained && retained.TryGetValue<bool>(out var keep) && keep);
    }

    private static string Required(JsonObject lease, string name) =>
        lease[name] is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0
            ? text
            : throw new InvalidOperationException($"Invalid lease response from daemon: no {name}");
}
