// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using E2E.Engine;
using E2E.Internal;

namespace E2E;

/// <summary>
/// Semantic queries against the current screen. An action or a single-node
/// assertion fails when the query matches more than one node. Narrow it with
/// <see cref="Locator.Filter(TextMatch)"/>, <see cref="Locator.First"/>, <see cref="Locator.Last"/>, or <see cref="Locator.Nth"/>.
/// </summary>
public class Screen
{
    private readonly Func<CancellationToken, Task<Observation>> _observe;
    private readonly Func<SemanticNode, LocatorAction, CancellationToken, Task> _perform;
    private readonly Func<LocatorExpression, TimeSpan, CancellationToken, Task<IReadOnlyList<SemanticNode>>>? _locate;
    private readonly Func<CancellationToken> _cancellation;
    private readonly Action _verified;
    private readonly Func<ScrollDirection, Momentum?, CancellationToken, Task>? _swipe;
    private readonly Screen? _scopeOf;
    private readonly Func<LocatorExpression, LocatorExpression>? _wrap;
    private ScreenshotContext? _screenshots;
    private Action<Secret>? _rememberSecret;
    private Func<string, string> _redact = static text => text;
    private Func<Observation, string>? _renderTree;
    private StepRecorder? _steps;
    private string? _projectRoot;
    private Action _failed = static () => { };
    private EngineCapabilities _capabilities;

    internal Screen(
        Func<CancellationToken, Task<Observation>> observe,
        Func<SemanticNode, LocatorAction, CancellationToken, Task> perform,
        Func<CancellationToken> cancellation,
        Action verified,
        TimeSpan actionTimeout,
        TimeSpan assertionTimeout,
        SoftFailures? softFailures = null,
        TimeProvider? clock = null,
        Func<LocatorExpression, TimeSpan, CancellationToken, Task<IReadOnlyList<SemanticNode>>>? locate = null,
        Func<ScrollDirection, Momentum?, CancellationToken, Task>? swipe = null)
    {
        _swipe = swipe;
        SoftFailures = softFailures ?? new SoftFailures();
        _observe = observe;
        _perform = perform;
        _locate = locate;
        _cancellation = cancellation;
        _verified = verified;
        ActionTimeout = actionTimeout;
        AssertionTimeout = assertionTimeout;
        Clock = clock ?? TimeProvider.System;
    }

    /// <summary>
    /// A scope of <paramref name="screen"/> whose every query expression goes through <paramref name="wrap"/> first (upstream's
    /// <c>createScopedScreen</c>): the screen of a frame. It shares the attempt's state with <paramref name="screen"/>.
    /// </summary>
    internal Screen(Screen screen, Func<LocatorExpression, LocatorExpression> wrap)
    {
        var root = screen.Root;
        _scopeOf = root;
        _wrap = wrap;
        _observe = root._observe;
        _perform = root._perform;
        _locate = root._locate;
        _cancellation = root._cancellation;
        _verified = root._verified;
        _swipe = root._swipe;
        SoftFailures = root.SoftFailures;
        ActionTimeout = root.ActionTimeout;
        AssertionTimeout = root.AssertionTimeout;
        Clock = root.Clock;
    }

    /// <summary>The screen this one scopes, or this screen when it is not a scope: the one an attempt's state lives on.</summary>
    internal Screen Root => _scopeOf ?? this;

    /// <summary>Whether this screen is a scope of part of the screen, such as a frame (upstream's <c>scoped</c>).</summary>
    internal bool IsScoped => _wrap is not null;

    private LocatorExpression Scoped(LocatorExpression expression) => _wrap is null ? expression : _wrap(expression);

    internal TimeSpan ActionTimeout { get; }

    internal TimeSpan AssertionTimeout { get; }

    internal TimeProvider Clock { get; }

    /// <summary>What the attempt's engine declares; none for a screen outside a session. The locator reads <see cref="EngineCapabilities.TapModifiers"/>, upstream's <c>session.tapModifiers</c>.</summary>
    internal EngineCapabilities Capabilities { get => Root._capabilities; set => Root._capabilities = value; }

    internal SoftFailures SoftFailures { get; }

    internal TimeSpan PollInterval { get; } = TimeSpan.FromMilliseconds(50);

    /// <summary>Where <c>ToHaveScreenshotAsync</c> keeps screenshots; null for a screen outside a session.</summary>
    internal ScreenshotContext? Screenshots { get => Root._screenshots; set => Root._screenshots = value; }

    internal void MarkSecretFilled(Secret secret) => RememberSecret?.Invoke(secret);

    /// <summary>Makes the attempt know a secret filled through this screen, so every message it redacts covers it.</summary>
    internal Action<Secret>? RememberSecret { get => Root._rememberSecret; set => Root._rememberSecret = value; }

    /// <summary>Replaces the secrets the attempt knows in text a failure quotes from the screen; the text as it is for a screen outside a session.</summary>
    internal Func<string, string> Redact { get => Root._redact; set => Root._redact = value; }

    /// <summary>Creates a lazy role query. <c>img</c> is read as <c>image</c>. A role query never matches a hidden node.</summary>
    public Locator GetByRole(string role, TextMatch? name = null, bool exact = true)
    {
        return new Locator(this, Scoped(LocatorExpression.ForRole(role, name, new RoleOptions { Exact = exact })));
    }

    /// <summary>Creates a lazy role query narrowed by name, states, or heading level.</summary>
    public Locator GetByRole(string role, RoleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new Locator(this, Scoped(LocatorExpression.ForRole(role, options.Name, options)));
    }

    /// <summary>Creates a lazy role query narrowed to an accessible name and the given options.</summary>
    public Locator GetByRole(string role, TextMatch name, RoleOptions options)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(options);
        if (options.Name is not null)
        {
            throw new TestException("INVALID_LOCATOR", "getByRole takes the \"name\" as its second argument or in the options, not both.");
        }

        return new Locator(this, Scoped(LocatorExpression.ForRole(role, name, options)));
    }

    /// <summary>
    /// Creates a lazy visible-text query. It answers with the innermost match, such as a
    /// <c>&lt;strong&gt;</c> inside a paragraph. Text inside a control's label answers with the control.
    /// </summary>
    public Locator GetByText(TextMatch text, bool exact = true) => GetByText(text, new TextMatchOptions { Exact = exact });

    /// <summary>
    /// Creates a lazy visible-text query. It answers with the innermost match, such as a
    /// <c>&lt;strong&gt;</c> inside a paragraph. Text inside a control's label answers with the control.
    /// </summary>
    public Locator GetByText(TextMatch text, TextMatchOptions options) => new(this, Scoped(LocatorExpression.For("text", text, options, nameof(text))));

    /// <summary>Creates a lazy accessible-label query: any node whose accessible name matches.</summary>
    public Locator GetByLabel(TextMatch label, bool exact = true) => GetByLabel(label, new TextMatchOptions { Exact = exact });

    /// <summary>Creates a lazy accessible-label query: any node whose accessible name matches.</summary>
    public Locator GetByLabel(TextMatch label, TextMatchOptions options) => new(this, Scoped(LocatorExpression.For("label", label, options, nameof(label))));

    /// <summary>Creates a lazy test-id query. A string matches the whole id, case-sensitive.</summary>
    public Locator GetByTestId(TextMatch testId) => GetByTestId(testId, new TextMatchOptions());

    /// <summary>Creates a lazy test-id query. A string matches the whole id, case-sensitive; <see cref="TextMatchOptions.Exact"/> is ignored.</summary>
    public Locator GetByTestId(TextMatch testId, TextMatchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new Locator(this, Scoped(LocatorExpression.For("testid", testId, new TextMatchOptions { Visible = options.Visible }, nameof(testId))));
    }

    /// <summary>Creates a lazy placeholder query.</summary>
    public Locator GetByPlaceholder(TextMatch placeholder, bool exact = true) => GetByPlaceholder(placeholder, new TextMatchOptions { Exact = exact });

    /// <summary>Creates a lazy placeholder query.</summary>
    public Locator GetByPlaceholder(TextMatch placeholder, TextMatchOptions options) => new(this, Scoped(LocatorExpression.For("placeholder", placeholder, options, nameof(placeholder))));

    /// <summary>Creates a lazy displayed-value query: the current value of an input, text area, or select. A checkbox or radio never shows its value, so it never matches.</summary>
    public Locator GetByDisplayValue(TextMatch value, bool exact = true) => GetByDisplayValue(value, new TextMatchOptions { Exact = exact });

    /// <summary>Creates a lazy displayed-value query: the current value of an input, text area, or select. A checkbox or radio never shows its value, so it never matches.</summary>
    public Locator GetByDisplayValue(TextMatch value, TextMatchOptions options) => new(this, Scoped(LocatorExpression.For("displayValue", value, options, nameof(value))));

    /// <summary>
    /// Reads the whole screen once, without waiting, as the agent reads it: one node per line, <c>#id role "name" text="…" [states]</c>,
    /// children indented, secrets redacted. The text has the format and size limit of the trace's screen at failure. A node the page does
    /// not show is left out. Use it to write a locator. Takes <see cref="ActionOptions.Timeout"/>, the configured action timeout by default.
    /// </summary>
    public Task<string> TreeAsync(ActionOptions? options = null, CancellationToken cancellationToken = default) => ReadTreeAsync("screen.tree", options, cancellationToken);

    /// <summary>
    /// Prints <see cref="TreeAsync"/> to <see cref="Console.Out"/>, which NUnit and xUnit v2 capture into the test's output.
    /// xUnit v3 shows it only when the test assembly has <c>[assembly: CaptureConsole]</c>.
    /// </summary>
    public async Task DebugAsync(ActionOptions? options = null, CancellationToken cancellationToken = default) =>
        Console.WriteLine(await ReadTreeAsync("screen.debug", options, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Scrolls the viewport until <paramref name="target"/> is visible: polls the target and swipes between polls, down and slowly
    /// unless <see cref="ScrollUntilVisibleOptions"/> say otherwise. Every swipe is bounded by the scroll's own deadline, not the
    /// action timeout. Upstream's <c>screen.scrollUntilVisible</c>.
    /// </summary>
    /// <exception cref="TestException"><c>LOCATOR_NOT_FOUND</c> when the target did not become visible in time; <c>INVALID_LOCATOR</c> when it belongs to another screen or attempt.</exception>
    public Task ScrollUntilVisibleAsync(Locator target, ScrollUntilVisibleOptions? options = null, CancellationToken cancellationToken = default) =>
        ScrollUntilVisibleAsync(target, options, null, cancellationToken);

    /// <summary>The scroll of <c>screen</c> or of a locator: on a locator (<paramref name="scope"/>) the node itself swipes, so a scroll container pages.</summary>
    internal async Task ScrollUntilVisibleAsync(Locator target, ScrollUntilVisibleOptions? options, Locator? scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        OwnLocator(target, "scrollUntilVisible");
        var timeout = options?.Timeout ?? ScrollUntilVisibleOptions.DefaultTimeout;
        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero, "timeout");
        var direction = options?.Direction ?? ScrollDirection.Down;
        var momentum = options?.Momentum ?? Momentum.Slow;
        var token = Token(cancellationToken);
        var label = target.Query.Describe();
        await RunStepAsync(StepKind.Screen, "screen.scrollUntilVisible", label, async () =>
        {
            var deadline = new Deadline(timeout, Clock);
            var startedAt = Clock.GetTimestamp();
            TestException NotVisible(Exception? cause)
            {
                var message = "target did not become visible while scrolling: " + label;
                return new TestException("LOCATOR_NOT_FOUND", message, cause!) { Details = LocatorResolver.LocatorDetails(target.Query, Clock.GetElapsedTime(startedAt)) };
            }

            var sampled = false;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                var startedWith = deadline.Remaining(Clock.GetUtcNow());
                SemanticNode? node;
                try
                {
                    node = await target.ResolveSingleAsync(token, deadline).ConfigureAwait(false);
                }
                catch (EngineException ex) when (sampled && Time.CutOffAtDeadline(ex, startedWith))
                {
                    throw NotVisible(ex);
                }

                sampled = true;
                if (node is { States.Hidden: false })
                {
                    return;
                }

                if (deadline.Expired(Clock.GetUtcNow()))
                {
                    throw NotVisible(null);
                }

                // Every swipe is bounded by the scroll's own deadline: the token a swipe runs under ends with it.
                using var bounded = CancellationTokenSource.CreateLinkedTokenSource(token);
                bounded.CancelAfter(deadline.Remaining(Clock.GetUtcNow()));
                try
                {
                    await SwipeStepAsync(scope, direction, momentum, deadline, bounded.Token).ConfigureAwait(false);
                }
                catch (Exception ex) when (bounded.IsCancellationRequested && !token.IsCancellationRequested)
                {
                    // The deadline ended the swipe: the scroll timed out, as an engine reporting a timeout on its budget would say.
                    throw NotVisible(ex is EngineException { Code: EngineErrorCodes.OperationTimeout } ? ex : new EngineException(EngineErrorCodes.OperationTimeout, "swipe exceeded the scroll timeout", retryable: false));
                }
                catch (Exception ex) when (!token.IsCancellationRequested
                    && (deadline.Expired(Clock.GetUtcNow()) || (ex is EngineException { Code: EngineErrorCodes.OperationTimeout } && deadline.Remaining(Clock.GetUtcNow()) < Time.PollInterval)))
                {
                    // The engine's timer can wake a moment before this clock reads the deadline, so a swipe that ran out of its
                    // budget within a poll interval of the deadline is the deadline too, whichever timer fired first.
                    throw NotVisible(ex);
                }
            }
        }).ConfigureAwait(false);
    }

    private Task SwipeStepAsync(Locator? scope, ScrollDirection direction, Momentum momentum, Deadline deadline, CancellationToken token) =>
        scope is null
            ? (_swipe ?? throw new EngineException("UNSUPPORTED_CAPABILITY", "scroll is not supported by this engine.")).Invoke(direction, momentum, token)
            : scope.PerformUntilAsync(new LocatorAction.Swipe(direction, momentum), deadline, token);

    /// <summary>
    /// Refuses a locator not made by this screen: one made elsewhere would be evaluated through this target's engine, so it is
    /// refused before any step. Upstream's <c>ownLocator</c>.
    /// </summary>
    internal void OwnLocator(Locator candidate, string api)
    {
        if (!ReferenceEquals(candidate.Screen.Root, Root))
        {
            throw new TestException(
                "INVALID_LOCATOR",
                api + " requires a locator made by this screen; the one given belongs to another target's screen or another attempt");
        }
    }

    /// <summary>Renders an observation as <see cref="TreeAsync"/> returns it: redacted, one node per line. A session sets its own redactor and app origin; a screen outside one redacts the text with <see cref="Redact"/>.</summary>
    internal Func<Observation, string>? RenderTree { get => Root._renderTree; set => Root._renderTree = value; }

    internal static string TreeText(Observation observation, Redactor redactor, string? appOrigin, int maxObservationBytes) =>
        string.Join('\n', FailureEvidence.Listing(observation, redactor, appOrigin, maxObservationBytes).Lines);

    /// <summary>One observation of the whole screen, rendered, as a screen step.</summary>
    private async Task<string> ReadTreeAsync(string api, ActionOptions? options, CancellationToken cancellationToken)
    {
        if (IsScoped)
        {
            // A frame refuses, since the tree is not scoped.
            throw new TestException("INVALID_ARGUMENT", api + " reads the whole screen; call it on screen");
        }

        var timeout = options?.Timeout ?? ActionTimeout;
        if (timeout != System.Threading.Timeout.InfiniteTimeSpan)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero, "timeout");
        }

        var token = Token(cancellationToken);
        async Task<string> Read()
        {
            using var bounded = CancellationTokenSource.CreateLinkedTokenSource(token);
            bounded.CancelAfter(timeout);  // an infinite timeout never fires
            try
            {
                // The wait ends with the timeout even when the engine ignores cancellation; the abandoned read's fault is observed.
                var read = ObserveAsync(bounded.Token);
                _ = read.ContinueWith(static finished => _ = finished.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                var observation = await read.WaitAsync(bounded.Token).ConfigureAwait(false);
                return RenderTree?.Invoke(observation) ?? Redact(TreeText(observation, Redactor.None, null, E2EDefaults.MaxObservationBytes));
            }
            catch (OperationCanceledException) when (bounded.IsCancellationRequested && !token.IsCancellationRequested)
            {
                throw new EngineException(EngineErrorCodes.OperationTimeout, api + " timed out", retryable: false);
            }
        }

        return Steps is null ? await Read().ConfigureAwait(false) : await Steps.RunAsync(StepKind.Screen, api, "whole screen", Read).ConfigureAwait(false);
    }

    internal async Task<Observation> ObserveAsync(CancellationToken cancellationToken)
    {
        return await AppNotOpen.GuardAsync(() => _observe(cancellationToken == default ? _cancellation() : cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether the engine resolves a locator itself. Upstream's engines all do; a port engine that does not has its locator resolved
    /// here over <see cref="ObserveAsync"/>, until it implements <see cref="IEngineSession.LocateAsync"/> (.NET-only).
    /// </summary>
    internal bool LocatesInEngine => _locate is not null;

    /// <summary>One immediate engine resolve: the nodes the query matches now, within <paramref name="timeout"/> of the engine's own work.</summary>
    internal async Task<IReadOnlyList<SemanticNode>> LocateAsync(LocatorExpression query, TimeSpan timeout, CancellationToken cancellationToken)
    {
        return await AppNotOpen.GuardAsync(() => _locate!(query, timeout, cancellationToken == default ? _cancellation() : cancellationToken)).ConfigureAwait(false);
    }

    internal async Task PerformAsync(SemanticNode node, LocatorAction action, CancellationToken cancellationToken)
    {
        await AppNotOpen.GuardAsync(() => _perform(node, action, cancellationToken == default ? _cancellation() : cancellationToken)).ConfigureAwait(false);
    }

    internal CancellationToken Token(CancellationToken cancellationToken)
    {
        return Time.Link(cancellationToken, _cancellation());
    }

    internal void NotifyVerified() => _verified();

    /// <summary>The project root that <see cref="Locator.SetInputFilesAsync(string, ActionOptions, CancellationToken)"/> resolves paths from (upstream's <c>context.projectRoot</c>); null outside a session, where a path stays as given.</summary>
    internal string? ProjectRoot { get => Root._projectRoot; set => Root._projectRoot = value; }

    /// <summary>The attempt's step timeline; null for a screen outside a session, whose calls then run unrecorded.</summary>
    internal StepRecorder? Steps { get => Root._steps; set => Root._steps = value; }

    /// <summary>Runs one call that touches the app as a recorded step of the attempt, or as it is without a timeline.</summary>
    internal Task RunStepAsync(StepKind kind, string api, string label, Func<Task> body, bool verifies = false) =>
        Steps is null ? body() : Steps.RunAsync(kind, api, label, body, new StepRunOptions { Verifies = verifies });

    /// <summary>Runs one call that touches the app and returns a value as a recorded step of the attempt, or as it is without a timeline.</summary>
    internal Task<T> RunStepAsync<T>(StepKind kind, string api, string label, Func<Task<T>> body, bool verifies = false) =>
        Steps is null ? body() : Steps.RunAsync(kind, api, label, body, new StepRunOptions { Verifies = verifies });

    /// <summary>Records one child event under the running step; nothing outside a session listens.</summary>
    internal void RecordEvent(StepEvent stepEvent) => Steps?.RecordEvent(stepEvent);

    /// <summary>Tells the attempt that an assertion failed on the app. Nothing outside a session listens.</summary>
    internal Action Failed { get => Root._failed; set => Root._failed = value; }

    internal void NotifyFailed() => Failed();
}

/// <summary>A query that resolves against a fresh observation every time it is used.</summary>
public sealed class Locator
{
    private readonly Screen _screen;

    internal Locator(Screen screen, LocatorExpression query)
    {
        _screen = screen;
        Query = query;
    }

    internal LocatorExpression Query { get; }

    /// <summary>Keeps the matches whose own or descendants' name, text, or value contains <paramref name="hasText"/>, ignoring case.</summary>
    public Locator Filter(TextMatch hasText)
    {
        ArgumentNullException.ThrowIfNull(hasText);
        hasText.ThrowIfBlank(nameof(hasText));
        return new Locator(_screen, new LocatorExpression { Kind = "filter", Source = Query, HasText = hasText });
    }

    /// <summary>Keeps the matches with a descendant that <paramref name="has"/> matches. <paramref name="has"/> resolves inside each match.</summary>
    public Locator Filter(Locator has)
    {
        ArgumentNullException.ThrowIfNull(has);
        _screen.OwnLocator(has, "filter({ has })");
        return new Locator(_screen, new LocatorExpression { Kind = "filter", Source = Query, Has = has.Query });
    }

    /// <summary>
    /// Adds deterministic filters, upstream's <c>locator.filter({ hasText, hasNotText, has, hasNot })</c>. Every filter given must hold
    /// for a match. <c>hasText</c> and <c>hasNotText</c> are case-insensitive substrings of the match's own or descendants' name, text, or
    /// value, or a <see cref="System.Text.RegularExpressions.Regex"/> as written. <c>has</c> keeps a match whose subtree resolves the inner
    /// locator, and <c>hasNot</c> drops it. Both resolve inside each match. .NET-only: the named-argument form of upstream's options
    /// object, beside <see cref="Filter(TextMatch)"/> and <see cref="Filter(Locator)"/>.
    /// </summary>
    /// <exception cref="TestException"><c>INVALID_LOCATOR</c>: no filter is given, or the <c>has</c> or <c>hasNot</c> locator belongs to another screen or attempt.</exception>
    public Locator Filter(TextMatch? hasText = null, TextMatch? hasNotText = null, Locator? has = null, Locator? hasNot = null)
    {
        if (hasText is null && hasNotText is null && has is null && hasNot is null)
        {
            throw new TestException("INVALID_LOCATOR", "filter() requires hasText, hasNotText, has, and/or hasNot");
        }

        hasText?.ThrowIfBlank(nameof(hasText));
        if (has is not null)
        {
            _screen.OwnLocator(has, "filter({ has })");
        }

        if (hasNot is not null)
        {
            _screen.OwnLocator(hasNot, "filter({ hasNot })");
        }

        return new Locator(_screen, new LocatorExpression
        {
            Kind = "filter",
            Source = Query,
            HasText = hasText,
            HasNotText = hasNotText,
            Has = has?.Query,
            HasNot = hasNot?.Query,
        });
    }

    /// <summary>
    /// Matches either locator, preserving document order without duplicate nodes. <c>a.Or(b)</c> stays strict: when both match
    /// different nodes, an action fails with <c>STRICT_MODE</c>, so add <see cref="First"/> if either will do. Both locators must come
    /// from the same screen and attempt. Upstream's <c>locator.or</c>.
    /// </summary>
    /// <exception cref="TestException"><c>INVALID_LOCATOR</c>: <paramref name="other"/> belongs to another screen or attempt.</exception>
    public Locator Or(Locator other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (!ReferenceEquals(other._screen.Root, _screen.Root))
        {
            throw new TestException(
                "INVALID_LOCATOR",
                "or requires a locator made by this screen; the one given belongs to another target's screen or another attempt");
        }

        return new Locator(_screen, new LocatorExpression { Kind = "union", Left = Query, Right = other.Query });
    }

    public Locator First() => Nth(0);

    public Locator Last() => new(_screen, new LocatorExpression { Kind = "index", Source = Query, Index = LocatorExpression.LastIndex });

    public Locator Nth(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return new Locator(_screen, new LocatorExpression { Kind = "index", Source = Query, Index = index });
    }

    /// <summary>Creates a role query among this locator's descendants.</summary>
    public Locator GetByRole(string role, TextMatch? name = null, bool exact = true) => Scope(_screen.Root.GetByRole(role, name, exact));

    /// <summary>Creates a role query among this locator's descendants.</summary>
    public Locator GetByRole(string role, RoleOptions options) => Scope(_screen.Root.GetByRole(role, options));

    /// <summary>Creates a role query among this locator's descendants.</summary>
    public Locator GetByRole(string role, TextMatch name, RoleOptions options) => Scope(_screen.Root.GetByRole(role, name, options));

    /// <summary>Creates a visible-text query among this locator's descendants.</summary>
    public Locator GetByText(TextMatch text, bool exact = true) => Scope(_screen.Root.GetByText(text, exact));

    /// <summary>Creates a visible-text query among this locator's descendants.</summary>
    public Locator GetByText(TextMatch text, TextMatchOptions options) => Scope(_screen.Root.GetByText(text, options));

    /// <summary>Creates a label query among this locator's descendants.</summary>
    public Locator GetByLabel(TextMatch label, bool exact = true) => Scope(_screen.Root.GetByLabel(label, exact));

    /// <summary>Creates a label query among this locator's descendants.</summary>
    public Locator GetByLabel(TextMatch label, TextMatchOptions options) => Scope(_screen.Root.GetByLabel(label, options));

    /// <summary>Creates a test-id query among this locator's descendants.</summary>
    public Locator GetByTestId(TextMatch testId) => Scope(_screen.Root.GetByTestId(testId));

    /// <summary>Creates a test-id query among this locator's descendants.</summary>
    public Locator GetByTestId(TextMatch testId, TextMatchOptions options) => Scope(_screen.Root.GetByTestId(testId, options));

    /// <summary>Creates a placeholder query among this locator's descendants.</summary>
    public Locator GetByPlaceholder(TextMatch placeholder, bool exact = true) => Scope(_screen.Root.GetByPlaceholder(placeholder, exact));

    /// <summary>Creates a placeholder query among this locator's descendants.</summary>
    public Locator GetByPlaceholder(TextMatch placeholder, TextMatchOptions options) => Scope(_screen.Root.GetByPlaceholder(placeholder, options));

    /// <summary>Creates a displayed-value query among this locator's descendants.</summary>
    public Locator GetByDisplayValue(TextMatch value, bool exact = true) => Scope(_screen.Root.GetByDisplayValue(value, exact));

    /// <summary>Creates a displayed-value query among this locator's descendants.</summary>
    public Locator GetByDisplayValue(TextMatch value, TextMatchOptions options) => Scope(_screen.Root.GetByDisplayValue(value, options));

    /// <summary>Taps the node, waiting for exactly one enabled match.</summary>
    public Task TapAsync(ActionOptions? options = null, CancellationToken cancellationToken = default) => TapAsync(AsClick(options), cancellationToken);

    /// <summary>
    /// Taps the node. <see cref="ClickOptions.Modifiers"/> are held for the tap, on an engine that declares
    /// <see cref="EngineCapabilities.TapModifiers"/>; any other engine refuses them with <c>UNSUPPORTED_CAPABILITY</c> before the node is resolved.
    /// </summary>
    public Task TapAsync(ClickOptions? options, CancellationToken cancellationToken = default) =>
        ClickWithAsync("tap", static modifiers => new LocatorAction.Tap(modifiers), options, cancellationToken);

    /// <summary>Alias of <see cref="TapAsync(ActionOptions, CancellationToken)"/>.</summary>
    public Task ClickAsync(ActionOptions? options = null, CancellationToken cancellationToken = default) => TapAsync(AsClick(options), cancellationToken);

    /// <summary>Alias of <see cref="TapAsync(ClickOptions, CancellationToken)"/>.</summary>
    public Task ClickAsync(ClickOptions? options, CancellationToken cancellationToken = default) => TapAsync(options, cancellationToken);

    /// <summary>Double taps the node, waiting for exactly one enabled match.</summary>
    public Task DoubleTapAsync(ActionOptions? options = null, CancellationToken cancellationToken = default) => DoubleTapAsync(AsClick(options), cancellationToken);

    /// <summary>Double taps the node. <see cref="ClickOptions.Modifiers"/> are held for both taps, as for <see cref="TapAsync(ClickOptions, CancellationToken)"/>.</summary>
    public Task DoubleTapAsync(ClickOptions? options, CancellationToken cancellationToken = default) =>
        ClickWithAsync("doubleTap", static modifiers => new LocatorAction.DoubleTap(modifiers), options, cancellationToken);

    /// <summary>Alias of <see cref="DoubleTapAsync(ActionOptions, CancellationToken)"/>.</summary>
    public Task DblClickAsync(ActionOptions? options = null, CancellationToken cancellationToken = default) => DoubleTapAsync(AsClick(options), cancellationToken);

    /// <summary>Alias of <see cref="DoubleTapAsync(ClickOptions, CancellationToken)"/>.</summary>
    public Task DblClickAsync(ClickOptions? options, CancellationToken cancellationToken = default) => DoubleTapAsync(options, cancellationToken);

    private static ClickOptions? AsClick(ActionOptions? options) =>
        options as ClickOptions ?? (options is null ? null : new ClickOptions { Timeout = options.Timeout });

    /// <summary>
    /// A right click on the node, or the platform's equivalent. <see cref="ClickOptions.Modifiers"/> are held for it, as for <see cref="TapAsync(ClickOptions, CancellationToken)"/>.
    /// An engine with no secondary tap (a device, the document engine) fails with <c>UNSUPPORTED_CAPABILITY</c>.
    /// </summary>
    public Task SecondaryTapAsync(ClickOptions? options = null, CancellationToken cancellationToken = default) =>
        ClickWithAsync("secondaryTap", static modifiers => new LocatorAction.SecondaryTap(modifiers), options, cancellationToken);

    /// <summary>
    /// Holds a press on the node for <see cref="LongPressOptions.Duration"/>, from 100 ms through 10 s; without one the engine holds its default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The duration is not a whole number of milliseconds from 100 through 10000 (upstream's <c>INVALID_ARGUMENT</c>).</exception>
    public Task LongPressAsync(LongPressOptions? options = null, CancellationToken cancellationToken = default)
    {
        var duration = ValidateLongPress(options?.Duration);
        return ActAsync(new LocatorAction.LongPress(duration), options, cancellationToken);
    }

    /// <summary>Moves the pointer over the node.</summary>
    public Task HoverAsync(ActionOptions? options = null, CancellationToken cancellationToken = default) =>
        ActAsync(new LocatorAction.Hover(), options, cancellationToken);

    /// <summary>
    /// Drags the node onto <paramref name="target"/>. The drop target resolves inside the retry, so a target that changes under the
    /// drag is resolved again along with the source.
    /// </summary>
    /// <exception cref="TestException"><paramref name="target"/> is a locator of another screen (<c>INVALID_LOCATOR</c>).</exception>
    public Task DragToAsync(Locator target, ActionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        _screen.OwnLocator(target, "dragTo");
        return ActAsync("locator.dragTo", async (deadline, token) => new LocatorAction.DragTo(await target.ResolveExactlyOneAsync(deadline, token).ConfigureAwait(false)), options, null, null, cancellationToken);
    }

    /// <summary>
    /// Scrolls the node about three quarters of its extent along <see cref="SwipeOptions.Direction"/>, further or less far with
    /// <see cref="SwipeOptions.Momentum"/>. The viewport swipe is the screen's.
    /// </summary>
    public Task SwipeAsync(SwipeOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return ActAsync(new LocatorAction.Swipe(options.Direction, options.Momentum), options, cancellationToken);
    }

    public Task FillAsync(string value, ActionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        return ActAsync(new LocatorAction.Fill(value), options, cancellationToken);
    }

    /// <summary>Fills a secret. The engine marks the value sensitive and never reports it.</summary>
    public Task FillAsync(Secret value, ActionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        _screen.MarkSecretFilled(value);
        return ActAsync(new LocatorAction.Fill(value.Value, Sensitive: true), options, cancellationToken);
    }

    /// <summary>
    /// Focuses the input and types <paramref name="text"/> one character at a time,
    /// so the app receives key events. <see cref="PressSequentiallyOptions.Delay"/> waits between characters.
    /// </summary>
    public Task PressSequentiallyAsync(string text, PressSequentiallyOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (options?.Delay is TimeSpan wait)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(wait, TimeSpan.Zero, nameof(options));
        }

        return ActAsync(new LocatorAction.PressSequentially(text, options?.Delay), options, cancellationToken);
    }

    public Task PressAsync(string key, ActionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return ActAsync(new LocatorAction.Press(key), options, cancellationToken);
    }

    public Task CheckAsync(ActionOptions? options = null, CancellationToken cancellationToken = default)
    {
        return ActAsync(new LocatorAction.Check(), options, cancellationToken);
    }

    public Task UncheckAsync(ActionOptions? options = null, CancellationToken cancellationToken = default)
    {
        return ActAsync(new LocatorAction.Uncheck(), options, cancellationToken);
    }

    public Task ClearAsync(ActionOptions? options = null, CancellationToken cancellationToken = default)
    {
        return ActAsync(new LocatorAction.Clear(), options, cancellationToken);
    }

    /// <summary>Selects the option whose value or label is <paramref name="value"/>.</summary>
    public Task SelectOptionAsync(string value, ActionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        return ActAsync(new LocatorAction.Select(value), options, cancellationToken);
    }

    /// <summary>
    /// Sets the files of exactly one file input, or of a control that opens a file chooser; <paramref name="path"/> resolves from the project root.
    /// On the web it also takes a button that opens a file chooser. A button that opens none fails with <c>NOT_ACTIONABLE</c> (upstream reports <c>ACTION_FAILED</c>).
    /// </summary>
    public Task SetInputFilesAsync(string path, ActionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        return SetInputFilesAsync([path], options, cancellationToken);
    }

    /// <inheritdoc cref="SetInputFilesAsync(string, ActionOptions, CancellationToken)"/>
    /// <exception cref="ArgumentException"><paramref name="paths"/> is empty or has a blank entry (upstream's <c>INVALID_ARGUMENT</c>).</exception>
    public Task SetInputFilesAsync(IEnumerable<string> paths, ActionOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var list = paths.ToList();
        if (list.Count == 0 || list.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("setInputFiles requires one or more non-empty paths", nameof(paths));
        }

        var root = _screen.ProjectRoot;
        var resolved = list.Select(entry => root is null ? entry : Path.GetFullPath(entry, root)).ToList();
        return ActAsync(new LocatorAction.SetInputFiles(resolved), options, cancellationToken);
    }

    public Task FocusAsync(ActionOptions? options = null, CancellationToken cancellationToken = default)
    {
        return ActAsync(new LocatorAction.Focus(), options, cancellationToken);
    }

    public Task ScrollIntoViewAsync(ActionOptions? options = null, CancellationToken cancellationToken = default)
    {
        return ActAsync(new LocatorAction.ScrollIntoView(), options, cancellationToken);
    }

    /// <summary>
    /// Waits up to the action timeout for one match, then reads whitespace-normalized text once: the node's rendered text (<c>innerText</c> on the web), not its label. Null when the node has no text.
    /// Does not retry for a value and does not verify an earlier <c>act</c>. Fails with <c>POLICY_DENIED</c> on a secure field.
    /// </summary>
    public async Task<string?> TextContentAsync(CancellationToken cancellationToken = default)
    {
        var node = await ResolveReadableAsync(cancellationToken).ConfigureAwait(false);
        return node.Text is null ? null : TextRules.Normalize(node.Text);
    }

    /// <summary>
    /// Waits up to the action timeout for one match, then reads an input value once, the empty string when the node has none. Does not retry for a value and does not verify an earlier <c>act</c>. A checkbox or
    /// radio reads its value attribute, <c>on</c> when it has none, whatever its checked state. Fails with
    /// <c>POLICY_DENIED</c> on a secure field.
    /// </summary>
    public async Task<string> InputValueAsync(CancellationToken cancellationToken = default)
    {
        var node = await ResolveReadableAsync(cancellationToken).ConfigureAwait(false);
        return node.Value ?? "";
    }

    /// <summary>Waits up to the action timeout for one match, then reads one platform attribute once, or null when the node does not have it. Fails with <c>POLICY_DENIED</c> on a secure field.</summary>
    public async Task<string?> GetAttributeAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var node = await ResolveReadableAsync(cancellationToken).ConfigureAwait(false);
        return node.AttributeOf(name);
    }

    /// <summary>Reads current visibility: true when exactly one visible node matches. More than one match fails.</summary>
    public async Task<bool> IsVisibleAsync(CancellationToken cancellationToken = default)
    {
        return await ResolveSingleAsync(_screen.Token(cancellationToken), missingFrame: MissingFrame.Empty).ConfigureAwait(false) is { States.Hidden: false };
    }

    /// <summary>Reads current hidden or absent state, the negation of <see cref="IsVisibleAsync"/>.</summary>
    public async Task<bool> IsHiddenAsync(CancellationToken cancellationToken = default)
    {
        return !await IsVisibleAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Waits up to the action timeout for one match, then reads its enabled state once.</summary>
    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        var node = await ResolveStrictAsync(cancellationToken).ConfigureAwait(false);
        return !node.States.Disabled;
    }

    /// <summary>Waits up to the action timeout for one match, then reads its disabled state once, the negation of <see cref="IsEnabledAsync"/>.</summary>
    public async Task<bool> IsDisabledAsync(CancellationToken cancellationToken = default)
    {
        return !await IsEnabledAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Waits up to the action timeout for one match, then reads its checked state once.</summary>
    public async Task<bool> IsCheckedAsync(CancellationToken cancellationToken = default)
    {
        var node = await ResolveStrictAsync(cancellationToken).ConfigureAwait(false);
        return node.States.Checked;
    }

    /// <summary>
    /// Waits up to the action timeout for one match, then reads its viewport-relative box once, or null when the node lays out no box
    /// (<c>display: none</c> on it or an ancestor, or any <c>display: contents</c> element) or the engine does not report one.
    /// </summary>
    public async Task<BoundingBox?> BoundingBoxAsync(CancellationToken cancellationToken = default)
    {
        var node = await ResolveStrictAsync(cancellationToken).ConfigureAwait(false);
        return node.Rect;
    }

    /// <summary>Counts current matches without waiting.</summary>
    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var matches = await ResolveAsync(_screen.Token(cancellationToken), missingFrame: MissingFrame.Empty).ConfigureAwait(false);
        return matches.Count;
    }

    /// <summary>One <see cref="Nth"/> locator per current match, without waiting. Empty when nothing matches.</summary>
    public async Task<IReadOnlyList<Locator>> AllAsync(CancellationToken cancellationToken = default)
    {
        var count = await CountAsync(cancellationToken).ConfigureAwait(false);
        var locators = new List<Locator>(count);
        for (var i = 0; i < count; i++)
        {
            locators.Add(Nth(i));
        }

        return locators;
    }

    /// <summary>
    /// Reads the whitespace-normalized rendered text of every current match, without waiting. Empty when nothing matches.
    /// Fails with <c>POLICY_DENIED</c> when any match is a secure field.
    /// </summary>
    public async Task<IReadOnlyList<string>> AllTextContentsAsync(CancellationToken cancellationToken = default)
    {
        var matches = await ResolveAsync(_screen.Token(cancellationToken), missingFrame: MissingFrame.Empty).ConfigureAwait(false);
        DenySecureRead(matches);
        return matches.Select(node => TextRules.Normalize(node.Text ?? "")).ToList();
    }

    /// <summary>
    /// Waits for <see cref="LocatorWaitForOptions.State"/>, <see cref="WaitForState.Visible"/> by default:
    /// <see cref="WaitForState.Attached"/> for one match, visible or not;
    /// <see cref="WaitForState.Detached"/> for none; <see cref="WaitForState.Hidden"/> for none
    /// or a hidden one. The timeout defaults to the action timeout. More than one match fails at once.
    /// Does not verify an earlier <c>act</c>.
    /// </summary>
    public Task WaitForAsync(LocatorWaitForOptions? options = null, CancellationToken cancellationToken = default) =>
        _screen.RunStepAsync(
            StepKind.Locator,
            "locator.waitFor",
            Query.Describe() + " → " + StateName(options?.State ?? WaitForState.Visible),
            () => WaitForCoreAsync(options, cancellationToken),
            verifies: true);

    private async Task WaitForCoreAsync(LocatorWaitForOptions? options, CancellationToken cancellationToken)
    {
        var state = options?.State ?? WaitForState.Visible;
        var token = _screen.Token(cancellationToken);
        var started = DateTime.UtcNow;
        var deadline = started + TimeoutOf(options?.Timeout);
        var samples = new SampleHistory("waitFor", _screen.Redact);
        await samples.RecordAsync(_screen.Steps, token, () => WaitForPollAsync(state, started, deadline, samples, token)).ConfigureAwait(false);
    }

    private async Task WaitForPollAsync(WaitForState state, DateTime started, DateTime deadline, SampleHistory samples, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            // The states absence satisfies read a frame missing from the document as zero matches.
            var matches = await ResolveAsync(token, includeHidden: true, new Deadline(deadline), state is WaitForState.Detached or WaitForState.Hidden ? MissingFrame.Empty : MissingFrame.Wait).ConfigureAwait(false);
            if (matches.Count > 1)
            {
                throw Ambiguous(matches.Count);
            }

            samples.Add(matches.Count == 0 ? "absent" : matches[0].States.Hidden ? "hidden" : "visible");

            var done = state switch
            {
                WaitForState.Attached => matches.Count == 1,
                WaitForState.Visible => matches.Count == 1 && !matches[0].States.Hidden,
                WaitForState.Hidden => matches.Count == 0 || matches[0].States.Hidden,
                _ => matches.Count == 0,
            };
            if (done)
            {
                return;
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw ErrorRedaction.Failure(
                    "TIMEOUT",
                    Query.Describe() + ".waitFor(" + StateName(state) + ") timed out: matched " + matches.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " nodes.",
                    null,
                    _screen.Redact,
                    LocatorResolver.LocatorDetails(Query, DateTime.UtcNow - started));
            }

            await Task.Delay(_screen.PollInterval, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The nodes the locator matches now. An engine that resolves locators answers it, and a role query of its skips a hidden node unless
    /// <paramref name="includeHidden"/> asks for it; any other engine is resolved over its observation. <paramref name="deadline"/> bounds the retries of an engine
    /// that asks for one, and defaults to the action timeout. <paramref name="missingFrame"/> says what a frame the engine reports missing means.
    /// </summary>
    internal async Task<IReadOnlyList<SemanticNode>> ResolveAsync(CancellationToken cancellationToken, bool includeHidden = false, Deadline? deadline = null, MissingFrame missingFrame = MissingFrame.Wait)
    {
        if (_screen.LocatesInEngine)
        {
            return await LocateAsync(deadline ?? new Deadline(_screen.ActionTimeout, _screen.Clock), includeHidden, missingFrame, cancellationToken).ConfigureAwait(false);
        }

        var observation = await _screen.ObserveAsync(cancellationToken).ConfigureAwait(false);
        return LocatorResolver.Resolve(observation, Query, includeHidden);
    }

    /// <summary>One immediate engine resolve, retrying a retryable engine error until the deadline (upstream's <c>resolveOnce</c>).</summary>
    private async Task<IReadOnlyList<SemanticNode>> LocateAsync(Deadline deadline, bool includeHidden, MissingFrame missingFrame, CancellationToken cancellationToken)
    {
        var expression = includeHidden ? Query with { IncludeHidden = true } : Query;
        while (true)
        {
            try
            {
                var budget = TimeSpan.FromMilliseconds(Math.Max(1, deadline.Remaining(_screen.Clock.GetUtcNow()).TotalMilliseconds));
                var nodes = await _screen.LocateAsync(expression, budget, cancellationToken).ConfigureAwait(false);

                // An engine answers `visible` inside scopes, filters, and indices, where only it can. A direct query is held to the same
                // predicate here, so it never resolves to a node its own state calls hidden.
                return Query.Visible && Query.Kind is not ("filter" or "index" or "selector") ? nodes.Where(node => !node.States.Hidden).ToList() : nodes;
            }
            catch (EngineException ex) when (missingFrame == MissingFrame.Empty && ex is { Code: EngineErrorCodes.FrameNotFound, Retryable: true })
            {
                // Nothing under a frame that is not in the document matches, and a read that promised not to wait must not wait for it.
                return [];
            }
            catch (EngineException ex) when (ex.Retryable && !deadline.Expired(_screen.Clock.GetUtcNow()) && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_screen.PollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (EngineException ex) when (ex.Code is EngineErrorCodes.FrameNotFound or EngineErrorCodes.FrameAmbiguous && !cancellationToken.IsCancellationRequested)
            {
                // Upstream's translateLocatorError: a frame that is missing or matches twice is a locator that matched nothing, or too much.
                throw new TestException(ex.Code == EngineErrorCodes.FrameNotFound ? "NOT_FOUND" : "STRICT_MODE", ex.Message + ": " + Query.Describe(), ex) { Details = LocatorResolver.LocatorDetails(Query) };
            }
        }
    }

    internal Screen Screen => _screen;

    private Locator Scope(Locator query) => new(_screen, query.Query with { Parent = Query });

    /// <summary>
    /// Waits up to the action timeout for exactly one enabled match, then acts on it.
    /// More than one match fails at once.
    /// </summary>
    private Task ActAsync(LocatorAction action, ActionOptions? options, CancellationToken cancellationToken) =>
        ActAsync("locator." + ApiName(action), (_, _) => Task.FromResult(action), options, null, null, cancellationToken);

    /// <summary>
    /// <see cref="ActAsync(LocatorAction, ActionOptions, CancellationToken)"/> for an action built once the node is found, with the step's
    /// label and a check that runs inside the step before the node is resolved.
    /// </summary>
    private Task ActAsync(string api, Func<Deadline, CancellationToken, Task<LocatorAction>> build, ActionOptions? options, string? label, Action? check, CancellationToken cancellationToken) =>
        _screen.RunStepAsync(StepKind.Locator, api, label ?? Query.Describe(), () =>
        {
            check?.Invoke();
            return PerformAsync(build, options, cancellationToken);
        });

    /// <summary>
    /// A tap, double tap, or secondary tap with the keys <see cref="ClickOptions.Modifiers"/> holds. The modifiers are validated at once; an
    /// engine that does not declare <see cref="EngineCapabilities.TapModifiers"/> refuses them inside the step, before the node is resolved.
    /// </summary>
    private Task ClickWithAsync(string api, Func<IReadOnlyList<KeyModifier>?, LocatorAction> make, ClickOptions? options, CancellationToken cancellationToken)
    {
        // Validation only: every capability is passed, so the engine's own check waits for the step (RequireClickModifiers below).
        var modifiers = ActionDispatcher.RequireClickModifiers(options, api, EngineCapabilities.TapModifiers);
        var label = modifiers is null ? null : Query.Describe() + " with " + string.Join('+', modifiers);
        var action = make(modifiers);
        return ActAsync(
            "locator." + api,
            (_, _) => Task.FromResult(action),
            options,
            label,
            modifiers is null ? null : () => ActionDispatcher.RequireClickModifiers(options, api, _screen.Capabilities),
            cancellationToken);
    }

    /// <summary>The hold of a long press: whole milliseconds from 100 through 10000, or null for the engine's default.</summary>
    private static TimeSpan? ValidateLongPress(TimeSpan? duration)
    {
        if (duration is not { } hold)
        {
            return null;
        }

        var milliseconds = hold.TotalMilliseconds;
        if (milliseconds != Math.Floor(milliseconds) || milliseconds < 100 || milliseconds > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), hold, "longPress duration must be an integer from 100 through 10000 milliseconds, got " + milliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return hold;
    }

    // The method the test called, as upstream names it: tap, doubleTap, fill, selectOption.
    private static string ApiName(LocatorAction action) => action switch
    {
        LocatorAction.Tap => "tap",
        LocatorAction.DoubleTap => "doubleTap",
        LocatorAction.Fill => "fill",
        LocatorAction.Press => "press",
        LocatorAction.PressSequentially => "pressSequentially",
        LocatorAction.Select => "selectOption",
        LocatorAction.SetInputFiles => "setInputFiles",
        LocatorAction.Check => "check",
        LocatorAction.Uncheck => "uncheck",
        LocatorAction.Clear => "clear",
        LocatorAction.Focus => "focus",
        LocatorAction.ScrollIntoView => "scrollIntoView",
        LocatorAction.Swipe => "swipe",
        LocatorAction.SecondaryTap => "secondaryTap",
        LocatorAction.LongPress => "longPress",
        LocatorAction.Hover => "hover",
        LocatorAction.DragTo => "dragTo",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "unknown locator action"),
    };

    private async Task PerformAsync(Func<Deadline, CancellationToken, Task<LocatorAction>> build, ActionOptions? options, CancellationToken cancellationToken)
    {
        var token = _screen.Token(cancellationToken);
        var started = DateTime.UtcNow;
        var deadline = started + TimeoutOf(options?.Timeout);
        var startedAt = DateTimeOffset.UtcNow;
        var stamp = Stopwatch.GetTimestamp();
        var rounds = 0;
        while (true)
        {
            var node = await ResolveSingleAsync(token, new Deadline(deadline)).ConfigureAwait(false);
            rounds++;
            if (node is { States.Disabled: false, States.Hidden: false })
            {
                var waitedMs = rounds > 1 ? (long)Stopwatch.GetElapsedTime(stamp).TotalMilliseconds : 0;
                var action = await build(new Deadline(deadline), token).ConfigureAwait(false);
                try
                {
                    await _screen.PerformAsync(node, action, token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    RecordAction(node, action, startedAt, stamp, waitedMs, token.IsCancellationRequested ? StepEventStatus.Cancelled : StepEventStatus.Failed, ErrorSerializer.Classify(ex).Code);
                    throw;
                }

                RecordAction(node, action, startedAt, stamp, waitedMs, StepEventStatus.Passed, null);
                return;
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw node is null
                    ? NotFound(DateTime.UtcNow - started)
                    : new TestException("NOT_ACTIONABLE", Query.Describe() + (node.States.Hidden ? " is hidden." : " is disabled."));
            }

            await Task.Delay(_screen.PollInterval, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Scrolls this locator's node until <paramref name="target"/> is visible: the node itself swipes, so a scroll container pages
    /// without the pointer having to hover it first. Both locators must come from the same screen. Upstream's <c>locator.scrollUntilVisible</c>.
    /// </summary>
    public Task ScrollUntilVisibleAsync(Locator target, ScrollUntilVisibleOptions? options = null, CancellationToken cancellationToken = default) =>
        _screen.ScrollUntilVisibleAsync(target, options, this, cancellationToken);

    /// <summary>Performs one action on the one enabled match, waiting for it until <paramref name="deadline"/> (upstream's <c>performUntil</c>).</summary>
    internal async Task PerformUntilAsync(LocatorAction action, Deadline deadline, CancellationToken token)
    {
        var started = _screen.Clock.GetTimestamp();
        while (true)
        {
            var node = await ResolveSingleAsync(token, deadline).ConfigureAwait(false);
            if (node is { States.Disabled: false, States.Hidden: false })
            {
                await _screen.PerformAsync(node, action, token).ConfigureAwait(false);
                return;
            }

            if (deadline.Expired(_screen.Clock.GetUtcNow()))
            {
                throw node is null
                    ? NotFound(_screen.Clock.GetElapsedTime(started))
                    : new TestException("NOT_ACTIONABLE", Query.Describe() + (node.States.Hidden ? " is hidden." : " is disabled."));
            }

            await Task.Delay(_screen.PollInterval, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Records one performed action as an <c>engine</c> event of the running step (upstream's <c>recordAction</c>, <c>locator/engine.ts</c>):
    /// the node it landed on as the resolve read it, and how long the node took to appear. The port retries no stale node, so the
    /// event never says one went stale.
    /// </summary>
    private void RecordAction(SemanticNode node, LocatorAction action, DateTimeOffset startedAt, long started, long waitedMs, StepEventStatus status, string? code)
    {
        if (_screen.Steps is null)
        {
            return;
        }

        string? Field(string? value)
        {
            var text = value is null ? "" : _screen.Redact(TextRules.Normalize(value));
            return text.Length == 0 ? null : text;
        }

        var target = RecordedTarget.TargetLabel(new RecordedTarget
        {
            Role = node.Role,
            Name = Field(node.Name),
            Text = Field(node.Text),
            Placeholder = Field(node.AttributeOf("placeholder")),
            TestId = Field(node.TestId),
        });
        // The action kind the engine contract names, which is the method's name but for selectOption.
        var kind = action is LocatorAction.Select ? "select" : ApiName(action);
        _screen.RecordEvent(new StepEvent
        {
            Kind = StepEventKind.Engine,
            Name = kind,
            StartedAt = startedAt,
            DurationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            Status = status,
            Code = code,
            Detail = kind + " " + target + (waitedMs > 0 ? " (appeared after " + waitedMs.ToString(System.Globalization.CultureInfo.InvariantCulture) + "ms)" : ""),
        });
    }

    /// <summary>A secure field's text, value, and attributes are withheld, so reading them is denied rather than read as empty.</summary>
    internal void DenySecureRead(IReadOnlyList<SemanticNode> nodes)
    {
        if (nodes.Any(node => node.States.Secure))
        {
            throw new TestException("POLICY_DENIED", "reading values from a secure field is denied: " + Query.Describe());
        }
    }

    /// <summary>Returns the one match for a text or value read. A secure match fails with <c>POLICY_DENIED</c>.</summary>
    private async Task<SemanticNode> ResolveReadableAsync(CancellationToken cancellationToken)
    {
        var node = await ResolveStrictAsync(cancellationToken).ConfigureAwait(false);
        DenySecureRead([node]);
        return node;
    }

    /// <summary>Waits up to the action timeout for the one match of a read. More than one match fails at once.</summary>
    private async Task<SemanticNode> ResolveStrictAsync(CancellationToken cancellationToken)
    {
        var token = _screen.Token(cancellationToken);
        var started = DateTime.UtcNow;
        var deadline = started + _screen.ActionTimeout;
        while (true)
        {
            var node = await ResolveSingleAsync(token, new Deadline(deadline)).ConfigureAwait(false);
            if (node is not null)
            {
                return node;
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw NotFound(DateTime.UtcNow - started);
            }

            await Task.Delay(_screen.PollInterval, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Waits until <paramref name="deadline"/> for the one match. More than one match fails at once. Upstream's <c>resolveExactlyOne</c>,
    /// which a drag uses for the node it drops on.
    /// </summary>
    internal async Task<SemanticNode> ResolveExactlyOneAsync(Deadline deadline, CancellationToken token)
    {
        var started = _screen.Clock.GetTimestamp();
        while (true)
        {
            var node = await ResolveSingleAsync(token, deadline).ConfigureAwait(false);
            if (node is not null)
            {
                return node;
            }

            if (deadline.Expired(_screen.Clock.GetUtcNow()))
            {
                throw NotFound(_screen.Clock.GetElapsedTime(started));
            }

            await Task.Delay(_screen.PollInterval, token).ConfigureAwait(false);
        }
    }

    /// <summary>Returns the one match, or null when nothing matches. More than one match fails.</summary>
    internal async Task<SemanticNode?> ResolveSingleAsync(CancellationToken token, Deadline? deadline = null, MissingFrame missingFrame = MissingFrame.Wait)
    {
        var matches = await ResolveAsync(token, deadline: deadline, missingFrame: missingFrame).ConfigureAwait(false);
        if (matches.Count > 1)
        {
            throw Ambiguous(matches.Count);
        }

        return matches.Count == 1 ? matches[0] : null;
    }

    private TimeSpan TimeoutOf(TimeSpan? timeout)
    {
        if (timeout is TimeSpan chosen)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(chosen, TimeSpan.Zero, "timeout");
            return chosen;
        }

        return _screen.ActionTimeout;
    }

    private static string StateName(WaitForState state) => state switch
    {
        WaitForState.Attached => "attached",
        WaitForState.Detached => "detached",
        WaitForState.Hidden => "hidden",
        _ => "visible",
    };

    private TestException Ambiguous(int count) =>
        new("STRICT_MODE", Query.Describe() + " matched " + count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " nodes.") { Details = LocatorResolver.LocatorDetails(Query) with { Matches = count } };

    private TestException NotFound(TimeSpan waited) => new("NOT_FOUND", Query.Describe() + " matched 0 nodes.") { Details = LocatorResolver.LocatorDetails(Query, waited) };
}

/// <summary>What a frame the engine reports missing means to a resolve (upstream's <c>MissingFrame</c>).</summary>
internal enum MissingFrame
{
    /// <summary>Treated like a node that is not there yet: the resolve keeps polling for it.</summary>
    Wait,

    /// <summary>Answers zero matches.</summary>
    Empty,
}

/// <summary>Options for one locator action.</summary>
public class ActionOptions
{
    /// <summary>How long to wait for exactly one enabled match. Defaults to the configured action timeout.</summary>
    public TimeSpan? Timeout { get; init; }
}

/// <summary>Options for <see cref="Locator.TapAsync(ClickOptions, CancellationToken)"/>, <see cref="Locator.DoubleTapAsync(ClickOptions, CancellationToken)"/>, and <see cref="Locator.SecondaryTapAsync"/>. Upstream's <c>ClickOptions</c>.</summary>
public class ClickOptions : ActionOptions
{
    /// <summary>
    /// Keys held for the click, as a Shift-click extends a selection. Distinct; an empty list holds nothing. Only an engine that declares
    /// <see cref="EngineCapabilities.TapModifiers"/> takes them.
    /// </summary>
    public IReadOnlyList<KeyModifier>? Modifiers { get; init; }
}

/// <summary>Options for <see cref="Locator.LongPressAsync"/>.</summary>
public sealed class LongPressOptions : ActionOptions
{
    /// <summary>How long the press is held: whole milliseconds from 100 through 10000. Without one the engine holds its default.</summary>
    public TimeSpan? Duration { get; init; }
}

/// <summary>Options for <see cref="Locator.SwipeAsync"/>.</summary>
public sealed class SwipeOptions : ActionOptions
{
    /// <summary>Which way the content moves into view, as for <see cref="ScrollDirection"/>.</summary>
    public required ScrollDirection Direction { get; init; }

    /// <summary>The stride of the gesture. Without one the engine strides as it does for a scroll.</summary>
    public Momentum? Momentum { get; init; }
}

/// <summary>Options for <c>Screen.ScrollUntilVisibleAsync</c> and <c>Locator.ScrollUntilVisibleAsync</c>.</summary>
public sealed class ScrollUntilVisibleOptions
{
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Which way to scroll. Defaults to <see cref="ScrollDirection.Down"/>.</summary>
    public ScrollDirection? Direction { get; init; }

    /// <summary>The stride of each step. Defaults to <see cref="Momentum.Slow"/>.</summary>
    public Momentum? Momentum { get; init; }

    /// <summary>How long to scroll before giving up, every swipe included. Defaults to 30 seconds.</summary>
    public TimeSpan? Timeout { get; init; }
}

/// <summary>Options for <see cref="Locator.PressSequentiallyAsync"/>.</summary>
public sealed class PressSequentiallyOptions : ActionOptions
{
    /// <summary>How long to wait between characters.</summary>
    public TimeSpan? Delay { get; init; }
}

/// <summary>The state <see cref="Locator.WaitForAsync"/> waits for.</summary>
public enum WaitForState
{
    /// <summary>Exactly one visible match.</summary>
    Visible,

    /// <summary>Exactly one match, visible or not.</summary>
    Attached,

    /// <summary>No match, visible or not.</summary>
    Detached,

    /// <summary>No visible match.</summary>
    Hidden,
}

/// <summary>Options for <see cref="Locator.WaitForAsync"/>.</summary>
public sealed class LocatorWaitForOptions
{
    public WaitForState State { get; init; } = WaitForState.Visible;

    /// <summary>How long to wait. Defaults to the configured action timeout.</summary>
    public TimeSpan? Timeout { get; init; }
}

/// <summary>
/// A locator expression. Query kinds (<c>role</c>, <c>text</c>, <c>label</c>,
/// <c>testid</c>, <c>placeholder</c>, <c>displayValue</c>) search the
/// descendants of <see cref="Parent"/> when it is set. <c>filter</c> and
/// <c>index</c> narrow <see cref="Source"/>. <c>union</c> matches either
/// <see cref="Left"/> or <see cref="Right"/>, each node once, in document order. <c>selector</c> is a CSS or XPath selector in the
/// engine's own language, and <c>frame</c> scopes <see cref="Source"/> into the iframe <see cref="Selector"/> names.
/// </summary>
public sealed record LocatorExpression
{
    public const int LastIndex = -1;

    public LocatorExpression? Parent { get; init; }

    public LocatorExpression? Source { get; init; }

    public string Kind { get; init; } = "role";

    public string? Role { get; init; }

    /// <summary>The role query's name, or the value every other query kind matches.</summary>
    public TextMatch? Value { get; init; }

    public bool Exact { get; init; } = true;

    public bool Visible { get; init; }

    public bool? Checked { get; init; }

    public bool? Disabled { get; init; }

    public bool? Selected { get; init; }

    public bool? Expanded { get; init; }

    public bool? Pressed { get; init; }

    public int? Level { get; init; }

    public TextMatch? HasText { get; init; }

    /// <summary>
    /// Whether <see cref="HasText"/> must equal the text instead of being a case-insensitive substring of it, as upstream's text pattern
    /// carries its <c>exact</c>. <see cref="Locator.Filter(TextMatch)"/> builds a substring match (upstream's <c>{ exact: false }</c>), so
    /// it leaves this <c>false</c>. A <see cref="System.Text.RegularExpressions.Regex"/> ignores it. .NET-only: <see cref="TextMatch"/> holds no
    /// exactness of its own.
    /// </summary>
    public bool HasTextExact { get; init; }

    /// <summary>Upstream's <c>hasNotText</c>: drops a match whose subtree text matches. A case-insensitive substring unless <see cref="HasNotTextExact"/>.</summary>
    public TextMatch? HasNotText { get; init; }

    /// <summary>
    /// Whether <see cref="HasNotText"/> must equal the text instead of being a case-insensitive substring of it. .NET-only, as
    /// <see cref="HasTextExact"/> is: <see cref="TextMatch"/> holds no exactness of its own.
    /// </summary>
    public bool HasNotTextExact { get; init; }

    public LocatorExpression? Has { get; init; }

    /// <summary>Upstream's <c>hasNot</c>: drops a match whose subtree resolves the inner expression.</summary>
    public LocatorExpression? HasNot { get; init; }

    public int? Index { get; init; }

    public LocatorExpression? Left { get; init; }

    public LocatorExpression? Right { get; init; }

    /// <summary>
    /// The expression of a <c>selector</c> query, in the engine's own selector language (a device engine's, or the web engine's CSS and
    /// XPath), or the selector of the iframe a <c>frame</c> expression scopes <see cref="Source"/> into.
    /// </summary>
    public string? Selector { get; init; }

    /// <summary>
    /// The refs of the nodes a <c>refs</c> expression names. .NET-only: upstream's web engine projects a <c>selector</c> or <c>frame</c>
    /// expression onto a Playwright locator, but the port's web engine resolves over its semantic tree, so it asks Playwright which
    /// elements a selector names and hands the resolver their refs.
    /// </summary>
    internal IReadOnlySet<string>? Refs { get; init; }

    /// <summary>
    /// Asks an engine that resolves over its tree to match hidden nodes too, which a role query otherwise skips. .NET-only: upstream's
    /// engines resolve with Playwright, which has no such switch. <c>toBeAttached</c> and <c>waitFor</c> set it on the expression they hand
    /// the engine; an engine that does not read it leaves it off.
    /// </summary>
    internal bool IncludeHidden { get; init; }

    internal static LocatorExpression ForRole(string role, TextMatch? name, RoleOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        if (options.Level is int level && (level < 1 || level > 6))
        {
            throw new ArgumentOutOfRangeException(nameof(options), level, "Level must be 1 through 6.");
        }

        return new LocatorExpression
        {
            Kind = "role",
            Role = string.Equals(role, "img", StringComparison.OrdinalIgnoreCase) ? "image" : role,
            Value = name,
            Exact = options.Exact,
            Visible = options.Visible,
            Checked = options.Checked,
            Disabled = options.Disabled,
            Selected = options.Selected,
            Expanded = options.Expanded,
            Pressed = options.Pressed,
            Level = options.Level,
        };
    }

    internal static LocatorExpression For(string kind, TextMatch value, TextMatchOptions options, string paramName)
    {
        ArgumentNullException.ThrowIfNull(value, paramName);
        ArgumentNullException.ThrowIfNull(options);
        value.ThrowIfBlank(paramName);
        return new LocatorExpression { Kind = kind, Value = value, Exact = options.Exact, Visible = options.Visible };
    }

    /// <summary>
    /// What the locator asked for, as an error reports it (upstream's <c>expressionHints</c>): the role and name of a role query, a
    /// test id given as a string, or the text another query kind looks for. A filter or an index answers for the query under it. A
    /// selector has no hint to give.
    /// </summary>
    internal ErrorDetails ExpressionHints()
    {
        static string? PatternText(TextMatch? match) => match is null ? null : match.Pattern is null ? match.Text : match.Pattern.ToString();
        switch (Kind)
        {
            case "union":
                return new ErrorDetails();
            case "filter" or "index":
                return Source!.ExpressionHints();
            case "role":
                return new ErrorDetails { Role = Role, Name = PatternText(Value) };
            case "testid":
                return new ErrorDetails { TestId = Value?.Pattern is null ? Value?.Text : null };
            case "frame":
                return Source!.ExpressionHints();
            case "selector" or "refs":
                return new ErrorDetails();
            default:
                return new ErrorDetails { Name = PatternText(Value) };
        }
    }

    public string Describe()
    {
        switch (Kind)
        {
            case "union":
                return Left!.Describe() + ".or(" + Right!.Describe() + ")";
            case "filter":
                {
                    var parts = new List<string>();
                    if (HasText is not null)
                    {
                        parts.Add("hasText: " + HasText + (HasTextExact ? ", exact" : ""));
                    }

                    if (HasNotText is not null)
                    {
                        parts.Add("hasNotText: " + HasNotText + (HasNotTextExact ? ", exact" : ""));
                    }

                    if (Has is not null)
                    {
                        parts.Add("has: " + Has.Describe());
                    }

                    if (HasNot is not null)
                    {
                        parts.Add("hasNot: " + HasNot.Describe());
                    }

                    return Source!.Describe() + ".filter(" + string.Join(", ", parts) + ")";
                }
            case "frame":
                return "frameLocator(" + Quoted(Selector) + ") >> " + Source!.Describe();
            case "index":
                return Source!.Describe() + (Index == LastIndex
                    ? ".last()"
                    : ".nth(" + Index!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")");
        }

        var visible = Visible ? ", visible: true" : "";
        var core = Kind switch
        {
            "role" => "getByRole(\"" + Role + "\"" + (Value is null ? "" : ", " + Value) + RoleOptions() + visible + ")",
            "text" => "getByText(" + Value + visible + ")",
            "label" => "getByLabel(" + Value + visible + ")",
            "testid" => "getByTestId(" + Value + visible + ")",
            "placeholder" => "getByPlaceholder(" + Value + visible + ")",
            "displayValue" => "getByDisplayValue(" + Value + visible + ")",
            "selector" => "locator(" + Quoted(Selector) + ")",
            "refs" => "locator(" + string.Join(", ", Refs ?? (IEnumerable<string>)[]) + ")",
            _ => Kind,
        };
        return Parent is null ? core : Parent.Describe() + "." + core;
    }

    private static string Quoted(string? selector) =>
        "\"" + selector?.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private string RoleOptions()
    {
        var parts = new List<string>();
        void Add(string key, bool? state)
        {
            if (state is bool value)
            {
                parts.Add(key + ": " + (value ? "true" : "false"));
            }
        }

        Add("checked", Checked);
        Add("disabled", Disabled);
        Add("selected", Selected);
        Add("expanded", Expanded);
        Add("pressed", Pressed);
        if (Level is int level)
        {
            parts.Add("level: " + level.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return parts.Count == 0 ? "" : ", { " + string.Join(", ", parts) + " }";
    }
}

/// <summary>
/// The upstream reference locator semantics over a semantic tree. Role queries
/// never match a hidden node unless asked to; the other kinds keep hidden nodes
/// unless the query says <c>visible</c>, which always wins. Text and label queries answer with the innermost
/// match. A scope searches strict descendants of its matches.
/// </summary>
internal static class LocatorResolver
{
    /// <summary>
    /// The facts of a locator failure an error keeps beside the message (upstream's <c>locatorDetails</c>): the locator as written,
    /// what it asked for, and how long it waited, when it waited.
    /// </summary>
    public static ErrorDetails LocatorDetails(LocatorExpression query, TimeSpan? waited = null) =>
        query.ExpressionHints() with { Locator = query.Describe(), WaitedMs = waited is { } elapsed ? (long)elapsed.TotalMilliseconds : null };

    /// <summary>
    /// Returns the nodes <paramref name="query"/> names. <paramref name="includeHidden"/>
    /// asks for every attached node, so role queries match hidden nodes too.
    /// A <c>visible</c> query never matches a hidden node.
    /// </summary>
    public static IReadOnlyList<SemanticNode> Resolve(Observation observation, LocatorExpression query, bool includeHidden = false)
    {
        var tree = new TreeIndex(observation.Roots);
        return Resolve(query, tree.All, tree, includeHidden);
    }

    public static IEnumerable<SemanticNode> Walk(IEnumerable<SemanticNode> roots)
    {
        foreach (var root in roots)
        {
            yield return root;
            foreach (var child in Walk(root.Children))
            {
                yield return child;
            }
        }
    }

    private static List<SemanticNode> Resolve(LocatorExpression query, IReadOnlyList<SemanticNode> candidates, TreeIndex tree, bool includeHidden)
    {
        switch (query.Kind)
        {
            case "union":
                {
                    var matches = new HashSet<SemanticNode>(ReferenceEqualityComparer.Instance);
                    matches.UnionWith(Resolve(query.Left!, candidates, tree, includeHidden));
                    matches.UnionWith(Resolve(query.Right!, candidates, tree, includeHidden));
                    return candidates.Where(matches.Contains).ToList();
                }

            case "filter":
                {
                    if ((query.Has is not null && UsesDisplayValue(query.Has)) || (query.HasNot is not null && UsesDisplayValue(query.HasNot)))
                    {
                        throw UnsupportedDisplayValue();
                    }

                    var source = Resolve(query.Source!, candidates, tree, includeHidden);
                    var includeValue = !UsesDisplayValue(query.Source!);
                    return source.Where(node =>
                    {
                        if (query.HasText is not null && !SubtreeHasText(node, query.HasText, query.HasTextExact, includeValue))
                        {
                            return false;
                        }

                        if (query.HasNotText is not null && SubtreeHasText(node, query.HasNotText, query.HasNotTextExact, includeValue))
                        {
                            return false;
                        }

                        if (query.Has is null && query.HasNot is null)
                        {
                            return true;
                        }

                        var within = tree.DescendantsOf([node], candidates);
                        if (query.Has is not null && Resolve(query.Has, within, tree, includeHidden).Count == 0)
                        {
                            return false;
                        }

                        return query.HasNot is null || Resolve(query.HasNot, within, tree, includeHidden).Count == 0;
                    }).ToList();
                }

            case "index":
                {
                    var source = Resolve(query.Source!, candidates, tree, includeHidden);
                    var position = query.Index == LocatorExpression.LastIndex ? source.Count - 1 : query.Index!.Value;
                    return position >= 0 && position < source.Count ? [source[position]] : [];
                }

            case "selector":
                throw new EngineException(
                    "UNSUPPORTED_CAPABILITY",
                    $"selector {System.Text.Json.JsonSerializer.Serialize(query.Selector)} needs a platform selector language, and this surface has none",
                    retryable: false);

            case "frame":
                throw new EngineException(
                    EngineErrorCodes.FrameNotFound,
                    $"no nested document matches {System.Text.Json.JsonSerializer.Serialize(query.Selector)}: this surface has none to scope a query into",
                    retryable: false);

            default:
                {
                    if (query.Parent is not null && UsesDisplayValue(query.Parent))
                    {
                        throw UnsupportedDisplayValue();
                    }

                    var pool = query.Parent is null
                        ? candidates
                        : tree.DescendantsOf(Resolve(query.Parent, candidates, tree, includeHidden), candidates);
                    var matches = pool.Where(node => Matches(node, query, includeHidden)).ToList();
                    return query.Kind is "text" or "label" ? tree.InnermostOnly(matches) : matches;
                }
        }
    }

    private static bool UsesDisplayValue(LocatorExpression query) =>
        query.Kind == "displayValue"
        || (query.Source is not null && UsesDisplayValue(query.Source))
        || (query.Parent is not null && UsesDisplayValue(query.Parent));

    private static EngineException UnsupportedDisplayValue() =>
        new("UNSUPPORTED_CAPABILITY", "displayValue queries cannot scope child queries or serve as a has or hasNot filter in this engine");

    private static bool Matches(SemanticNode node, LocatorExpression query, bool includeHidden)
    {
        if (query.Visible && node.States.Hidden)
        {
            return false;
        }

        var value = query.Value;
        return query.Kind switch
        {
            "role" => RoleMatches(node, query, includeHidden),
            "text" => value!.Matches(node.Name, query.Exact) || value.Matches(node.Text, query.Exact),
            "label" => value!.Matches(node.Name, query.Exact),
            "testid" => value!.Matches(node.TestId, exact: true),
            "placeholder" => value!.Matches(node.Placeholder, query.Exact),
            // A checkbox or radio carries a value it never shows (`on` by default), so it is no candidate.
            "displayValue" => !IsCheckable(node) && value!.Matches(node.Value, query.Exact),
            "refs" => query.Refs!.Contains(node.Ref),
            _ => false,
        };
    }

    // An input whose type is checkbox or radio, as upstream's `input:not([type="checkbox" i]):not([type="radio" i])`.
    internal static bool IsCheckable(SemanticNode node) =>
        node.Attributes.TryGetValue("type", out var type) && (type.Equals("checkbox", StringComparison.OrdinalIgnoreCase) || type.Equals("radio", StringComparison.OrdinalIgnoreCase));

    private static bool RoleMatches(SemanticNode node, LocatorExpression query, bool includeHidden)
    {
        if (node.Role is null || !string.Equals(node.Role, query.Role, StringComparison.OrdinalIgnoreCase) || ((node.States.Hidden || node.States.AriaHidden) && !includeHidden))
        {
            return false;
        }

        if (query.Value is not null && !query.Value.Matches(node.Name, query.Exact))
        {
            return false;
        }

        var states = node.States;
        return StateIs(query.Checked, states.Checked)
            && StateIs(query.Disabled, states.Disabled)
            && StateIs(query.Selected, states.Selected)
            && StateIs(query.Expanded, states.Expanded)
            && StateIs(query.Pressed, states.Pressed)
            && (query.Level is null || node.Level == query.Level);
    }

    private static bool StateIs(bool? wanted, bool actual) => wanted is null || wanted.Value == actual;

    /// <summary>
    /// Whether the node's own name, text, or value, or a descendant's, contains the text, ignoring case.
    /// <paramref name="includeValue"/> false leaves values out, as the text content of a display-value match has none.
    /// </summary>
    private static bool SubtreeHasText(SemanticNode node, TextMatch expected, bool exact, bool includeValue)
    {
        if (expected.Matches(node.Name, exact) || expected.Matches(node.Text, exact) || (includeValue && expected.Matches(node.Value, exact)))
        {
            return true;
        }

        foreach (var child in node.Children)
        {
            if (SubtreeHasText(child, expected, exact, includeValue))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Every node of an observation in document order, with each node's parent.</summary>
    private sealed class TreeIndex
    {
        private readonly Dictionary<SemanticNode, SemanticNode> _parents = new(ReferenceEqualityComparer.Instance);

        public TreeIndex(IEnumerable<SemanticNode> roots)
        {
            foreach (var root in roots)
            {
                Add(root, null);
            }
        }

        public List<SemanticNode> All { get; } = [];

        /// <summary>The candidates that are strict descendants of any of <paramref name="ancestors"/>, in document order.</summary>
        public List<SemanticNode> DescendantsOf(IReadOnlyCollection<SemanticNode> ancestors, IReadOnlyList<SemanticNode> candidates)
        {
            if (ancestors.Count == 0)
            {
                return [];
            }

            var set = new HashSet<SemanticNode>(ancestors, ReferenceEqualityComparer.Instance);
            return candidates.Where(node => Ancestors(node).Any(set.Contains)).ToList();
        }

        /// <summary>
        /// Drops every match that contains another match. A container that
        /// repeats its child's text is not a second match.
        /// </summary>
        public List<SemanticNode> InnermostOnly(List<SemanticNode> matches)
        {
            var containers = new HashSet<SemanticNode>(ReferenceEqualityComparer.Instance);
            foreach (var match in matches)
            {
                containers.UnionWith(Ancestors(match));
            }

            return matches.Where(node => !containers.Contains(node)).ToList();
        }

        private IEnumerable<SemanticNode> Ancestors(SemanticNode node)
        {
            for (var current = node; _parents.TryGetValue(current, out var parent); current = parent)
            {
                yield return parent;
            }
        }

        private void Add(SemanticNode node, SemanticNode? parent)
        {
            All.Add(node);
            if (parent is not null)
            {
                _parents[node] = parent;
            }

            foreach (var child in node.Children.Concat(node.InlineNodes))
            {
                Add(child, node);
            }
        }
    }
}
