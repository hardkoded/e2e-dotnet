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
/// asks for. Upstream's <c>commitTargeted</c> is <see cref="Commit"/>, and its private <c>targeted</c> is
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

    // secrets: what TypeSecretAsync may fill, the secrets the step's params declare.
    public ActionDispatcher(AttemptScope scope, ObservationFeed feed, StepAccounting accounting, IReadOnlyDictionary<string, object?>? parameters, Func<IReadOnlyList<Secret>> secrets)
    {
        _scope = scope;
        _feed = feed;
        _accounting = accounting;
        _parameters = parameters;
        _secrets = secrets;
    }

    public Task TapAsync(ExecutorTarget target, CancellationToken cancellationToken = default) =>
        RunAsync(
            async token =>
            {
                var node = Resolve(target);
                await TargetedAsync(node, new LocatorAction.Tap(), token).ConfigureAwait(false);
                Commit(Record(node, "tap"));
            },
            cancellationToken);

    public Task DoubleTapAsync(ExecutorTarget target, CancellationToken cancellationToken = default) =>
        RunAsync(
            async token =>
            {
                var node = Resolve(target);
                await TargetedAsync(node, new LocatorAction.DoubleTap(), token).ConfigureAwait(false);
                Commit(Record(node, "doubleTap"));
            },
            cancellationToken);

    public Task TypeAsync(ExecutorTarget target, string value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        return RunAsync(
            async token =>
            {
                var node = Resolve(target);
                await TargetedAsync(node, new LocatorAction.Fill(value, false), token).ConfigureAwait(false);
                Commit(new RecordedAction
                {
                    Kind = "fill",
                    Role = node.Role,
                    Name = RedactLabel(node, _scope.Redactor),
                    TestId = Redact(node.TestId, _scope.Redactor),
                    Value = CacheKeys.Template(value, _parameters),
                });
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
            async token =>
            {
                var node = Resolve(target);
                AuthorizeSecretFill(secret, node);
                // Before the engine call: a fill that types the value and then fails has still put it on screen.
                _scope.Exposure.RaiseFilled();
                await TargetedAsync(node, new LocatorAction.Fill(secret.Value, true), token).ConfigureAwait(false);
                // Recorded by stable name only; replay runs the full authorization again.
                Commit(new RecordedAction
                {
                    Kind = "fill",
                    Role = node.Role,
                    Name = RedactLabel(node, _scope.Redactor),
                    TestId = Redact(node.TestId, _scope.Redactor),
                    Value = "<secret:" + secret.Name + ">",
                });
            },
            cancellationToken).ConfigureAwait(false);
    }

    public Task ClearAsync(ExecutorTarget target, CancellationToken cancellationToken = default) =>
        RunAsync(
            async token =>
            {
                var node = Resolve(target);
                await TargetedAsync(node, new LocatorAction.Clear(), token).ConfigureAwait(false);
                Commit(Record(node, "clear"));
            },
            cancellationToken);

    public Task PressAsync(ExecutorTarget? target, string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return RunAsync(
            async token =>
            {
                if (target is null)
                {
                    await _scope.Session.PressAsync(key, token).ConfigureAwait(false);
                    Commit(new RecordedAction { Kind = "press", Key = key });
                    return;
                }

                var node = Resolve(target);
                await TargetedAsync(node, new LocatorAction.Press(key), token).ConfigureAwait(false);
                Commit(new RecordedAction { Kind = "press", Role = node.Role, Name = RedactLabel(node, _scope.Redactor), TestId = Redact(node.TestId, _scope.Redactor), Key = key });
            },
            cancellationToken);
    }

    public Task SelectAsync(ExecutorTarget target, string value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        return RunAsync(
            async token =>
            {
                var node = Resolve(target);
                await TargetedAsync(node, new LocatorAction.Select(value), token).ConfigureAwait(false);
                Commit(new RecordedAction { Kind = "select", Role = node.Role, Name = RedactLabel(node, _scope.Redactor), TestId = Redact(node.TestId, _scope.Redactor), Value = CacheKeys.Template(value, _parameters) });
            },
            cancellationToken);
    }

    public Task CheckAsync(ExecutorTarget target, bool check, CancellationToken cancellationToken = default) =>
        RunAsync(
            async token =>
            {
                var node = Resolve(target);
                await TargetedAsync(node, check ? new LocatorAction.Check() : new LocatorAction.Uncheck(), token).ConfigureAwait(false);
                Commit(Record(node, check ? "check" : "uncheck"));
            },
            cancellationToken);

    public Task ScrollToAsync(ExecutorTarget target, CancellationToken cancellationToken = default) =>
        RunAsync(
            async token =>
            {
                var node = Resolve(target);
                await _scope.Session.PerformAsync(node, new LocatorAction.ScrollIntoView(), token).ConfigureAwait(false);
                Commit(Record(node, "scrollTo"));
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
            async token =>
            {
                var recorded = list is null ? null : Record(Resolve(list), "scrollUntil");
                await PageUntilAsync(text, direction, recorded, token).ConfigureAwait(false);
                Commit(new RecordedAction
                {
                    Kind = "scrollUntil",
                    Role = recorded?.Role,
                    Name = recorded?.Name,
                    TestId = recorded?.TestId,
                    Direction = DirectionName(direction),
                    Text = CacheKeys.Template(text, _parameters),
                });
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
            async token =>
            {
                var actions = _accounting.Recorded;
                var node = list is null ? null : Resolve(list);
                var recorded = node is null ? null : Record(node, "scroll");
                for (var repeat = 0; repeat < times; repeat++)
                {
                    if (repeat > 0)
                    {
                        // Each later repeat reads a settled screen first, and pages the list as that screen shows it.
                        _feed.ArmAfter(actions[^1]);
                        var look = await _feed.ObserveAsync(SettleMode.HeldStill, token).ConfigureAwait(false);
                        if (recorded is not null)
                        {
                            node = Single(Find(look, recorded.Role, recorded.Name, recorded.TestId, null, _scope.Redactor))
                                ?? throw new TestException("NOT_FOUND", "The scrolled list is no longer on the screen.");
                        }
                    }

                    if (node is null)
                    {
                        await _scope.Session.SwipeAsync(direction, token).ConfigureAwait(false);
                    }
                    else
                    {
                        await _scope.Session.PerformAsync(node, new LocatorAction.Swipe(direction), token).ConfigureAwait(false);
                    }

                    RecordScroll(actions, direction, node);
                }

                if (list is not null && node is not null)
                {
                    list.Node = node;
                }

                _feed.ArmAfter(actions[^1]);
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
            async token =>
            {
                await _scope.Session.OpenAsync(Routes.Resolve(_scope.BaseUrl, url), token).ConfigureAwait(false);
                Commit(new RecordedAction { Kind = "navigate", Url = CacheKeys.Template(url, _parameters) });
            },
            cancellationToken);
    }

    public Task BackAsync(CancellationToken cancellationToken = default) =>
        RunAsync(
            async token =>
            {
                await _scope.Session.BackAsync(token).ConfigureAwait(false);
                Commit(new RecordedAction { Kind = "back" });
            },
            cancellationToken);

    /// <summary>
    /// Runs one grammar action against the action budget: the slot is taken before anything else, since a failed action was
    /// still an attempt. A failure of an action that ran past the step's end is the step's own hard stop, not the action's.
    /// </summary>
    private async Task RunAsync(Func<CancellationToken, Task> body, CancellationToken cancellationToken)
    {
        _accounting.Reserve();
        try
        {
            await body(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (CheckStep())
        {
            throw;
        }
    }

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
            ?? (target.Node = _feed.LastSeen(target.Id))
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
    internal async Task PageUntilAsync(string text, ScrollDirection direction, RecordedAction? list, CancellationToken token)
    {
        string? previous = null;
        var still = 0;
        for (var screens = 0; ; screens++)
        {
            // A settled look after each page: a browser scrolls on a later frame than the wheel event, and a windowed
            // list renders its rows on a later scroll event, so a raw read right after the swipe sees the page as it was.
            var observation = await _feed.ObserveAsync(screens == 0 ? SettleMode.Raw : SettleMode.HeldStill, token).ConfigureAwait(false);
            if (screens == 0)
            {
                _feed.MarkActing();
            }

            var within = list is null ? null : Single(Find(observation, list.Role, list.Name, list.TestId, null, _scope.Redactor));
            var found = Reading(within is null ? observation.Roots : within.Children, text, _scope.Redactor);
            if (found is not null)
            {
                await _scope.Session.PerformAsync(found, new LocatorAction.ScrollIntoView(), token).ConfigureAwait(false);
                return;
            }

            if (screens >= MaxScrollUntilScreens)
            {
                throw new TestException("LOCATOR_NOT_FOUND", "Nothing reading \"" + text + "\" came into view within " + MaxScrollUntilScreens.ToString(CultureInfo.InvariantCulture) + " screens.");
            }

            // The tree can stay the same while the page moves under it (every node
            // already fits the budget), so the scroll position counts too.
            var shape = SnapshotText.Render(observation, Redactor.None) + "\n" + observation.ScrollPosition;
            still = string.Equals(shape, previous, StringComparison.Ordinal) ? still + 1 : 0;
            if (still >= ScrollUntilStillPages)
            {
                throw new TestException(
                    "LOCATOR_NOT_FOUND",
                    "Nothing reading \"" + text + "\" came into view before the screen stopped moving " + DirectionName(direction) + ", after " + screens.ToString(CultureInfo.InvariantCulture) + " screens.");
            }

            if (still >= ScrollUntilWrongListPages && within is not null)
            {
                within = null;
                still = 0;
            }

            previous = shape;
            if (within is null)
            {
                // A list that is gone, or that does not move, gives the paging to the viewport for good.
                list = null;
                await _scope.Session.SwipeAsync(direction, token).ConfigureAwait(false);
            }
            else
            {
                await _scope.Session.PerformAsync(within, new LocatorAction.Swipe(direction), token).ConfigureAwait(false);
            }
        }
    }

    // The innermost visible node whose redacted name or text contains the text.
    private static SemanticNode? Reading(IReadOnlyList<SemanticNode> nodes, string text, Redactor redactor)
    {
        foreach (var node in nodes)
        {
            if (node.States.Hidden || node.States.AriaHidden)
            {
                continue;
            }

            var inner = Reading(node.Children, text, redactor);
            if (inner is not null)
            {
                return inner;
            }

            if (TextRules.Matches(RedactField(node.Name, ObservationLimits.Name, redactor), text, exact: false) || TextRules.Matches(RedactField(node.Text, ObservationLimits.Text, redactor), text, exact: false))
            {
                return node;
            }
        }

        return null;
    }

    private static SemanticNode? Single(List<SemanticNode> matches)
    {
        return matches.Count == 1 ? matches[0] : null;
    }

    // Consecutive identical scrolls fold into one recorded action with a repeat count.
    private void RecordScroll(List<RecordedAction> actions, ScrollDirection direction, SemanticNode? list)
    {
        var way = DirectionName(direction);
        var scroll = list is null ? new RecordedAction { Kind = "scroll" } : Record(list, "scroll");
        if (actions.Count > 0
            && actions[^1] is { Kind: "scroll" } last
            && string.Equals(last.Direction, way, StringComparison.Ordinal)
            && string.Equals(last.Role, scroll.Role, StringComparison.Ordinal)
            && string.Equals(last.Name, scroll.Name, StringComparison.Ordinal)
            && string.Equals(last.TestId, scroll.TestId, StringComparison.Ordinal))
        {
            last.Times = (last.Times ?? 1) + 1;
            return;
        }

        scroll.Direction = way;
        actions.Add(scroll);
    }

    private RecordedAction Record(SemanticNode node, string kind)
    {
        return new RecordedAction { Kind = kind, Role = node.Role, Name = RedactLabel(node, _scope.Redactor), TestId = Redact(node.TestId, _scope.Redactor) };
    }

    // Upstream's commitTargeted: the action is the step's own once it ran, so it is recorded and arms the next look.
    private void Commit(RecordedAction action)
    {
        _accounting.Recorded.Add(action);
        _feed.ArmAfter(action);
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
