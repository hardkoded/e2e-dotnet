// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using E2E.Internal;

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
            var told = detail?.Invoke(value);
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
