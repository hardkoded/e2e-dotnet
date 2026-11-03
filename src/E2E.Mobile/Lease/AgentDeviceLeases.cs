// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace E2E.Mobile.Lease;

/// <summary>
/// The device a lease asks the service for, and the <c>provider*</c> capabilities its session starts with (upstream's
/// <c>LeaseSelectors</c>, a pick of the client configuration's keys).
/// </summary>
public sealed record LeaseSelectors
{
    /// <summary>The device, as the service's catalog names it.</summary>
    public required string Device { get; init; }

    public string? ProviderApp { get; init; }

    public string? ProviderOsVersion { get; init; }

    public string? ProviderDeviceType { get; init; }

    public string? ProviderProject { get; init; }

    public string? ProviderBuild { get; init; }

    public string? ProviderSessionName { get; init; }

    public string? ProviderDeviceOrientation { get; init; }

    public string? ProviderGeoLocation { get; init; }

    public string? ProviderTimezone { get; init; }

    public string? ProviderAppiumVersion { get; init; }

    public string? ProviderLanguage { get; init; }

    public string? ProviderLocale { get; init; }

    public string? ProviderNetworkProfile { get; init; }

    public string? ProviderCustomNetwork { get; init; }

    /// <summary>Sent only when true: a flag is allocated only when set.</summary>
    public bool? ProviderNoResignApp { get; init; }

    /// <summary>The selectors as the agent-device client configuration and the allocate call spell them. A key that is not set is left out.</summary>
    public JsonObject ToJson()
    {
        var result = new JsonObject { ["device"] = Device };
        void Put(string key, string? value)
        {
            if (value is not null)
            {
                result[key] = value;
            }
        }

        Put("providerApp", ProviderApp);
        Put("providerOsVersion", ProviderOsVersion);
        Put("providerDeviceType", ProviderDeviceType);
        Put("providerProject", ProviderProject);
        Put("providerBuild", ProviderBuild);
        Put("providerSessionName", ProviderSessionName);
        Put("providerDeviceOrientation", ProviderDeviceOrientation);
        Put("providerGeoLocation", ProviderGeoLocation);
        Put("providerTimezone", ProviderTimezone);
        Put("providerAppiumVersion", ProviderAppiumVersion);
        Put("providerLanguage", ProviderLanguage);
        Put("providerLocale", ProviderLocale);
        Put("providerNetworkProfile", ProviderNetworkProfile);
        Put("providerCustomNetwork", ProviderCustomNetwork);
        if (ProviderNoResignApp is { } noResign)
        {
            result["providerNoResignApp"] = noResign;
        }

        return result;
    }
}

/// <summary>One service agent-device leases devices from.</summary>
public sealed record AgentDeviceLeaseOptions
{
    /// <summary>agent-device's name for the service, as the lease provider and tenant: <c>browserstack</c>.</summary>
    public required string Provider { get; init; }

    /// <summary>The service's name in errors: <c>BrowserStack</c>.</summary>
    public required string Label { get; init; }
}

/// <summary>What one <see cref="IAgentDeviceLeases.AcquireAsync"/> allocates.</summary>
public sealed record AgentDeviceLeaseRequest
{
    /// <summary>Set on the daemon's environment over the run's: the service's credentials.</summary>
    public required IReadOnlyDictionary<string, string> Env { get; init; }

    /// <summary>Allocated with the lease and put on its <see cref="DeviceLease.Client"/>, so the worker's session matches it.</summary>
    public required LeaseSelectors Selectors { get; init; }

    /// <summary>What the progress line names once the lease is granted, after <c>lease &lt;id&gt;: </c>.</summary>
    public required string Summary { get; init; }

    /// <summary>Runs on the daemon's empty agent-device home before it starts, to install a plugin there.</summary>
    public Func<string, CancellationToken, Task>? PrepareHome { get; init; }
}

/// <summary>
/// <c>AcquireAsync</c> and <c>ReleaseAsync</c> of a <see cref="IDeviceProvider"/> backed by agent-device leases (upstream's
/// <c>AgentDeviceLeases</c> type; the <c>I</c> prefix is C# casing).
/// </summary>
public interface IAgentDeviceLeases
{
    Task<DeviceLease> AcquireAsync(DeviceRequest request, AgentDeviceLeaseRequest lease, CancellationToken cancellationToken);

    /// <summary>Stops the lease's daemon, and throws unless agent-device confirms it ended the lease's session.</summary>
    Task ReleaseAsync(DeviceLease lease, CancellationToken cancellationToken);
}

/// <summary>
/// Leases from one service for a <see cref="IDeviceProvider"/> (upstream's <c>agentDeviceLeases()</c>, <c>@e2e-dev/mobile/lease</c>):
/// one isolated agent-device daemon per lease, allocated with <c>retainOnClose</c> so closing the engine session keeps it, renewed
/// while held, and stopped on release, which ends the session at the service. The daemon the provider starts is the pinned release.
/// </summary>
public sealed class AgentDeviceLeases : IAgentDeviceLeases
{
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(30);

    private readonly AgentDeviceLeaseOptions _options;
    private readonly ILeaseHost _host;
    private readonly ConcurrentDictionary<string, Held> _held = new(StringComparer.Ordinal);

    public AgentDeviceLeases(AgentDeviceLeaseOptions options)
        : this(options, LeaseDaemon.Instance)
    {
    }

    internal AgentDeviceLeases(AgentDeviceLeaseOptions options, ILeaseHost host)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _host = host;
    }

    /// <summary>One lease's daemon. <see cref="Ended"/> once its stop confirmed the session ended, so a retried release only cleans up.</summary>
    private sealed class Held(string directory, string stateDirectory, IReadOnlyDictionary<string, string?> environment)
    {
        public string Directory { get; } = directory;

        public string StateDirectory { get; } = stateDirectory;

        public IReadOnlyDictionary<string, string?> Environment { get; } = environment;

        public Func<Task>? StopHeartbeat { get; set; }

        public Task? Releasing { get; set; }

        public bool Ended { get; set; }
    }

    public async Task<DeviceLease> AcquireAsync(DeviceRequest request, AgentDeviceLeaseRequest lease, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(lease);

        // The daemon the provider starts is the pinned release, and a client that speaks another would read it wrongly.
        if (request.AgentDeviceVersion != AgentDeviceVersion.Pinned)
        {
            throw new InvalidOperationException($"The {_options.Label} provider starts agent-device {AgentDeviceVersion.Pinned}, but the engine asked for {request.AgentDeviceVersion}.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var directory = Directory.CreateTempSubdirectory($"e2e-{_options.Provider}-").FullName;
        var home = Path.Combine(directory, "home");
        var resource = new Held(directory, Path.Combine(directory, "state"), DaemonEnvironment(request.Env, lease.Env, home));
        var leaseBackend = request.Platform == MobilePlatform.Ios ? "ios-instance" : "android-instance";
        var allocated = new JsonObject { ["platform"] = request.Platform.WireName(), ["target"] = "mobile" };
        foreach (var (key, value) in lease.Selectors.ToJson())
        {
            allocated[key] = value?.DeepClone();
        }

        string? id = null;
        try
        {
            if (lease.PrepareHome is { } prepareHome)
            {
                await prepareHome(home, cancellationToken).ConfigureAwait(false);
            }

            var allocate = new JsonObject
            {
                ["tenant"] = _options.Provider,
                ["runId"] = request.RunId,
                ["leaseBackend"] = leaseBackend,
                ["leaseProvider"] = _options.Provider,
                ["ttlMs"] = LeaseHeartbeat.LeaseTtlMilliseconds,

                // Retain the provider lease when closeApp or cleanup closes the engine session.
                // The provider releases the lease at run end.
                ["retainOnClose"] = true,
            };
            foreach (var (key, value) in allocated)
            {
                allocate[key] = value?.DeepClone();
            }

            var grant = await _host.StartDriverAsync(new DriverRequest(resource.StateDirectory, allocate), resource.Environment, _options.Label, cancellationToken).ConfigureAwait(false);
            id = grant.LeaseId;
            _held[id] = resource;
            cancellationToken.ThrowIfCancellationRequested();
            if (!grant.RetainOnClose)
            {
                throw new InvalidOperationException($"agent-device did not acknowledge retainOnClose. Closing the engine session could end the {_options.Label} lease before run-end release");
            }

            var scope = new JsonObject
            {
                ["tenant"] = grant.TenantId,
                ["runId"] = grant.RunId,
                ["leaseId"] = grant.LeaseId,
                ["leaseBackend"] = leaseBackend,
                ["leaseProvider"] = _options.Provider,
            };

            // The heartbeat's client is created with the state directory beside the scope.
            var heartbeatScope = new JsonObject { ["stateDir"] = resource.StateDirectory };
            foreach (var (key, value) in scope)
            {
                heartbeatScope[key] = value?.DeepClone();
            }

            resource.StopHeartbeat = await _host.StartHeartbeatAsync(heartbeatScope, request.Log, cancellationToken).ConfigureAwait(false);
            request.Log($"lease {id}: {lease.Summary}");

            // The worker's client is created with these fields: the scope picks the lease, and the selectors match the session it holds.
            var client = new JsonObject { ["stateDir"] = resource.StateDirectory };
            foreach (var (key, value) in scope)
            {
                client[key] = value?.DeepClone();
            }

            foreach (var (key, value) in allocated)
            {
                client[key] = value?.DeepClone();
            }

            return new DeviceLease { Id = id, Client = client };
        }
        catch (Exception cause)
        {
            // The engine releases only leases AcquireAsync returned. Not the request's token: cleanup is owed after an interrupt too.
            using var cleanup = new CancellationTokenSource(CleanupTimeout);
            try
            {
                await ReleaseAsync(id, resource, cleanup.Token).ConfigureAwait(false);
            }
            catch (Exception cleanupCause)
            {
                throw new AggregateException($"{_options.Label} lease failed: {cause.Message}; cleanup also failed: {cleanupCause.Message}", cause, cleanupCause);
            }

            throw;
        }
    }

    public async Task ReleaseAsync(DeviceLease lease, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if (_held.TryGetValue(lease.Id, out var resource))
        {
            await ReleaseAsync(lease.Id, resource, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>One release per lease, shared by every caller; a failed one may be tried again.</summary>
    private async Task ReleaseAsync(string? id, Held resource, CancellationToken cancellationToken)
    {
        Task releasing;
        lock (resource)
        {
            releasing = resource.Releasing ??= Task.Run(() => StopAsync(id, resource, cancellationToken), CancellationToken.None);
        }

        try
        {
            await releasing.ConfigureAwait(false);
        }
        catch (Exception)
        {
            lock (resource)
            {
                if (ReferenceEquals(resource.Releasing, releasing))
                {
                    resource.Releasing = null;
                }
            }

            throw;
        }
    }

    private async Task StopAsync(string? id, Held resource, CancellationToken cancellationToken)
    {
        if (resource.StopHeartbeat is { } stopHeartbeat)
        {
            await stopHeartbeat().ConfigureAwait(false);
        }

        if (!resource.Ended)
        {
            await _host.StopDriverAsync(resource.StateDirectory, resource.Environment, id, _options.Label, cancellationToken).ConfigureAwait(false);
            resource.Ended = true;
        }

        // `daemon stop --clean` may have removed it already.
        if (Directory.Exists(resource.Directory))
        {
            _host.DeleteDirectory(resource.Directory);
        }

        if (id is not null)
        {
            _held.TryRemove(id, out _);
        }
    }

    /// <summary>
    /// The environment for a daemon in <paramref name="home"/>: the run's, with the service's credentials over it, and no daemon
    /// setting of the user's that would select a different daemon, home, or connection profile. The user's daemon policy stays, as
    /// it applies to the daemon and its clients alike.
    /// </summary>
    private static Dictionary<string, string?> DaemonEnvironment(IReadOnlyDictionary<string, string?> run, IReadOnlyDictionary<string, string> service, string home)
    {
        var result = new Dictionary<string, string?>(run, StringComparer.Ordinal);
        foreach (var (key, value) in service)
        {
            result[key] = value;
        }

        foreach (var key in result.Keys.ToArray())
        {
            if (key != "AGENT_DEVICE_DAEMON_POLICY" && key.StartsWith("AGENT_DEVICE_", StringComparison.Ordinal))
            {
                result.Remove(key);
            }
        }

        result["AGENT_DEVICE_HOME"] = home;
        return result;
    }
}
