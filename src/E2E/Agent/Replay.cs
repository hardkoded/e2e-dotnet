// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Engine;
using E2E.Internal;
using static E2E.Agent;

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
            if (previous is not null && !HasTarget(action) && options?.LooksBeforeFree?.Invoke() == true)
            {
                await host.ObserveAsync(look.Settle).ConfigureAwait(false);
            }

            var summary = DefaultAgent.Describe(action);

            // A replayed action draws on the step's action budget like a live one. The first always fits.
            // An action with a target takes its slot once the target is found, so a lost target is a miss.
            if ((action.Kind is "back" or "scroll" or "scrollUntil" or "press") && !HasTarget(action) && !host.TryReserve())
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
                    case "press" when !HasTarget(action):
                        await host.Actions.PressAsync(null, action.Key ?? "Enter", token).ConfigureAwait(false);
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
    /// Re-finds a recorded target. The first look is the one in hand, or a capture settled as far as the previous action
    /// requires; the looks after it read the screen as it is. Exactly one match is the target: several are ambiguity, which
    /// is never worth another look, and none is worth looking again until the replay timeout runs out.
    /// </summary>
    private static async Task<(SemanticNode? Node, ReplayHandOffReason Failure)> Relocate(IReplayHost host, RecordedAction action, Look look)
    {
        var deadline = host.Time.GetUtcNow() + host.ReplayTimeout;
        var first = true;
        while (true)
        {
            var observation = first && look.InHand is { } inHand ? inHand : await host.ObserveAsync(first ? look.Settle : SettleMode.Raw).ConfigureAwait(false);
            first = false;
            var matches = Find(observation, action.Role, action.Name, action.TestId, null, host.Redact);
            if (matches.Count == 1)
            {
                host.MarkActing();
                return (matches[0], default);
            }

            if (matches.Count > 1)
            {
                return (null, ReplayHandOffReason.TargetAmbiguous);
            }

            if (host.Time.GetUtcNow() >= deadline)
            {
                return (null, ReplayHandOffReason.TargetNotFound);
            }

            await Task.Delay(RetryDelay, host.Time, host.Signal).ConfigureAwait(false);
        }
    }

    private static string FirstLine(string text)
    {
        var end = text.IndexOfAny(['\r', '\n']);
        return (end < 0 ? text : text[..end]).Trim();
    }
}
