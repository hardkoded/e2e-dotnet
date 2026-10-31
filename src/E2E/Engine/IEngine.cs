// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine;

/// <summary>Capabilities the harness reads off an engine. An engine never calls a model.</summary>
[Flags]
public enum EngineCapabilities
{
    None = 0,
    Observation = 1,
    Actions = 2,
    Location = 4,
    Keyboard = 8,

    /// <summary>The viewport swipe, <see cref="LocatorAction.Swipe"/>, and <see cref="LocatorAction.ScrollIntoView"/>.</summary>
    Scroll = 16,

    /// <summary><see cref="IEngineSession.BackAsync"/>.</summary>
    History = 32,

    /// <summary><see cref="IEngineSession.ScreenshotAsync"/>, which <c>toHaveScreenshot</c> compares.</summary>
    Screenshot = 64,

    /// <summary>The <c>Modifiers</c> of <see cref="LocatorAction.Tap"/> and <see cref="LocatorAction.DoubleTap"/>: keys held for the click.</summary>
    TapModifiers = 128,

    /// <summary><see cref="IEngineSession.StartVideoAsync"/> and <see cref="IEngineSession.StopVideoAsync"/>: the engine can record the attempt as video.</summary>
    Video = 256,

    /// <summary>
    /// The engine opens no URL: <see cref="IEngineSession.OpenAsync"/> relaunches the app the target pins. .NET-only name for upstream's
    /// <c>launchesPinnedApp</c>, which tests for a session with a <c>restart</c> and no <c>open</c>; the port's <c>OpenAsync</c> is not optional.
    /// </summary>
    PinnedApp = 512,
}

/// <summary>
/// What an engine's fixture gets from the attempt, once per attempt. Upstream's <c>EngineFixtureContext</c>, of which the port has
/// <c>locator</c> only: its fixtures take their own cancellation tokens, and record their steps through <c>RunOperationAsync</c>.
/// </summary>
public sealed class EngineFixtureContext
{
    private readonly Screen _screen;

    internal EngineFixtureContext(Screen screen) => _screen = screen;

    /// <summary>The attempt's screen, which a scope such as a frame's is made of.</summary>
    internal Screen Root => _screen;

    /// <summary>
    /// Runs one fixture call as a recorded step of the attempt (upstream's <c>FixtureRecorder.run</c>, which wraps each operation a fixture
    /// declares through <c>fixture</c>). .NET-only: the port's fixtures call it themselves instead of declaring their operations.
    /// </summary>
    internal Task<T> RunOperationAsync<T>(StepKind kind, string api, string label, Func<Task<T>> body) =>
        _screen.RunStepAsync(kind, api, label, body);

    /// <summary>Mints a public locator from a raw expression (a platform selector, say). Upstream's <c>locator</c>.</summary>
    public Locator Locator(LocatorExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return new Locator(_screen, expression);
    }
}

/// <summary>
/// An engine session that contributes a fixture, which the harness tells about the attempt when it starts. .NET-only: upstream's engine
/// declares <c>fixtures</c>, factories the harness calls with an <see cref="EngineFixtureContext"/>; the port's session builds its own
/// fixture (<c>Device</c>), so this hands it the context.
/// </summary>
public interface IEngineFixtureHost
{
    void CreateFixtures(EngineFixtureContext context);
}

/// <summary>Passed to <see cref="IEngine.StartAsync"/> once per test attempt.</summary>
public sealed class EngineStartOptions
{
    public string? BaseUrl { get; init; }

    /// <summary>The target's name, for the errors an engine raises about its <c>app</c>. Upstream's <c>EngineInitContext.targetName</c>.</summary>
    public string? TargetName { get; init; }

    /// <summary>
    /// The permissions the app holds, by name: the target's <c>app.permissions</c> (upstream's <c>EngineAppInfo.permissions</c>). What each
    /// state means is the engine's. A browser grants each name to every page of the attempt and takes <c>grant</c> only.
    /// </summary>
    public IReadOnlyDictionary<string, AppPermissionState>? Permissions { get; init; }

    public TimeSpan ActionTimeout { get; init; } = E2EDefaults.ActionTimeout;

    /// <summary>Anchors the files the engine options name, such as an init script's <c>Path</c>. Defaults to the working directory.</summary>
    public string? ProjectRoot { get; init; }

    /// <summary>
    /// The plaintext of a secret an engine option carries, resolved per attempt. The value is registered for redaction
    /// before it is returned, with the forms <see cref="ResolveSecretOptions.Derived"/> computes from it.
    /// </summary>
    internal Func<Secret, ResolveSecretOptions?, Task<string>>? ResolveSecret { get; init; }

    /// <summary>
    /// The attempt's id, for naming what an engine leases for it at a hosted service. Defaults to a new one per start.
    /// Upstream's <c>EngineAttemptContext.attemptId</c>.
    /// </summary>
    public string? AttemptId { get; init; }

    /// <summary>
    /// The run's environment: where a hosted service's provider reads its token from, with what the engine's prepare left in it.
    /// An engine that leases for the one attempt it starts, as <see cref="WebEngine"/> does, reads it to prepare that run; it defaults
    /// to the process environment.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? Env { get; init; }

    /// <summary>
    /// The attempt's artifact directory: where a recording of the attempt is written, under <c>video</c>. Upstream's
    /// <c>EngineAttemptContext.artifactsDir</c>. An engine that records video needs it.
    /// </summary>
    public string? ArtifactsDir { get; init; }

    /// <summary>
    /// Takes the screen an action left, for the attempt's trace. Upstream's <c>EngineAttemptContext.screen</c>. Null when the
    /// attempt keeps no trace, and an engine then takes no screen. An engine may read the screen on the action's path; it never
    /// fails the action for it, and a sink that throws never fails the attempt.
    /// </summary>
    public Action<Observation>? Screen { get; init; }

    /// <summary>
    /// Takes the facts the engine learns about what the attempt runs on, such as the browser and its user agent, for the attempt's
    /// trace. Upstream's <c>EngineAttemptContext.environment</c>. An engine tells it at most a few times an attempt, and never
    /// fails the attempt for it.
    /// </summary>
    public Action<IReadOnlyDictionary<string, string>>? Environment { get; init; }

    /// <summary>
    /// Takes one line of what the app did on its own, such as console output, an exception nothing caught, or a failed request, for
    /// the attempt's trace. Upstream's <c>EngineAttemptContext.appLog</c>. An engine may report from the moment it starts, before any
    /// step records; the harness redacts and bounds each line, and a sink that throws never fails the attempt.
    /// </summary>
    public Action<AppLogEntry>? AppLog { get; init; }

    /// <summary>
    /// Takes one line saying where the app went on its own, such as <c>navigated to /checkout</c> or <c>the app opened a new tab at /help</c>.
    /// Upstream's <c>EngineAttemptContext.navigation</c>. A navigation the engine started itself is not told: its step says it.
    /// </summary>
    public Action<string>? Navigation { get; init; }
}

/// <summary>What an engine asks of <see cref="EngineStartOptions.ResolveSecret"/> beside the value.</summary>
internal sealed record ResolveSecretOptions
{
    /// <summary>The forms of the plaintext the engine hands to the app in its place, which are redacted wherever the value is.</summary>
    public Func<string, IReadOnlyList<string>>? Derived { get; init; }
}

/// <summary>
/// The body of one target. Core knows this contract and never an engine's internals.
/// <see cref="Version"/> is the engine version. It is not part of the replay cache key:
/// a recording re-finds its nodes at replay.
/// </summary>
public interface IEngine
{
    /// <summary>
    /// The engine's name, which the report records in <c>run.targets[].engine</c> and the trace cache keys on (upstream's <c>engine.name</c>).
    /// Defaults to <see cref="Platform"/>, so an engine that does not name itself keeps compiling and keeps its cache key.
    /// </summary>
    string Name => Platform;

    string Platform { get; }

    string Version { get; }

    EngineCapabilities Capabilities { get; }

    Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken);
}

/// <summary>One attempt's session with the app. Disposed at the end of the attempt, after <c>afterEach</c>.</summary>
public interface IEngineSession : IAsyncDisposable
{
    /// <summary>Location of the current screen: its path, query and fragment, without the origin.</summary>
    string Route { get; }

    Task OpenAsync(string url, CancellationToken cancellationToken);

    Task<Observation> ObserveAsync(CancellationToken cancellationToken);

    Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken);

    /// <summary>Sends a key to whatever holds focus. The harness has already validated the key.</summary>
    Task PressAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Whether <see cref="LocateAsync"/> is implemented. A session that says false has its locators resolved in core over
    /// <see cref="ObserveAsync"/>. .NET-only and transitional: upstream's engines all locate, and this goes once the web and document
    /// engines do.
    /// </summary>
    bool CanLocate => false;

    /// <summary>
    /// Resolves a locator against the screen as it is now and returns every match in document order. Polling and strictness stay with
    /// the harness. A screen the engine cannot read yet matches nothing, so the harness keeps polling. <paramref name="timeout"/> is the
    /// budget left for the resolve, which an engine uses to tell a screen that is still arriving from one that never rendered. A
    /// <c>union</c> expression matches either branch in document order, without duplicate nodes. Engines that declare
    /// <see cref="EngineCapabilities.Location"/> and set <see cref="CanLocate"/> implement it. Upstream's <c>locate</c>.
    /// </summary>
    Task<IReadOnlyList<SemanticNode>> LocateAsync(LocatorExpression expression, TimeSpan timeout, CancellationToken cancellationToken) =>
        Task.FromException<IReadOnlyList<SemanticNode>>(new EngineException("UNSUPPORTED_CAPABILITY", "locators (screen) is not supported by this engine."));

    /// <summary>
    /// Scrolls the viewport about three quarters of a screen. Engines that declare
    /// <see cref="EngineCapabilities.Scroll"/> implement it.
    /// </summary>
    Task SwipeAsync(ScrollDirection direction, CancellationToken cancellationToken) => Unsupported("scroll");

    /// <summary>
    /// <see cref="SwipeAsync(ScrollDirection, CancellationToken)"/> with the stride of the gesture: upstream's <c>swipe(direction,
    /// momentum)</c>. An engine that takes a momentum overrides it; the default drops it, so an engine with one stride swipes by that.
    /// </summary>
    Task SwipeAsync(ScrollDirection direction, Momentum? momentum, CancellationToken cancellationToken) => SwipeAsync(direction, cancellationToken);

    /// <summary>
    /// Goes back one entry in the session history. With no earlier entry it does nothing.
    /// Engines that declare <see cref="EngineCapabilities.History"/> implement it.
    /// </summary>
    Task BackAsync(CancellationToken cancellationToken) => Unsupported("app.back");

    /// <summary>Closes the current document and opens a blank one, keeping cookies and storage.</summary>
    Task RestartAsync(CancellationToken cancellationToken) => Unsupported("app.restart");

    /// <summary>Discards cookies, storage, and history, and opens a blank document.</summary>
    Task ClearStateAsync(CancellationToken cancellationToken) => Unsupported("app.clearState");

    /// <summary>
    /// Captures the viewport as a PNG for <c>toHaveScreenshot</c>, with animations stopped, the text caret hidden,
    /// and every secure field covered before the image leaves the engine. Engines that declare
    /// <see cref="EngineCapabilities.Screenshot"/> implement it.
    /// </summary>
    Task<EngineScreenshot> ScreenshotAsync(CancellationToken cancellationToken) =>
        Task.FromException<EngineScreenshot>(new EngineException("UNSUPPORTED_CAPABILITY", "toHaveScreenshot is not supported by this engine."));

    /// <summary>
    /// Captures a redacted screenshot into the attempt's artifact directory and returns its artifact-relative path, <c>screenshots/NNN-label.png</c>.
    /// Every secure field is covered before the file is written. Upstream's <c>session.artifacts.screenshot</c>; an engine that keeps none
    /// fails with <c>UNSUPPORTED_CAPABILITY</c>, as upstream's guard does.
    /// </summary>
    Task<string> ScreenshotArtifactAsync(string? label, CancellationToken cancellationToken) =>
        Task.FromException<string>(new EngineException("UNSUPPORTED_CAPABILITY", "screenshots is not supported by this engine."));

    /// <summary>
    /// Starts recording the surface, once its page can be recorded. Engines that declare
    /// <see cref="EngineCapabilities.Video"/> implement it. An attempt may start and stop more than once, never overlapping: the
    /// runner calls it for the attempt's video and for each <c>app.StartRecordingAsync()</c>, and moves the files a stop returns
    /// before the next start. Upstream's <c>startVideo</c>.
    /// </summary>
    Task StartVideoAsync(CancellationToken cancellationToken) => Unsupported("video recording");

    /// <summary>
    /// Stops the recording and returns the segments written, in order: files in the attempt's <c>video</c> directory, or
    /// links to recordings a hosted service keeps. Engines that declare <see cref="EngineCapabilities.Video"/> implement
    /// it. Upstream's <c>stopVideo</c>.
    /// </summary>
    Task<IReadOnlyList<VideoSegment>> StopVideoAsync(CancellationToken cancellationToken) =>
        Task.FromException<IReadOnlyList<VideoSegment>>(new EngineException("UNSUPPORTED_CAPABILITY", "video recording is not supported by this engine."));

    /// <summary>
    /// After the body has settled, and again after teardown, before the verdict, within the cleanup budget: report a failure that landed
    /// on a path no step awaited (a callback the session ran for the test: a request interceptor, a native dialog handler). Wait for one
    /// still running, then throw its error; the attempt fails with it the way a step would, an error already classified keeping its code.
    /// MUST report each failure once. Upstream's <c>settleAttempt</c>, whose <c>EngineCleanupContext</c> is the timeout and the token.
    /// </summary>
    Task SettleAttemptAsync(TimeSpan timeout, CancellationToken cancellationToken) => Task.CompletedTask;

    private static Task Unsupported(string operation) =>
        Task.FromException(new EngineException("UNSUPPORTED_CAPABILITY", operation + " is not supported by this engine."));
}

/// <summary>A captured viewport: PNG bytes, and how many image pixels one viewport (CSS) pixel takes.</summary>
/// <param name="Png">The image, with every secure field covered.</param>
/// <param name="Scale">Image pixels per viewport (CSS) pixel.</param>
/// <param name="MaskedRegionCount">How many regions the engine covered, so a caller can prove the image is at least as redacted as the tree. Null when the engine cannot say.</param>
public sealed record EngineScreenshot(byte[] Png, double Scale, int? MaskedRegionCount = null);

/// <summary>
/// The facts handed to an engine's prepare (<see cref="IPreparableEngine.PrepareAsync"/>), once per run and target in the runner
/// process, before any worker exists. Upstream's <c>EnginePrepareInfo</c>.
/// </summary>
public sealed record EnginePrepareInfo
{
    /// <summary>The run id.</summary>
    public required string RunId { get; init; }

    /// <summary>The target being prepared.</summary>
    public required string TargetName { get; init; }

    /// <summary>
    /// The worker slots the run will start for this target, <c>0</c> to <see cref="Slots"/> minus one. Provision for exactly these slots
    /// (boot that many devices of a pool); the workers then find them ready. A standalone attempt, as <c>e2e explore</c> and
    /// <c>e2e mcp</c> open, prepares one slot.
    /// </summary>
    public required int Slots { get; init; }

    /// <summary>
    /// The run's environment: what every worker is started with. A host may hand the run an environment other than the process's own,
    /// so anything provisioned here that a worker later looks up by environment must be resolved against this, not the process
    /// environment.
    /// </summary>
    public required IReadOnlyDictionary<string, string?> Env { get; init; }

    /// <summary>
    /// The directory relative paths in the config resolve against: where an engine resolves a relative option, such as a build to install.
    /// Null leaves it to the working directory. Upstream's <c>projectRoot</c>.
    /// </summary>
    public string? ProjectRoot { get; init; }

    /// <summary>
    /// Reports one line of progress. A run streams it as a notice to the reporter; the standalone host of <c>e2e explore</c> and
    /// <c>e2e mcp</c> has no reporter, so it drops the line.
    /// </summary>
    public required Action<string> Log { get; init; }
}

/// <summary>What prepare learned that the run must honour. Upstream's <c>EnginePrepareResult</c>.</summary>
public sealed record EnginePrepareResult
{
    /// <summary>
    /// Upstream's contract: lowers the target's worker cap for this run, <c>1</c> to <see cref="EnginePrepareInfo.Slots"/>: the surfaces the engine actually
    /// provisioned, one per leased resource. Null when prepare bound nothing, so the declared cap stays. The standalone host of
    /// <c>e2e explore</c> and <c>e2e mcp</c> starts one worker and reads no cap. The port has no test runner to apply it, and no host in
    /// this repository reads it.
    /// </summary>
    public int? Workers { get; init; }

    /// <summary>
    /// Variables every worker of this target is started with, on top of the run's environment: how prepare hands what it provisioned
    /// (a device binding) to each worker's start, in <see cref="EngineStartOptions.Env"/>. The run's own variables win on a clash.
    /// </summary>
    public IReadOnlyDictionary<string, string> Env { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// The facts handed to an engine's finish (<see cref="IPreparableEngine.FinishAsync"/>), once per run and target after the last worker is
/// gone, with the cleanup budget of one hook. Upstream's <c>EngineFinishInfo</c>.
/// </summary>
public sealed record EngineFinishInfo
{
    /// <summary>The run id.</summary>
    public required string RunId { get; init; }

    /// <summary>The target being finished.</summary>
    public required string TargetName { get; init; }

    /// <summary>The run's environment, the same prepare saw.</summary>
    public required IReadOnlyDictionary<string, string?> Env { get; init; }

    /// <summary>Reports one line of progress, as <see cref="EnginePrepareInfo.Log"/> does.</summary>
    public required Action<string> Log { get; init; }

    /// <summary>
    /// The cleanup budget the host leaves this call: the finish hooks of every prepared target share one <c>cleanupTimeout</c>, so the
    /// host hands each what is left of it when the call starts and cancels the call's token when it is spent. A hook that cannot finish in
    /// time stops instead of hanging the run; one that ignores the token is abandoned at the deadline with <c>CLEANUP_TIMEOUT</c>.
    /// Upstream's <c>timeoutMs</c>.
    /// </summary>
    public TimeSpan CleanupTimeout { get; init; } = E2EDefaults.CleanupTimeout;
}

/// <summary>What a worker's init is given, once per worker, before its first attempt. Upstream's <c>EngineInitInfo</c>.</summary>
public sealed record EngineInitInfo
{
    public required string RunId { get; init; }

    public required string TargetName { get; init; }

    /// <summary>The worker slot this worker serves, <c>0</c> to the target's workers minus one.</summary>
    public required int WorkerSlot { get; init; }

    /// <summary>The run's environment with what prepare left in it.</summary>
    public required IReadOnlyDictionary<string, string?> Env { get; init; }

    public required Action<string> Log { get; init; }
}
