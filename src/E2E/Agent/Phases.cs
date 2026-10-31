// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using E2E.Engine;
using E2E.Internal;
using PolicyDecision = E2E.Reports.PolicyDecision;

namespace E2E;

/// <summary>
/// Shared per-phase machinery for agent step execution (upstream <c>agent/phases.ts</c>). The observe and action phases of every agent
/// step go through <see cref="InstrumentPhaseAsync{T}"/>, so the activity a step announces, its child events, and the
/// <c>E2E_DEBUG</c> buckets cannot drift apart between the paths that run them.
/// </summary>
internal static class Phases
{
    public const string Observe = "agent.observe";

    public const string Model = "agent.model";

    public const string Action = "agent.action";

    /// <summary>
    /// Fails when the step clock has run out (upstream's <c>checkStepClock</c>). <paramref name="cause"/> carries the failure that was
    /// being handled when the clock was found to be out, so a step that timed out mid-operation still says what the operation reported,
    /// and decides the clock question when that failure is an operation timeout within one poll tick of the deadline:
    /// a bounded operation was given the deadline's remaining time, so its timer and the deadline mark one instant on two clocks, and
    /// running out of that budget is the step running out. An operation that times out on its own, with time to spare, keeps its code.
    /// </summary>
    /// <param name="signal">The cancellation of the step; a cancelled one fails the check as <c>CANCELLED</c>.</param>
    /// <param name="deadline">The step's deadline.</param>
    /// <param name="api">The call the step is, which the message names.</param>
    /// <param name="timeoutMs">The step's timeout, which the message names.</param>
    /// <param name="cause">The failure being handled, if any.</param>
    /// <param name="clock">The clock the deadline reads. .NET-only: the port's <see cref="Deadline"/> takes its time as an argument.</param>
    public static void CheckStepClock(CancellationToken signal, Deadline deadline, string api, long timeoutMs, Exception? cause = null, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(deadline);
        if (signal.IsCancellationRequested)
        {
            throw cause is null ? new AgentException("CANCELLED", api + " was cancelled") : new AgentException("CANCELLED", api + " was cancelled", cause);
        }

        var now = (clock ?? TimeProvider.System).GetUtcNow();
        if (deadline.Expired(now) || TimedOutAtDeadline(cause, deadline, now))
        {
            var message = api + " exceeded its " + timeoutMs.ToString(CultureInfo.InvariantCulture) + " ms timeout";
            throw cause is null ? new AgentException("STEP_TIMEOUT", message, blocked: true) : new AgentException("STEP_TIMEOUT", message, blocked: true, cause);
        }
    }

    // True when the cause is an operation timeout and the deadline is less than one poll tick away.
    private static bool TimedOutAtDeadline(Exception? cause, Deadline deadline, DateTimeOffset now) =>
        deadline.Remaining(now) < Time.PollInterval && cause is EngineException { Code: EngineErrorCodes.OperationTimeout };

    /// <summary>
    /// Captures one raw observation, re-capturing while the engine reports a retryable failure and the step clock allows (upstream's
    /// <c>retryingObserve</c>). A screen that navigates as it is read makes the capture lose its document; that is a race, not a broken
    /// app, so it is re-read rather than surfaced as a failed call. <paramref name="guard"/> is the caller's clock check, so a capture
    /// that outlives the deadline reports the step's own timeout rather than whichever transport error the truncated budget produced.
    /// </summary>
    public static async Task<Observation> RetryingObserveAsync(Func<Task<Observation>> capture, Action<Exception?> guard, CancellationToken signal)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(guard);
        while (true)
        {
            try
            {
                return await capture().ConfigureAwait(false);
            }
            catch (Exception cause)
            {
                guard(cause);
                if (cause is not EngineException { Retryable: true })
                {
                    throw;
                }

                await Task.Delay(Time.PollInterval, signal).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Records one policy decision as a child event of the current step (upstream's <c>recordPolicyEvent</c>).</summary>
    public static void RecordPolicyEvent(StepRecorder? steps, string name, PolicyDecision decision, string? code = null) =>
        steps?.RecordEvent(new StepEvent
        {
            Kind = StepEventKind.Policy,
            StartedAt = DateTimeOffset.UtcNow,
            DurationMs = 0,
            Status = decision == PolicyDecision.Allowed ? StepEventStatus.Passed : StepEventStatus.Failed,
            Name = name,
            Decision = decision,
            Code = code,
        });

    /// <summary>The live activity a phase announces as it begins (upstream's <c>ACTIVITY_BY_PHASE</c>).</summary>
    private static StepActivity? ActivityOf(string phase) => phase switch
    {
        Observe => StepActivity.Observe,
        Model => StepActivity.Model,
        Action => StepActivity.Action,
        _ => null,
    };

    /// <summary>
    /// Runs one phase with uniform accounting: the activity announced as it begins, a child event on success and failure, and one
    /// debug bucket. A failed phase records the code of the error and what it said, and rethrows it.
    /// </summary>
    public static async Task<T> InstrumentPhaseAsync<T>(PhaseHost host, PhaseSpec spec, Func<Task<T>> body, Func<T, PhaseDetail>? detail = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(body);
        var startedAt = DateTimeOffset.UtcNow;
        var started = Stopwatch.GetTimestamp();
        if (ActivityOf(spec.Phase) is { } activity)
        {
            host.Steps?.Activity(activity);
        }

        try
        {
            var value = await body().ConfigureAwait(false);

            // What the event says is a nicety: a callback that fails must not fail a phase that succeeded.
            PhaseDetail? told = null;
            try
            {
                told = detail?.Invoke(value);
            }
            catch (Exception)
            {
            }

            host.Steps?.RecordEvent(new StepEvent
            {
                Kind = spec.Kind,
                StartedAt = startedAt,
                DurationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                Status = StepEventStatus.Passed,
                Name = spec.Name,
                Count = told?.Count,
                Bytes = told?.Bytes,
                Detail = told?.Detail,
            });
            return value;
        }
        catch (Exception cause)
        {
            var (_, code, message, _) = ErrorSerializer.Classify(cause);
            var first = message.Split('\n')[0].Trim();
            host.Steps?.RecordEvent(new StepEvent
            {
                Kind = spec.Kind,
                StartedAt = startedAt,
                DurationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                Status = string.Equals(code, ErrorSerializer.Cancelled, StringComparison.Ordinal) ? StepEventStatus.Cancelled : StepEventStatus.Failed,
                Name = spec.Name,

                // The runner's or the engine's code, or the error's type for a failure of neither.
                Code = string.Equals(code, "ERROR", StringComparison.Ordinal) ? cause.GetType().Name : code,

                // What went wrong, not only its code: the tool result the model read said it too.
                Detail = first.Length == 0 ? null : (spec.Name is null ? "" : spec.Name + ": ") + first,
            });
            throw;
        }
        finally
        {
            host.Debug.Record(spec.Phase, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}

/// <summary>The step recorder and debug trace a phase reports into (upstream's <c>PhaseHost</c>); no recorder when the work belongs to no attempt.</summary>
internal sealed record PhaseHost(StepRecorder? Steps, DebugTrace Debug);

/// <summary>One instrumented phase: the event kind it records, the debug bucket it feeds, and its name (upstream's <c>PhaseSpec</c>, without the <c>api</c> it never reads).</summary>
internal sealed record PhaseSpec(StepEventKind Kind, string Phase, string? Name = null);

/// <summary>What a phase's event says about its value (upstream's <c>detail</c> of <c>instrumentPhase</c>, a <c>Partial&lt;StepEvent&gt;</c>). .NET-only name: the event's required members are the phase's own.</summary>
internal sealed record PhaseDetail(int? Count = null, int? Bytes = null, string? Detail = null);
