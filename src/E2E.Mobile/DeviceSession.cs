// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Mobile;

/// <summary>
/// One simulator or emulator session, driven through an <see cref="IDeviceClient"/>, exposed to the runner as the
/// contract's session. Its device and session come from the target's <see cref="DevicePool"/>, by worker slot. Every
/// observation projects the device's accessibility tree under one root of a stable id
/// (<see cref="SnapshotProjection.RootId"/>), and its node refs are valid for that observation only. It owns the id
/// space (one fresh generation per observation), the transition state, and every translation between the contract's
/// vocabulary and agent-device's commands. The actions themselves are <see cref="DeviceActions"/>.
/// </summary>
public sealed partial class DeviceSession : IEngineSession, IEngineFixtureHost
{
    /// <summary>
    /// The Apple runner defers a full snapshot after slow accessibility work and returns a sparse one-node tree.
    /// Retrying device-side costs under a second. Returning the sparse tree costs the model a confused turn.
    /// </summary>
    private static readonly int[] SparseRetryBackoffMilliseconds = [0, 1_200, 3_000];

    private static readonly JsonSerializerOptions NodeJson = new() { PropertyNameCaseInsensitive = true };

    private readonly MobileEngine _engine;
    private readonly string _name;
    private readonly string _where;
    private readonly SlotBinding? _binding;
    private readonly MobileApp _app;
    private readonly string _projectRoot;
    private readonly string? _artifactsDir;
    private readonly Action<Observation>? _screenSink;
    private readonly InflightCommands _inflight;
    private IDeviceClient? _client;
    private IDeviceClient? _tracked;
    private int _idCounter;

    /// <summary>Screenshots the attempt kept, which number their files (upstream's <c>attempt.screenshots</c>).</summary>
    private int _screenshots;
    private string? _appIdentity;

    /// <summary>The agent-device session this worker drives and the device <c>BootAsync</c> booted under it (upstream's <c>bound</c>).</summary>
    private BoundDevice? _bound;

    /// <summary>True from a launch until a snapshot succeeds: an app iOS reports not running in between is still starting, not gone.</summary>
    private bool _launching;
    private ViewportSize? _knownViewport;

    private DeviceSession(MobileEngine engine, MobileStartInfo info, SlotBinding? binding, string name, string where, IDeviceClient client)
    {
        _engine = engine;
        _name = name;
        _app = engine.Options.App;
        _projectRoot = info.ProjectRoot;
        _artifactsDir = info.ArtifactsDir;
        _screenSink = info.Screen;
        ActionTimeout = info.ActionTimeout;
        _binding = binding;
        _where = where;
        _client = client;
        _inflight = engine.InflightOf(info.TargetName, info.WorkerSlot, binding);
        if (engine.Pool.Recorder is { } provider && DeviceProviders.TravelledLease(binding) is { } lease)
        {
            _leaseRecorder = new LeaseRecorder(provider, lease, info.RunId, info.TargetName, info.AttemptId, info.Env);
        }
        InstalledApp = binding?.InstalledApp;
    }

    public string Route { get; private set; } = "";

    /// <summary>
    /// The budget of one input (a tap or other node action, a point action, typing, a key, <c>back</c>): <c>actionTimeout</c>. Upstream passes
    /// each call its remaining budget, <c>operation.timeoutMs</c>. The port's session calls carry no timeout, so every input gets this.
    /// </summary>
    public TimeSpan ActionTimeout { get; }

    public MobileOptions Options => _engine.Options;

    /// <summary>The device this worker drives: the pool's binding for its slot. Empty leaves the choice to agent-device.</summary>
    public DeviceSelection Selection => DevicePool.DeviceSelectionFor(Options.Platform, _binding);

    /// <summary>The app the build <c>appPath</c> installed, once a device provider (or the suite) has.</summary>
    public string? InstalledApp { get; set; }

    /// <summary>The app <c>app.open()</c> launches: the target's bundle id, else the app its build installed.</summary>
    public string? PinnedApp => DeviceBindings.PinnedApp(_app, InstalledApp);

    /// <summary>The build the target names, resolved against the project root.</summary>
    public string? BuildPath => _app.AppPath is null ? null : Path.GetFullPath(_app.AppPath, _projectRoot);

    /// <summary>The build the target's <c>appPath</c> names, as declared.</summary>
    public string? AppPath => _app.AppPath;

    /// <summary>
    /// Who acts through this session. A test's step never settles, and it waits out a transition. The agent reads the
    /// screen right after acting, so its actions settle first. The core session does not say who is calling, so the
    /// caller sets it. The default is the agent's.
    /// </summary>
    public ActionOrigin Origin { get; set; } = ActionOrigin.Agent;

    /// <summary>The device fixture of this session: deterministic device management for a test.</summary>
    public Device Device => _device ??= new Device(this);

    private Device? _device;

    private EngineFixtureContext? _fixtureContext;

    /// <summary>What the harness hands the device fixture when the attempt starts: the locator <see cref="Device.Locator"/> mints with.</summary>
    void IEngineFixtureHost.CreateFixtures(EngineFixtureContext context) => _fixtureContext = context;

    /// <summary>The attempt's fixture context, or <c>INVALID_STATE</c> for a session no test harness started.</summary>
    internal EngineFixtureContext FixtureContext =>
        _fixtureContext ?? throw DeviceSupport.InvalidState("device.locator needs the attempt's test session: start the engine through a test, not on its own");

    /// <summary>The attempt's fixture context, or null for a session no test harness started, whose fixture calls then run unrecorded.</summary>
    internal EngineFixtureContext? FixtureContextOrNull => _fixtureContext;

    /// <summary>The agent-device client of the live session, or <c>INVALID_STATE</c> once it closed.</summary>
    internal IDeviceClient Client => _tracked ??= _client switch
    {
        null => throw DeviceSupport.InvalidState("the agent-device engine is not initialized"),
        ICancellableDeviceClient cancellable => cancellable,
        var client => new TrackedClient(client, _inflight),
    };

    /// <summary>The agent-device session and the device it runs on; <c>INVALID_STATE</c> before the boot or after the session closed (upstream's <c>boundDevice</c>).</summary>
    internal BoundDevice BoundDevice => _bound ?? throw DeviceSupport.InvalidState("the agent-device engine is not initialized");

    /// <summary>The session and device this worker drives, for the error messages whose recovery is per device.</summary>
    internal string Where => _where;

    /// <summary>Whether the target pins an app for <c>app.open()</c>, <c>app.restart()</c>, and <c>app.clearState()</c> to launch.</summary>
    private bool ManagesApp => _app.BundleId is not null || _app.AppPath is not null;

    /// <summary>
    /// Upstream's <c>startAttempt</c> waits for the commands an earlier attempt abandoned before anything of this one goes out. The wait is
    /// bounded by the launch timeout, as the web engine's connects are, so a start given no token still ends.
    /// </summary>
    private static async Task SettleBeforeStartAsync(InflightCommands inflight, CancellationToken cancellationToken)
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bound.CancelAfter(E2EDefaults.LaunchTimeout);
        try
        {
            await inflight.SettleAsync(bound.Token).ConfigureAwait(false);
        }
        catch (EngineException ex) when (ex.Code == EngineErrorCodes.Cancelled && !cancellationToken.IsCancellationRequested)
        {
            throw new EngineException(EngineErrorCodes.EngineFailure, $"device commands an earlier attempt abandoned did not settle within {E2EDefaults.LaunchTimeout.TotalSeconds:0} s", retryable: false, ex);
        }
    }

    internal static async Task<DeviceSession> StartAsync(MobileEngine engine, MobileStartInfo info, DeviceClientFactory createClient, CancellationToken cancellationToken)
    {
        var binding = engine.Pool.Binding(info.TargetName, info.WorkerSlot, info.Env);
        await SettleBeforeStartAsync(engine.InflightOf(info.TargetName, info.WorkerSlot, binding), cancellationToken).ConfigureAwait(false);
        var session = engine.Pool.Session(info.TargetName, info.WorkerSlot);
        var label = DeviceBindings.DeviceLabel(binding);
        var where = $"session {session}{(label is null ? "" : $" on {label}")}";
        var started = new DeviceSession(engine, info, binding, session, where, createClient(session, binding));
        try
        {
            await started.BootAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // The boot may have opened the agent-device session. Close it without hiding why the boot failed.
            await started.CloseAsync(E2EDefaults.CleanupTimeout, CancellationToken.None).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            throw;
        }

        engine.Register(started);
        started.TellEnvironment(info.Environment);
        return started;
    }

    /// <summary>Tells the trace what the attempt runs on. The trace is a reader of the attempt, so a hook that throws never fails it.</summary>
    private void TellEnvironment(Action<IReadOnlyDictionary<string, string>>? environment)
    {
        var facts = new Dictionary<string, string> { ["platform"] = Options.Platform.WireName() };
        if (DeviceBindings.DeviceLabel(_binding) is { } device)
        {
            facts["device"] = device;
        }

        try
        {
            environment?.Invoke(facts);
        }
        catch (Exception)
        {
            // The trace is a reader of the attempt, never a reason it fails.
        }
    }

    /// <summary>
    /// Runs one agent-device command under a cancellation token and translates its failure. Fixture methods route
    /// through here too, so none carries its own error mapping.
    /// </summary>
    public Task<T> CommandAsync<T>(string label, Func<IDeviceClient, CancellationToken, Task<T>> run, CancellationToken cancellationToken)
    {
        var client = Client;
        return DeviceErrors.RunCommandAsync(label, token => Track(run(client, token)), cancellationToken, _where);
    }

    /// <summary>The budget one input runs under. Its device commands stay in flight until they settle. Upstream's <c>inputBudget</c>.</summary>
    internal InputBudget InputBudget(string label) => new(label, ActionTimeout, pending => Track(pending), _engine.Clock);

    /// <summary>Registers one device command as in flight until it settles. Upstream's <c>track</c>.</summary>
    internal Task<T> Track<T>(Task<T> pending)
    {
        _inflight.Track(pending);
        return pending;
    }

    /// <summary>Registers one device command as in flight until it settles. Upstream's <c>track</c>.</summary>
    internal Task Track(Task pending)
    {
        _inflight.Track(pending);
        return pending;
    }

    /// <summary>Waits for every command an earlier attempt abandoned. Upstream's <c>settleInflight</c>.</summary>
    internal Task SettleInflightAsync(CancellationToken cancellationToken) => _inflight.SettleAsync(cancellationToken);

    /// <summary>Sends an input under its operation's budget, or directly from a fixture call, which has none. Upstream's <c>sendInput</c>.</summary>
    internal static Task<T> SendInputAsync<T>(InputBudget? input, Func<Task<T>> dispatch, CancellationToken cancellationToken, TimeSpan? hold = null) =>
        input is null ? dispatch() : input.SendAsync(dispatch, cancellationToken, hold);

    public Task<JsonNode?> CommandAsync(string label, string method, JsonObject? options, CancellationToken cancellationToken) =>
        CommandAsync(label, (client, token) => client.CallAsync(method, options, token), cancellationToken);

    private async Task BootAsync(CancellationToken cancellationToken)
    {
        var booted = await CommandAsync("boot", DeviceMethods.DevicesBoot, Selection.ToArgs(), cancellationToken).ConfigureAwait(false);
        _bound = new BoundDevice(_name, Text(booted?["id"]) ?? "", Text(booted?["device"]) ?? "");

        // Nothing is installed here: a device provider that installed the build from appPath says so on the binding,
        // and otherwise the suite installs it where it wants to, with device.installApp().
    }

    public Task OpenAsync(string url, CancellationToken cancellationToken)
    {
        // A device app has no URL: app.open() is the fresh launch of the pinned app.
        if (DeviceLinks.IsLink(url))
        {
            return Task.FromException(DeviceSupport.Unsupported("a device app has no URL to open; app.open() launches the pinned app, and a deep link opens with device.openLink"));
        }

        // A provider whose agent lives inside the running app cannot relaunch it: the open attaches to the app you launched.
        return Options.Provider is { AttachesToRunningApp: true } ? AttachAsync(cancellationToken) : RestartAsync(cancellationToken);
    }

    private async Task AttachAsync(CancellationToken cancellationToken)
    {
        var app = PinnedApp ?? throw DeviceSupport.Unsupported($"launching the app needs the target's `app.bundleId`, or {UninstalledBuild}");
        await OpenAppAsync(app, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Launches the pinned app fresh: the device's "recreate the context".</summary>
    public async Task RestartAsync(CancellationToken cancellationToken)
    {
        var app = PinnedApp ?? throw DeviceSupport.Unsupported($"launching the app needs the target's `app.bundleId`, or {UninstalledBuild}");
        await OpenAppAsync(app, new OpenAppOptions { Relaunch = true }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Clears the pinned app's persisted state and relaunches it: the device equivalent of a fresh context.</summary>
    public async Task ClearStateAsync(CancellationToken cancellationToken)
    {
        var app = PinnedApp ?? throw DeviceSupport.Unsupported($"app.clearState needs the target's `app.bundleId`, or {UninstalledBuild}");
        await CommandAsync(
            "clear app state",
            DeviceMethods.SettingsUpdate,
            Selection.ToArgs(("setting", JsonValue.Create("clear-app-state")), ("state", JsonValue.Create("clear")), ("app", JsonValue.Create(app))),
            cancellationToken).ConfigureAwait(false);
        await OpenAppAsync(app, new OpenAppOptions { Relaunch = true }, cancellationToken).ConfigureAwait(false);
    }

    public async Task BackAsync(CancellationToken cancellationToken)
    {
        var input = InputBudget("back");
        var before = _latestIndex;
        await CommandAsync(
            "back",
            (client, token) => SendInputAsync(input, () => client.CallAsync(DeviceMethods.CommandBack, ActionSettle(), token), token),
            cancellationToken).ConfigureAwait(false);
        MarkAction(before);
    }

    public bool CanLocate => true;

    public async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
    {
        var raw = await SnapshotOrEmptyAsync(Options.Snapshot == SnapshotMode.Interactive, cancellationToken).ConfigureAwait(false);
        var projected = Project(raw);
        _generation = projected.Index.ToDictionary(entry => entry.Id, entry => Bind(entry, projected.Index, projected.RefsGeneration));
        var viewport = await ViewportForAsync(projected, cancellationToken).ConfigureAwait(false);
        Route = DeviceSupport.ScreenLocation(raw.AppBundleId ?? raw.AppName ?? _appIdentity, SnapshotProjection.ScreenTitle(projected)) ?? "";
        return new Observation
        {
            Route = Route,
            Roots = [SnapshotProjection.ScreenRoot(projected.Roots, viewport)],
            Viewport = new TraceViewport((int)Math.Round(viewport.Width), (int)Math.Round(viewport.Height)),
            Truncated = raw.Truncated || raw.Sparse,
            KeyboardVisible = MeasuredKeyboard(raw),
        };
    }

    public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) =>
        _engine.Actions.PerformAsync(this, node, action, cancellationToken);

    public Task PressAsync(string key, CancellationToken cancellationToken) =>
        _engine.Actions.PressAsync(this, key, cancellationToken);

    public Task SwipeAsync(ScrollDirection direction, CancellationToken cancellationToken) =>
        _engine.Actions.SwipeAsync(this, direction, cancellationToken);

    public Task<EngineScreenshot> ScreenshotAsync(CancellationToken cancellationToken) =>
        _engine.Actions.ScreenshotAsync(this, cancellationToken);

    /// <summary>
    /// A redacted screenshot artifact. The device paints secure fields as dots, but the last typed character shows in clear, so every
    /// secure node's bounds are painted over before the file is kept. An image that cannot be redacted is not written at all (upstream's
    /// <c>screenshot</c> in <c>mobile/src/surface.ts</c>).
    /// </summary>
    public async Task<string> ScreenshotArtifactAsync(string? label, CancellationToken cancellationToken)
    {
        var artifactsDir = _artifactsDir ?? throw DeviceSupport.InvalidState("screenshot outside an attempt");
        var masked = await MaskedScreenshotAsync(normalizeStatusBar: false, cancellationToken).ConfigureAwait(false);
        var name = Interlocked.Increment(ref _screenshots).ToString("D3", System.Globalization.CultureInfo.InvariantCulture)
            + "-" + DeviceSupport.SanitizeFilename(label ?? "screenshot") + ".png";
        var directory = Path.Combine(artifactsDir, "screenshots");
        Directory.CreateDirectory(directory);
        var absolute = Path.Combine(directory, name);
        var temporary = absolute + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, masked.Png, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, absolute, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }

        return "screenshots/" + name;
    }

    public ValueTask DisposeAsync() => new(CloseAsync(E2EDefaults.CleanupTimeout, CancellationToken.None));

    /// <summary>Ends the agent-device session, within a budget: a close that outlives it is abandoned, as one that fails is.</summary>
    internal async Task CloseAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var client = _client;
        var dangling = _recording;
        _client = null;
        _tracked = null;
        _bound = null;
        _recording = null;
        _engine.Unregister(this);
        _appIdentity = null;
        InstalledApp = null;
        _knownViewport = null;
        _latestIndex = null;
        _latestScreen = null;
        _generation = [];
        ClearLocated();
        if (client is null)
        {
            return;
        }

        await CleanupBudget.WithinAsync(token => CloseQuietlyAsync(client, dangling, token), timeout, cancellationToken).ConfigureAwait(false);
    }

    private async Task CloseQuietlyAsync(IDeviceClient client, Recording? dangling, CancellationToken cancellationToken)
    {
        // The harness stops the video before it ends the attempt. A recording still marked belongs to an attempt cut
        // short, or to a stop that failed, and the device must not keep recording into the next one. A provider's
        // recording whose start outlived its budget may still be landing: its stop waits for it.
        if (dangling is LeaseRecording lease)
        {
            await StopLeaseQuietlyAsync(lease, cancellationToken).ConfigureAwait(false);
        }
        else if (dangling is not null)
        {
            await StopQuietlyAsync(client, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await client.CallAsync(DeviceMethods.SessionsClose, null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The session is going away either way.
        }
    }

    /// <summary>
    /// Snapshot for an observation. Without a pinned app, an observation before anything is open is an empty screen
    /// rather than a fault: the model's next move is the open tool, and failing the step would take that move away.
    /// </summary>
    private async Task<RawSnapshot> SnapshotOrEmptyAsync(bool interactiveOnly, CancellationToken cancellationToken)
    {
        try
        {
            return await SnapshotAsync(interactiveOnly, cancellationToken).ConfigureAwait(false);
        }
        catch (EngineException cause) when (_client is not null && !ManagesApp && cause.Code == EngineErrorCodes.InvalidState)
        {
            return RawSnapshot.Empty;
        }
    }

    internal async Task<RawSnapshot> SnapshotAsync(bool interactiveOnly, CancellationToken cancellationToken)
    {
        var last = RawSnapshot.Empty;
        foreach (var backoff in SparseRetryBackoffMilliseconds)
        {
            if (backoff > 0)
            {
                await _engine.Delay(TimeSpan.FromMilliseconds(backoff), cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            last = await CaptureAsync(interactiveOnly, cancellationToken).ConfigureAwait(false);
            _launching = false;
            if ((last.AppBundleId ?? last.AppName) is { } identity)
            {
                _appIdentity = identity;
            }

            if (!last.Sparse)
            {
                return last;
            }
        }

        return last;
    }

    /// <summary>
    /// One capture, taken again once when the iOS runner acquired the tree but failed its own presentation check on it
    /// (a viewport it could not read, a malformed graph). The check is per capture and a capture is a read, so the
    /// retry is safe. A second failure is the runner's and propagates.
    /// </summary>
    private async Task<RawSnapshot> CaptureAsync(bool interactiveOnly, CancellationToken cancellationToken)
    {
        async Task<RawSnapshot> Take() => RawSnapshot.Read(await CommandAsync(
            "snapshot",
            DeviceMethods.CaptureSnapshot,
            DeviceArgs.Of(("interactiveOnly", JsonValue.Create(interactiveOnly))),
            cancellationToken).ConfigureAwait(false));

        try
        {
            return await Take().ConfigureAwait(false);
        }
        catch (Exception cause) when (!cancellationToken.IsCancellationRequested && DeviceErrors.IsSnapshotPresentationFailure(cause))
        {
            return await Take().ConfigureAwait(false);
        }
    }

    /// <summary>Whether two viewports are one screen, in the same orientation or rotated; rects from the tree carry fractions.</summary>
    private static bool SameScreen(ViewportSize a, ViewportSize b)
    {
        static bool Near(double x, double y) => Math.Abs(x - y) < 1;
        return (Near(a.Width, b.Width) && Near(a.Height, b.Height)) || (Near(a.Width, b.Height) && Near(a.Height, b.Width));
    }

    /// <summary>
    /// Projects a snapshot and learns its viewport when it is plainly the screen's: the first one the session reads, the one it
    /// knows, or that one rotated. A tree of any other size may be an app drawn smaller than the screen (behind Notification
    /// Center, iOS shrinks it to a third), so an observation's <see cref="ViewportForAsync"/> checks it against the device
    /// before it replaces the known one.
    /// </summary>
    private ProjectedSnapshot Project(RawSnapshot raw)
    {
        var projected = SnapshotProjection.ProjectSnapshot(raw.Nodes, () => $"n{++_idCounter}", raw.Viewport, raw.RefsGeneration);
        _latestScreen = projected;
        _latestIndex = projected.Index;
        if (projected.Viewport is { } read && (_knownViewport is not { } known || SameScreen(read, known)))
        {
            _knownViewport = read;
        }

        return projected;
    }

    /// <summary>
    /// The viewport of a snapshot: its own geometry when that is the screen, else the last one this session learned, else the
    /// device's logical screen size read off one screenshot. A tree whose size is neither the known one nor that one rotated
    /// replaces it only when the screenshot's size, in either orientation, agrees: a screenshot reports the portrait size even
    /// in landscape, so it confirms a size but cannot pick the orientation. Only a simulator's screenshot reports a logical
    /// size; on a real device the tree's size is taken as it comes. A device whose size cannot be learned at all fails the
    /// observation: the harness measures every rect and point against the viewport, so an invented one would misplace every tap.
    /// </summary>
    private async Task<ViewportSize> ViewportForAsync(ProjectedSnapshot projected, CancellationToken cancellationToken)
    {
        if (_knownViewport is { } known)
        {
            if (projected.Viewport is not { } read)
            {
                return known;
            }

            if (!SameScreen(read, known)
                && await ProbeViewportAsync(cancellationToken).ConfigureAwait(false) is { } device
                && !SameScreen(read, device))
            {
                return known;
            }

            _knownViewport = read;
            return read;
        }

        var probed = await ProbeViewportAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new EngineException(
                EngineErrorCodes.EngineFailure,
                "the device reported no screen geometry: the snapshot has no bounds and the screenshot no logical size, so the viewport is unknown",
                retryable: false);
        _knownViewport = probed;
        return probed;
    }

    /// <summary>The device's logical screen size as its screenshot reports it. Null when it reports none.</summary>
    private Task<ViewportSize?> ProbeViewportAsync(CancellationToken cancellationToken) =>
        CaptureScreenshotAsync((shot, _) => Task.FromResult(DeviceSupport.LogicalScreenSize(shot)), normalizeStatusBar: false, cancellationToken);

    private const string UninstalledBuild = "the build the target's `app.appPath` names installed first with `device.installApp()`";

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>A JSON number, whatever type backs it: one parsed from the daemon's text and one built in code differ.</summary>
    private static double? Number(JsonNode? node) =>
        node is JsonValue && JsonSerializer.SerializeToElement(node) is { ValueKind: JsonValueKind.Number } element ? element.GetDouble() : null;

    /// <summary>The snapshot fields this engine reads off agent-device's response.</summary>
    internal sealed record RawSnapshot(
        IReadOnlyList<RawNode> Nodes,
        bool Truncated,
        string? AppName,
        string? AppBundleId,
        bool Sparse,
        ViewportSize? Viewport = null,
        string? KeyboardKind = null,
        Rect? KeyboardFrame = null,
        long? RefsGeneration = null)
    {
        public static RawSnapshot Empty { get; } = new([], false, null, null, false);

        public static RawSnapshot Read(JsonNode? response)
        {
            if (response is not JsonObject data)
            {
                return Empty;
            }

            IReadOnlyList<RawNode> nodes;
            try
            {
                nodes = data["nodes"] is JsonArray array ? array.Deserialize<List<RawNode>>(NodeJson) ?? [] : [];
            }
            catch (JsonException cause)
            {
                throw new EngineException(EngineErrorCodes.EngineFailure, $"the device returned a snapshot this engine cannot read: {cause.Message}", retryable: false, cause);
            }

            ViewportSize? viewport = Number(data["viewport"]?["width"]) is > 0 and var width && Number(data["viewport"]?["height"]) is > 0 and var height
                ? new ViewportSize(width, height)
                : null;
            return new RawSnapshot(
                nodes,
                data["truncated"] is JsonValue truncated && truncated.TryGetValue<bool>(out var cut) && cut,
                Text(data["appName"]),
                Text(data["appBundleId"]),
                Text(data["snapshotQuality"]?["state"]) == "sparse",
                viewport,
                Text(data["keyboard"]?["kind"]),
                Number(data["keyboard"]?["frame"]?["x"]) is { } frameX
                    && Number(data["keyboard"]?["frame"]?["y"]) is { } frameY
                    && Number(data["keyboard"]?["frame"]?["width"]) is { } frameWidth
                    && Number(data["keyboard"]?["frame"]?["height"]) is { } frameHeight
                    ? new Rect(frameX, frameY, frameWidth, frameHeight)
                    : null,
                Number(data["refsGeneration"]) is { } generation ? (long)generation : null);
        }
    }
}

/// <summary>
/// Commands still running on a device. agent-device takes no abort signal, so a cancelled or timed-out call is only abandoned by its
/// caller: it keeps executing. The next attempt waits for these to settle before it opens anything, so a ghost tap can never land in
/// a retry. Upstream's <c>inflight</c> set, with its <c>track</c> and <c>settleInflight</c>, on the surface.
/// </summary>
internal sealed class InflightCommands
{
    private readonly HashSet<Task> _pending = [];
    private bool _unconfirmedAbort;

    /// <summary>Registers one device command as in flight until it settles.</summary>
    public void Track(Task pending, Func<Task, bool>? unconfirmed = null)
    {
        lock (_pending)
        {
            if (!_pending.Add(pending))
            {
                return;
            }
        }

        _ = pending.ContinueWith(
            settled =>
            {
                // Read, so a command that fails after its caller gave up is not left unobserved.
                _ = settled.Exception;
                lock (_pending)
                {
                    // A request the caller left that ends without the daemon's answer is not confirmation that the daemon finished the command.
                    _unconfirmedAbort |= unconfirmed?.Invoke(settled) ?? false;
                    _pending.Remove(settled);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>
    /// Waits for every abandoned command to settle, within the caller's budget. A command that never settles fails the attempt launch
    /// instead of racing it: the launch timeout is the honest bound on a stuck device. Upstream's <c>settleInflight</c>.
    /// </summary>
    public async Task SettleAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task[] pending;
            lock (_pending)
            {
                if (_unconfirmedAbort)
                {
                    throw new EngineException(EngineErrorCodes.EngineFailure, "an earlier device request was aborted without confirmation that the daemon finished; later attempts on this device are blocked", retryable: false);
                }

                pending = [.. _pending];
            }

            if (pending.Length == 0)
            {
                return;
            }

            try
            {
                var settled = pending.Select(task => task.ContinueWith(static _ => { }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default));
                await Task.WhenAll(settled).WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw DeviceSupport.Cancelled("settling in-flight device commands cancelled");
            }
        }
    }
}

/// <summary>
/// The client a session's commands go through. agent-device takes no abort signal, and upstream's client is called without one, so a command
/// the caller gave up on keeps running on the device. The port's HTTP client would abort its request on a cancel and end the tracked task
/// while the daemon may still run the command; this call runs under a token the caller cannot cancel, and the caller leaves by racing it
/// (<c>WaitAsync</c> in <c>RunCommandAsync</c>, <c>ActAsync</c>, and <see cref="InputBudget"/>). The call is tracked in flight itself, so it
/// stays there until the daemon answers. Once the caller left, the request is given <see cref="E2EDefaults.LaunchTimeout"/> to answer (the
/// bound of <c>SettleInflightAsync</c> on the next start) and is aborted after that. An abort without a successful answer blocks later
/// attempts on that device, since daemon completion is unknown. .NET-only: upstream's client has no signal to detach.
/// </summary>
internal sealed class TrackedClient(IDeviceClient inner, InflightCommands inflight, TimeProvider? timeProvider = null) : IDeviceClient
{
    public Task<JsonNode?> CallAsync(string method, JsonObject? options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var abort = new CancellationTokenSource(Timeout.InfiniteTimeSpan, timeProvider ?? TimeProvider.System);
        var callerLeft = new System.Runtime.CompilerServices.StrongBox<bool>();
        var left = cancellationToken.CanBeCanceled
            ? cancellationToken.Register(
                static state =>
                {
                    var (source, flag) = ((CancellationTokenSource, System.Runtime.CompilerServices.StrongBox<bool>))state!;
                    Volatile.Write(ref flag.Value, true);
                    source.CancelAfter(E2EDefaults.LaunchTimeout);
                },
                (abort, callerLeft))
            : default;
        Task<JsonNode?> pending;
        try
        {
            pending = inner.CallAsync(method, options, abort.Token);
        }
        catch
        {
            left.Dispose();
            abort.Dispose();
            throw;
        }

        inflight.Track(pending, settled => Volatile.Read(ref callerLeft.Value) && !settled.IsCompletedSuccessfully && !DaemonAnswered(settled));
        _ = pending.ContinueWith(
            static (_, state) =>
            {
                var (registration, source) = ((CancellationTokenRegistration, CancellationTokenSource))state!;
                registration.Dispose();
                source.Dispose();
            },
            (left, abort),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return pending;
    }

    /// <summary>True when the daemon itself answered with a failure: an error it reported, not a transport failure that carries a cause.</summary>
    private static bool DaemonAnswered(Task settled) => settled.Exception?.InnerException is AgentDeviceError { Reported: true };
}

/// <summary>The agent-device session a worker drives, and the device agent-device booted for it (upstream's <c>BoundDevice</c>, <c>surface.ts</c>).</summary>
internal sealed record BoundDevice(string Session, string DeviceId, string DeviceName);
