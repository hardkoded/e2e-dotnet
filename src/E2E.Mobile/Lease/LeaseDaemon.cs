// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Mobile.Lease;

/// <summary>
/// What a lease needs of the machine it runs on: the daemon beside it, its renewals, and its directory. A test swaps it for a fake.
/// .NET-only: upstream's tests replace the functions of <c>daemon.ts</c> and <c>heartbeat.ts</c> by mocking their modules.
/// </summary>
internal interface ILeaseHost
{
    /// <summary>Starts the daemon of <paramref name="request"/> with <paramref name="environment"/> and allocates a lease from it.</summary>
    Task<DriverGrant> StartDriverAsync(DriverRequest request, IReadOnlyDictionary<string, string?> environment, string label, CancellationToken cancellationToken);

    /// <summary>Stops the daemon, which ends its leases' sessions, and throws unless it confirms they ended.</summary>
    Task StopDriverAsync(string stateDirectory, IReadOnlyDictionary<string, string?> environment, string? leaseId, string label, CancellationToken cancellationToken);

    /// <summary>Starts renewing the lease; the function it returns stops that and waits for it.</summary>
    Task<Func<Task>> StartHeartbeatAsync(JsonObject scope, Action<string> log, CancellationToken cancellationToken);

    void DeleteDirectory(string directory);
}

/// <summary>The real host (upstream's <c>daemon.ts</c>): a local agent-device daemon per lease, started with that lease's credentials alone.</summary>
internal sealed class LeaseDaemon : ILeaseHost
{
    /// <summary>The longest an allocation may take. The daemon bounds it too; this keeps a daemon that never answers from holding the run.</summary>
    private static readonly TimeSpan AllocateTimeout = TimeSpan.FromMinutes(15);

    private static readonly DeviceClientFactory Connect = static (session, connection) => DeviceClientConfig.For(session, connection).Connect();

    public static LeaseDaemon Instance { get; } = new();

    /// <summary>
    /// Allocates a lease from a fresh daemon, which gets its own credentials and home without changing the runner's environment.
    /// Not cancellable once the allocation starts: an interrupted allocation can still grant a lease, which the caller then releases.
    /// </summary>
    public async Task<DriverGrant> StartDriverAsync(DriverRequest request, IReadOnlyDictionary<string, string?> environment, string label, CancellationToken cancellationToken)
    {
        await AgentDeviceDaemon.LocalAsync(request.StateDirectory, cancellationToken, environment).ConfigureAwait(false);
        using var bound = new CancellationTokenSource(AllocateTimeout);
        try
        {
            return await LeaseDriver.AllocateAsync(request, Connect, bound.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException cause) when (bound.IsCancellationRequested)
        {
            throw new TimeoutException($"{label} driver did not finish allocating within {AllocateTimeout.TotalMinutes:0} minutes", cause);
        }
    }

    /// <summary>
    /// Stops the daemon, which releases its leases and so ends their sessions at the service, and throws unless it confirms they
    /// ended. <paramref name="leaseId"/> is the lease the daemon granted, when it granted one.
    /// </summary>
    public async Task StopDriverAsync(string stateDirectory, IReadOnlyDictionary<string, string?> environment, string? leaseId, string label, CancellationToken cancellationToken)
    {
        var report = await AgentDeviceDaemon.StopReportAsync(stateDirectory, environment, cancellationToken).ConfigureAwait(false);
        AssertSessionsEnded(report, leaseId, label);
    }

    public Task<Func<Task>> StartHeartbeatAsync(JsonObject scope, Action<string> log, CancellationToken cancellationToken) =>
        LeaseHeartbeat.StartAsync(scope, Connect, log, static (delay, token) => Task.Delay(delay, token), cancellationToken);

    public void DeleteDirectory(string directory) => Directory.Delete(directory, recursive: true);

    /// <summary>
    /// Throws unless <c>daemon stop --json</c> reports every session ended. A forced stop or a lost shutdown report comes back as
    /// <c>status: "unknown"</c> with <c>pending: null</c>: the session may still be running, and billed.
    /// <para>
    /// A granted lease also needs the report to list it as released. A daemon that is not running reports nothing pending
    /// (<c>mode: "not-running"</c>), and so does one started after the granting daemon died, while the session may still be open.
    /// Without <paramref name="leaseId"/>, before anything was granted, such a report means there is nothing to release.
    /// </para>
    /// </summary>
    public static void AssertSessionsEnded(string stdout, string? leaseId, string label)
    {
        JsonNode? report;
        try
        {
            report = JsonNode.Parse(stdout);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"agent-device stopped without a readable shutdown report; {label} sessions may still be running");
        }

        var data = Field(report, "data");
        var releases = Field(data, "providerReleases");
        var pending = Field(releases, "pending") as JsonArray;
        if (pending is { Count: > 0 })
        {
            throw new InvalidOperationException($"agent-device stopped without ending {pending.Count} {label} session(s)");
        }

        if (Text(Field(releases, "status")) != "completed" || pending is null)
        {
            var warnings = Field(data, "warnings") is JsonArray list ? string.Join(' ', list.Select(Text).OfType<string>()) : "";
            throw new InvalidOperationException($"agent-device could not confirm its {label} sessions ended{(warnings.Length == 0 ? "" : ": " + warnings)}");
        }

        if (leaseId is null)
        {
            return;
        }

        if (Field(releases, "released") is JsonArray released && released.Any(entry => Text(Field(entry, "leaseId")) == leaseId))
        {
            return;
        }

        var what = Text(Field(data, "mode")) == "not-running" ? $"found no daemon running for lease {leaseId}" : $"stopped without listing lease {leaseId} as released";
        throw new InvalidOperationException($"agent-device {what}, so nothing confirms its {label} session ended");
    }

    /// <summary>A property of a parsed JSON value, or null when the value is not an object.</summary>
    private static JsonNode? Field(JsonNode? node, string key) => (node as JsonObject)?[key];

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
