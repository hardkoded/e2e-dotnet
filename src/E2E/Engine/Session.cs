// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Reports;

namespace E2E.Engine;

/// <summary>Upstream's <c>createEngineSession</c> options (<c>engine/session.ts</c>).</summary>
public sealed record EngineSessionOptions
{
    /// <summary>The engine handle, or null for a target that declares none.</summary>
    public EngineHandle? Engine { get; init; }

    /// <summary>The target's name, for error messages.</summary>
    public required string TargetName { get; init; }

    /// <summary>Whether the attempt keeps a trace; without one, no screen is taken for it.</summary>
    public bool Traced { get; init; }
}

/// <summary>
/// Upstream's <c>createEngineSession</c> (<c>engine/session.ts</c>): the engine-to-session adapter. One adapter per attempt satisfies
/// <see cref="ITargetSession"/> over the engine contract, and is the single seam every engine call crosses. A capability the engine does not
/// declare fails loud with <c>UNSUPPORTED_CAPABILITY</c> at the moment of use, a <c>press</c> key outside the grammar fails with
/// <c>INVALID_ARGUMENT</c> before any engine sees it, and anything an engine throws that is not an <c>EngineException</c> is normalized to a
/// non-retryable <c>ENGINE_FAILURE</c>. .NET-only holder name: C# has no free functions.
/// </summary>
public static class EngineSessions
{
    /// <summary>Located nodes are pruned oldest-first past this bound, so the map cannot grow unboundedly.</summary>
    public const int MaxLocatedRefs = 2048;

    /// <summary>
    /// The action kinds an engine must declare for each kind-derived grammar verb. <c>check</c> sets a state rather than flipping one, so it needs
    /// both directions; <c>type</c> unlocks the plain and the secret fill alike.
    /// </summary>
    private static readonly (string Verb, string[] Kinds)[] KindsByVerb =
    [
        ("tap", ["tap"]),
        ("doubleTap", ["doubleTap"]),
        ("longPress", ["longPress"]),
        ("secondaryTap", ["secondaryTap"]),
        ("hover", ["hover"]),
        ("type", ["fill"]),
        ("typeSecret", ["fill"]),
        ("press", ["press"]),
        ("select", ["selectOption"]),
        ("check", ["check", "uncheck"]),
        ("scroll", ["swipe"]),
        ("scrollTo", ["scrollIntoView"]),
        ("scrollUntil", ["swipe"]),
        ("drag", ["dragTo"]),
        ("upload", ["setInputFiles"]),
    ];

    /// <summary>Builds one session over an engine. Revisions are minted per adapter, so refs can never leak across attempts.</summary>
    public static ITargetSession CreateEngineSession(EngineSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new EngineSession(options, TimeProvider.System);
    }

    /// <summary>Counts the secure nodes of a tree, as the redaction counts of an observation do.</summary>
    internal static int CountSecure(EngineNode node)
    {
        var count = node.States?.Secure == true ? 1 : 0;
        foreach (var child in node.Children ?? [])
        {
            count += CountSecure(child);
        }

        return count;
    }

    /// <summary>Stamps the adapter's revision onto every ref of a tree, since the engine mints stable ids and the adapter owns revisions.</summary>
    internal static EngineNode StampRevision(EngineNode node, string revision) => node with
    {
        Ref = node.Ref with { Revision = revision },
        Children = node.Children?.Select(child => StampRevision(child, revision)).ToArray(),
    };

    /// <summary>The grammar verbs an engine's declared kinds and hooks can honor.</summary>
    internal static IReadOnlySet<string> DeclaredVerbs(EngineHandle? engine)
    {
        var verbs = new HashSet<string>(StringComparer.Ordinal);
        var kinds = new HashSet<string>(engine?.Actions ?? [], StringComparer.Ordinal);
        foreach (var (verb, required) in KindsByVerb)
        {
            if (required.All(kinds.Contains))
            {
                verbs.Add(verb);
            }
        }

        var pointer = engine?.PointerActions ?? [];
        if (pointer.Contains("tap"))
        {
            verbs.Add("tapAt");
        }

        if (pointer.Contains("hover"))
        {
            verbs.Add("hoverAt");
        }

        if (pointer.Contains("dragTo"))
        {
            verbs.Add("dragAt");
        }

        if (engine?.Session?.Back is not null)
        {
            verbs.Add("back");
        }

        if (engine?.Session?.Resize is not null)
        {
            verbs.Add("resize");
        }

        if (engine?.Keyboard is { } keyboard)
        {
            verbs.Add("typeText");
            verbs.Add("pressKey");
            if (keyboard.Dismiss is not null)
            {
                verbs.Add("dismissKeyboard");
            }
        }

        if (engine?.Session?.Open is not null)
        {
            verbs.Add("navigate");
        }

        return verbs;
    }

    /// <summary>The pointer kind name of a point action, as the engine declares it.</summary>
    internal static string PointerKind(PointerAction action) => action switch
    {
        PointerAction.Tap => "tap",
        PointerAction.DoubleTap => "doubleTap",
        PointerAction.LongPress => "longPress",
        PointerAction.SwipeTo => "swipeTo",
        PointerAction.SecondaryTap => "secondaryTap",
        PointerAction.Hover => "hover",
        PointerAction.DragTo => "dragTo",
        PointerAction.Swipe => "swipe",
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    /// <summary>The cleanup budget of a call: the operation's timeout, or the unbounded budget when the call carries none.</summary>
    internal static TimeSpan Budget(OperationContext operation) => operation.Timeout ?? E2E.Internal.Time.Unbounded;

    /// <summary>Upstream's <c>requireKey</c>: a <c>press</c> key outside the grammar fails before any engine sees it.</summary>
    internal static void RequireKey(string key)
    {
        if (KeyGrammar.ParseKey(key) is null)
        {
            throw new TestException("INVALID_ARGUMENT", $"key \"{key}\" is outside the key grammar");
        }
    }

    private sealed class EngineSession : ITargetSession
    {
        private readonly EngineHandle? _engine;
        private readonly string _targetName;
        private readonly TimeProvider _clock;
        private readonly Dictionary<string, EngineNode> _located = new(StringComparer.Ordinal);
        private readonly List<string> _locatedOrder = [];
        private int _revision;
        private int _locateRevision;
        private bool _ended;
        private NodeRef? _root;

        internal EngineSession(EngineSessionOptions options, TimeProvider clock)
        {
            _engine = options.Engine;
            _targetName = options.TargetName;
            _clock = clock;
            Verbs = DeclaredVerbs(_engine);
            Actions = new HashSet<string>(_engine?.Actions ?? [], StringComparer.Ordinal);
            PointerActions = new HashSet<string>(_engine?.PointerActions ?? [], StringComparer.Ordinal);
            TapModifiers = _engine?.TapModifiers == true;
            App = new SessionApp(this);
            Artifacts = new SessionArtifacts(this);
            Keyboard = new SessionKeyboard(this);
            AppLog = new AppLogRoute(clock);
            Screens = new ScreenRoute(options.Traced, clock);
            Environment = new EnvironmentFacts();
            CaptureState = _engine?.State is { } state ? (operation) => Call<EngineState>("state capture", state.Capture is null ? null : () => state.Capture(operation)) : null;
            RestoreState = _engine?.State is { } restoreState
                ? (captured, operation) => Call("state restore", restoreState.Restore is null ? null : () => restoreState.Restore(captured, operation))
                : null;
        }

        public IReadOnlySet<string> Verbs { get; }

        public IReadOnlySet<string> Actions { get; }

        public IReadOnlySet<string> PointerActions { get; }

        public bool TapModifiers { get; }

        public ISessionKeyboard Keyboard { get; }

        public ISessionApp App { get; }

        public ISessionArtifacts Artifacts { get; }

        public AppLogRoute AppLog { get; }

        public ScreenRoute Screens { get; }

        public EnvironmentFacts Environment { get; }

        public Func<OperationContext, Task<EngineState>>? CaptureState { get; }

        public Func<EngineState, OperationContext, Task>? RestoreState { get; }

        /// <summary>Throws the engine's unsupported-capability error for a member the engine does not declare.</summary>
        internal ConfigurationException Unsupported(string what)
        {
            var remedy = _engine is null
                ? "the target declares no engine; add one, such as the web engine or a mobile engine"
                : $"engine {_engine.Name} does not implement it";
            return new ConfigurationException("UNSUPPORTED_CAPABILITY", $"target \"{_targetName}\" has no engine capability for {what}: {remedy}");
        }

        /// <summary>One declared member as a guarded call: capability-checked at the moment of use, and error-normalized.</summary>
        internal async Task Call(string what, Func<Task>? invoke)
        {
            if (invoke is null)
            {
                throw Unsupported(what);
            }

            try
            {
                await invoke().ConfigureAwait(false);
            }
            catch (Exception cause) when (cause is not E2EException)
            {
                throw Normalize(cause, what);
            }
        }

        /// <summary>The value-returning form of <see cref="Call(string, Func{Task})"/>.</summary>
        internal async Task<T> Call<T>(string what, Func<Task<T>>? invoke)
        {
            if (invoke is null)
            {
                throw Unsupported(what);
            }

            try
            {
                return await invoke().ConfigureAwait(false);
            }
            catch (Exception cause) when (cause is not E2EException)
            {
                throw Normalize(cause, what);
            }
        }

        public async Task<SessionObservation> ObserveAsync(OperationContext operation, ObserveOptions? options = null)
        {
            var observe = _engine?.Observe ?? throw Unsupported("observation");
            var snapshot = await Call("observation", () => observe(operation, options)).ConfigureAwait(false);
            var minted = $"b{_revision + 1}";
            var root = new NodeRef(snapshot.Root.Ref.Id, minted);
            var redaction = new ObservationRedaction(CountSecure(snapshot.Root), snapshot.MaskedRegionCount ?? 0);
            if (snapshot.TreeUnavailable)
            {
                if (options?.PixelFallback != true || snapshot.Pixels is null)
                {
                    throw new TestException("UNSUPPORTED_CAPABILITY", "unavailable semantics require permitted fallback pixels");
                }

                if (snapshot.Root.Role is not null || snapshot.Root.Name is not null || snapshot.Root.Children is { Count: > 0 } || snapshot.Root.States is not null)
                {
                    throw new EngineException("INVALID_STATE", "an unavailable semantic tree must contain only the stable root reference", retryable: false);
                }

                _located.Clear();
                _locatedOrder.Clear();
                _revision++;
                _root = root;
                return new SessionObservation
                {
                    Kind = "pixels",
                    Root = root,
                    Revision = minted,
                    CapturedAt = _clock.GetUtcNow().ToString("O"),
                    Location = snapshot.Location,
                    Viewport = snapshot.Viewport,
                    Redaction = redaction,
                    Pixels = snapshot.Pixels,
                };
            }

            _revision++;
            _root = root;
            var observation = new SessionObservation
            {
                Kind = "semantic",
                Root = root,
                Revision = minted,
                CapturedAt = _clock.GetUtcNow().ToString("O"),
                Location = snapshot.Location,
                Viewport = snapshot.Viewport,
                Redaction = redaction,
                Tree = StampRevision(snapshot.Root, minted),
                Truncated = snapshot.Truncated,
                KeyboardVisible = snapshot.KeyboardVisible,
                Pixels = snapshot.Pixels,
            };
            Screens.Push(snapshot);
            return observation;
        }

        public async Task<IReadOnlyList<NodeRef>> LocateAsync(LocatorExpression expression, OperationContext operation)
        {
            var locate = _engine?.Locate ?? throw Unsupported("locators (screen)");
            var nodes = (await Call("locators (screen)", () => locate(expression, operation)).ConfigureAwait(false))
                .Where(node => !(expression.Visible && node.States?.Hidden == true))
                .ToList();
            _locateRevision++;
            var locateRev = $"l{_locateRevision}";
            var refs = new List<NodeRef>(nodes.Count);
            foreach (var node in nodes)
            {
                var stamped = StampRevision(node, locateRev);
                _located.Remove(stamped.Ref.Id);
                _located[stamped.Ref.Id] = stamped;
                _locatedOrder.Add(stamped.Ref.Id);
                refs.Add(stamped.Ref);
            }

            // Pruned oldest-first: the order list holds one entry per located id, so the oldest entries are the first that are still mapped.
            while (_located.Count > MaxLocatedRefs && _locatedOrder.Count > 0)
            {
                var oldest = _locatedOrder[0];
                _locatedOrder.RemoveAt(0);
                _located.Remove(oldest);
            }

            return refs;
        }

        public Task<EngineNode> ReadAsync(NodeRef reference, OperationContext operation)
        {
            if (_engine?.Locate is null)
            {
                throw Unsupported("locators (screen)");
            }

            if (!_located.TryGetValue(reference.Id, out var node) || node.Ref.Revision != reference.Revision)
            {
                throw Stale(reference);
            }

            return Task.FromResult(node);
        }

        public async Task PerformAsync(NodeRef reference, LocatorAction action, OperationContext operation)
        {
            var kind = EngineActionKinds.Of(action);
            if (!Actions.Contains(kind))
            {
                throw Unsupported($"the \"{kind}\" action");
            }

            if (action is LocatorAction.Tap { Modifiers.Count: > 0 } or LocatorAction.DoubleTap { Modifiers.Count: > 0 } or LocatorAction.SecondaryTap { Modifiers.Count: > 0 } && !TapModifiers)
            {
                throw Unsupported($"modifiers on the \"{kind}\" action");
            }

            if (action is LocatorAction.Press press)
            {
                RequireKey(press.Key);
            }

            RejectSupersededLocate(reference);
            var perform = _engine?.Perform ?? throw Unsupported("actions");
            await Call("actions", () => perform(reference, action, operation)).ConfigureAwait(false);
        }

        public async Task SwipeAsync(ScrollDirection direction, Momentum? momentum, OperationContext operation)
        {
            if (!Actions.Contains("swipe"))
            {
                throw Unsupported("swipe gestures");
            }

            var target = _root ?? (await ObserveAsync(operation).ConfigureAwait(false)).Root;
            await PerformAsync(target, new LocatorAction.Swipe(direction, momentum), operation).ConfigureAwait(false);
        }

        public async Task PerformAtAsync(Point point, PointerAction action, OperationContext operation)
        {
            var kind = PointerKind(action);
            if (!PointerActions.Contains(kind))
            {
                throw Unsupported($"the \"{kind}\" action at a point");
            }

            var performAt = _engine?.PerformAt ?? throw Unsupported("point actions");
            await Call("point actions", () => performAt(point, action, operation)).ConfigureAwait(false);
        }

        public async Task SettleAsync(OperationContext operation)
        {
            if (_engine?.SettleAttempt is { } settle)
            {
                await settle(new EngineCleanupContext(operation.Signal, Budget(operation))).ConfigureAwait(false);
            }
        }

        public async Task CloseAsync(OperationContext operation)
        {
            if (_ended)
            {
                return;
            }

            // The engine outlives the attempt; only the per-attempt isolation ends here, exactly once.
            _ended = true;
            if (_engine?.EndAttempt is { } end)
            {
                await Call("attempt end", () => end(new EngineCleanupContext(operation.Signal, Budget(operation)))).ConfigureAwait(false);
            }
        }

        /// <summary>A located ref from a superseded resolution never reaches the engine. Observation refs are exempt: the engine checks them.</summary>
        private void RejectSupersededLocate(NodeRef reference)
        {
            if (!reference.Revision.StartsWith('l'))
            {
                return;
            }

            if (_located.TryGetValue(reference.Id, out var known) && known.Ref.Revision != reference.Revision)
            {
                throw Stale(reference);
            }
        }

        private static EngineException Stale(NodeRef reference) =>
            new(EngineErrorCodes.NodeStale, $"node {reference.Id} is stale; re-resolve", retryable: true);

        /// <summary>Normalizes what an engine threw onto the error contract: anything outside it is an engine failure, never retryable.</summary>
        private static EngineException Normalize(Exception cause, string what) =>
            new(EngineErrorCodes.EngineFailure, $"{what} failed: {cause.Message}", retryable: false, cause);

        private sealed class SessionKeyboard(EngineSession session) : ISessionKeyboard
        {
            public Task TypeAsync(string text, bool replace, OperationContext operation) =>
                session.Call("keyboard input", session._engine?.Keyboard?.Type is { } type ? () => type(text, replace, operation) : null);

            public Task PressAsync(string key, OperationContext operation)
            {
                RequireKey(key);
                return session.Call("keyboard input", session._engine?.Keyboard?.Press is { } press ? () => press(key, operation) : null);
            }

            public Task DismissAsync(OperationContext operation) =>
                session.Call("keyboard dismissal", session._engine?.Keyboard?.Dismiss is { } dismiss ? () => dismiss(operation) : null);
        }

        private sealed class SessionApp(EngineSession session) : ISessionApp
        {
            public Task OpenAsync(string url, OperationContext operation) =>
                session.Call("navigation", session._engine?.Session?.Open is { } open ? () => open(url, operation) : null);

            public Task BackAsync(OperationContext operation) =>
                session.Call("back navigation", session._engine?.Session?.Back is { } back ? () => back(operation) : null);

            public Task ResizeAsync(ViewportSize size, OperationContext operation) =>
                session.Call("viewport resizing", session._engine?.Session?.Resize is { } resize ? () => resize(size, operation) : null);

            public Task RestartAsync(OperationContext operation) =>
                session.Call("app restart", session._engine?.Session?.Restart is { } restart ? () => restart(operation) : null);

            public Task ResetAsync(OperationContext operation) =>
                session.Call("app state reset", session._engine?.Session?.Reset is { } reset ? () => reset(operation) : null);
        }

        private sealed class SessionArtifacts(EngineSession session) : ISessionArtifacts
        {
            public Task<string> ScreenshotAsync(string? label, OperationContext operation) =>
                session.Call<string>("screenshots", session._engine?.Artifacts?.Screenshot is { } screenshot ? () => screenshot(label, operation) : null);

            public Func<OperationContext, Task>? StartVideo =>
                session._engine?.Artifacts?.StartVideo is { } start ? operation => session.Call("video recording", () => start(operation)) : null;

            public Func<OperationContext, Task<IReadOnlyList<VideoSegment>>>? StopVideo =>
                session._engine?.Artifacts?.StopVideo is { } stop
                    ? operation => session.Call("video recording", () => stop(operation))
                    : null;
        }
    }
}
