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
}

/// <summary>Passed to <see cref="IEngine.StartAsync"/> once per test attempt.</summary>
public sealed class EngineStartOptions
{
    public string? BaseUrl { get; init; }

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
    /// The run's environment: where a hosted service's provider reads its token from. Only an engine that starts without
    /// a runner reads it, to prepare the run of its one attempt; it defaults to the process environment.
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
    /// budget left for the resolve, which an engine uses to tell a screen that is still arriving from one that never rendered. Engines
    /// that declare <see cref="EngineCapabilities.Location"/> and set <see cref="CanLocate"/> implement it. Upstream's <c>locate</c>.
    /// </summary>
    Task<IReadOnlyList<SemanticNode>> LocateAsync(LocatorQuery expression, TimeSpan timeout, CancellationToken cancellationToken) =>
        Task.FromException<IReadOnlyList<SemanticNode>>(new EngineException("UNSUPPORTED_CAPABILITY", "locators (screen) is not supported by this engine."));

    /// <summary>
    /// Scrolls the viewport about three quarters of a screen. Engines that declare
    /// <see cref="EngineCapabilities.Scroll"/> implement it.
    /// </summary>
    Task SwipeAsync(ScrollDirection direction, CancellationToken cancellationToken) => Unsupported("scroll");

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
    /// Starts recording the attempt as video, once its page can be recorded. Engines that declare
    /// <see cref="EngineCapabilities.Video"/> implement it. Upstream's <c>startVideo</c>.
    /// </summary>
    Task StartVideoAsync(CancellationToken cancellationToken) => Unsupported("video recording");

    /// <summary>
    /// Stops the recording and returns the segments written, in order: files in the attempt's <c>video</c> directory, or
    /// links to recordings a hosted service keeps. Engines that declare <see cref="EngineCapabilities.Video"/> implement
    /// it. Upstream's <c>stopVideo</c>.
    /// </summary>
    Task<IReadOnlyList<VideoSegment>> StopVideoAsync(CancellationToken cancellationToken) =>
        Task.FromException<IReadOnlyList<VideoSegment>>(new EngineException("UNSUPPORTED_CAPABILITY", "video recording is not supported by this engine."));

    private static Task Unsupported(string operation) =>
        Task.FromException(new EngineException("UNSUPPORTED_CAPABILITY", operation + " is not supported by this engine."));
}

/// <summary>A captured viewport: PNG bytes, and how many image pixels one viewport (CSS) pixel takes.</summary>
/// <param name="Png">The image, with every secure field covered.</param>
/// <param name="Scale">Image pixels per viewport (CSS) pixel.</param>
/// <param name="MaskedRegionCount">How many regions the engine covered, so a caller can prove the image is at least as redacted as the tree. Null when the engine cannot say.</param>
public sealed record EngineScreenshot(byte[] Png, double Scale, int? MaskedRegionCount = null);

/// <summary>What an engine's prepare is given, once per run and target, before any worker starts. Upstream's <c>EnginePrepareInfo</c>.</summary>
public sealed record EnginePrepareInfo
{
    public required string RunId { get; init; }

    public required string TargetName { get; init; }

    /// <summary>Worker slots the run will use for this target.</summary>
    public required int Slots { get; init; }

    public required IReadOnlyDictionary<string, string?> Env { get; init; }

    /// <summary>Reports one line of progress to the run's reporter.</summary>
    public required Action<string> Log { get; init; }
}

/// <summary>What prepare reports back. Upstream's <c>EnginePrepareResult</c>.</summary>
public sealed record EnginePrepareResult
{
    /// <summary>The worker cap of the target for this run, one per leased resource. Null when prepare bound nothing.</summary>
    public int? Workers { get; init; }

    /// <summary>The environment the target's workers read.</summary>
    public IReadOnlyDictionary<string, string> Env { get; init; } = new Dictionary<string, string>();
}

/// <summary>What an engine's finish is given, once per run and target, after the last worker ended. Upstream's <c>EngineFinishInfo</c>.</summary>
public sealed record EngineFinishInfo
{
    public required string RunId { get; init; }

    public required string TargetName { get; init; }

    public required IReadOnlyDictionary<string, string?> Env { get; init; }

    public required Action<string> Log { get; init; }
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
