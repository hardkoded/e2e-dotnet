// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.AgentFallbackHandoff;

/// <summary>
/// The tests' <c>defineEngine</c> (upstream's <c>agent-fallback-handoff.test.ts</c>): an engine whose looks, actions, and keyboard are the
/// test's own functions, so a test scripts a screen that loses its tree, a point a tap lands on, or an action that throws. Every
/// function is optional. A look with no <see cref="Observe"/> lists the one Save button.
/// </summary>
internal sealed class FallbackEngine : IEngine
{
    /// <summary>The engine's look: the observe options the agent asked for, the timeout it has left, and the token of the call.</summary>
    public Func<ObserveOptions?, TimeSpan, CancellationToken, Task<Observation>>? Observe { get; init; }

    /// <summary>Runs for a locator action on a node; a throw is the action's failure.</summary>
    public Action<SemanticNode, LocatorAction>? Perform { get; init; }

    /// <summary>Runs for a pointer action at a point; a throw is the action's failure.</summary>
    public Action<Point, PointerAction>? PerformAt { get; init; }

    /// <summary>Runs for the keyboard's type, with the text and whether it replaces the field.</summary>
    public Action<string, bool>? TypeText { get; init; }

    /// <summary>Runs for a viewport swipe; a throw is the action's failure.</summary>
    public Action<ScrollDirection>? Swipe { get; init; }

    /// <summary>Runs when an attempt's engine session starts (upstream's <c>startAttempt</c>).</summary>
    public Action? StartAttempt { get; init; }

    /// <summary>Resolves a locator to the nodes it names, without a look (upstream's <c>locate</c>). Unset, the session resolves it in core.</summary>
    public Func<LocatorExpression, IReadOnlyList<SemanticNode>>? Locate { get; init; }

    /// <summary>The locator action kinds the engine declares (upstream's <c>actions</c>).</summary>
    public IReadOnlyCollection<string> LocatorKinds { get; init; } = EngineActionKinds.Locator;

    /// <summary>The pointer action kinds the engine declares (upstream's <c>pointerActions</c>).</summary>
    public IReadOnlyCollection<string> PointerKinds { get; init; } = [];

    /// <summary>The route the session reports as its current page.</summary>
    public string Route { get; init; } = "/";

    public string Platform => "fixture";

    public string Version => "1";

    public EngineCapabilities Capabilities =>
        EngineCapabilities.Observation
        | EngineCapabilities.Actions
        | EngineCapabilities.Location
        | EngineCapabilities.Keyboard
        | EngineCapabilities.Scroll
        | EngineCapabilities.Screenshot;

    public Task<IEngineSession> StartAsync(EngineStartOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StartAttempt?.Invoke();
        return Task.FromResult<IEngineSession>(new Session(this));
    }

    private sealed class Session(FallbackEngine engine) : IEngineSession
    {
        public string Route => engine.Route;

        public IReadOnlyCollection<string> ActionKinds => engine.LocatorKinds;

        public IReadOnlyCollection<string> PointerActionKinds => engine.PointerKinds;

        public Task OpenAsync(string url, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Observation> ObserveAsync(CancellationToken cancellationToken) => ObserveAsync(null, TimeSpan.FromSeconds(5), cancellationToken);

        public Task<Observation> ObserveAsync(ObserveOptions? options, TimeSpan timeout, CancellationToken cancellationToken) =>
            engine.Observe is { } observe
                ? observe(options, timeout, cancellationToken)
                : Task.FromResult(FallbackFixtures.TreeOf(FallbackFixtures.Save));

        public bool CanLocate => engine.Locate is not null;

        public Task<IReadOnlyList<SemanticNode>> LocateAsync(LocatorExpression expression, TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromResult(engine.Locate?.Invoke(expression) ?? []);

        public Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken)
        {
            engine.Perform?.Invoke(node, action);
            return Task.CompletedTask;
        }

        public Task PerformAtAsync(Point point, PointerAction action, CancellationToken cancellationToken)
        {
            engine.PerformAt?.Invoke(point, action);
            return Task.CompletedTask;
        }

        public Task TypeTextAsync(string text, bool replace, CancellationToken cancellationToken)
        {
            engine.TypeText?.Invoke(text, replace);
            return Task.CompletedTask;
        }

        public Task PressAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SwipeAsync(ScrollDirection direction, CancellationToken cancellationToken)
        {
            engine.Swipe?.Invoke(direction);
            return Task.CompletedTask;
        }

        public Task BackAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RestartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ClearStateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<EngineScreenshot> ScreenshotAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new EngineScreenshot(FallbackFixtures.Shot(1), 1, 0));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

/// <summary>Screens and pixels the fallback tests build, as upstream's <c>pixelSnapshot</c> and its tree screens do.</summary>
internal static class FallbackFixtures
{
    /// <summary>The viewport every screen here reports, 2 by 2 CSS pixels.</summary>
    public static TraceViewport Viewport { get; } = new(2, 2);

    /// <summary>One button named Save, the node most screens here list.</summary>
    public static SemanticNode Save { get; } = new() { Ref = "save", Role = "button", Name = "Save" };

    /// <summary>A tree look over the given nodes on the test viewport.</summary>
    public static Observation TreeOf(params SemanticNode[] nodes) => TreeAt("/", null, null, nodes);

    /// <summary>A look at a route, with the location the engine reports when it differs from the route, the pixels it takes (if any), and the nodes it lists.</summary>
    public static Observation TreeAt(string route, string? location, EngineScreenshot? pixels, params SemanticNode[] nodes) =>
        new()
        {
            Route = route,
            Location = location,
            Roots = nodes,
            Viewport = Viewport,
            Pixels = pixels,
        };

    /// <summary>The engine keeps a viewport reference while exposing no semantic nodes: a look with pixels and no tree.</summary>
    public static Observation PixelSnapshot(byte value = 1) =>
        new()
        {
            Route = "fixture:result",
            Roots = [],
            Viewport = Viewport,
            TreeUnavailable = true,
            Pixels = Screenshot(value),
        };

    /// <summary>The screenshot of <see cref="PixelSnapshot"/>, with its masked regions counted as none.</summary>
    public static EngineScreenshot Screenshot(byte value) => new(Shot(value), 1, 0);

    /// <summary>A 2 by 2 PNG whose first byte is <paramref name="value"/>, so each capture differs from the one before it.</summary>
    public static byte[] Shot(byte value)
    {
        var data = new byte[2 * 2 * 4];
        for (var at = 0; at < data.Length; at += 4)
        {
            data[at] = value;
            data[at + 1] = 2;
            data[at + 2] = 3;
            data[at + 3] = 255;
        }

        return ImageOps.EncodePng(new RgbaImage(2, 2, data));
    }
}
