// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

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
        ActParams.Validate(options?.Params, "agent.act");
        var agent = _scope.Select(options?.Agent);
        return StepAsync(
            "agent.act",
            instruction,
            agent,
            verifies: false,
            () => DispatchStepAsync(
                new StepSpec
                {
                    Kind = ExecutorStepKind.Act,
                    Instruction = instruction,
                    Params = options?.Params,
                    Timeout = options?.Timeout,
                    MaxSteps = options?.MaxSteps,
                    MaxModelCalls = options?.MaxModelCalls,
                    OnAction = options?.OnAction,
                },
                agent,
                cancellationToken));
    }

    /// <summary>
    /// Runs one agent call as a recorded step of the attempt (upstream's <c>runtime.steps.run('agent', ...)</c>). A failed call keeps its
    /// explanation on the step, so the steps after it read why it ended. A judgment (assert, waitFor) is a verification step.
    /// </summary>
    private Task<T> StepAsync<T>(string api, string label, ResolvedAgent agent, bool verifies, Func<Task<T>> body) =>
        _scope.Steps.RunAsync(
            StepKind.Agent,
            api,
            label,
            async () =>
            {
                try
                {
                    return await body().ConfigureAwait(false);
                }
                catch (AgentException ex)
                {
                    _scope.Steps.AttachAgentDetails(new StepAgentDetails { Explanation = ex.VerdictSummary ?? ex.Message });
                    throw;
                }
            },
            new StepRunOptions { Verifies = verifies, Agent = agent.Name });

    /// <inheritdoc cref="StepAsync{T}(string, string, ResolvedAgent, bool, Func{Task{T}})"/>
    private Task StepAsync(string api, string label, ResolvedAgent agent, bool verifies, Func<Task> body) =>
        StepAsync(
            api,
            label,
            agent,
            verifies,
            async () =>
            {
                await body().ConfigureAwait(false);
                return true;
            });

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
                Params = options?.Params,
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

        // What every turn resends, which the observation is cut around: the context, the prior-step ledger, and the instruction.
        var ledger = E2E.Ledger.SerializeLedger(E2E.Ledger.ProjectPriorSteps(_scope.Steps.Completed()), E2E.Ledger.MaxLedgerBytes);
        var feed = new ObservationFeed(_scope.Session, TimeProvider.System)
        {
            Scope = _scope,
            Accounting = budget,
            Options = new ObservationFeedOptions(
                agent.MaxObservationBytes,
                agent.MaxInputTokens,
                ModelAdapter.TokenUpperBound(instruction),
                agent.Context is null ? 0 : ModelAdapter.TokenUpperBound(agent.Context),
                ledger.Bytes),
        };
        _feed = feed;
        var stepIndex = _scope.NextStepIndex();
        // An assert changes no state, so its secrets are named to the executor but never fillable.
        var declared = spec.Kind == ExecutorStepKind.Act ? ActParams.SecretsIn(spec.Params) : [];
        if (spec.Kind == ExecutorStepKind.Act)
        {
            _scope.Remember(spec.Params);
        }
        var dispatcher = new ActionDispatcher(_scope, feed, budget, () => declared, spec.OnAction);
        budget.Attach(_scope, timeout, outer.Token, stop, feed.ArmAfter, dispatcher.NotifyToolAsync);
        ActContext? context = null;
        StepTraceSession? session = null;
        try
        {
            // An act is cacheable unless its executor opted out. An assert must not change the app, so its recording would be empty.
            if (spec.Kind == ExecutorStepKind.Act && executor.Cache != ExecutorCache.Off && _scope.CacheEnabled && _scope.Cache is { } cache)
            {
                var key = _scope.ClaimKey(instruction, spec.Params, agent, out var callIndex, out var keyContext);
                session = new StepTraceSession(
                    new ActReplayHost(_scope, feed, budget, dispatcher, token),
                    new StepCacheOptions
                    {
                        Cache = cache,
                        Write = _scope.CacheWrite,
                        Strict = _scope.CacheStrict,
                        ReplayEligible = _scope.ReplayEligible,
                        Claim = new ClaimedKey(key, Provenance(instruction, spec.Params, callIndex, agent.Name, keyContext)),
                        StepIndex = _scope.Steps.CurrentStepIndex,
                        Instruction = instruction,
                        Params = spec.Params,
                        Recorder = budget.Recorder,
                        AppOrigin = _scope.AppOrigin,
                    });
                _scope.Acts.Add(session.Pending);
            }

            // The baseline a recording's delta is read from, the screen a replay starts on, and the built-in agent's first look:
            // a settled screen, as upstream's start capture is. An executor that brings its own eyes owes the cache nothing
            // when the screen cannot be read: it runs, and nothing is recorded.
            var replayed = session is null ? null : await BeginAsync(session).ConfigureAwait(false);
            var recorder = budget.Recorder;
            if (replayed is { } finished)
            {
                _scope.Steps.AttachAgentDetails(new StepAgentDetails { Explanation = finished.Summary });
                await session!.ConcludeAsync(StepOutcome.Passed).ConfigureAwait(false);
                return new ActResult { Summary = finished.Summary, Cache = session.CacheInfo, ModelCalls = 0, Actions = budget.Used };
            }

            var start = session?.Start;
            if (start is null && !custom)
            {
                start = await feed.ObserveAsync(SettleMode.HeldStill, token).ConfigureAwait(false);
            }

            // The screen the step began on is stale once a replay acted, so the executor takes a fresh look.
            var prefix = session?.ReplayedPrefix;
            context = new ActContext(_scope, feed, budget, dispatcher, spec, agent, stepIndex, declared, ledger, prefix is not null && recorder.RecordedCount > 0 ? null : start, prefix);
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

            verdict = ValidateVerdict(verdict, executor.Name);

            // Evidence for an assert is taken after the verdict, on a pass and on a failure alike.
            var kept = spec.Kind == ExecutorStepKind.Assert && spec.Screenshot != false && budget.HardStop is null
                ? await KeepBoundedEvidenceAsync(budget, outer.Token, cancellationToken).ConfigureAwait(false)
                : null;
            Settle(verdict, budget, spec, executor, kept, api, out var summary);
            if (summary.Length > 0)
            {
                _scope.Steps.AttachAgentDetails(new StepAgentDetails { Explanation = summary });
            }

            if (spec.Kind == ExecutorStepKind.Assert)
            {
                _scope.MarkVerified();
                return new ActResult { Summary = summary, ModelCalls = budget.ModelCalls, Actions = budget.Used };
            }

            if (session is not null)
            {
                await session.ConcludeAsync(StepOutcome.Passed, agent.Model?.Name).ConfigureAwait(false);
            }

            return new ActResult { Summary = summary, Cache = session?.CacheInfo, ModelCalls = budget.ModelCalls, Actions = budget.Used };
        }
        catch (Exception ex) when (budget.HardStop is { } hardStop && (ex is OperationCanceledException || (ex is EngineException { Code: EngineErrorCodes.Cancelled })))
        {
            // The clock or a cancel ended the step while it looked at the screen or replayed a recording.
            throw hardStop;
        }
        finally
        {
            budget.Close();
            _scope.Steps.AttachAgentDetails(new StepAgentDetails
            {
                Metrics = new StepMetrics
                {
                    ModelCalls = budget.ModelCalls,
                    ActionSteps = budget.Used,
                    ContextBytes = agent.Context is null ? 0 : System.Text.Encoding.UTF8.GetByteCount(agent.Context),
                    LedgerBytes = context?.LedgerBytes ?? 0,
                    ObservationBytes = feed.MaxObservationBytes,
                },
                Cache = session?.CacheInfo,
                Turns = context?.Turns,
                ObservationTruncated = feed.ReportDetails().ObservationTruncated,
                ObservationRevision = feed.Latest?.Revision is { Length: > 0 } revision ? revision : null,
            });
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

    // Opens the step's cache session, and counts how the cache took it, even when strict mode ends the step there.
    private async Task<StepVerdict?> BeginAsync(StepTraceSession session)
    {
        try
        {
            return await session.BeginAsync().ConfigureAwait(false);
        }
        finally
        {
            switch (session.CacheInfo?.Mode)
            {
                case "missed":
                    _scope.Missed++;
                    break;
                case "agent-concluded":
                    _scope.HandedOff++;
                    break;
                case "self-finalized":
                    _scope.Replayed++;
                    break;
            }
        }
    }

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
    private static void Settle(StepVerdict verdict, StepAccounting budget, StepSpec spec, IStepExecutor executor, string? screenshot, string api, out string summary)
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

            // The test's own hook failed the step: its error stands, so the recording is neither kept nor evicted for it.
            if (stop.FromActionHook)
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
            throw new AgentException(code!, api + " blocked: " + summary, blocked: true) { Screenshot = screenshot, VerdictSummary = summary };
        }

        var failure = code is not null && StepExecutors.AgentCodes.Contains(code) ? code : spec.Kind == ExecutorStepKind.Assert ? "ASSERTION_FAILED" : "ACTION_FAILED";
        throw new AgentException(failure, api + " failed: " + summary) { Screenshot = screenshot, VerdictSummary = summary };
    }

    /// <summary>
    /// The step an entry is recorded for, as the entry stores it. None of it is replay input, so a
    /// secret value in the test title, instruction, or agent name is masked like any other recorded string.
    /// </summary>
    private CacheEntry Provenance(string instruction, IReadOnlyDictionary<string, object?>? parameters, int callIndex, string agent, TraceKeyContext keyContext)
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
            KeyedBy = keyContext,
        };
    }
}
