// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Mobile.DevFlow;

/// <summary>Where the DevFlow agent of the app under test is, which device runs it, and how long to wait for it.</summary>
public sealed record DevFlowAgentsOptions
{
    /// <summary>The host the agent listens on. The agent binds loopback only, so this is <c>localhost</c> unless a tunnel forwards it.</summary>
    public string Host { get; init; } = "localhost";

    /// <summary>The agent's port. Set, it wins over the broker; unset, the broker is asked, then <see cref="DevFlowAgents.DefaultAgentPort"/>.</summary>
    public int? Port { get; init; }

    /// <summary>Whether to ask a running DevFlow broker for the agent's port. The broker is never started from here.</summary>
    public bool UseBroker { get; init; } = true;

    /// <summary>The broker's port.</summary>
    public int BrokerPort { get; init; } = DevFlowAgents.DefaultBrokerPort;

    /// <summary>The app name the broker lists, to pick among several agents of the platform.</summary>
    public string? AppName { get; init; }

    /// <summary>How long prepare waits for the agent to answer: the time the app takes to start.</summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Whether the provider drives the device: it installs the target's build, launches the app when it is not running,
    /// relaunches and terminates it on request, and forwards the agent's port on Android, through <c>xcrun simctl</c>
    /// and <c>adb</c>. Off, the provider attaches to an app you launched, and those verbs are unsupported.
    /// </summary>
    public bool Launch { get; init; } = true;

    /// <summary>
    /// The device that runs the app, when <see cref="Launch"/> is on: a simulator UDID or name, or an adb serial. Unset,
    /// the one booted device of the platform.
    /// </summary>
    public string? Device { get; init; }
}

/// <summary>
/// The provider for a MAUI app running the DevFlow agent (<c>Microsoft.Maui.DevFlow.Agent</c>, registered with
/// <c>builder.AddMauiDevFlowAgent()</c>): it finds the agent, claims its mutation lease for the run, and makes the
/// <see cref="IDeviceClient"/> that drives the app through it. Pass it as <see cref="MobileOptions.Provider"/>.
/// <para>
/// The agent lives inside the app, so the device-side verbs go through the simulator's and Android's own tools when
/// <see cref="DevFlowAgentsOptions.Launch"/> is on: the build is installed and the app launched at prepare, and a
/// test can relaunch or close it. Off, the provider attaches to an app that is already running. There is no upstream
/// counterpart: every type in this package is .NET-only.
/// </para>
/// </summary>
public sealed class DevFlowAgents : IDeviceProvider, IDeviceClientSource
{
    /// <summary>The port the agent falls back to without a broker or a <c>.mauidevflow</c> config.</summary>
    public const int DefaultAgentPort = 9223;

    /// <summary>The well-known port of the DevFlow broker.</summary>
    public const int DefaultBrokerPort = 19223;

    /// <summary>How long the probe for an app that may already be running waits before the provider launches it. Tests shorten it.</summary>
    internal TimeSpan RunningProbe { get; init; } = TimeSpan.FromSeconds(3);

    private readonly DevFlowAgentsOptions _options;
    private readonly HttpClient _http;
    private readonly AgentDiscovery _discovery;
    private readonly IDeviceTools _tools;

    public DevFlowAgents(DevFlowAgentsOptions? options = null)
        : this(options, new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) }) { Timeout = Timeout.InfiniteTimeSpan })
    {
    }

    /// <summary>Over a client and tools of the caller's making: the seam unit tests script.</summary>
    internal DevFlowAgents(DevFlowAgentsOptions? options, HttpClient http, AgentDiscovery? discovery = null, IDeviceTools? tools = null)
    {
        _options = options ?? new DevFlowAgentsOptions();
        if (_options.Port is { } port && (port <= 0 || port > ushort.MaxValue))
        {
            throw new ConfigurationException("INVALID_CONFIG", $"devflow: `Port` must be between 1 and {ushort.MaxValue}, not {port}");
        }

        if (_options.ConnectTimeout <= TimeSpan.Zero)
        {
            throw new ConfigurationException("INVALID_CONFIG", "devflow: `ConnectTimeout` must be positive");
        }

        _http = http;
        _discovery = discovery ?? new AgentDiscovery(http, _options);
        _tools = tools ?? DeviceTools.Instance;
    }

    public string Name => "devflow";

    public DevFlowAgentsOptions Options => _options;

    /// <summary>
    /// Finds the agent for the slot, waits for it to answer, checks that it runs the target's platform and app, and
    /// claims its mutation lease for the run. With <see cref="DevFlowAgentsOptions.Launch"/>, the device is found
    /// first, the build installed when the target names one, and the app launched when its agent does not answer. The
    /// lease, the endpoint, and the device travel to the worker in the lease's client configuration.
    /// </summary>
    public async Task<DeviceLease> AcquireAsync(DeviceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Slots > 1)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                $"devflow: target \"{request.TargetName}\" asks for {request.Slots} devices, but one DevFlow agent is one running app; run the target with one worker");
        }

        var lease = new LeaseIdentity($"e2e-{request.RunId}-{request.Slot}", "e2e", $"e2e {request.TargetName} slot {request.Slot + 1} of {request.Slots}");
        try
        {
            var device = _options.Launch ? await AppLaunchers.ForAsync(request.Platform, _options.Device, _tools, cancellationToken).ConfigureAwait(false) : null;
            var client = new DevFlowAgentClient(_http, _discovery.ResolveAsync, lease) { PlatformHint = request.Platform };
            string? installed = null;
            if (device is not null)
            {
                if (_options.UseBroker && _options.Port is null)
                {
                    await device.ReverseAsync(_options.BrokerPort, cancellationToken).ConfigureAwait(false);
                }

                if (request.AppPath is { } build)
                {
                    request.Log($"installing {build} on {device.DeviceId}");
                    await device.InstallAsync(build, request.App, reinstall: false, cancellationToken).ConfigureAwait(false);
                    installed = request.App;
                }
            }

            var control = device is null ? null : new AppControl(device, _discovery, _options.ConnectTimeout, RunningProbe);
            var status = control is null
                ? await WaitAsync(client, request.Log, cancellationToken).ConfigureAwait(false)
                : await control.ProbeAsync(client, device!, cancellationToken).ConfigureAwait(false);
            if (status is null && control is not null)
            {
                var app = request.App ?? throw new ConfigurationException("INVALID_CONFIG", $"devflow: target \"{request.TargetName}\" needs app.bundleId to launch the app");
                request.Log($"launching {app} on {device!.DeviceId}");
                await device.LaunchAsync(app, [], cancellationToken).ConfigureAwait(false);
                request.Log("waiting for the DevFlow agent of the launched app");
                status = await control.WaitForAgentAsync(client, device, cancellationToken).ConfigureAwait(false);
            }

            var endpoint = client.Resolved ?? await client.EndpointAsync(cancellationToken).ConfigureAwait(false);
            var platform = DevFlowDeviceClient.PlatformOf(status);
            if (platform is not null && !string.Equals(platform, request.Platform.WireName(), StringComparison.OrdinalIgnoreCase))
            {
                throw DevFlowErrors.Failed($"the DevFlow agent at {endpoint} runs a {platform} app, but target \"{request.TargetName}\" is {request.Platform.WireName()}");
            }

            var packageId = Text(status?["app"]?["packageId"]);
            var appName = Text(status?["app"]?["name"]);
            if (request.App is { } wanted && packageId is not null && !string.Equals(packageId, wanted, StringComparison.OrdinalIgnoreCase) && !string.Equals(appName, wanted, StringComparison.OrdinalIgnoreCase))
            {
                throw DevFlowErrors.Failed($"the DevFlow agent at {endpoint} runs {packageId}, not {wanted}; launch the right app or fix the target's bundle id");
            }

            await client.ClaimLeaseAsync(cancellationToken).ConfigureAwait(false);
            request.Log($"attached to {appName ?? packageId ?? "the app"} ({platform ?? request.Platform.WireName()}) at {endpoint}");
            var config = new JsonObject
            {
                ["host"] = endpoint.Host,
                ["port"] = endpoint.Port,
                ["leaseId"] = lease.LeaseId,
                ["label"] = lease.Label,
            };
            if (device is not null)
            {
                config["device"] = device.DeviceId;
            }

            return new DevFlowLease
            {
                Id = endpoint.Authority,
                Device = device is null ? appName ?? packageId : device.DeviceId,
                Client = config,
                InstalledApp = installed,
                Endpoint = endpoint,
                Lease = lease,
                Launcher = device,
            };
        }
        catch (DevFlowAgentException failure)
        {
            throw DevFlowErrors.Translate(failure, "acquire");
        }
    }

    /// <summary>Releases the mutation lease and removes the Android forward and reverse, so another host can drive the app. The app keeps running.</summary>
    public async Task ReleaseAsync(DeviceLease lease, DeviceReleaseContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(context);
        if (lease is not DevFlowLease { Endpoint: { } endpoint, Lease: { } identity } held)
        {
            return;
        }

        try
        {
            await new DevFlowAgentClient(_http, endpoint, identity).ReleaseLeaseAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DevFlowAgentException failure)
        {
            // The app may be gone already, and a lease with no holder lapses on its own.
            context.Log($"the mutation lease was not released ({failure.Message})");
        }

        if (held.Launcher is { } device)
        {
            try
            {
                await device.RemoveForwardAsync(endpoint.Port, cancellationToken).ConfigureAwait(false);
                if (_options.UseBroker && _options.Port is null)
                {
                    await device.RemoveReverseAsync(_options.BrokerPort, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (AgentDeviceError failure)
            {
                context.Log($"the port forwards were not removed ({failure.Message})");
            }
        }
    }

    /// <summary>
    /// The client for one session. With a binding, the agent is the one prepare found, and the device the one it drove.
    /// Without one (a run without prepare, as under the NUnit adapter), the agent is found on the session's first call
    /// and the session claims the lease itself; the device is found when a verb first needs it. The seam passes no
    /// platform: <c>devices.boot</c> names it, so the client resolves and checks it there.
    /// </summary>
    public IDeviceClient CreateClient(string session, DeviceConnection? connection)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (connection?.Client is { } bound && Text(bound["host"]) is { } host && bound["port"]?.GetValue<int>() is { } port and > 0)
        {
            var identity = Text(bound["leaseId"]) is { } leaseId ? new LeaseIdentity(leaseId, "e2e", Text(bound["label"])) : null;
            var known = new Uri($"http://{host}:{port}/");
            var agent = new DevFlowAgentClient(_http, _discovery.ResolveAsync, identity, known);
            var control = Text(bound["device"]) is { } deviceId
                ? new AppControl((platform, _) => Task.FromResult(AppLaunchers.For(platform, deviceId, _tools)), _discovery, _options.ConnectTimeout, RunningProbe)
                : null;
            return new DevFlowDeviceClient(agent, control: control);
        }

        var lease = new LeaseIdentity($"e2e-{session}", "e2e", $"e2e {session}");
        var lazy = _options.Launch
            ? new AppControl((platform, token) => AppLaunchers.ForAsync(platform, _options.Device, _tools, token), _discovery, _options.ConnectTimeout, RunningProbe)
            : null;
        return new DevFlowDeviceClient(new DevFlowAgentClient(_http, _discovery.ResolveAsync, lease), control: lazy);
    }

    /// <summary>The attach-only wait: the endpoint is resolved once, and the status polled until the app you launched answers.</summary>
    private async Task<JsonNode?> WaitAsync(DevFlowAgentClient client, Action<string> log, CancellationToken cancellationToken)
    {
        var endpoint = await client.EndpointAsync(cancellationToken).ConfigureAwait(false);
        log($"waiting for the DevFlow agent at {endpoint}");
        return await _discovery.WaitForStatusAsync(client, _options.ConnectTimeout, cancellationToken).ConfigureAwait(false);
    }

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>A lease on one agent: what release needs beyond what travels to the worker.</summary>
    private sealed class DevFlowLease : DeviceLease
    {
        public Uri? Endpoint { get; init; }

        public LeaseIdentity? Lease { get; init; }

        public IAppLauncher? Launcher { get; init; }
    }
}
