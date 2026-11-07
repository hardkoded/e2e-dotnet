// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Engine;

/// <summary>
/// Upstream's <c>createEngineSession</c> (<c>engine/session.ts</c>): the engine-to-session adapter, the single seam every engine call
/// crosses. One adapter per attempt wraps the engine's session. It enforces the rules of that seam the port has here: anything an engine
/// throws that is not an <see cref="E2EException"/> is normalized to a non-retryable <c>ENGINE_FAILURE</c> (<c>${label} failed: ${message}</c>,
/// upstream's <c>normalize</c>), so a crashed engine is an infrastructure failure everywhere and never a retry-eligible test failure, and a
/// <c>press</c> key outside the contract grammar fails with <c>INVALID_ARGUMENT</c> before the engine sees it.
/// A caller's cancel stays a cancellation (.NET-only: upstream has no cancellation exception). The capability gate upstream puts in this seam
/// lives where the port already has it, in <see cref="IEngineSession.ActionKinds"/> and <see cref="IEngineSession.PointerActionKinds"/> read by the
/// screen and the dispatcher, and in the members' <c>UNSUPPORTED_CAPABILITY</c> defaults.
/// </summary>
internal sealed class EngineSession : IEngineSession, IEngineFixtureHost
{
    private readonly IEngineSession _engine;

    private EngineSession(IEngineSession engine) => _engine = engine;

    /// <summary>Wraps <paramref name="engine"/> in the adapter; a session that is wrapped already is returned as it is.</summary>
    internal static IEngineSession Create(IEngineSession engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return engine as EngineSession ?? new EngineSession(engine);
    }

    /// <summary>
    /// The engine's own session behind the adapter. .NET-only: a caller that needs the engine's own type (the <see cref="Browser"/> fixture's
    /// <see cref="IBrowserSession"/>, the web engine's test hooks) reads it through the adapter's wrapper.
    /// </summary>
    internal static IEngineSession Unwrap(IEngineSession session) => session is EngineSession adapter ? adapter._engine : session;

    /// <summary>
    /// Upstream's <c>guard</c> with its <c>normalize</c>: one declared member as a call whose throw is put onto the error contract. An e2e
    /// error (an engine error, or a runner error) passes through, and so does a cancellation (.NET-only); anything else is the engine failing
    /// outside its contract, reported as infrastructure. The message is the engine's own text, so it is redacted.
    /// </summary>
    private static async Task<T> Guard<T>(string label, CancellationToken cancellationToken, Func<Task<T>> call)
    {
        try
        {
            return await call().ConfigureAwait(false);
        }
        catch (Exception cause) when (cause is not (OperationCanceledException or E2EException))
        {
            // A caller's cancel surfaces as a cancellation, whatever the engine threw for it (a closed target, a disposed handle) (.NET-only).
            cancellationToken.ThrowIfCancellationRequested();
            throw new EngineException(EngineErrorCodes.EngineFailure, label + " failed: " + ErrorRedaction.Redact(cause.Message), retryable: false, cause);
        }
    }

    private static Task Guard(string label, CancellationToken cancellationToken, Func<Task> call) =>
        Guard(label, cancellationToken, async () =>
        {
            await call().ConfigureAwait(false);
            return true;
        });

    public string Route => _engine.Route;

    public IReadOnlyCollection<string> ActionKinds => _engine.ActionKinds;

    public IReadOnlyCollection<string> PointerActionKinds => _engine.PointerActionKinds;

    public bool CanDismissKeyboard => _engine.CanDismissKeyboard;

    public bool CanLocate => _engine.CanLocate;

    public Task OpenAsync(string url, CancellationToken cancellationToken) =>
        Guard("navigation", cancellationToken, () => _engine.OpenAsync(url, cancellationToken));

    public Task<Observation> ObserveAsync(CancellationToken cancellationToken) =>
        Guard("observation", cancellationToken, () => _engine.ObserveAsync(cancellationToken));

    public Task<Observation> ObserveAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        Guard("observation", cancellationToken, () => _engine.ObserveAsync(timeout, cancellationToken));

    public Task<Observation> ObserveAsync(ObserveOptions? options, TimeSpan timeout, CancellationToken cancellationToken) =>
        Guard("observation", cancellationToken, () => _engine.ObserveAsync(options, timeout, cancellationToken));

    public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) =>
        Guard("actions", cancellationToken, () =>
        {
            RequireKeyOf(action);
            return _engine.PerformAsync(node, action, cancellationToken);
        });

    public Task PerformAsync(SemanticNode node, LocatorAction action, OperationContext context, CancellationToken cancellationToken) =>
        Guard("actions", cancellationToken, () =>
        {
            RequireKeyOf(action);
            return _engine.PerformAsync(node, action, context, cancellationToken);
        });

    public Task PerformAtAsync(Point point, PointerAction action, CancellationToken cancellationToken) =>
        Guard("point actions", cancellationToken, () => _engine.PerformAtAsync(point, action, cancellationToken));

    public Task PerformAtAsync(Point point, PointerAction action, OperationContext context, CancellationToken cancellationToken) =>
        Guard("point actions", cancellationToken, () => _engine.PerformAtAsync(point, action, context, cancellationToken));

    public Task PressAsync(string key, CancellationToken cancellationToken) =>
        Guard("keyboard input", cancellationToken, () =>
        {
            Keys.RequireKey(key);
            return _engine.PressAsync(key, cancellationToken);
        });

    public Task TypeTextAsync(string text, bool replace, CancellationToken cancellationToken) =>
        Guard("keyboard input", cancellationToken, () => _engine.TypeTextAsync(text, replace, cancellationToken));

    public Task DismissKeyboardAsync(CancellationToken cancellationToken) =>
        Guard("keyboard dismissal", cancellationToken, () => _engine.DismissKeyboardAsync(cancellationToken));

    public Task<IReadOnlyList<SemanticNode>> LocateAsync(LocatorExpression expression, TimeSpan timeout, CancellationToken cancellationToken) =>
        Guard("locators (screen)", cancellationToken, () => _engine.LocateAsync(expression, timeout, cancellationToken));

    public Task SwipeAsync(ScrollDirection direction, CancellationToken cancellationToken) =>
        Guard("actions", cancellationToken, () => _engine.SwipeAsync(direction, cancellationToken));

    public Task SwipeAsync(ScrollDirection direction, Momentum? momentum, CancellationToken cancellationToken) =>
        Guard("actions", cancellationToken, () => _engine.SwipeAsync(direction, momentum, cancellationToken));

    public Task SwipeAsync(ScrollDirection direction, Momentum? momentum, OperationContext context, CancellationToken cancellationToken) =>
        Guard("actions", cancellationToken, () => _engine.SwipeAsync(direction, momentum, context, cancellationToken));

    public Task BackAsync(CancellationToken cancellationToken) =>
        Guard("back navigation", cancellationToken, () => _engine.BackAsync(cancellationToken));

    public Task BackAsync(OperationContext context, CancellationToken cancellationToken) =>
        Guard("back navigation", cancellationToken, () => _engine.BackAsync(context, cancellationToken));

    public Task ResizeAsync(TraceViewport size, OperationContext context, CancellationToken cancellationToken) =>
        Guard("viewport resizing", cancellationToken, () => _engine.ResizeAsync(size, context, cancellationToken));

    public Task RestartAsync(CancellationToken cancellationToken) =>
        Guard("app restart", cancellationToken, () => _engine.RestartAsync(cancellationToken));

    public Task ClearStateAsync(CancellationToken cancellationToken) =>
        Guard("app state reset", cancellationToken, () => _engine.ClearStateAsync(cancellationToken));

    public Task<EngineScreenshot> ScreenshotAsync(CancellationToken cancellationToken) =>
        Guard("screenshots", cancellationToken, () => _engine.ScreenshotAsync(cancellationToken));

    public Task<EngineScreenshot> ScreenshotAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        Guard("screenshots", cancellationToken, () => _engine.ScreenshotAsync(timeout, cancellationToken));

    public Task<string> ScreenshotArtifactAsync(string? label, CancellationToken cancellationToken) =>
        Guard("screenshots", cancellationToken, () => _engine.ScreenshotArtifactAsync(label, cancellationToken));

    public Task<string> ScreenshotArtifactAsync(string? label, TimeSpan timeout, CancellationToken cancellationToken) =>
        Guard("screenshots", cancellationToken, () => _engine.ScreenshotArtifactAsync(label, timeout, cancellationToken));

    public Task StartVideoAsync(CancellationToken cancellationToken) =>
        Guard("video recording", cancellationToken, () => _engine.StartVideoAsync(cancellationToken));

    public Task<IReadOnlyList<VideoSegment>> StopVideoAsync(CancellationToken cancellationToken) =>
        Guard("video recording", cancellationToken, () => _engine.StopVideoAsync(cancellationToken));

    public Task<EngineState> CaptureStateAsync(CancellationToken cancellationToken) =>
        Guard("state capture", cancellationToken, () => _engine.CaptureStateAsync(cancellationToken));

    public Task RestoreStateAsync(EngineState state, CancellationToken cancellationToken) =>
        Guard("state restore", cancellationToken, () => _engine.RestoreStateAsync(state, cancellationToken));

    public Task SettleAttemptAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        Guard("attempt settle", cancellationToken, () => _engine.SettleAttemptAsync(timeout, cancellationToken));

    /// <summary>Releasing the session belongs to the worker (upstream's <c>dispose()</c>), and is not normalized.</summary>
    public ValueTask DisposeAsync() => _engine.DisposeAsync();

    void IEngineFixtureHost.CreateFixtures(EngineFixtureContext context) => (_engine as IEngineFixtureHost)?.CreateFixtures(context);

    private static void RequireKeyOf(LocatorAction action)
    {
        if (action is LocatorAction.Press press)
        {
            Keys.RequireKey(press.Key);
        }
    }
}
