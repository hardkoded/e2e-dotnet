// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Mobile;

/// <summary>What <see cref="MobileEngine"/> hands the code that turns a node and an action into device commands.</summary>
/// <remarks>
/// Upstream's <c>AgentDeviceSurface</c> owns these members next to observe and the session hooks. <see cref="DeviceActions"/> is
/// the implementation the engine uses unless a caller sets another through <see cref="MobileEngine.Actions"/>.
/// </remarks>
public interface IDeviceActions
{
    Task PerformAsync(DeviceSession session, SemanticNode node, LocatorAction action, CancellationToken cancellationToken);

    Task PressAsync(DeviceSession session, string key, CancellationToken cancellationToken);

    Task SwipeAsync(DeviceSession session, ScrollDirection direction, CancellationToken cancellationToken);

    Task<EngineScreenshot> ScreenshotAsync(DeviceSession session, CancellationToken cancellationToken);
}

/// <summary>What a worker's start is given: which target and worker slot it serves, and the environment <c>prepare</c> left.</summary>
public sealed record MobileStartInfo
{
    public string RunId { get; init; } = "";

    /// <summary>The attempt's id, which a provider that records names the recording by. Upstream's <c>EngineAttemptContext.attemptId</c>. Defaults to a new one.</summary>
    public string AttemptId { get; init; } = Guid.NewGuid().ToString("n");

    public required string TargetName { get; init; }

    public int WorkerSlot { get; init; }

    public string ProjectRoot { get; init; } = Directory.GetCurrentDirectory();

    /// <summary>The environment the worker reads its binding from, never the process environment.</summary>
    public IReadOnlyDictionary<string, string?> Env { get; init; } = new Dictionary<string, string?>();

    public Action<string> Log { get; init; } = static _ => { };

    /// <summary>The attempt's artifact directory: where a video is written. Null for a worker started without one.</summary>
    public string? ArtifactsDir { get; init; }

    /// <summary>Takes the screen a locate matched against, for the attempt's trace. Upstream's <c>EngineAttemptContext.screen</c>.</summary>
    public Action<Observation>? Screen { get; init; }

    /// <summary>Takes the facts about what the attempt runs on. Upstream's <c>EngineAttemptContext.environment</c>.</summary>
    public Action<IReadOnlyDictionary<string, string>>? Environment { get; init; }

    /// <summary>The budget of one input. Upstream reads it per call, from <c>operation.timeoutMs</c>; see <see cref="DeviceSession.ActionTimeout"/>.</summary>
    public TimeSpan ActionTimeout { get; init; } = E2EDefaults.ActionTimeout;
}

/// <summary>
/// The agent-device engine for e2e: a mobile body that implements the core engine contract, one device session per
/// worker, over an <see cref="IDeviceClient"/>. A test launches the pinned app with <c>app.open()</c>, which is the
/// fresh launch of the pinned app. The device is a simulator, an emulator, or a connected phone, named by the options,
/// discovered, or leased from an <see cref="IDeviceProvider"/>.
/// </summary>
public sealed class MobileEngine : IPreparableEngine
{
    /// <summary>The version of the engine, as upstream's <c>@e2e-dev/mobile</c> numbers the surface this one follows.</summary>
    public const string EngineVersion = "0.11.0";

    private static readonly Dictionary<MobilePlatform, string> LoopbackNotes = new()
    {
        [MobilePlatform.Ios] = "a simulator shares this machine's loopback, and a hosted device cannot reach it at all",
        [MobilePlatform.Android] = "127.0.0.1 on an Android emulator is the emulator itself (this machine is 10.0.2.2), and a hosted device cannot reach this machine's loopback at all",
    };

    private readonly DeviceClientFactory _createClient;

    /// <summary>
    /// Creates the engine over the local agent-device daemon, or the daemon a lease names. Needs Node 22.12 or newer at
    /// run time. A provider that implements <see cref="IDeviceClientSource"/> makes the clients itself instead.
    /// </summary>
    public MobileEngine(MobileOptions options)
        : this(options, ClientsOf(options))
    {
    }

    /// <summary>Creates the engine over a client of the caller's making: the seam unit tests script.</summary>
    internal MobileEngine(MobileOptions options, DeviceClientFactory createClient)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(createClient);
        Validate(options);
        Options = options;
        _createClient = createClient;
        Pool = new DevicePool(options, createClient);
    }

    public MobileOptions Options { get; }

    /// <summary>The target's devices, one per worker slot.</summary>
    public DevicePool Pool { get; }

    /// <summary>The code that performs node actions, keys, scrolls, and screenshots.</summary>
    public IDeviceActions Actions { get; init; } = new DeviceActions();

    /// <summary>The clock the transition budget counts on. Tests replace it with one the delays advance.</summary>
    internal TimeProvider Clock { get; init; } = TimeProvider.System;

    private readonly List<DeviceSession> _sessions = [];

    /// <summary>The sessions this engine started that have not closed: the attempts running on it right now.</summary>
    public IReadOnlyList<DeviceSession> ActiveSessions
    {
        get
        {
            lock (_sessions)
            {
                return [.. _sessions];
            }
        }
    }

    internal void Register(DeviceSession session)
    {
        lock (_sessions)
        {
            _sessions.Add(session);
        }
    }

    internal void Unregister(DeviceSession session)
    {
        lock (_sessions)
        {
            _sessions.Remove(session);
        }
    }

    private readonly Dictionary<(string Target, int Slot, string? Device), InflightCommands> _inflight = [];

    /// <summary>
    /// The device commands still running on one worker slot's device (the lease or device its binding names), kept across the attempts that drive it. Upstream's surface is
    /// the worker and holds them itself; the port's session is one attempt, so the engine holds them for the slot.
    /// </summary>
    internal InflightCommands InflightOf(string targetName, int slot, SlotBinding? binding)
    {
        lock (_inflight)
        {
            var key = (targetName, slot, binding?.LeaseId ?? DeviceBindings.DeviceLabel(binding));
            if (!_inflight.TryGetValue(key, out var commands))
            {
                _inflight[key] = commands = new InflightCommands();
            }

            return commands;
        }
    }

    /// <summary>The wait between two attempts at a sparse snapshot. Tests replace it to spend no time.</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; init; } = static (span, token) => Task.Delay(span, token);

    public string Name => "mobile";

    public string Platform => Options.Platform.WireName();

    public string Version => EngineVersion;

    public EngineCapabilities Capabilities =>
        EngineCapabilities.Observation
        | EngineCapabilities.Actions
        | EngineCapabilities.Location
        | EngineCapabilities.Keyboard
        | EngineCapabilities.Scroll
        | EngineCapabilities.History
        | EngineCapabilities.Screenshot
        | EngineCapabilities.Video
        | EngineCapabilities.PinnedApp;

    /// <summary>
    /// A device target launches an installed app or a build: one of the bundle id and the build path is required. A URL
    /// (a website in the device's browser) is not supported yet, and a link as the bundle id is a mistake: a test opens
    /// one with <c>device.openLink</c>.
    /// </summary>
    public static void ValidateApp(MobileApp app, string? url, string targetName, MobilePlatform platform)
    {
        ArgumentNullException.ThrowIfNull(app);
        var where = $"target \"{targetName}\"";
        if (url is not null)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                $"{where} declares app.url, but a mobile() target cannot open a website in the device's browser yet; test the installed app with app.bundleId or app.appPath. When it lands, note that {LoopbackNotes[platform]}");
        }

        if (app.BundleId is null && app.AppPath is null)
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                $"{where} needs app.bundleId or app.appPath: a mobile() target launches an installed app, targets: [{{ engine: mobile({{ platform }}), app: {{ bundleId: 'com.example.app' }} }}]");
        }

        if (app.BundleId is not null && DeviceLinks.IsLink(app.BundleId))
        {
            throw new ConfigurationException(
                "INVALID_CONFIG",
                $"{where} app.bundleId names an app by bundle id, package, or display name, not a link; a test opens a deep link or web link with device.openLink");
        }
    }

    /// <summary>
    /// Binds a device to every worker slot the run will use and warms each, once per run and target, before any worker
    /// starts. See <see cref="DevicePool.PrepareAsync"/>. The result's environment is what the workers read.
    /// </summary>
    public Task<MobilePrepareResult> PrepareAsync(MobilePrepareInfo info, CancellationToken cancellationToken) =>
        Pool.PrepareAsync(info, cancellationToken);

    /// <summary>Closes the sessions prepare opened and releases the leases, once per run and target.</summary>
    public Task FinishAsync(MobileFinishInfo info, CancellationToken cancellationToken) =>
        Pool.FinishAsync(info, cancellationToken);

    /// <summary>The core hook of upstream's <c>prepare</c>: <see cref="PrepareAsync(MobilePrepareInfo, CancellationToken)"/> for this engine's app.</summary>
    async Task<EnginePrepareResult> IPreparableEngine.PrepareAsync(EnginePrepareInfo info, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(info);
        var prepared = await PrepareAsync(
            new MobilePrepareInfo
            {
                RunId = info.RunId,
                TargetName = info.TargetName,
                ProjectRoot = info.ProjectRoot ?? Directory.GetCurrentDirectory(),
                Slots = info.Slots,
                Env = info.Env,
                App = Options.App,
                Log = info.Log,
            },
            cancellationToken).ConfigureAwait(false);
        return new EnginePrepareResult { Workers = prepared.Workers, Env = prepared.Env };
    }

    /// <summary>The core hook of upstream's <c>finish</c>: <see cref="FinishAsync(MobileFinishInfo, CancellationToken)"/>.</summary>
    Task IPreparableEngine.FinishAsync(EngineFinishInfo info, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(info);
        return FinishAsync(
            new MobileFinishInfo { RunId = info.RunId, TargetName = info.TargetName, Env = info.Env, Log = info.Log, CleanupTimeout = info.CleanupTimeout },
            cancellationToken);
    }

    /// <summary>
    /// Starts one attempt: the worker of slot 0, on the device prepare bound for the target. A host that starts the engine on its own, as
    /// <c>e2e explore</c> and <c>e2e mcp</c> do, calls <see cref="IPreparableEngine.PrepareAsync"/> and <see cref="IPreparableEngine.FinishAsync"/>
    /// around it, as upstream's <c>openStandaloneAttempt</c> does.
    /// </summary>
    public async Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateApp(Options.App, options.BaseUrl, Options.TargetName, Options.Platform);
        return await StartAsync(
            new MobileStartInfo
            {
                TargetName = Options.TargetName,
                AttemptId = options.AttemptId ?? Guid.NewGuid().ToString("n"),
                ProjectRoot = options.ProjectRoot ?? Directory.GetCurrentDirectory(),
                Env = options.Env ?? new Dictionary<string, string?>(),
                ArtifactsDir = options.ArtifactsDir,
                Screen = options.Screen,
                Environment = options.Environment,
                ActionTimeout = options.ActionTimeout,
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Starts the worker of one slot: boots its device under its session and resumes the app prepare opened there.</summary>
    public async Task<DeviceSession> StartAsync(MobileStartInfo info, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(info);
        return await DeviceSession.StartAsync(this, info, _createClient, cancellationToken).ConfigureAwait(false);
    }

    private static DeviceClientFactory ClientsOf(MobileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Provider is IDeviceClientSource source
            ? source.CreateClient
            : static (session, connection) => DeviceClientConfig.For(session, connection).Connect();
    }

    private static void Validate(MobileOptions options)
    {
        if (new object?[] { options.Device, options.Devices, options.Provider }.Count(choice => choice is not null) > 1)
        {
            throw new ConfigurationException("INVALID_CONFIG", "mobile: set at most one of `Device`, `Devices`, and `Provider`");
        }

        if (options.Provider is { } provider && string.IsNullOrWhiteSpace(provider.Name))
        {
            throw new ConfigurationException("INVALID_CONFIG", "mobile: a `device` provider needs a non-empty `name`");
        }

        if (options.SettleMilliseconds < 0)
        {
            throw new ConfigurationException("INVALID_CONFIG", "mobile: `settle` must be a non-negative integer of milliseconds, or false");
        }

        if (options.TransitionMilliseconds < 0)
        {
            throw new ConfigurationException("INVALID_CONFIG", "mobile: `transition` must be a non-negative integer of milliseconds");
        }

        if (options.Device is not null && options.Device.Length == 0)
        {
            throw new ConfigurationException("INVALID_CONFIG", "mobile: `device` is an empty name; name a simulator or emulator, or omit it to use every booted one");
        }
    }
}
