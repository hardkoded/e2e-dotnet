// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using E2E.Reports;

namespace E2E.Engine;

/// <summary>
/// Upstream's <c>TargetSession</c> (<c>engine/surface.ts</c>): the internal session surface the harness consumes for one attempt. It is
/// produced only by <see cref="EngineSessions.CreateEngineSession"/>, the engine-to-session adapter, so revision minting, staleness checks, and
/// capability gating exist once.
/// </summary>
public interface ITargetSession
{
    /// <summary>The grammar verbs the engine can honor, derived from its declared action kinds and hooks.</summary>
    IReadOnlySet<string> Verbs { get; }

    /// <summary>The action kinds the engine declared for <c>perform</c>; empty without the actions capability.</summary>
    IReadOnlySet<string> Actions { get; }

    /// <summary>The pointer action kinds the engine declared for <c>performAt</c>; empty without the pointer capability.</summary>
    IReadOnlySet<string> PointerActions { get; }

    /// <summary>Whether the engine declared tap modifiers, so the tap actions may carry modifier keys.</summary>
    bool TapModifiers { get; }

    /// <summary>Captures one atomic agent observation; the harness redacts it downstream.</summary>
    Task<SessionObservation> ObserveAsync(OperationContext operation, ObserveOptions? options = null);

    /// <summary>Resolves a query immediately; the runner owns query polling and strictness.</summary>
    Task<IReadOnlyList<NodeRef>> LocateAsync(LocatorExpression expression, OperationContext operation);

    /// <summary>Reads one node from the resolution that minted its ref.</summary>
    Task<EngineNode> ReadAsync(NodeRef reference, OperationContext operation);

    /// <summary>Performs exactly one action on a located or observed node.</summary>
    Task PerformAsync(NodeRef reference, LocatorAction action, OperationContext operation);

    /// <summary>Performs a viewport swipe: <c>perform(root, swipe)</c>, observing first when no observation has named the root yet.</summary>
    Task SwipeAsync(ScrollDirection direction, Momentum? momentum, OperationContext operation);

    /// <summary>Performs one pointer action at a viewport point, in CSS pixels, with no node behind it.</summary>
    Task PerformAtAsync(Point point, PointerAction action, OperationContext operation);

    /// <summary>Input to whatever holds focus; each member fails with <c>UNSUPPORTED_CAPABILITY</c> when the engine lacks it.</summary>
    ISessionKeyboard Keyboard { get; }

    /// <summary>The app's session hooks.</summary>
    ISessionApp App { get; }

    /// <summary>The app's artifact hooks.</summary>
    ISessionArtifacts Artifacts { get; }

    /// <summary>Where the engine's app log goes.</summary>
    AppLogRoute AppLog { get; }

    /// <summary>Where the screens of the attempt go for its trace.</summary>
    ScreenRoute Screens { get; }

    /// <summary>What the engine said the attempt runs on, bounded.</summary>
    EnvironmentFacts Environment { get; }

    /// <summary>Captures immutable app state for a session envelope; null without the state capability.</summary>
    Func<OperationContext, Task<EngineState>>? CaptureState { get; }

    /// <summary>Replaces current app state with a captured state; null without the state capability.</summary>
    Func<EngineState, OperationContext, Task>? RestoreState { get; }

    /// <summary>Rethrows a failure the engine collected on a path no step awaited; a no-op without the hook.</summary>
    Task SettleAsync(OperationContext operation);

    /// <summary>Ends the attempt's isolation within the operation's budget. Idempotent.</summary>
    Task CloseAsync(OperationContext operation);
}

/// <summary>Upstream's <c>SessionKeyboard</c>: typing, pressing, and dismissing for whatever holds focus.</summary>
public interface ISessionKeyboard
{
    Task TypeAsync(string text, bool replace, OperationContext operation);

    Task PressAsync(string key, OperationContext operation);

    Task DismissAsync(OperationContext operation);
}

/// <summary>Upstream's <c>SessionApp</c>: the app's session hooks, each failing with <c>UNSUPPORTED_CAPABILITY</c> when the engine lacks it.</summary>
public interface ISessionApp
{
    /// <summary>Opens the app at one resolved URL.</summary>
    Task OpenAsync(string url, OperationContext operation);

    /// <summary>Navigates back once.</summary>
    Task BackAsync(OperationContext operation);

    /// <summary>Sets the viewport size in CSS pixels.</summary>
    Task ResizeAsync(ViewportSize size, OperationContext operation);

    /// <summary>Recreates the app context without clearing persisted state; shows nothing until <see cref="OpenAsync"/>.</summary>
    Task RestartAsync(OperationContext operation);

    /// <summary>Clears persisted client state and recreates the context; shows nothing until <see cref="OpenAsync"/>.</summary>
    Task ResetAsync(OperationContext operation);
}

/// <summary>Upstream's <c>SessionArtifacts</c>: the screenshot, and video started and stopped together when the engine declares them.</summary>
public interface ISessionArtifacts
{
    /// <summary>Captures a redacted screenshot and returns an artifact-relative path.</summary>
    Task<string> ScreenshotAsync(string? label, OperationContext operation);

    /// <summary>Starts video recording; null without the video hooks.</summary>
    Func<OperationContext, Task>? StartVideo { get; }

    /// <summary>Stops video recording and returns the segments written, in order; null without the video hooks.</summary>
    Func<OperationContext, Task<IReadOnlyList<VideoSegment>>>? StopVideo { get; }
}

/// <summary>What the app did on its own, as the session passes it on: a log line, or where it went.</summary>
public abstract record AppEvent;

/// <summary>An app log entry the engine reported.</summary>
public sealed record AppLogEvent(AppLogEntry Entry) : AppEvent;

/// <summary>A line saying where the app went (upstream's <c>navigated</c>).</summary>
public sealed record AppNavigationEvent(string Line) : AppEvent;

/// <summary>
/// Upstream's <c>AppLogRoute</c> (<c>engine/session.ts</c>): where the engine's app log goes. The engine reports from <c>startAttempt</c> on,
/// before any step records; what arrives before a sink is set waits, up to a bound per kind, and each kind has its own quota so chatter never
/// crowds out an error.
/// </summary>
public sealed class AppLogRoute
{
    /// <summary>Errors and warnings held while no sink is set.</summary>
    public const int MaxErrorsAndWarnings = 200;

    /// <summary>Info entries held while no sink is set.</summary>
    public const int MaxInfo = 100;

    /// <summary>Navigations held while no sink is set.</summary>
    public const int MaxNavigations = 50;

    private readonly List<(AppEvent Event, DateTimeOffset At)> _waiting = [];
    private readonly Dictionary<string, int> _held = new(StringComparer.Ordinal) { ["errorsAndWarnings"] = 0, ["info"] = 0, ["navigation"] = 0 };
    private Action<AppEvent, DateTimeOffset>? _sink;
    private readonly TimeProvider _clock;

    internal AppLogRoute(TimeProvider clock)
    {
        _clock = clock;
    }

    /// <summary>Takes one entry from the engine, checked: a malformed one is dropped.</summary>
    public void Push(AppLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(entry.Text))
        {
            return;
        }

        Pass(new AppLogEvent(entry));
    }

    /// <summary>Takes one line saying where the app went; a blank one is dropped.</summary>
    public void Navigated(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.Trim().Length == 0)
        {
            return;
        }

        Pass(new AppNavigationEvent(line));
    }

    /// <summary>Sends everything from now on, and everything still waiting, to <paramref name="sink"/>; null holds it again.</summary>
    public void Route(Action<AppEvent, DateTimeOffset>? sink)
    {
        _sink = sink;
        if (sink is null)
        {
            return;
        }

        foreach (var (appEvent, at) in _waiting)
        {
            sink(appEvent, at);
        }

        _waiting.Clear();
        _held["errorsAndWarnings"] = 0;
        _held["info"] = 0;
        _held["navigation"] = 0;
    }

    private void Pass(AppEvent appEvent)
    {
        if (_sink is not null)
        {
            _sink(appEvent, _clock.GetUtcNow());
            return;
        }

        var (kind, limit) = appEvent switch
        {
            AppNavigationEvent => ("navigation", MaxNavigations),
            AppLogEvent { Entry.Level: AppLogLevel.Info } => ("info", MaxInfo),
            _ => ("errorsAndWarnings", MaxErrorsAndWarnings),
        };
        if (_held[kind] >= limit)
        {
            return;
        }

        _held[kind]++;
        _waiting.Add((appEvent, _clock.GetUtcNow()));
    }
}

/// <summary>
/// Upstream's <c>ScreenRoute</c> (<c>engine/session.ts</c>): where the screens of an attempt go for its trace. Without a sink they are dropped,
/// and nothing waits for them.
/// </summary>
public sealed class ScreenRoute
{
    private readonly TimeProvider _clock;
    private int _screenRevision;
    private Action<SessionObservation>? _sink;

    internal ScreenRoute(bool traced, TimeProvider clock)
    {
        Traced = traced;
        _clock = clock;
    }

    /// <summary>Whether the attempt keeps a trace; without one nothing is pushed or routed, and the engine is not asked.</summary>
    public bool Traced { get; }

    /// <summary>Takes one engine snapshot, checked: a malformed one is dropped.</summary>
    public void Push(EngineSnapshot snapshot)
    {
        if (_sink is null || snapshot.TreeUnavailable)
        {
            return;
        }

        _screenRevision++;
        var minted = $"s{_screenRevision}";
        _sink(new SessionObservation
        {
            Kind = "semantic",
            Root = new NodeRef(snapshot.Root.Ref.Id, minted),
            Revision = minted,
            CapturedAt = _clock.GetUtcNow().ToString("O"),
            Location = snapshot.Location,
            Viewport = snapshot.Viewport,
            Redaction = new ObservationRedaction(EngineSessions.CountSecure(snapshot.Root), snapshot.MaskedRegionCount ?? 0),
            Tree = EngineSessions.StampRevision(snapshot.Root, minted),
            Truncated = snapshot.Truncated,
        });
    }

    /// <summary>Sends every screen from now on to <paramref name="sink"/>; null drops them again.</summary>
    public void Route(Action<SessionObservation>? sink) => _sink = sink;
}

/// <summary>
/// Upstream's <c>EnvironmentFacts</c> (<c>engine/session.ts</c>): what the engine said the attempt runs on, bounded to eight facts, with each name
/// and value redacted before it is clipped.
/// </summary>
public sealed class EnvironmentFacts
{
    /// <summary>The most facts kept.</summary>
    public const int MaxFacts = 8;

    /// <summary>The most characters of a fact's name, after redaction.</summary>
    public const int MaxKeyChars = 40;

    /// <summary>The most characters of a fact's value, after redaction.</summary>
    public const int MaxValueChars = 200;

    private readonly Dictionary<string, string> _facts = new(StringComparer.Ordinal);

    /// <summary>Merges facts in, checked: a blank name or value is dropped.</summary>
    public void Push(IReadOnlyDictionary<string, string> facts)
    {
        foreach (var (key, value) in facts)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            // Held whole, however long: a cut before redaction could leave a secret's head behind.
            var name = key.Trim();
            if (!_facts.ContainsKey(name) && _facts.Count >= MaxFacts)
            {
                continue;
            }

            _facts[name] = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }
    }

    /// <summary>The facts so far, each name and value redacted with <paramref name="redact"/>, then clipped; null when there are none.</summary>
    public IReadOnlyDictionary<string, string>? Read(Func<string, string> redact)
    {
        if (_facts.Count == 0)
        {
            return null;
        }

        // Two facts can redact to one name; the later one wins, so the read never throws on a duplicate.
        var read = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in _facts)
        {
            read[Clip(redact(key), MaxKeyChars)] = Clip(redact(value), MaxValueChars);
        }

        return new ReadOnlyDictionary<string, string>(read);
    }

    private static string Clip(string text, int limit) => text.Length <= limit ? text : text[..limit];
}
