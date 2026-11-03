// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Mobile.Lease;

/// <summary>Renews idle leases while other targets run: retaining a lease across sessions does not keep it from expiring (upstream's <c>heartbeat.ts</c>).</summary>
internal static class LeaseHeartbeat
{
    /// <summary>How long a lease lives without a renewal, in milliseconds (upstream's <c>LEASE_TTL_MS</c>).</summary>
    public const int LeaseTtlMilliseconds = 60_000;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan BeatTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Renews the lease once, so a lease that cannot be renewed fails the acquisition, then every 20 seconds until the returned
    /// function is called. A later renewal that fails is logged and retried, never raised, and a log that throws is ignored. The
    /// function cancels a renewal still in flight and returns when it has ended, so it can run before the daemon stops. The scope is the
    /// client configuration that scopes a request to the lease: <c>stateDir</c>, <c>tenant</c>, <c>runId</c>, <c>leaseId</c>,
    /// <c>leaseBackend</c>, <c>leaseProvider</c>.
    /// </summary>
    public static async Task<Func<Task>> StartAsync(
        JsonObject scope,
        DeviceClientFactory connect,
        Action<string> log,
        Func<TimeSpan, CancellationToken, Task> delay,
        CancellationToken cancellationToken)
    {
        var leaseId = scope["leaseId"] is JsonValue value && value.TryGetValue<string>(out var id) && id.Length > 0
            ? id
            : throw new InvalidOperationException("The lease scope has no lease id");

        var stateDirectory = scope["stateDir"] is JsonValue directory && directory.TryGetValue<string>(out var path) && path.Length > 0
            ? path
            : throw new InvalidOperationException("The lease scope has no state directory");

        // The lease's own stateDir selects the local daemon, whatever remote daemon the environment names.
        var client = connect(LeaseSessions.LeaseSession(stateDirectory), new DeviceConnection { Client = scope });
        var stopping = new CancellationTokenSource();

        async Task BeatAsync(CancellationToken outer)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(outer, stopping.Token);
            linked.CancelAfter(BeatTimeout);
            await client.CallAsync(DeviceMethods.LeasesHeartbeat, new JsonObject { ["leaseId"] = leaseId, ["ttlMs"] = LeaseTtlMilliseconds }, linked.Token).ConfigureAwait(false);
        }

        try
        {
            await BeatAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            stopping.Dispose();
            throw;
        }

        var loop = Task.Run(async () =>
        {
            while (!stopping.IsCancellationRequested)
            {
                try
                {
                    await delay(Interval, stopping.Token).ConfigureAwait(false);
                    await BeatAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception cause) when (!stopping.IsCancellationRequested)
                {
                    try
                    {
                        log($"lease {leaseId}: renewal failed: {cause.Message}");
                    }
                    catch (Exception)
                    {
                        // The run's log may be closed by now.
                    }
                }
                catch (Exception)
                {
                    // Stopped: a renewal cancelled in flight is the expected end.
                }
            }
        });

        async Task StopAsync()
        {
            await stopping.CancelAsync().ConfigureAwait(false);
            await loop.ConfigureAwait(false);
            stopping.Dispose();
        }

        // A release that failed is retried, and stops the heartbeat again.
        Task? stopped = null;
        return () => stopped ??= StopAsync();
    }
}
