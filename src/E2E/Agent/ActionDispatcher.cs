// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Cache;
using E2E.Engine;
using E2E.Internal;
using static E2E.Agent;

namespace E2E;

/// <summary>
/// Upstream's action dispatcher (<c>agent/action-dispatcher.ts</c>): the <see cref="IExecutorActions"/> of one act step. Each
/// verb performs the action on the engine, records it in the step's accounting, and arms the change wait its settle policy
/// asks for. Upstream's <c>commitTargeted</c> is <see cref="CommitAsync(RecordedAction)"/>, and its private <c>targeted</c> is
/// <see cref="TargetedAsync"/>.
/// </summary>
internal sealed class ActionDispatcher : IExecutorActions
{
    // A scroll to a text gives up after this many pages, or once the screen stops moving.
    private const int MaxScrollUntilScreens = 800;

    // One relocation covers a re-render between the look and the action. The second covers a render landing between the
    // fresh look and the action itself. A control that vanishes faster than that is reported as gone.
    private const int MaxStaleRelocations = 2;
    private const int ScrollUntilStillPages = 3;
    private const int ScrollUntilWrongListPages = 2;

    private readonly AttemptScope _scope;
    private readonly ObservationFeed _feed;
    private readonly StepAccounting _accounting;
    private readonly IReadOnlyDictionary<string, object?>? _parameters;
    private readonly Func<IReadOnlyList<Secret>> _secrets;
    private readonly ActionHook? _onAction;

    // secrets: what TypeSecretAsync may fill, the secrets the step's params declare. onAction: the test's hook for each completed action.
    public ActionDispatcher(AttemptScope scope, ObservationFeed feed, StepAccounting accounting, IReadOnlyDictionary<string, object?>? parameters, Func<IReadOnlyList<Secret>> secrets, ActionHook? onAction = null)
    {
        _scope = scope;
        _feed = feed;
        _accounting = accounting;
        _parameters = parameters;
        _secrets = secrets;
        _onAction = onAction;
    }

    public Task TapAsync(ExecutorTarget target, ClickOptions? options, CancellationToken cancellationToken = default) =>
        ClickAsync("tap", target, options, cancellationToken);

    public Task DoubleTapAsync(ExecutorTarget target, ClickOptions? options, CancellationToken cancellationToken = default) =>
        ClickAsync("doubleTap", target, options, cancellationToken);

    // Upstream's nodeVerb for the tap family: the modifiers are checked before a slot is taken, and recorded with the action.
    private Task ClickAsync(string kind, ExecutorTarget target, ClickOptions? options, CancellationToken cancellationToken)
    {
        var modifiers = RequireClickModifiers(options, kind, _scope.EngineCapabilities);
        return RunAsync(
            kind,
            async token =>
            {
                var node = Resolve(target);
                await TargetedAsync(node, ClickAction(kind, modifiers), token).ConfigureAwait(false);
                var recorded = Target(node, kind);
                recorded.Modifiers = modifiers?.Select(modifier => modifier.ToString()).ToList();
                await CommitAsync(recorded).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// The <c>modifiers</c> of a tap-family action, validated: distinct key modifiers, on an engine that declares
    /// <see cref="EngineCapabilities.TapModifiers"/>. An empty list holds nothing and is dropped, so the recorded action stays
    /// clean. Upstream's <c>requireClickModifiers</c>; the capability check is its session's <c>perform</c>.
    /// </summary>
    internal static IReadOnlyList<KeyModifier>? RequireClickModifiers(ClickOptions? options, string verb, EngineCapabilities capabilities)
    {
        if (options?.Modifiers is not { } given)
        {
            return null;
        }

        var modifiers = new List<KeyModifier>();
        foreach (var modifier in given)
        {
            if (!Enum.IsDefined(modifier))
            {
                throw new TestException("INVALID_ARGUMENT", verb + "() modifiers must be an array of " + string.Join(", ", KeyModifiers.Names));
            }

            if (modifiers.Contains(modifier))
            {
                throw new TestException("INVALID_ARGUMENT", verb + "() names a modifier twice");
            }

            modifiers.Add(modifier);
        }

        if (modifiers.Count == 0)
        {
            return null;
        }

        if (!capabilities.HasFlag(EngineCapabilities.TapModifiers))
        {
            throw new EngineException(EngineErrorCodes.UnsupportedCapability, "The engine cannot hold modifiers on " + verb + ".");
        }

        return modifiers;
    }

    /// <summary>The engine action of a <c>tap</c> or <c>doubleTap</c> holding <paramref name="modifiers"/>.</summary>
    internal static LocatorAction ClickAction(string kind, IReadOnlyList<KeyModifier>? modifiers) =>
        string.Equals(kind, "doubleTap", StringComparison.Ordinal) ? new LocatorAction.DoubleTap(modifiers) : new LocatorAction.Tap(modifiers);

    public Task TypeAsync(ExecutorTarget target, string value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        return RunAsync(
            "type",
            async token =>
            {
                var node = Resolve(target);
                await TargetedAsync(node, new LocatorAction.Fill(value, false), token).ConfigureAwait(false);
                await CommitAsync("fill", node, value: CacheKeys.Template(value, _parameters)).ConfigureAwait(false);
            },
            cancellationToken);
    }

    public async Task TypeSecretAsync(ExecutorTarget target, string secretName, CancellationToken cancellationToken = default)
    {
        // Checked before a slot is taken: the name must come from the step's own params, so an executor can never fill a
        // secret the test did not hand it.
        var secret = _secrets().FirstOrDefault(item => string.Equals(item.Name, secretName, StringComparison.Ordinal))
            ?? throw new AgentException("POLICY_DENIED", "secret \"" + secretName + "\" was not declared in this step's params; only declared secrets can be filled", blocked: true);
        await RunAsync(
            "typeSecret",
            async token =>
            {
                var node = Resolve(target);
                AuthorizeSecretFill(secret, node);
                // Before the engine call: a fill that types the value and then fails has still put it on screen.
                _scope.Exposure.RaiseFilled();
                await TargetedAsync(node, new LocatorAction.Fill(secret.Value, true), token).ConfigureAwait(false);
                // Recorded by stable name only; replay runs the full authorization again.
                await CommitAsync("fill", node, value: "<secret:" + secret.Name + ">").ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public Task ClearAsync(ExecutorTarget target, CancellationToken cancellationToken = default) =>
        RunAsync(
            "clear",
            async token =>
            {
                var node = Resolve(target);
                await TargetedAsync(node, new LocatorAction.Clear(), token).ConfigureAwait(false);
                await CommitAsync("clear", node).ConfigureAwait(false);
            },
            cancellationToken);

    public Task PressAsync(ExecutorTarget? target, string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new TestException("INVALID_ARGUMENT", "press requires a key");
        }

        return RunAsync(
            "press",
            async token =>
            {
                if (target is null)
                {
                    await _scope.Session.PressAsync(key, token).ConfigureAwait(false);
                    await CommitAsync(new RecordedAction { Kind = "press", Key = key }).ConfigureAwait(false);
                    return;
                }

                var node = Resolve(target);
                await TargetedAsync(node, new LocatorAction.Press(key), token).ConfigureAwait(false);
                await CommitAsync("press", node, key: key).ConfigureAwait(false);
            },
            cancellationToken);
    }

    public Task SelectAsync(ExecutorTarget target, string value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        return RunAsync(
            "selectOption",
            async token =>
            {
                var node = Resolve(target);
                await TargetedAsync(node, new LocatorAction.Select(value), token).ConfigureAwait(false);
                await CommitAsync("select", node, value: CacheKeys.Template(value, _parameters)).ConfigureAwait(false);
            },
            cancellationToken);
    }

    public Task CheckAsync(ExecutorTarget target, bool check, CancellationToken cancellationToken = default) =>
        RunAsync(
            check ? "check" : "uncheck",
            async token =>
            {
                var node = Resolve(target);
                await TargetedAsync(node, check ? new LocatorAction.Check() : new LocatorAction.Uncheck(), token).ConfigureAwait(false);
                await CommitAsync(check ? "check" : "uncheck", node).ConfigureAwait(false);
            },
            cancellationToken);

    public Task ScrollToAsync(ExecutorTarget target, CancellationToken cancellationToken = default) =>
        RunAsync(
            "scrollIntoView",
            async token =>
            {
                var node = Resolve(target);
                await _scope.Session.PerformAsync(node, new LocatorAction.ScrollIntoView(), token).ConfigureAwait(false);
                await CommitAsync("scrollTo", node).ConfigureAwait(false);
            },
            cancellationToken);

    public Task ScrollUntilAsync(string text, ScrollDirection direction, ExecutorTarget? list = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > AgentTools.MaxScrollToText)
        {
            throw new TestException("INVALID_ARGUMENT", "scroll to a text requires the text to reach, up to " + AgentTools.MaxScrollToText.ToString(CultureInfo.InvariantCulture) + " characters");
        }

        CheckDirection(direction);
        return RunAsync(
            "scrollUntil",
            async token =>
            {
                var (paged, spans) = await PageUntilAsync(text, direction, list, token).ConfigureAwait(false);
                var recorded = paged is null ? null : Target(paged, "scrollUntil");
                await CommitAsync(new RecordedAction
                {
                    Kind = "scrollUntil",
                    Role = recorded?.Role,
                    Name = recorded?.Name,
                    TestId = recorded?.TestId,
                    Direction = DirectionName(direction),
                    Text = CacheKeys.Template(text, _parameters),
                    Spans = spans,
                }).ConfigureAwait(false);
            },
            cancellationToken);
    }

    public Task ScrollAsync(ExecutorTarget? list, ScrollDirection direction, int times = 1, CancellationToken cancellationToken = default)
    {
        CheckDirection(direction);
        if (times is < 1 or > AgentTools.MaxScrollTimes)
        {
            throw new TestException("INVALID_ARGUMENT", "scroll times must be 1 to " + AgentTools.MaxScrollTimes.ToString(CultureInfo.InvariantCulture));
        }

        return RunAsync(
            "scroll",
            async token =>
            {
                var actions = _accounting.Recorder.Actions;
                SemanticNode? node = null;
                for (var repeat = 0; repeat < times; repeat++)
                {
                    if (repeat > 0)
                    {
                        // Each later repeat reads a settled screen first, and pages the list as that screen shows it.
                        _feed.ArmAfter(actions[^1]);
                        await _feed.ObserveAsync(SettleMode.HeldStill, "page", token).ConfigureAwait(false);
                    }

                    // A list that filled the screen and left the tree scrolls as the viewport does (scroll-target.ts).
                    var resolved = list is null ? null : ScrollTarget.ResolveScrollTarget(_feed, list);
                    if (resolved is null)
                    {
                        await _scope.Session.SwipeAsync(direction, token).ConfigureAwait(false);
                        RecordScroll(direction, null, null);
                        continue;
                    }

                    node = resolved.Node;
                    await _scope.Session.PerformAsync(node, new LocatorAction.Swipe(direction), token).ConfigureAwait(false);
                    RecordScroll(direction, node, resolved.Observation.Viewport);
                }

                if (list is not null && node is not null)
                {
                    list.Node = node;
                }

                await ArmAndNotifyAsync(actions[^1]).ConfigureAwait(false);
            },
            cancellationToken);
    }

    public Task NavigateAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new TestException("INVALID_ARGUMENT", "navigate requires a URL");
        }

        return RunAsync(
            "navigate",
            async token =>
            {
                await _scope.Session.OpenAsync(Routes.Resolve(_scope.BaseUrl, url), token).ConfigureAwait(false);
                await CommitAsync(new RecordedAction { Kind = "navigate", Url = CacheKeys.Template(url, _parameters) }).ConfigureAwait(false);
            },
            cancellationToken);
    }

    public Task BackAsync(CancellationToken cancellationToken = default) =>
        RunAsync(
            "back",
            async token =>
            {
                await _scope.Session.BackAsync(token).ConfigureAwait(false);
                await CommitAsync(new RecordedAction { Kind = "back" }).ConfigureAwait(false);
            },
            cancellationToken);

    /// <summary>
    /// Runs one grammar action against the action budget: the slot is taken before anything else, since a failed action was
    /// still an attempt. A failure of an action that ran past the step's end is the step's own hard stop, not the action's.
    /// </summary>
    private Task RunAsync(string name, Func<CancellationToken, Task> body, CancellationToken cancellationToken) =>
        _accounting.Queue.RunAsync(async () =>
        {
            _accounting.Reserve();
            // The call runs under the step's own token as well as the caller's, so a step that ends stops an action in flight.
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_accounting.Token, cancellationToken);
            var actions = _accounting.Recorder.Actions;
            try
            {
                // One engine event named for the action, saying what it did once the action was recorded.
                await Phases.InstrumentPhaseAsync(
                    new PhaseHost(_scope.Steps, _scope.Debug),
                    new PhaseSpec(StepEventKind.Engine, Phases.Action, name),
                    async () =>
                    {
                        var before = actions.Count;
                        await body(linked.Token).ConfigureAwait(false);
                        return before;
                    },
                    before => actions.Count > before ? new PhaseDetail(Detail: _scope.Redactor.Redact(DefaultAgent.Describe(actions[^1]))) : new PhaseDetail()).ConfigureAwait(false);
            }
            catch (Exception) when (CheckStep())
            {
                throw;
            }
        });

    // Throws the step's hard stop when the clock or the cancel ended it. Otherwise the action's own failure goes through.
    private bool CheckStep()
    {
        _accounting.Checkpoint();
        return false;
    }

    private static void CheckDirection(ScrollDirection direction)
    {
        if (!Enum.IsDefined(direction))
        {
            throw new TestException("INVALID_ARGUMENT", "invalid scroll direction \"" + direction + "\"");
        }
    }

    // The node an id names on the screens the step has seen. The tool loop's targets hold theirs already.
    private SemanticNode Resolve(ExecutorTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.Node
            ?? (target.Node = _feed.LastSeen(target.Id)?.Node)
            ?? throw new TestException("LOCATOR_NOT_FOUND", "node " + target.Id + " is not on the current screen; it was removed or never existed");
    }

    // The sink policy of upstream's authorizeSecretFill (agent/secrets.ts), with its reasons: a secret the attempt
    // knows, an enabled editable field, and a password field for a password. There is no origin check.
    private void AuthorizeSecretFill(Secret secret, SemanticNode node)
    {
        if (!_scope.Secrets.Any(item => string.Equals(item.Name, secret.Name, StringComparison.Ordinal)))
        {
            throw new AgentException("AUTH_CREDENTIAL_UNAVAILABLE", "secret \"" + secret.Name + "\" is not configured", blocked: true);
        }

        if (node.States.Disabled)
        {
            throw Denied("the target field is disabled");
        }

        if (!Roles.IsEditable(node))
        {
            throw Denied("the target is not an editable input (role " + (node.Role ?? "none") + ")");
        }

        // A password belongs in a password field, where the surface masks it. A generic secret has no field of its
        // own: it goes wherever the test says, and redaction covers the value the moment it shows on screen.
        var inputPurpose = string.IsNullOrEmpty(node.InputPurpose) ? "none" : node.InputPurpose;
        if (secret.Purpose == "password" && inputPurpose != "password")
        {
            throw Denied("field purpose " + inputPurpose + " is incompatible with secret purpose " + secret.Purpose);
        }
    }

    private static AgentException Denied(string reason) => new("POLICY_DENIED", reason, blocked: true);

    /// <summary>
    /// Pages a list, or the viewport, until a node reading <paramref name="text"/> is on the
    /// screen, then brings it into view. A screen that stops moving ends the paging, and a
    /// list that does not move hands the paging to the viewport. The list is re-found before
    /// every page.
    /// </summary>
    internal async Task<(SemanticNode? Node, double? Spans)> PageUntilAsync(string text, ScrollDirection direction, ExecutorTarget? list, CancellationToken token)
    {
        string? previous = null;
        (SemanticNode? Node, double? Spans) paged = (null, null);
        var still = 0;
        for (var screens = 0; ; screens++)
        {
            // A settled look after each page: a browser scrolls on a later frame than the wheel event, and a windowed
            // list renders its rows on a later scroll event, so a raw read right after the swipe sees the page as it was.
            var observation = await _feed.ObserveAsync(screens == 0 ? SettleMode.Raw : SettleMode.HeldStill, "page", token).ConfigureAwait(false);
            if (screens == 0)
            {
                _feed.MarkActing();
            }

            // Only the list being paged is read while it is on screen; once it is gone the whole screen is.
            var within = list is null ? null : LocatorResolver.Walk(observation.Roots).FirstOrDefault(node => node.Ref == list.Id.TrimStart('#'));
            var found = Reading.NodeReading(observation, text, _scope.Redactor, within);
            if (found is not null)
            {
                await _scope.Session.PerformAsync(found, new LocatorAction.ScrollIntoView(), token).ConfigureAwait(false);
                return paged;
            }

            if (screens >= MaxScrollUntilScreens)
            {
                throw new TestException("LOCATOR_NOT_FOUND", "Nothing reading \"" + text + "\" came into view within " + MaxScrollUntilScreens.ToString(CultureInfo.InvariantCulture) + " screens.");
            }

            var shape = Reading.ReadingShape(observation);
            still = string.Equals(shape, previous, StringComparison.Ordinal) ? still + 1 : 0;
            if (still >= ScrollUntilStillPages)
            {
                throw new TestException(
                    "LOCATOR_NOT_FOUND",
                    "Nothing reading \"" + text + "\" came into view before the screen stopped moving " + DirectionName(direction) + ", after " + screens.ToString(CultureInfo.InvariantCulture) + " screens.");
            }

            if (still >= ScrollUntilWrongListPages && within is not null)
            {
                list = null;
                still = 0;
            }

            previous = shape;
            var turned = await PageAsync(direction, list, token).ConfigureAwait(false);
            paged = turned.Paged;
            list = turned.Within;
        }
    }

    // One page of a scroll to a text: on the re-found list, else on the viewport, which also takes over for good once the
    // list cannot be re-found. What was paged goes into the record, and the list to page next (upstream's page).
    private async Task<((SemanticNode? Node, double? Spans) Paged, ExecutorTarget? Within)> PageAsync(ScrollDirection direction, ExecutorTarget? list, CancellationToken token)
    {
        var resolved = list is null ? null : ScrollTargetOrLost(list);
        if (resolved is null)
        {
            await _scope.Session.SwipeAsync(direction, token).ConfigureAwait(false);
            return ((null, null), null);
        }

        await _scope.Session.PerformAsync(resolved.Node, new LocatorAction.Swipe(direction), token).ConfigureAwait(false);
        return ((resolved.Node, SpansOf(resolved.Node, resolved.Observation.Viewport)), list);
    }

    // The list a scroll to a text pages, or null once neither the newest screen nor its old place has it.
    private Resolved? ScrollTargetOrLost(ExecutorTarget list)
    {
        try
        {
            return ScrollTarget.ResolveScrollTarget(_feed, list);
        }
        catch (TestException exception) when (exception.Code == "LOCATOR_NOT_FOUND")
        {
            return null;
        }
    }

    // The share of the viewport a list covered, or null when the engine measured no box or no viewport.
    private static double? SpansOf(SemanticNode? list, TraceViewport? viewport) =>
        list?.Rect is { } box && viewport is not null ? Geometry.ViewportShare(box, viewport) : null;

    private static SemanticNode? Single(List<SemanticNode> matches)
    {
        return matches.Count == 1 ? matches[0] : null;
    }

    private void RecordScroll(ScrollDirection direction, SemanticNode? list, TraceViewport? viewport)
    {
        var scroll = list is null ? new RecordedAction { Kind = "scroll" } : Target(list, "scroll");
        scroll.Direction = DirectionName(direction);
        // How much of the screen the list covered decides, on replay, whether a list that cannot be re-found scrolls as the viewport.
        scroll.Spans = SpansOf(list, viewport);
        _accounting.Recorder.RecordScroll(scroll);
    }

    // The node as the target an action names it by, not yet recorded.
    private RecordedAction Target(SemanticNode node, string kind)
    {
        var target = TraceRecorder.DescribeTarget(node, _scope.Redactor);
        return new RecordedAction { Kind = kind, Role = target.Role, Name = target.Name, TestId = target.TestId };
    }

    // Upstream's commitTargeted: the action is the step's own once it ran, so it is recorded and arms the next look.
    private Task CommitAsync(string kind, SemanticNode node, string? value = null, string? key = null) =>
        ArmAndNotifyAsync(_accounting.Recorder.Record(kind, node, value, key));

    private Task CommitAsync(RecordedAction action) => ArmAndNotifyAsync(_accounting.Recorder.Record(action));

    // A recorded action arms the next look, then goes to the test's hook.
    private Task ArmAndNotifyAsync(RecordedAction recorded)
    {
        _feed.ArmAfter(recorded);
        return NotifyAsync(recorded, ActionSource.Model);
    }

    /// <summary>
    /// Upstream's <c>notify</c>: hands one completed action to the test's <c>OnAction</c> hook once the screen it left settles as
    /// the action's policy asks, so what the hook captures is the action's effect. The action already happened, so a failure
    /// from here on, the settle's or the hook's, hard-stops the step instead of reaching the executor as a failed action to
    /// retry, or the replay as a divergence.
    /// </summary>
    internal Task NotifyAsync(RecordedAction action, ActionSource source) =>
        NotifyAsync(action.Kind, SettlePolicy.For(action).Look, () => DefaultAgent.Describe(action), source);

    /// <summary>A mutating project tool that ran: the hook reads it as <c>tool:name</c>.</summary>
    internal Task NotifyToolAsync(string name) =>
        NotifyAsync(ActionNames.ProjectToolEventPrefix + name, SettlePolicy.For(new RecordedAction { Kind = "tool" }).Look, () => "ran " + name, ActionSource.Model);

    private async Task NotifyAsync(string action, SettleMode look, Func<string> summary, ActionSource source)
    {
        if (_onAction is not { } hook || _accounting.Closed)
        {
            return;
        }

        try
        {
            await _feed.ObserveAsync(look, _accounting.Token).ConfigureAwait(false);
            await hook(new ActionEvent(_accounting.Used, action, _scope.Redactor.Redact(summary()), source)).WaitAsync(_accounting.Token).ConfigureAwait(false);
        }
        catch (Exception cause) when (!ReferenceEquals(cause, _accounting.HardStop) && !_accounting.Token.IsCancellationRequested)
        {
            // A nested step's own budget or deadline is not this step's: the runtime vouches only for the stops it recorded itself.
            var code = cause is E2EException { Code: var named } && StepExecutors.AgentCodes.Contains(named) && !StepExecutors.RuntimeCodes.Contains(named) ? named : "ACTION_FAILED";
            throw _accounting.Fatalize(new AgentException(code, _accounting.Api + " onAction failed: " + _scope.Redactor.Redact(cause.Message), cause) { FromActionHook = true });
        }
    }

    /// <summary>
    /// Upstream's <c>targeted</c> loop: performs one action on a node. A node the engine reports stale is found again
    /// by its description in a fresh look (<see cref="ObservationFeed.Relocate"/>) and the action retried,
    /// <see cref="MaxStaleRelocations"/> times: a list that remounts its rows between the look and the action keeps the
    /// control on screen under a dead ref. A description that matches nothing, or several nodes, fails the action.
    /// </summary>
    private async Task TargetedAsync(SemanticNode node, LocatorAction action, CancellationToken token)
    {
        for (var relocations = 0; ; relocations++)
        {
            try
            {
                await _scope.Session.PerformAsync(node, action, token).ConfigureAwait(false);
                return;
            }
            catch (EngineException ex) when (ex.Code == EngineErrorCodes.NodeStale)
            {
                var relocated = relocations < MaxStaleRelocations ? await _feed.Relocate(node, _scope.Redactor, token).ConfigureAwait(false) : null;
                if (relocated is null)
                {
                    throw new TestException("LOCATOR_NOT_FOUND", "The target node left the screen before the action reached it.", ex);
                }

                node = relocated;
            }
        }
    }
}
