// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Desktop;

/// <summary>
/// One attempt's session with the desktop app, exposed to the runner as the contract's session. .NET-only adapter: upstream's engine hands the
/// harness the <c>DesktopSurface</c>'s methods through <c>defineEngine</c> (<c>observe</c>, <c>locate</c>, <c>perform</c>, <c>keyboard</c>,
/// <c>session.restart</c>, <c>artifacts.screenshot</c>), and the port's <see cref="IEngineSession"/> is that same set as one object. It holds no
/// state of its own: every call goes to its <see cref="DesktopSurface"/>.
/// </summary>
public sealed class DesktopSession : IEngineSession, IEngineFixtureHost
{
    private readonly DesktopEngine _engine;
    private readonly DesktopSurface _surface;
    private readonly TimeSpan _actionTimeout;
    private EngineFixtureContext? _fixtureContext;
    private Desktop? _desktop;
    private int _disposed;

    internal DesktopSession(DesktopEngine engine, DesktopSurface surface, TimeSpan actionTimeout)
    {
        _engine = engine;
        _surface = surface;
        _actionTimeout = actionTimeout;
    }

    /// <summary>The <c>desktop</c> fixture of this session.</summary>
    public Desktop Desktop => _desktop ??= new Desktop(_surface, () => _fixtureContext, _actionTimeout);

    /// <summary>The surface this session drives: the seam the engine's own tests read.</summary>
    internal DesktopSurface Surface => _surface;

    /// <summary>What the harness hands the desktop fixture when the attempt starts: the context its steps record through.</summary>
    void IEngineFixtureHost.CreateFixtures(EngineFixtureContext context) => _fixtureContext = context;

    /// <inheritdoc />
    public string Route { get; private set; } = "";

    /// <inheritdoc />
    public IReadOnlyCollection<string> ActionKinds => _engine.ActionKinds;

    /// <inheritdoc />
    public IReadOnlyCollection<string> PointerActionKinds => _engine.PointerActionKinds;

    /// <inheritdoc />
    public bool CanLocate => true;

    private DesktopOperation Operation(
        OperationContext context,
        TimeSpan? timeout,
        CancellationToken cancellationToken
    ) => DesktopOperation.Of(context, timeout ?? _actionTimeout, cancellationToken);

    private DesktopOperation Operation(OperationContext context, CancellationToken cancellationToken) =>
        Operation(context, null, cancellationToken);

    /// <summary>
    /// A desktop app has no URL: <c>app.open()</c> is the fresh launch of the target's app, which the runner serves with <c>restart</c> upstream and
    /// the port with <see cref="OpenAsync(string, CancellationToken)"/>.
    /// </summary>
    public Task OpenAsync(string url, CancellationToken cancellationToken) => RestartAsync(cancellationToken);

    /// <inheritdoc />
    public Task OpenAsync(string url, OperationContext context, CancellationToken cancellationToken) =>
        RestartAsync(context, cancellationToken);

    /// <inheritdoc />
    public Task RestartAsync(CancellationToken cancellationToken) =>
        RestartAsync(OperationContext.Agent, cancellationToken);

    /// <inheritdoc />
    public Task RestartAsync(OperationContext context, CancellationToken cancellationToken) =>
        _surface.RestartAsync(Operation(context, cancellationToken));

    /// <inheritdoc />
    public Task<Observation> ObserveAsync(CancellationToken cancellationToken) =>
        ObserveAsync(null, _actionTimeout, cancellationToken);

    /// <inheritdoc />
    public Task<Observation> ObserveAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        ObserveAsync(null, timeout, cancellationToken);

    /// <inheritdoc />
    public async Task<Observation> ObserveAsync(
        ObserveOptions? options,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        var observation = await _surface
            .ObserveAsync(Operation(OperationContext.Agent, timeout, cancellationToken), options)
            .ConfigureAwait(false);
        Route = observation.Route;
        return observation;
    }

    /// <inheritdoc />
    public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken) =>
        PerformAsync(node, action, OperationContext.Agent, cancellationToken);

    /// <inheritdoc />
    public Task PerformAsync(
        SemanticNode node,
        LocatorAction action,
        OperationContext context,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(node);
        return _surface.PerformAsync(node.Ref, action, Operation(context, cancellationToken));
    }

    /// <inheritdoc />
    public Task PerformAtAsync(Point point, PointerAction action, CancellationToken cancellationToken) =>
        PerformAtAsync(point, action, OperationContext.Agent, cancellationToken);

    /// <inheritdoc />
    public Task PerformAtAsync(
        Point point,
        PointerAction action,
        OperationContext context,
        CancellationToken cancellationToken
    ) => _surface.PerformAtAsync(point, action, Operation(context, cancellationToken));

    /// <inheritdoc />
    public Task PressAsync(string key, CancellationToken cancellationToken) =>
        PressAsync(key, OperationContext.Agent, cancellationToken);

    /// <inheritdoc />
    public Task PressAsync(string key, OperationContext context, CancellationToken cancellationToken) =>
        _surface.PressKeyAsync(key, Operation(context, cancellationToken));

    /// <inheritdoc />
    public Task TypeTextAsync(string text, bool replace, CancellationToken cancellationToken) =>
        TypeTextAsync(text, replace, OperationContext.Agent, cancellationToken);

    /// <inheritdoc />
    public Task TypeTextAsync(
        string text,
        bool replace,
        OperationContext context,
        CancellationToken cancellationToken
    ) => _surface.TypeTextAsync(text, replace, Operation(context, cancellationToken));

    /// <inheritdoc />
    public Task<IReadOnlyList<SemanticNode>> LocateAsync(
        LocatorExpression expression,
        TimeSpan timeout,
        CancellationToken cancellationToken
    ) => LocateAsync(expression, timeout, OperationContext.Test, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<SemanticNode>> LocateAsync(
        LocatorExpression expression,
        TimeSpan timeout,
        OperationContext context,
        CancellationToken cancellationToken
    ) => _surface.LocateAsync(expression, Operation(context, timeout, cancellationToken));

    /// <inheritdoc />
    public Task SwipeAsync(ScrollDirection direction, CancellationToken cancellationToken) =>
        SwipeAsync(direction, null, OperationContext.Agent, cancellationToken);

    /// <inheritdoc />
    public Task SwipeAsync(ScrollDirection direction, Momentum? momentum, CancellationToken cancellationToken) =>
        SwipeAsync(direction, momentum, OperationContext.Agent, cancellationToken);

    /// <summary>The window's swipe: upstream swipes the screen root through <c>perform(root, swipe, operation)</c>.</summary>
    public Task SwipeAsync(
        ScrollDirection direction,
        Momentum? momentum,
        OperationContext context,
        CancellationToken cancellationToken
    ) =>
        _surface.PerformAsync(
            DesktopNodes.RootId,
            new LocatorAction.Swipe(direction, momentum),
            Operation(context, cancellationToken)
        );

    /// <inheritdoc />
    public Task<EngineScreenshot> ScreenshotAsync(CancellationToken cancellationToken) =>
        ScreenshotAsync(_actionTimeout, cancellationToken);

    /// <inheritdoc />
    public Task<EngineScreenshot> ScreenshotAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        _surface.CaptureScreenshotAsync(Operation(OperationContext.Test, timeout, cancellationToken));

    /// <inheritdoc />
    public Task<string> ScreenshotArtifactAsync(string? label, CancellationToken cancellationToken) =>
        ScreenshotArtifactAsync(label, _actionTimeout, cancellationToken);

    /// <inheritdoc />
    public Task<string> ScreenshotArtifactAsync(string? label, TimeSpan timeout, CancellationToken cancellationToken) =>
        _surface.ScreenshotAsync(label, Operation(OperationContext.Test, timeout, cancellationToken));

    /// <summary>Ends this attempt: the app the attempt launched is quit, and the worker's private state removed. Upstream's <c>endAttempt</c> then <c>dispose</c>.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _engine.Unregister(this);
        var cleanup = (int)Math.Min(E2EDefaults.CleanupTimeout.TotalMilliseconds, int.MaxValue);
        using var budget = new CancellationTokenSource(E2EDefaults.CleanupTimeout);
        try
        {
            await _surface.EndAttemptAsync(budget.Token, cleanup).ConfigureAwait(false);
        }
        finally
        {
            await _surface.DisposeAsync(budget.Token, cleanup).ConfigureAwait(false);
        }
    }
}
