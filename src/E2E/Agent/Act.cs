// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using E2E.Cache;
using E2E.Engine;
using E2E.Internal;

namespace E2E;

// The harness of an act step (upstream's agent/act.ts, step-cache.ts): the budget, the cache replay and recording, and the
// verdict the step ends with. What thinks is the step's executor (see Executor.cs), which a built-in tool loop is.
public sealed partial class Agent
{
    /// <summary>How long an executor that was told to stop gets to unwind before the runtime settles the step without it.</summary>
    private static readonly TimeSpan ExecutorUnwindGrace = TimeSpan.FromSeconds(1);

    private Task<ActResult> RunActStepAsync(string instruction, ActOptions? options, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instruction);
        CheckInstruction(instruction);
        ActParams.Validate(options?.Params);
        var agent = _scope.Select(options?.Agent);
        return DispatchStepAsync(
            new StepSpec
            {
                Kind = ExecutorStepKind.Act,
                Instruction = instruction,
                Params = options?.Params,
                Timeout = options?.Timeout,
                MaxSteps = options?.MaxSteps,
                MaxModelCalls = options?.MaxModelCalls,
            },
            agent,
            cancellationToken);
    }

    /// <summary>
    /// Upstream's <c>runAssertStep</c>: routes one <c>assert</c> through the agent's custom executor. The brain that plans the
    /// flows judges the assertions too, so swapping brains swaps all the thinking. <c>Screenshot</c> is evidence the runtime
    /// takes after the verdict, so it is honored here as on the built-in path. <c>Vision</c> is what the executor's model
    /// sees, which only the executor decides, so the call is refused.
    /// </summary>
    private async Task AssertWithExecutorAsync(string statement, AssertOptions? options, ResolvedAgent agent, CancellationToken cancellationToken)
    {
        if ((options?.Vision ?? VisionMode.Off) != VisionMode.Off)
        {
            throw new ConfigurationException("UNSUPPORTED_CAPABILITY", "agent.assert vision (AssertOptions.Vision) is not supported with a custom executor: the executor decides what its model sees");
        }

        await DispatchStepAsync(
            new StepSpec
            {
                Kind = ExecutorStepKind.Assert,
                Instruction = statement,
                Timeout = options?.Timeout,
                Screenshot = options?.Screenshot,
            },
            agent,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Upstream's <c>dispatchAgentStep</c>: opens the step, tries a cached replay, hands the executor the step when the cache
    /// misses or diverges, settles its verdict, and stages the recording. An assert is a verification step: its passing is
    /// what confirms the recordings staged before it.
    /// </summary>
    private async Task<ActResult> DispatchStepAsync(StepSpec spec, ResolvedAgent agent, CancellationToken cancellationToken)
    {
        try
        {
            return await DispatchCoreAsync(spec, agent, cancellationToken).ConfigureAwait(false);
        }
        catch (AgentException ex) when (string.Equals(ex.Code, "CANCELLED", StringComparison.Ordinal) && (cancellationToken.IsCancellationRequested || _scope.Token().IsCancellationRequested))
        {
            // The caller's own cancel reads as cancellation, not as a failed step.
            throw new OperationCanceledException(ex.Message, ex, cancellationToken.IsCancellationRequested ? cancellationToken : _scope.Token());
        }
    }

    private async Task<ActResult> DispatchCoreAsync(StepSpec spec, ResolvedAgent agent, CancellationToken cancellationToken)
    {
        var api = spec.Kind == ExecutorStepKind.Act ? "agent.act" : "agent.assert";
        var custom = agent.Executor is not null;
        var executor = agent.Executor ?? DefaultAgent.CreateBuiltInAgent(new BuiltInAgentOptions { System = agent.System, Tools = agent.Tools });
        var instruction = spec.Instruction;
        var maxCalls = Budget(spec.MaxModelCalls, agent.MaxModelCalls, "MaxModelCalls");
        var budget = new StepAccounting(Budget(spec.MaxSteps, agent.MaxSteps, "MaxSteps"), maxCalls, api);
        var timeout = ResolveTimeout(spec.Timeout, spec.Kind == ExecutorStepKind.Act ? _scope.StepTimeout : agent.JudgmentTimeout);

        // The caller's token and the attempt's end the step as a cancel. The step's own token also ends on any hard stop, so a timeout
        // and a cancel are told apart by what the budget recorded.
        using var outer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _scope.Token());
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(outer.Token);
        using var clock = new CancellationTokenSource(timeout);
        using var expiry = clock.Token.Register(() => budget.Fatalize(budget.TimedOut()));
        var token = stop.Token;
        var feed = new ObservationFeed(_scope.Session, TimeProvider.System) { Scope = _scope };
        _feed = feed;
        var stepIndex = _scope.NextStepIndex();
        budget.Attach(_scope, timeout, outer.Token, stop, _scope.Redactor.Redact, feed.ArmAfter);
        _scope.Remember(spec.Params);
        var declared = ActParams.SecretsIn(spec.Params);
        var dispatcher = new ActionDispatcher(_scope, feed, budget, spec.Params, () => declared);
        ActContext? context = null;
        try
        {
            // An act is cacheable unless its executor opted out. An assert must not change the app, so its recording would be empty.
            var cacheable = spec.Kind == ExecutorStepKind.Act && executor.Cache != ExecutorCache.Off;
            PendingAct? pending = null;
            var key = "";
            if (cacheable)
            {
                key = _scope.ClaimKey(instruction, spec.Params, agent, out var callIndex);
                pending = new PendingAct
                {
                    Key = key,
                    ParamCollision = CacheKeys.Collides(spec.Params),
                    Step = Provenance(instruction, spec.Params, callIndex, agent.Name),
                };
                if (_scope.CacheEnabled)
                {
                    _scope.Acts.Add(pending);
                }
            }

            // The baseline a recording's delta is read from, the screen a replay starts on, and the built-in agent's first look:
            // a settled screen, as upstream's start capture is. An executor that brings its own eyes owes the cache nothing
            // when the screen cannot be read: it runs, and nothing is recorded.
            Observation? start = null;
            if (!custom || (cacheable && _scope.CacheEnabled))
            {
                try
                {
                    start = await _feed.ObserveAsync(SettleMode.HeldStill, token).ConfigureAwait(false);
                }
                catch (EngineException ex) when (custom && !StepExecutors.IsRuntimeHardStop(ex) && !token.IsCancellationRequested)
                {
                    start = null;
                }
            }

            var actions = budget.Recorded;
            CacheInfo? info = null;
            string? uncertain = null;
            var handoff = false;
            var actionsAtEndMismatch = -1;
            if (pending is not null && start is not null && _scope.CacheEnabled && _scope.ReplayEligible && _scope.Cache is not null)
            {
                var replay = await TryReplayAsync(pending, start, actions, budget, dispatcher, spec.Params, token).ConfigureAwait(false);
                info = new CacheInfo { Mode = replay.Info!.Mode, Reason = replay.Info.Reason, ReplayedActions = actions.Count, TotalActions = pending.RecordedActions, Detail = replay.Info.Detail };
                handoff = replay.Handoff;
                // A store that could not be read says nothing about a recording, so strict mode runs the step live too.
                // So does a recording that stops at a project tool: as upstream, a gap is no stale recording, only a step the cache cannot finish.
                if (_scope.CacheStrict && !replay.Completed && !pending.ReadFailed && info?.Reason is not null and not ("no-entry" or "gap"))
                {
                    throw new AgentException(
                        "REPLAY_STALE",
                        "The recording for '" + instruction + "' no longer matches (" + info.Reason + "). Strict cache mode does not run it live; re-record it without cache.strict.");
                }

                if (_scope.CacheStrict && string.Equals(info?.Reason, "no-entry", StringComparison.Ordinal))
                {
                    FailIfRekeyed(pending, instruction);
                }

                if (replay.Completed)
                {
                    pending.Completed = true;
                    pending.ReplayedWhole = true;
                    pending.Entry = replay.Entry;
                    _scope.Completed.Add("Replayed: " + instruction);
                    return new ActResult { Summary = "Replayed recorded actions.", Cache = info, ModelCalls = 0, Actions = budget.Used };
                }

                if (handoff && string.Equals(info?.Reason, "end-mismatch", StringComparison.Ordinal))
                {
                    actionsAtEndMismatch = actions.Count;
                }

                uncertain = replay.Uncertain;
            }
            else if (pending is not null && start is not null && _scope.CacheEnabled && _scope.Cache is not null)
            {
                // Outside strict mode, a retry records like any attempt but never replays.
                _scope.Missed++;
                info = ReplayAttempt.Miss("retry").Info;
            }

            // The screen the step began on is stale once a replay acted, so the executor takes a fresh look.
            context = new ActContext(_scope, feed, budget, dispatcher, spec, agent, stepIndex, declared, handoff && actions.Count > 0 ? null : start, handoff ? PrefixOf(actions, pending!, info!.Reason, uncertain) : null);
            StepVerdict verdict;
            try
            {
                verdict = await RunExecutorAsync(executor, context, budget, stop, outer.Token).ConfigureAwait(false);
                // A verdict that comes after the caller's cancel is not trusted over it.
                outer.Token.ThrowIfCancellationRequested();
            }
            catch (Exception ex) when (ex is not OperationCanceledException || budget.HardStop is not null)
            {
                throw SettleThrown(ToAgentException(ex, budget), budget, executor);
            }

            if (custom)
            {
                verdict = ValidateVerdict(verdict, executor.Name);
            }

            // Evidence for an assert is taken after the verdict, on a pass and on a failure alike.
            var kept = spec.Kind == ExecutorStepKind.Assert && spec.Screenshot != false && budget.HardStop is null
                ? await KeepBoundedEvidenceAsync(budget, outer.Token, cancellationToken).ConfigureAwait(false)
                : null;
            Settle(verdict, budget, spec, executor, kept, out var summary);
            if (spec.Kind == ExecutorStepKind.Assert)
            {
                _scope.MarkVerified();
                return new ActResult { Summary = summary, ModelCalls = budget.ModelCalls, Actions = budget.Used };
            }

            if (actionsAtEndMismatch >= 0 && actions.Count > actionsAtEndMismatch)
            {
                // The recorded actions ran but did not reach the recorded end, so they are proven
                // not to produce it. Evict the entry and let a clean run record the flow again.
                if (_scope.CacheWrite && pending is not null)
                {
                    _scope.Cache!.Delete(key);
                }

                _scope.Completed.Add(summary.Length == 0 ? instruction : summary);
                return new ActResult { Summary = summary, Cache = info, ModelCalls = budget.ModelCalls, Actions = budget.Used };
            }

            _scope.Completed.Add(summary.Length == 0 ? instruction : summary);
            if (pending is not null)
            {
                pending.Completed = true;
                // Only a cache that writes keeps the recording, so only it waits for the screen to settle: after the
                // last action's change, held still. A step that took no action has nothing to record.
                pending.Entry = _scope.CacheWrite && start is not null && actions.Count > 0
                    ? await StageAsync(pending.Step!, start, actions, spec.Params, token).ConfigureAwait(false)
                    : null;
                if (info is not null && pending.ParamCollision && pending.Entry is not null)
                {
                    // The recording cannot be templated safely, so it is not written, and the result says why.
                    info = new CacheInfo { Mode = info.Mode, Reason = info.Reason, ReplayedActions = info.ReplayedActions, TotalActions = info.TotalActions, Detail = info.Detail, NotRecorded = "param-collision" };
                }
            }

            return new ActResult { Summary = summary, Cache = info, ModelCalls = budget.ModelCalls, Actions = budget.Used };
        }
        catch (Exception ex) when (budget.HardStop is { } hardStop && (ex is OperationCanceledException || (ex is EngineException { Code: EngineErrorCodes.Cancelled })))
        {
            // The clock or a cancel ended the step while it looked at the screen or replayed a recording.
            throw hardStop;
        }
        finally
        {
            budget.Close();
            context?.WriteTranscript();
        }
    }

    // The evidence of a judgment an executor made, best effort and bounded as upstream bounds it: it waits no longer than one action may
    // take, or than the step has left, and a capture that outlives that is abandoned, so the verdict stands without it.
    private async Task<string?> KeepBoundedEvidenceAsync(StepAccounting budget, CancellationToken attempt, CancellationToken cancellationToken)
    {
        var bound = TimeSpan.FromMilliseconds(Math.Min(_scope.ActionTimeout.TotalMilliseconds, budget.RemainingMs()));
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(attempt);
        limit.CancelAfter(bound);
        var capture = KeepEvidenceAsync(limit.Token, cancellationToken);
        try
        {
            return await capture.WaitAsync(bound, CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _ = capture.ContinueWith(static task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            return null;
        }
    }

    // What the replay ran before it handed the step over, in the words an executor reads.
    private static ReplayedPrefix PrefixOf(List<RecordedAction> actions, PendingAct pending, string? reason, string? uncertain) =>
        new(actions.Select(DefaultAgent.Describe).ToList(), pending.RecordedActions, ReplayHandOffReasons.Parse(reason), uncertain);

    /// <summary>
    /// Runs the executor against the step's clock. An executor that ignores every call to the context still cannot outlive the
    /// deadline: once the step is stopped it gets <see cref="ExecutorUnwindGrace"/> to unwind, and then the runtime settles the step
    /// without it and abandons what it was doing.
    /// </summary>
    private static async Task<StepVerdict> RunExecutorAsync(IStepExecutor executor, StepExecutorContext context, StepAccounting budget, CancellationTokenSource stop, CancellationToken attemptCancellation)
    {
        var running = Task.Run(() => executor.RunStepAsync(context, stop.Token), CancellationToken.None);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = stop.Token.Register(() => stopped.TrySetResult());
        if (await Task.WhenAny(running, stopped.Task).ConfigureAwait(false) == running)
        {
            return await running.ConfigureAwait(false);
        }

        if (await Task.WhenAny(running, Task.Delay(ExecutorUnwindGrace)).ConfigureAwait(false) == running)
        {
            return await running.ConfigureAwait(false);
        }

        // The abandoned run must not fault unobserved.
        _ = running.ContinueWith(static task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        if (budget.HardStop is { } hardStop)
        {
            throw hardStop;
        }

        throw new OperationCanceledException(attemptCancellation);
    }

    // Maps a failure of the executor, or of a call it made, onto the closed agent codes. Cancellation stays cancellation.
    private static Exception ToAgentException(Exception error, StepAccounting budget)
    {
        if (budget.HardStop is { } hardStop && (error is OperationCanceledException || StepExecutors.IsRuntimeHardStop(error as E2EException ?? new AgentException("ACTION_FAILED", error.Message))))
        {
            return hardStop;
        }

        return error switch
        {
            AgentException or OperationCanceledException or SkipException => error,
            E2EException { Code: var code } e2e when StepExecutors.AgentCodes.Contains(code) => new AgentException(code, e2e.Message, e2e),
            E2EException { Code: "UNSUPPORTED_CAPABILITY" or "INVALID_CONFIG" } e2e => new AgentException("POLICY_DENIED", e2e.Message, blocked: true, e2e),
            _ => new AgentException("ACTION_FAILED", error.Message, error),
        };
    }

    // The recorded hard stop wins over any derived failure, and a runtime code the runtime cannot corroborate is an invalid
    // verdict, not a runtime failure.
    private static Exception SettleThrown(Exception error, StepAccounting budget, IStepExecutor executor)
    {
        if (error is not AgentException agent || !StepExecutors.RuntimeCodes.Contains(agent.Code))
        {
            return error;
        }

        // A loop that spent its model calls on actions the budget turned away names the action budget, which is the cause.
        if (budget.HardStop is null && budget.Exhausted && string.Equals(agent.Code, "STEP_BUDGET_EXHAUSTED", StringComparison.Ordinal))
        {
            return budget.Stop(null);
        }

        if (budget.HardStop is { } hardStop)
        {
            return hardStop;
        }

        return budget.Vouches(agent.Code) ? error : Invented(agent.Code, executor, agent);
    }

    private static AgentException Invented(string code, IStepExecutor executor, Exception? cause = null) =>
        cause is null
            ? new("MODEL_OUTPUT_INVALID", "executor \"" + executor.Name + "\" reported runtime code " + code + ", which the runtime never assigned")
            : new("MODEL_OUTPUT_INVALID", "executor \"" + executor.Name + "\" reported runtime code " + code + ", which the runtime never assigned", cause);

    /// <summary>Upstream's <c>validateVerdict</c>: closes the verdict grammar, so an executor cannot invent a status or a code.</summary>
    private static StepVerdict ValidateVerdict(StepVerdict verdict, string executorName)
    {
        static AgentException Invalid(string executorName, string issue) =>
            new("MODEL_OUTPUT_INVALID", "executor \"" + executorName + "\" returned an invalid verdict: " + issue);

        if (!Enum.IsDefined(verdict.Status))
        {
            throw Invalid(executorName, "status must be passed, failed, or blocked, got " + verdict.Status);
        }

        if (string.IsNullOrWhiteSpace(verdict.Summary))
        {
            throw Invalid(executorName, "summary must be a non-empty string");
        }

        var code = verdict.ErrorCode;
        if (code is not null && !StepExecutors.AgentCodes.Contains(code))
        {
            throw Invalid(executorName, "unknown errorCode \"" + code + "\"");
        }

        if (verdict.Status == StepVerdictStatus.Passed && code is not null)
        {
            throw Invalid(executorName, "a passed verdict cannot carry an errorCode");
        }

        if (verdict.Status == StepVerdictStatus.Blocked && (code is null || !StepExecutors.BlockableCodes.Contains(code)))
        {
            throw Invalid(executorName, "blocked requires a blockable errorCode (one of " + string.Join(", ", StepExecutors.BlockableCodes.Order(StringComparer.Ordinal)) + ")");
        }

        return verdict with { Summary = verdict.Summary.Trim() };
    }

    /// <summary>
    /// Maps the executor's verdict onto the step's outcome, carrying the evidence on a failure. A hard stop of the step, a
    /// spent budget, a timeout, or a cancel, is the step's truth: no verdict outranks it.
    /// </summary>
    private static void Settle(StepVerdict verdict, StepAccounting budget, StepSpec spec, IStepExecutor executor, string? screenshot, out string summary)
    {
        summary = verdict.Summary ?? "";
        var code = verdict.ErrorCode;
        var stop = budget.HardStop ?? (budget.Exhausted ? budget.Stop(null) : null);
        if (stop is not null)
        {
            if (string.Equals(stop.Code, "CANCELLED", StringComparison.Ordinal))
            {
                throw stop;
            }

            // The model cannot declare success over the runtime's own accounting, and a failure it gave no code for
            // inherits the hard stop, so the step names it.
            if (verdict.Status == StepVerdictStatus.Passed || code is null)
            {
                var reason = stop.Message.TrimEnd('.');
                throw new AgentException(stop.Code, summary.Length == 0 ? reason + "." : reason + ": " + summary, blocked: true);
            }
        }

        if (code is not null && StepExecutors.RuntimeCodes.Contains(code) && !budget.Vouches(code))
        {
            throw Invented(code, executor);
        }

        if (verdict.Status == StepVerdictStatus.Passed)
        {
            return;
        }

        var blocked = verdict.Status == StepVerdictStatus.Blocked;
        if (blocked)
        {
            throw new AgentException(code!, summary.Length == 0 ? "The step is blocked." : summary, blocked: true) { Screenshot = screenshot };
        }

        var failure = code is not null && StepExecutors.AgentCodes.Contains(code) ? code : spec.Kind == ExecutorStepKind.Assert ? "ASSERTION_FAILED" : "ACTION_FAILED";
        throw new AgentException(failure, summary.Length == 0 ? "The step failed." : summary) { Screenshot = screenshot };
    }

    private async Task<ReplayAttempt> TryReplayAsync(
        PendingAct pending,
        Observation start,
        List<RecordedAction> actions,
        StepAccounting budget,
        ActionDispatcher dispatcher,
        IReadOnlyDictionary<string, object?>? parameters,
        CancellationToken token)
    {
        CacheLookup lookup;
        try
        {
            lookup = _scope.Cache!.Read(pending.Key);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The cache is disposable: a store that cannot be read is a miss, never a failed step.
            pending.ReadFailed = true;
            _scope.Missed++;
            return ReplayAttempt.Miss("invalid-entry");
        }

        if (lookup.Entry is null)
        {
            _scope.Missed++;
            return ReplayAttempt.Miss(lookup.Reason ?? "no-entry");
        }

        var entry = lookup.Entry;
        pending.ReadEntry = true;

        // Any store can return a hit, so its shape is checked here, at the one read site.
        if (entry.Schema != FileStepCache.SchemaVersion || entry.Actions is not { Count: > 0 } || entry.Appeared is null || entry.Gone is null)
        {
            _scope.Missed++;
            return ReplayAttempt.Miss("invalid-entry");
        }

        pending.RecordedActions = entry.Actions.Count;
        // A recording that opens with navigate sets up its own start screen.
        var opensWithNavigate = string.Equals(entry.Actions[0].Kind, "navigate", StringComparison.Ordinal);
        if (!opensWithNavigate && !CacheRoute.Same(Recorded(entry.Route, parameters), start.Route))
        {
            _scope.Missed++;
            return ReplayAttempt.Miss("wrong-context");
        }

        pending.ConsumedReplay = true;

        // Every screen the replay looks at, in order. The end check measures the recorded delta from the first of them
        // on the route the replay ends on.
        var screens = new List<Observation> { start };
        var host = new ActReplayHost(_scope, _feed, budget, dispatcher, parameters, Recorded(entry.EndRoute, parameters), screens, token);
        var outcome = await Replay.ReplayTrace(host, entry.Actions, new ReplayOptions { Initial = start }).ConfigureAwait(false);

        // Where the replay stopped, for the report: the recorded action, one-based, and what went wrong there when
        // more than the reason says.
        string StoppedAt(string? why = null) =>
            "at action " + (actions.Count + 1).ToString(CultureInfo.InvariantCulture) + " of " + entry.Actions.Count.ToString(CultureInfo.InvariantCulture) + ", " + DefaultAgent.Describe(entry.Actions[actions.Count]) + (why is null ? "" : ": " + why);

        if (!outcome.Completed)
        {
            var reason = ReplayHandOffReasons.Name(outcome.StopReason!.Value);
            switch (outcome.StopReason)
            {
                case ReplayHandOffReason.Gap or ReplayHandOffReason.TargetNotFound or ReplayHandOffReason.TargetAmbiguous or ReplayHandOffReason.InvalidEntry when outcome.Executed == 0:
                    _scope.Missed++;
                    actions.Clear();
                    return ReplayAttempt.Miss(reason, StoppedAt());
                case ReplayHandOffReason.ActionUncertain:
                    _scope.HandedOff++;
                    return ReplayAttempt.Hand(reason, StoppedAt(outcome.Detail), outcome.UncertainAction);
                case ReplayHandOffReason.ActionFailed:
                    _scope.HandedOff++;
                    return ReplayAttempt.Hand(reason, StoppedAt(outcome.Detail));
                default:
                    _scope.HandedOff++;
                    return ReplayAttempt.Hand(reason, StoppedAt());
            }
        }

        var end = await WaitForEndAsync(entry, start, screens, parameters, token).ConfigureAwait(false);
        if (!end.Matches)
        {
            _scope.HandedOff++;
            return ReplayAttempt.Hand("end-mismatch", StopDetail(entry, end, parameters));
        }

        _scope.Replayed++;
        return ReplayAttempt.Done(entry);
    }

    // The last action may start a navigation or a slow render, so the end route and anchors get the replay timeout to show up.
    // The replay must also have produced the delta: an outcome already on screen before the actions proves nothing.
    // A failure says which of these failed, for the report: an outcome that already showed, or the anchors that
    // did not hold on the last look the wait took on the recorded end route.
    private async Task<EndStateCheck> WaitForEndAsync(
        CacheEntry entry,
        Observation start,
        List<Observation> screens,
        IReadOnlyDictionary<string, object?>? parameters,
        CancellationToken token)
    {
        // As a recording reads its delta, the evidence counts from the screen the first action was resolved against.
        // A screen a scroll to a text marked while it paged is not among the screens; the stretch then opens with it.
        var actedOn = _feed.FirstActedOn ?? start;
        var from = screens.IndexOf(actedOn);
        List<Observation> stretch = from >= 0 ? screens.GetRange(from, screens.Count - from) : [actedOn, .. screens.Skip(1)];
        var baseline = Baseline(stretch, Recorded(entry.EndRoute, parameters));
        var baselineAnchors = baseline is null ? null : Project(baseline, parameters);
        var inputTargets = entry.Actions.Where(HasTarget).Select(action => new RecordedTarget { Role = action.Role, Name = action.Name, TestId = action.TestId });
        if (!Anchors.Evidenced(entry, baselineAnchors, inputTargets))
        {
            // A recording with no delta fails the evidence too, but nothing of it showed before the replay
            // acted: that is the generic detail.
            return entry.Appeared.Count > 0 || entry.Gone.Count > 0 ? new EndStateCheck(false, AlreadyShowing: true) : new EndStateCheck(false);
        }

        var before = baselineAnchors ?? Project(actedOn, parameters);
        var deadline = DateTime.UtcNow + _scope.ReplayTimeout;
        DeltaMismatch? mismatch = null;
        while (true)
        {
            // Every look must still be on the recorded end route: a screen that moved on is another screen.
            var end = await _feed.ObserveAsync(SettleMode.Raw, token).ConfigureAwait(false);
            if (CacheRoute.Same(Recorded(entry.EndRoute, parameters), end.Route))
            {
                mismatch = Anchors.Mismatch(entry, Project(end, parameters), before);
                if (mismatch is null)
                {
                    return new EndStateCheck(true);
                }
            }

            if (DateTime.UtcNow >= deadline)
            {
                return new EndStateCheck(false, Failure: mismatch);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), token).ConfigureAwait(false);
        }
    }

    // Where and why a replay handed the step back, for the report: for an end state that did not hold, why: the
    // outcome already showed before the replay acted, or the anchors that failed on the last look. With neither
    // known, the screen was never read on the recorded route, and the detail says what the recording expected there.
    private static string StopDetail(CacheEntry entry, EndStateCheck end, IReadOnlyDictionary<string, object?>? parameters)
    {
        var route = Recorded(entry.EndRoute, parameters);
        var where = string.IsNullOrEmpty(route) ? "" : " on " + route;
        if (end.AlreadyShowing)
        {
            return "every recorded action ran, but the recorded outcome already showed" + where + " before the replay acted there, so nothing proves the replay produced it";
        }

        var what = end.Failure is { } failure
            ? MismatchParts(failure)
            : entry.Appeared.Count > 0 ? "expected " + string.Join(", ", entry.Appeared.Take(2).Select(DescribeAnchor)) : "";
        return "every recorded action ran, but the recorded end state did not show" + where + (what.Length == 0 ? "" : ": " + what);
    }

    // A recorded anchor as the detail names it: the node, then its text when the name alone would hide the state
    // the recording expects (status "Marker" text="saved").
    private static string DescribeAnchor(RecordedTarget anchor)
    {
        return anchor.Text is not null && anchor.Name is not null && anchor.Text != anchor.Name
            ? TargetLabel(anchor) + " text=" + Quote(TextRules.Bound(anchor.Text, 40))
            : TargetLabel(anchor);
    }

    // A node as every report line that names one reads it: its role, then its label in quotes.
    private static string TargetLabel(RecordedTarget target)
    {
        var label = target.Name ?? target.Text ?? target.Placeholder ?? target.TestId ?? "";
        var role = target.Role ?? "node";
        return label.Length == 0 ? role : role + " " + Quote(TextRules.Bound(label, 40));
    }

    private static string Quote(string text)
    {
        return JsonSerializer.Serialize(text, JsonQuote);
    }

    // The anchors a mismatch names, two of each kind at most: those the replay did not bring up, those it did not
    // clear, and alerts it raised that the recording never saw. Kinds are joined with commas, never semicolons: a
    // report line already separates its own parts with those.
    private static string MismatchParts(DeltaMismatch mismatch)
    {
        static IEnumerable<string> Part(string label, IReadOnlyList<RecordedTarget> anchors)
        {
            if (anchors.Count == 0)
            {
                yield break;
            }

            var more = anchors.Count > 2 ? " (+" + (anchors.Count - 2).ToString(CultureInfo.InvariantCulture) + " more)" : "";
            yield return label + " " + string.Join(" and ", anchors.Take(2).Select(DescribeAnchor)) + more;
        }

        return string.Join(", ", Part("missing", mismatch.Missing).Concat(Part("still showing", mismatch.Present)).Concat(Part("unrecorded", mismatch.UnrecordedAlerts)));
    }

    // The first screen of the replay's last stretch on the end route, or null when its last screen before the end
    // was on another route, so the last action moved it.
    private static Observation? Baseline(List<Observation> screens, string? endRoute)
    {
        Observation? baseline = null;
        foreach (var screen in screens)
        {
            baseline = CacheRoute.Same(screen.Route, endRoute) ? baseline ?? screen : null;
        }

        return baseline;
    }

    /// <summary>
    /// Under strict mode, ends a step whose key found no entry while the cache directory holds a
    /// recording made for the same step under another key: the runner or the agent's context changed
    /// since, and the recording no longer replays. A step whose instruction or params changed is a new
    /// step and still runs live. A custom <see cref="IStepCache"/> is not checked.
    /// </summary>
    private void FailIfRekeyed(PendingAct pending, string instruction)
    {
        if (_scope.Cache is not FileStepCache files
            || files.UnderAnotherKey(pending.Key, pending.Step!) is not { } previous)
        {
            return;
        }

        throw new AgentException(
            "REPLAY_STALE",
            "The recording for '" + instruction + "' sits under another cache key (" + previous + ".json), since the runner or the agent's context changed after it was recorded. Strict cache mode does not run it live; re-record it without cache.strict.");
    }

    // A route is recorded redacted, with each unique() value as its slot, and read back with this call's values.
    private string Template(string route, IReadOnlyDictionary<string, object?>? parameters) =>
        CacheKeys.Template(_scope.Redactor.Redact(route), parameters);

    private static string? Recorded(string? route, IReadOnlyDictionary<string, object?>? parameters) =>
        route is null ? null : CacheKeys.Detemplate(route, parameters);

    // The cache is disposable: a passing step whose end screen cannot be read records nothing, and still passes.
    private async Task<CacheEntry?> StageAsync(
        CacheEntry step,
        Observation start,
        List<RecordedAction> actions,
        IReadOnlyDictionary<string, object?>? parameters,
        CancellationToken token)
    {
        Observation end;
        try
        {
            end = await _feed.ObserveAsync(SettleMode.HeldStill, token).ConfigureAwait(false);
        }
        catch (EngineException)
        {
            return null;
        }

        return BuildEntry(step, start.Route, _feed.FirstActedOn ?? start, end, actions, parameters);
    }

    // A step that changed nothing a replay could check, no node and no route, records nothing:
    // its recording would replay on mechanics alone.
    private CacheEntry? BuildEntry(
        CacheEntry step,
        string route,
        Observation baseline,
        Observation end,
        List<RecordedAction> actions,
        IReadOnlyDictionary<string, object?>? parameters)
    {
        var routeMoved = !CacheRoute.Same(baseline.Route, end.Route);
        var (appeared, gone) = Anchors.Describe(Project(baseline, parameters), Project(end, parameters), routeMoved);
        if (appeared.Count == 0 && gone.Count == 0 && !routeMoved)
        {
            return null;
        }

        var entry = new CacheEntry { Schema = step.Schema };
        entry.SetStep(step);
        entry.Route = Template(route, parameters);
        entry.EndRoute = Template(end.Route, parameters);
        entry.Actions = actions.ToList();
        entry.Appeared = appeared;
        entry.Gone = gone;
        return entry;
    }

    /// <summary>
    /// The step an entry is recorded for, as the entry stores it. None of it is replay input, so a
    /// secret value in the test title, instruction, or agent name is masked like any other recorded string.
    /// </summary>
    private CacheEntry Provenance(string instruction, IReadOnlyDictionary<string, object?>? parameters, int callIndex, string agent)
    {
        return new CacheEntry
        {
            Schema = FileStepCache.SchemaVersion,
            Test = _scope.Redactor.Redact(_scope.TestTitle),
            Instruction = _scope.Redactor.Redact(instruction.Trim()),
            Engine = _scope.EnginePlatform,
            ParamsDigest = CacheKeys.ParamsDigest(parameters),
            CallIndex = callIndex,
            Agent = _scope.Redactor.Redact(agent),
        };
    }

    // Anchors as the recording stores them: a name or text holding a unique param is skipped, every field has
    // secrets redacted, and a name, text, or value has each unique param as its slot. The secrets are read as
    // they are now, so an unchanged node is no delta.
    private List<AnchorNode> Project(Observation observation, IReadOnlyDictionary<string, object?>? parameters)
    {
        var redactor = _scope.Redactor;
        return Anchors.Project(
            observation,
            text => ContainsParam(text, parameters),
            value => CacheKeys.Template(redactor.Redact(value), parameters),
            redactor.Redact,
            (text, limit) => AgentObservation.RedactText(text, redactor, limit));
    }

    private static bool ContainsParam(string name, IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters is null)
        {
            return false;
        }

        foreach (var value in parameters.Values)
        {
            if (value is UniqueValue unique && unique.Value.Length > 0 && name.Contains(unique.Value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
