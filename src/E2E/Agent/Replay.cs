// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Engine;
using E2E.Internal;
using static E2E.Agent;
using RelocateRules = E2E.Relocate;

namespace E2E;

/// <summary>
/// Upstream's <c>ReplayHost</c> (<c>agent/replay.ts</c>): what the replay engine needs from the act, and nothing more. The
/// members after <see cref="Redact"/> have no upstream counterpart. They carry what the port's act does around a replayed
/// action (its action slots, its recording), where upstream's dispatcher does it inside the
/// actions it hands the replay.
/// </summary>
internal interface IReplayHost
{
    /// <summary>
    /// One capture, settled as far as <paramref name="mode"/> asks: the act's own, so a replayed action never lands on a
    /// screen still reacting to the previous one.
    /// </summary>
    Task<Observation> ObserveAsync(SettleMode mode);

    /// <summary>The step's grammar. Targets are nodes of the newest observation.</summary>
    IExecutorActions Actions { get; }

    CancellationToken Signal { get; }

    Redactor Redact { get; }

    /// <summary>
    /// How long a replayed action waits for its target to show, and a look at the recorded end state before the recording's own wait
    /// starts (upstream's <c>RETRY_TIMEOUT_MS</c>, 15 s). Upstream backs off inside the step's clock.
    /// </summary>
    TimeSpan ReplayTimeout { get; }

    /// <summary>Upstream's <c>remainingMs</c>: the milliseconds left of the step's deadline.</summary>
    int RemainingMs();

    /// <summary>The clock the end wait reads and sleeps on. No upstream counterpart: upstream reads <c>Date.now()</c> and tests fake the timers.</summary>
    TimeProvider Time { get; }

    /// <summary>
    /// Takes one action slot. False when the step's budget is spent, and the replay hands the step over: upstream's dispatcher
    /// throws <c>STEP_BUDGET_EXHAUSTED</c> from the action instead.
    /// </summary>
    bool TryReserve();

    /// <summary>Marks the newest observation as the one the step's first action is taken on.</summary>
    void MarkActing();

    /// <summary>Records one action the replay finished, for the step's cache entry, and hands it to the test's <c>OnAction</c> hook.</summary>
    Task CommitAsync(RecordedAction action);
}

/// <summary>Upstream's <c>ReplayOptions</c>.</summary>
internal sealed class ReplayOptions
{
    /// <summary>
    /// The step's settled start capture. The first action's look reads it instead of capturing again: nothing has happened
    /// since it was taken.
    /// </summary>
    public Observation? Initial { get; init; }

    /// <summary>
    /// Upstream's <c>looksBeforeFree</c>: whether a free action takes a look first. A free action reads no screen, so without it the screen the previous
    /// action left goes unseen.
    /// </summary>
    public Func<bool>? LooksBeforeFree { get; init; }
}

/// <summary>Upstream's options of <c>verifyEndState</c>.</summary>
internal sealed class VerifyEndStateOptions
{
    /// <summary>How long the recording needed to reach its end state (<see cref="CacheEntry.EndWaitMs"/>). Null waits for nothing beyond the settling backoff.</summary>
    public int? WaitMs { get; init; }

    /// <summary>The end screen already in hand, which nothing has happened to since it was captured: the first look reads it instead of capturing again.</summary>
    public Observation? Initial { get; init; }
}

/// <summary>Upstream's <c>ReplayOutcome</c>.</summary>
internal sealed class ReplayOutcome
{
    /// <summary>True when every recorded action executed; the step self-finalizes.</summary>
    public required bool Completed { get; init; }

    /// <summary>The actions that ran. A folded scroll that ran only some of its repeats counts as one.</summary>
    public required int Executed { get; init; }

    public required int Total { get; init; }

    /// <summary>Prose summaries of the executed actions, for the hand-off notice.</summary>
    public required IReadOnlyList<string> Summaries { get; init; }

    /// <summary>Present exactly when <see cref="Completed"/> is false.</summary>
    public ReplayHandOffReason? StopReason { get; init; }

    /// <summary>The action whose commit state is unknown, on <see cref="ReplayHandOffReason.ActionUncertain"/> only.</summary>
    public string? UncertainAction { get; init; }

    /// <summary>
    /// The redacted first line of the error that stopped the replay, on <see cref="ReplayHandOffReason.ActionFailed"/> and
    /// <see cref="ReplayHandOffReason.ActionUncertain"/>. Upstream's report does not name it; the port's cache detail does.
    /// </summary>
    public string? Detail { get; init; }
}

/// <summary>
/// Upstream's <c>agent/replay.ts</c>: zero-turn trace replay. Replays one recorded trace through the step's action grammar, with
/// no model call. Adaptive, never fatal: any reason the trace cannot finish, a gap, a target that moved, an action the live app
/// rejected, ends the replayed prefix and hands the step to the executor mid-step. Runtime hard stops rethrow untouched; they
/// are the step's truth, not a divergence.
/// </summary>
internal static class Replay
{
    /// <summary>The wait between two looks for a target, where upstream backs off on a list of delays.</summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(20);

    /// <summary>
    /// The screen a look starts from: one already in hand, which nothing has happened to since it was captured, or a fresh
    /// capture settled as far as the previous action requires.
    /// </summary>
    private readonly record struct Look(Observation? InHand, SettleMode Settle);

    /// <summary>A fresh look that proves the screen holds still.</summary>
    private static readonly Look HeldStill = new(null, SettleMode.HeldStill);

    /// <summary>Replays one trace until it completes or diverges.</summary>
    public static async Task<ReplayOutcome> ReplayTrace(IReplayHost host, IReadOnlyList<RecordedAction> trace, ReplayOptions? options = null)
    {
        var summaries = new List<string>();
        var total = trace.Count;
        var token = host.Signal;

        ReplayOutcome Stop(ReplayHandOffReason stopReason, string? partial = null, string? detail = null, string? uncertainAction = null)
        {
            if (partial is not null)
            {
                summaries.Add(partial);
            }

            return new ReplayOutcome { Completed = false, Executed = summaries.Count, Total = total, Summaries = summaries, StopReason = stopReason, Detail = detail, UncertainAction = uncertainAction };
        }

        RecordedAction? previous = null;
        foreach (var action in trace)
        {
            // A project tool ran here, and the grammar cannot reproduce it: the replay ends and the model takes over.
            if (string.Equals(action.Kind, "tool", StringComparison.Ordinal))
            {
                return Stop(ReplayHandOffReason.Gap);
            }

            // The look before this action: the start capture serves the first one; after that, the previous action's settle
            // policy says how far a fresh capture settles.
            var look = previous is not null ? new Look(null, SettlePolicy.For(previous).Look) : options?.Initial is { } initial ? new Look(initial, SettleMode.Raw) : HeldStill;

            // An action with no target reads no screen, so the screen the previous action left would go unseen.
            if (previous is not null && !HasTarget(action) && !IsPointAction(action) && options?.LooksBeforeFree?.Invoke() == true)
            {
                await host.ObserveAsync(look.Settle).ConfigureAwait(false);
            }

            var summary = DefaultAgent.Describe(action);

            // A replayed action draws on the step's action budget like a live one. The first always fits.
            // An action with a target takes its slot once the target is found, so a lost target is a miss.
            if ((action.Kind is "back" or "resize" or "scroll" or "scrollUntil" or "press" or "typeText" or "pressKey" or "dismissKeyboard") && !HasTarget(action) && !host.TryReserve())
            {
                return Stop(ReplayHandOffReason.ActionBudget);
            }

            // Repeats of a folded scroll done before it failed moved the screen: the outcome counts them as executed. The host
            // commits whole actions only, so the act's recording and hand-off prefix leave the partial scroll out.
            var repeated = 0;
            string? Partial() => repeated == 0 ? null : summary + " (" + repeated.ToString(CultureInfo.InvariantCulture) + " of " + (action.Times ?? 1).ToString(CultureInfo.InvariantCulture) + " repeats)";
            try
            {
                // back, a viewport scroll, and a key press on the focused control target nothing, so they replay as given.
                // A scroll on a list re-finds the list before each repeat.
                switch (action.Kind)
                {
                    case "navigate":
                        if (!host.TryReserve())
                        {
                            return Stop(ReplayHandOffReason.ActionBudget);
                        }

                        await host.Actions.NavigateAsync(action.Url ?? "/", token).ConfigureAwait(false);
                        break;
                    case "back":
                        await host.Actions.BackAsync(token).ConfigureAwait(false);
                        break;
                    case "resize":
                        await host.Actions.ResizeAsync(action.Size!, token).ConfigureAwait(false);
                        break;
                    case "press" when !HasTarget(action):
                        // A recording of the key with no node from before pressKey, which replays as pressKey does.
                        await host.Actions.PressAsync(null, action.Key ?? "Enter", token).ConfigureAwait(false);
                        break;
                    case "typeText":
                        // Keyboard input to whatever held focus, replayed as given: the focus it relies on is the effect of the recorded
                        // actions before it, and the end anchors decide whether it landed.
                        await host.Actions.TypeTextAsync(action.Value ?? "", new TypeTextOptions { Replace = action.Replace == true }, token).ConfigureAwait(false);
                        break;
                    case "pressKey":
                        await host.Actions.PressKeyAsync(action.Key ?? "Enter", token).ConfigureAwait(false);
                        break;
                    case "dismissKeyboard":
                        await host.Actions.DismissKeyboardAsync(token).ConfigureAwait(false);
                        break;
                    case "scroll" or "scrollUntil" when !TryDirection(action.Direction, out _):
                        return Stop(ReplayHandOffReason.InvalidEntry);
                    case "scroll" when !HasTarget(action):
                        // A viewport scroll relocates nothing, so each later repeat takes a settled look of its own, as the
                        // live loop did between them.
                        for (var index = 0; index < (action.Times ?? 1); index++)
                        {
                            if (index > 0)
                            {
                                await host.ObserveAsync(SettleMode.HeldStill).ConfigureAwait(false);
                            }

                            await host.Actions.ScrollAsync(null, Direction(action.Direction), 1, token).ConfigureAwait(false);
                            repeated++;
                        }

                        break;
                    case "scroll":
                        // A scroll on a list is paced by the relocation before each repeat. A list that filled the screen when recorded
                        // and cannot be re-found scrolls as the viewport, which is what scrolling the main list does, for that repeat and
                        // every later one, instead of waiting out the relocation again on every repeat; a smaller region hands off.
                        var onList = true;
                        for (var index = 0; index < (action.Times ?? 1); index++)
                        {
                            SemanticNode? scrolledList = null;
                            if (onList)
                            {
                                var list = await Relocate(host, action, index == 0 ? look : HeldStill).ConfigureAwait(false);
                                if (list.Node is null)
                                {
                                    if (!FilledTheScreen(action))
                                    {
                                        return Stop(list.Failure, Partial());
                                    }

                                    onList = false;
                                }

                                scrolledList = list.Node;
                            }
                            else if (index > 0)
                            {
                                // A viewport scroll relocates nothing, so each later repeat takes a settled look of its own, as the
                                // live loop did between them.
                                await host.ObserveAsync(SettleMode.HeldStill).ConfigureAwait(false);
                            }

                            // One slot for the whole scroll, taken once its first repeat has a place to land.
                            if (index == 0 && !host.TryReserve())
                            {
                                return Stop(ReplayHandOffReason.ActionBudget);
                            }

                            await host.Actions.ScrollAsync(scrolledList is null ? null : new ExecutorTarget(scrolledList), Direction(action.Direction), 1, token).ConfigureAwait(false);
                            repeated++;
                        }

                        break;
                    case "scrollUntil":
                        var text = action.Text ?? "";
                        if (!HasTarget(action))
                        {
                            await host.Actions.ScrollUntilAsync(text, Direction(action.Direction), null, token).ConfigureAwait(false);
                            break;
                        }

                        // A list that filled the screen and cannot be re-found is paged as the viewport; a smaller region hands off.
                        var scrolled = await Relocate(host, action, look).ConfigureAwait(false);
                        if (scrolled.Node is null && !FilledTheScreen(action))
                        {
                            return Stop(scrolled.Failure);
                        }

                        if (!host.TryReserve())
                        {
                            return Stop(ReplayHandOffReason.ActionBudget);
                        }

                        await host.Actions.ScrollUntilAsync(text, Direction(action.Direction), scrolled.Node is null ? null : new ExecutorTarget(scrolled.Node) { Recorded = action }, token).ConfigureAwait(false);
                        break;
                    case "tapAt" or "hoverAt":
                        // A bare point, placed the way PlacePoint places one, then acted on through its point verb.
                        var placed = await PlacePoint(host, action.Point!.Value, action.Under, action.Viewport!, look).ConfigureAwait(false);
                        if (placed.Failure is { } placedFailure)
                        {
                            return Stop(placedFailure, null, placed.Why);
                        }

                        if (!host.TryReserve())
                        {
                            return Stop(ReplayHandOffReason.ActionBudget);
                        }

                        await (action.Kind == "tapAt" ? host.Actions.TapAtAsync(placed.Point, token) : host.Actions.HoverAtAsync(placed.Point, token)).ConfigureAwait(false);
                        break;
                    case "drag":
                        // Both ends are re-found on one screen, so the nodes the drag joins come from the same look at the app.
                        var pair = await RelocatePair(host, action, look).ConfigureAwait(false);
                        if (pair.Source is null || pair.Destination is null)
                        {
                            return Stop(pair.Failure);
                        }

                        if (!host.TryReserve())
                        {
                            return Stop(ReplayHandOffReason.ActionBudget);
                        }

                        await host.Actions.DragAsync(new ExecutorTarget(pair.Source), new ExecutorTarget(pair.Destination), token).ConfigureAwait(false);
                        break;
                    case "dragAt":
                        // Points, not ids, reach the drag, so each end may take its own look: nothing acts between the two, and a renumbered
                        // tree moves no box.
                        var start = await PlacePoint(host, action.From!.Point, action.From.Under, action.Viewport!, look).ConfigureAwait(false);
                        if (start.Failure is { } startFailure)
                        {
                            return Stop(startFailure, null, start.Why);
                        }

                        var end = await PlacePoint(host, action.To!.Point, action.To.Under, action.Viewport!, look).ConfigureAwait(false);
                        if (end.Failure is { } endFailure)
                        {
                            return Stop(endFailure, null, end.Why);
                        }

                        if (!host.TryReserve())
                        {
                            return Stop(ReplayHandOffReason.ActionBudget);
                        }

                        await host.Actions.DragAtAsync(start.Point, end.Point, token).ConfigureAwait(false);
                        break;
                    default:
                        var found = await Relocate(host, action, look).ConfigureAwait(false);
                        if (found.Node is null)
                        {
                            return Stop(found.Failure);
                        }

                        if (!host.TryReserve())
                        {
                            return Stop(ReplayHandOffReason.ActionBudget);
                        }

                        await PerformAsync(host.Actions, new ExecutorTarget(found.Node), action, token).ConfigureAwait(false);
                        break;
                }
            }
            catch (EngineException ex) when (ex.Code == EngineErrorCodes.ActionMayHaveCommitted && !token.IsCancellationRequested)
            {
                // The action's input may have reached the app. The executor verifies the state before it tries anything like it again.
                return Stop(ReplayHandOffReason.ActionUncertain, Partial(), host.Redact.Redact(FirstLine(ex.Message)), summary);
            }
            catch (E2EException ex) when (!StepExecutors.IsRuntimeHardStop(ex) && !token.IsCancellationRequested)
            {
                // A step or test timeout cancels the token. That is the step's own end, not a stale recording.
                return Stop(ReplayHandOffReason.ActionFailed, Partial(), host.Redact.Redact(FirstLine(ex.Message)));
            }

            await host.CommitAsync(action).ConfigureAwait(false);
            summaries.Add(summary);
            previous = action;
        }

        return new ReplayOutcome { Completed = true, Executed = summaries.Count, Total = total, Summaries = summaries };
    }

    /// <summary>Whether the list a recorded scroll moved filled the screen (<see cref="CacheKeys.MainListShare"/>), so it scrolls as the viewport once it is lost.</summary>
    private static bool FilledTheScreen(RecordedAction action) => (action.Spans ?? 0) >= CacheKeys.MainListShare;

    /// <summary>Poll cadence while a replay waits for the recorded end state beyond the settling backoff.</summary>
    private static readonly TimeSpan EndWaitPoll = TimeSpan.FromSeconds(1);

    /// <summary>The step clock kept back from that wait, so a hand-off still has room to act.</summary>
    private const int EndWaitReserveMs = 20_000;

    /// <summary>Backoff between looks at the screen while it settles.</summary>
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(600), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3)];

    /// <summary>
    /// Waits for a trace's recorded end state on the live screen: <paramref name="holds"/> (the recorded delta, <c>Anchors.Mismatch</c>)
    /// must be true of one look, or the replay must not pass on its own. Waits on the settling backoff first, because the recording
    /// run's final look came seconds of model latency after its last action and a replay's comes right away: a save still in flight is
    /// a wait, not a divergence. Then it waits up to what the recording needed (<see cref="VerifyEndStateOptions.WaitMs"/>), while the
    /// step clock leaves room for a hand-off to act. A surface that cannot be observed at all is a mismatch too: the executor gets
    /// the step and judges the live state; only runtime hard stops propagate.
    /// </summary>
    public static async Task<bool> VerifyEndState(IReplayHost host, Func<Observation, bool> holds, VerifyEndStateOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(holds);
        var started = host.Time.GetUtcNow();
        try
        {
            if (await PollSettled(host, holds, options?.Initial is { } initial ? new Look(initial, SettleMode.Raw) : HeldStill).ConfigureAwait(false))
            {
                return true;
            }

            // The settling backoff covers a slow re-render; the recorded run may have waited far longer than that for its effect, a
            // report that takes half a minute, and so does the replay, up to what the recording needed.
            var deadline = started + TimeSpan.FromMilliseconds(Math.Min(options?.WaitMs ?? 0, Math.Max(0, host.RemainingMs() - EndWaitReserveMs)));
            while (true)
            {
                var pause = deadline - host.Time.GetUtcNow();
                if (pause <= TimeSpan.Zero)
                {
                    return false;
                }

                await Task.Delay(pause < EndWaitPoll ? pause : EndWaitPoll, host.Time, host.Signal).ConfigureAwait(false);
                if (holds(await host.ObserveAsync(SettleMode.Raw).ConfigureAwait(false)))
                {
                    return true;
                }
            }
        }
        catch (E2EException ex) when (!StepExecutors.IsRuntimeHardStop(ex) && !host.Signal.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>
    /// Upstream's <c>pollSettled</c> for a yes or no answer: probes the first look, then re-probes fresh raw captures on a fixed
    /// backoff until the probe holds or the wait runs out, so a screen mid-transition gets a few looks before replay gives the step up.
    /// A screen the caller already settled (the step's start capture) is read as it is.
    /// </summary>
    private static async Task<bool> PollSettled(IReplayHost host, Func<Observation, bool> probe, Look look)
    {
        var started = host.Time.GetUtcNow();
        var screen = look.InHand ?? await host.ObserveAsync(look.Settle).ConfigureAwait(false);
        for (var attempt = 0; ; attempt++)
        {
            if (probe(screen))
            {
                return true;
            }

            // The backoff's last delay repeats until the wait runs out: the list shapes the first looks, the timeout bounds them.
            var delay = RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)];
            if (host.Time.GetUtcNow() - started + delay > host.ReplayTimeout || host.RemainingMs() <= delay.TotalMilliseconds)
            {
                return false;
            }

            await Task.Delay(delay, host.Time, host.Signal).ConfigureAwait(false);
            screen = await host.ObserveAsync(SettleMode.Raw).ConfigureAwait(false);
        }
    }

    /// <summary>One recorded action on a node, through the grammar call of its kind.</summary>
    private static Task PerformAsync(IExecutorActions actions, ExecutorTarget target, RecordedAction action, CancellationToken token)
    {
        switch (action.Kind)
        {
            case "tap":
                return actions.TapAsync(target, ClickOptionsOf(action), token);
            case "doubleTap":
                return actions.DoubleTapAsync(target, ClickOptionsOf(action), token);
            case "secondaryTap":
                return actions.SecondaryTapAsync(target, ClickOptionsOf(action), token);
            case "longPress":
                return actions.LongPressAsync(target, token);
            case "hover":
                return actions.HoverAsync(target, token);
            case "fill" when action.Value is { } value && value.StartsWith("<secret:", StringComparison.Ordinal) && value.EndsWith('>'):
                return actions.TypeSecretAsync(target, value["<secret:".Length..^1], token);
            case "fill":
                return actions.TypeAsync(target, action.Value ?? "", token);
            case "press":
                return actions.PressAsync(target, action.Key ?? "Enter", token);
            case "select":
                return actions.SelectAsync(target, action.Value ?? "", token);
            case "check":
                return actions.CheckAsync(target, true, token);
            case "uncheck":
                return actions.CheckAsync(target, false, token);
            case "clear":
                return actions.ClearAsync(target, token);
            case "upload" when action.Paths is { } paths:
                return actions.UploadAsync(target, paths, token);
            case "scrollTo":
                return actions.ScrollToAsync(target, token);
            default:
                throw new AgentException("AUTOMATION_UNSUPPORTED", "Cannot replay " + action.Kind + ".");
        }
    }

    // The keys a recorded tap held, which it replays with. Null for a tap that held none.
    private static ClickOptions? ClickOptionsOf(RecordedAction action) =>
        action.Modifiers is { } names ? new ClickOptions { Modifiers = [.. names.Select(name => KeyModifiers.TryParse(name, out var modifier) ? modifier : throw new AgentException("AUTOMATION_UNSUPPORTED", "Cannot replay modifier " + name + "."))] } : null;

    /// <summary>
    /// Upstream's <c>relocate</c> for a recorded action: re-finds the node the action named (<see cref="DescriptorOf"/>), looking again while
    /// the failure may pass.
    /// </summary>
    private static Task<Relocated> Relocate(IReplayHost host, RecordedAction action, Look look) => Relocate(host, DescriptorOf(action), look);

    /// <summary>
    /// The descriptor a recorded action carries. Upstream's actions nest it as <c>target</c>; the port's carry its fields flat (.NET-only
    /// shape, see <c>TraceRecorder.ActionOf</c>), so a replay reads them back into one.
    /// </summary>
    private static RecordedTarget DescriptorOf(RecordedAction action) => new()
    {
        Role = action.Role,
        Name = action.Name,
        TestId = action.TestId,
        Within = action.Within,
        Text = action.Text,
        Placeholder = action.Placeholder,
        InputPurpose = action.InputPurpose,
        Position = action.Position,
    };

    /// <summary>
    /// Upstream's <c>relocate</c>: relocates one descriptor against the settling screen, looking again while <see cref="Retryable"/> says the
    /// failure may pass. The first look is the one in hand, or a capture settled as far as the previous action requires; the looks after it
    /// read the screen as it is, until the replay timeout runs out. Exactly one match, or the recorded position picking one of several, is the
    /// target.
    /// </summary>
    private static async Task<Relocated> Relocate(IReplayHost host, RecordedTarget descriptor, Look look)
    {
        var last = new Relocated(null, ReplayHandOffReason.TargetNotFound, [], null);
        var deadline = host.Time.GetUtcNow() + host.ReplayTimeout;
        var first = true;
        while (true)
        {
            var observation = first && look.InHand is { } inHand ? inHand : await host.ObserveAsync(first ? look.Settle : SettleMode.Raw).ConfigureAwait(false);
            first = false;
            var result = RelocateRules.RelocateDescriptor(descriptor, observation, host.Redact);
            if (result is RelocationResult.Found found && RelocateRules.NodeOf(observation, found.Id) is { } node)
            {
                host.MarkActing();
                return new Relocated(node, default, [], observation);
            }

            if (result is RelocationResult.Failed failed)
            {
                last = Failure(failed, observation);
                if (!Retryable(last.Failure, descriptor))
                {
                    return last;
                }
            }

            if (host.Time.GetUtcNow() >= deadline)
            {
                return last;
            }

            await Task.Delay(RetryDelay, host.Time, host.Signal).ConfigureAwait(false);
        }
    }

    // A failed relocation as the replay reads it: a missing node carries nothing, an ambiguous one the look-alikes and the screen they are on.
    private static Relocated Failure(RelocationResult.Failed failed, Observation observation) =>
        failed.Failure == RelocationFailure.TargetAmbiguous
            ? new Relocated(null, ReplayHandOffReason.TargetAmbiguous, [.. (failed.Candidates ?? []).Select(id => RelocateRules.NodeOf(observation, id)).OfType<SemanticNode>()], observation)
            : new Relocated(null, ReplayHandOffReason.TargetNotFound, [], null);

    /// <summary>
    /// Upstream's <c>retryable</c>: whether a failed relocation is worth another look. A missing target is: the screen may still be settling.
    /// So is ambiguity for a descriptor that recorded its position among twins: a form still rendering shows fewer of them than the recording
    /// counted, and the count catches up. Ambiguity for a descriptor without a position never is: two matching nodes will not become one by
    /// waiting, and acting on either would be a guess.
    /// </summary>
    private static bool Retryable(ReplayHandOffReason failure, RecordedTarget descriptor) =>
        failure == ReplayHandOffReason.TargetNotFound || descriptor.Position is not null;

    // A relocation that keeps the look-alikes it found with the screen they are on, for a replay that needs boxes.
    private readonly record struct Relocated(SemanticNode? Node, ReplayHandOffReason Failure, IReadOnlyList<SemanticNode> Candidates, Observation? Screen);

    /// <summary>Whether a recorded action acts on a bare viewport point, which takes its own look and reads its viewport.</summary>
    private static bool IsPointAction(RecordedAction action) => action.Kind is "tapAt" or "hoverAt" or "dragAt";

    /// <summary>Where one recorded bare point lands on the live screen, or why it cannot (upstream's <c>placePoint</c> result).</summary>
    private readonly record struct Placed(Point Point, ReplayHandOffReason? Failure = null, string? Why = null);

    /// <summary>
    /// Upstream's <c>placePoint</c>: where one recorded bare point lands on the live screen: inside its re-found node's live box when it sat
    /// in one, else as recorded once the viewport is the recorded size. A failure names the hand-off. A node the replay cannot re-find
    /// (<c>Relocate.IsRelocatableDescriptor</c>: an anonymous container recorded without a place) places the point as no node was
    /// recorded, which is upstream's <c>pointEnd</c>.
    /// </summary>
    private static async Task<Placed> PlacePoint(IReplayHost host, Point point, RecordedPointPlace? within, TraceViewport viewport, Look look)
    {
        if (within?.Target is not { } recordedNode || !RelocateRules.IsRelocatableDescriptor(recordedNode))
        {
            var screen = look.InHand ?? await host.ObserveAsync(look.Settle).ConfigureAwait(false);
            if (screen.TreeUnavailable)
            {
                return new Placed(default, ReplayHandOffReason.ActionFailed);
            }

            host.MarkActing();
            if (screen.Viewport is not { } live)
            {
                return new Placed(default, ReplayHandOffReason.ViewportChanged, "recorded at " + Size(viewport) + ", the screen is of unknown size");
            }

            if (live.Width != viewport.Width || live.Height != viewport.Height)
            {
                return new Placed(default, ReplayHandOffReason.ViewportChanged, "recorded at " + Size(viewport) + ", the screen is " + Size(live));
            }

            return new Placed(point);
        }

        var relocated = await Relocate(host, recordedNode, look).ConfigureAwait(false);
        var at = PlaceWithin(relocated, within, point, viewport);
        return at is { } placedAt ? new Placed(placedAt) : new Placed(default, relocated.Node is null ? relocated.Failure : ReplayHandOffReason.TargetNotFound);
    }

    private static string Size(TraceViewport viewport) => viewport.Width.ToString(CultureInfo.InvariantCulture) + "x" + viewport.Height.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Upstream's <c>relocatePair</c>: re-finds a drag's two nodes on one screen, so the nodes the drag joins come from the same look
    /// at the app. Waits like <c>Relocate</c> and gives up the same way: a missing node or a positioned twin is worth another look, an
    /// unpositioned ambiguity is not (<see cref="Retryable"/>).
    /// </summary>
    private static async Task<(SemanticNode? Source, SemanticNode? Destination, ReplayHandOffReason Failure)> RelocatePair(IReplayHost host, RecordedAction action, Look look)
    {
        if (action.Destination is not { } destination)
        {
            return (null, null, ReplayHandOffReason.TargetNotFound);
        }

        var source = DescriptorOf(action);
        var last = ReplayHandOffReason.TargetNotFound;
        var deadline = host.Time.GetUtcNow() + host.ReplayTimeout;
        var first = true;
        while (true)
        {
            var observation = first && look.InHand is { } inHand ? inHand : await host.ObserveAsync(first ? look.Settle : SettleMode.Raw).ConfigureAwait(false);
            first = false;
            // The end that fails decides: a missing node or a positioned twin is worth another look, an unpositioned ambiguity is not.
            var from = RelocateRules.RelocateDescriptor(source, observation, host.Redact);
            var to = from is RelocationResult.Failed ? null : RelocateRules.RelocateDescriptor(destination, observation, host.Redact);
            if (from is RelocationResult.Found foundFrom && to is RelocationResult.Found foundTo
                && RelocateRules.NodeOf(observation, foundFrom.Id) is { } sourceNode
                && RelocateRules.NodeOf(observation, foundTo.Id) is { } destinationNode)
            {
                host.MarkActing();
                return (sourceNode, destinationNode, default);
            }

            if ((from as RelocationResult.Failed ?? to as RelocationResult.Failed) is { } failed)
            {
                last = Failure(failed, observation).Failure;
                if (!Retryable(last, from is RelocationResult.Failed ? source : destination))
                {
                    return (null, null, last);
                }
            }

            if (host.Time.GetUtcNow() >= deadline)
            {
                return (null, null, last);
            }

            await Task.Delay(RetryDelay, host.Time, host.Signal).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Upstream's <c>placeWithin</c>: where a recorded point lands on the live screen: its place inside the re-found node's box, or among the
    /// visible look-alikes inside the one that contains the recorded point on a viewport of the recorded size, as the hit test that recorded
    /// it skipped hidden nodes. Null when the node is gone or has no box, or the point settles nothing: tapping the bare point could press
    /// whatever now sits there.
    /// </summary>
    private static Point? PlaceWithin(Relocated relocated, RecordedPointPlace within, Point point, TraceViewport viewport)
    {
        static BoundingBox? Usable(BoundingBox? rect) => rect is null || rect.Width <= 0 || rect.Height <= 0 ? null : rect;
        Point Inside(BoundingBox box) => new(box.X + within.Fx * box.Width, box.Y + within.Fy * box.Height);

        if (relocated.Node is { } node)
        {
            return Usable(node.Rect) is { } box ? Inside(box) : null;
        }

        if (relocated.Failure != ReplayHandOffReason.TargetAmbiguous || relocated.Screen?.Viewport is not { } live || live.Width != viewport.Width || live.Height != viewport.Height)
        {
            return null;
        }

        var containing = relocated.Candidates
            .Where(candidate => !candidate.States.Hidden)
            .Select(candidate => Usable(candidate.Rect))
            .OfType<BoundingBox>()
            .Where(box => Geometry.ContainsPoint(box, point))
            .ToList();
        if (containing.Count == 1)
        {
            return Inside(containing[0]);
        }

        // Several look-alikes hold the point and nest: a host view and the view inside it, a group inside a group, an anonymous container among
        // its kind. Whichever of them is the recorded node, the recorded point on a viewport of the recorded size names the same pixel, and it
        // is acted on as a bare point would be. Siblings that merely overlap under the point stay ambiguous and hand off.
        return containing.Count > 1 && Nested(containing) ? point : null;
    }

    // Whether the boxes form one chain of containers, each holding the next smaller one, rather than siblings that overlap.
    private static bool Nested(List<BoundingBox> boxes)
    {
        var byArea = boxes.OrderBy(box => box.Width * box.Height).ToList();
        return byArea.Skip(1).Select((box, index) => (Outer: box, Inner: byArea[index]))
            .All(pair => pair.Inner.X >= pair.Outer.X && pair.Inner.Y >= pair.Outer.Y
                && pair.Inner.X + pair.Inner.Width <= pair.Outer.X + pair.Outer.Width
                && pair.Inner.Y + pair.Inner.Height <= pair.Outer.Y + pair.Outer.Height);
    }

    private static string FirstLine(string text)
    {
        var end = text.IndexOfAny(['\r', '\n']);
        return (end < 0 ? text : text[..end]).Trim();
    }
}
