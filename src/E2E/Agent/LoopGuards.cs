// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace E2E;

/// <summary>One tool result as the failure streak reads it (upstream's <c>GuardToolResult</c>). The loop knows whether a call failed, so the result carries no text to parse.</summary>
internal readonly record struct GuardToolResult(bool Failed);

/// <summary>
/// Guard thresholds (upstream's <c>LoopGuardThresholds</c>). Identical-call (period 1) counts are consecutive repetitions of one call;
/// cycle (period 2 to 4) counts are whole repetitions of the sequence. Each warns first, then forces the conclusion.
/// </summary>
/// <param name="RepeatWarn">Consecutive identical calls that warn.</param>
/// <param name="RepeatStop">Consecutive identical calls that force the conclusion.</param>
/// <param name="CycleWarn">Repetitions of a 2 to 4 call sequence that warn.</param>
/// <param name="CycleStop">Repetitions of a 2 to 4 call sequence that force the conclusion.</param>
/// <param name="FailureWarn">Consecutive failed tool results that warn.</param>
/// <param name="FailureStop">Consecutive failed tool results that force the conclusion.</param>
internal sealed record LoopGuardThresholds(int RepeatWarn, int RepeatStop, int CycleWarn, int CycleStop, int FailureWarn, int FailureStop);

/// <summary>
/// Detects a model going in circles: the same call again and again, a short sequence of calls
/// repeated, or a run of failed calls. Identity is the tool name plus its exact arguments, so two taps on different targets are
/// never a repeat. A warning lets the model react; a stop forces the verdict.
/// </summary>
internal static class LoopGuards
{
    private const int MaxCyclePeriod = 4;

    /// <summary>The thresholds the built-in agent ships with (upstream's <c>DEFAULT_LOOP_GUARD_THRESHOLDS</c>).</summary>
    public static readonly LoopGuardThresholds DefaultLoopGuardThresholds = new(RepeatWarn: 3, RepeatStop: 5, CycleWarn: 2, CycleStop: 3, FailureWarn: 3, FailureStop: 5);

    /// <summary>The calls of a transcript in order, without the verdict tool: ending the step is never a loop.</summary>
    public static IReadOnlyList<(string Name, string Input)> ExtractGuardCalls(IEnumerable<ModelMessage> messages) =>
        messages
            .Where(message => message.ToolCalls is not null)
            .SelectMany(message => message.ToolCalls!)
            .Where(call => !string.Equals(call.Name, "done", StringComparison.Ordinal))
            .Select(call => (call.Name, call.Arguments.GetRawText()))
            .ToList();

    /// <summary>
    /// Checks the trailing calls for exact repetition (period 1) and short exact cycles (periods 2 to 4).
    /// Returns no reason when the calls are clear.
    /// </summary>
    public static (bool Stop, string? Reason) CheckLoopGuards(IReadOnlyList<(string Name, string Input)> calls, LoopGuardThresholds? thresholds = null)
    {
        thresholds ??= DefaultLoopGuardThresholds;
        (bool Stop, string? Reason) warning = (false, null);
        for (var period = 1; period <= MaxCyclePeriod; period++)
        {
            var repeats = TrailingRepeats(calls, period);
            var (warnAt, stopAt) = period == 1 ? (thresholds.RepeatWarn, thresholds.RepeatStop) : (thresholds.CycleWarn, thresholds.CycleStop);
            if (repeats >= stopAt)
            {
                return (true, Describe(calls, period, repeats));
            }

            if (repeats >= warnAt && warning.Reason is null)
            {
                warning = (false, Describe(calls, period, repeats));
            }
        }

        return warning;
    }

    /// <summary>
    /// Checks the trailing run of failed results (upstream's <c>checkFailureStreak</c>). Different targets or inputs do not save a
    /// streak: five failures in a row say the approach is wrong, not that the last id was. Warns first, then forces the conclusion.
    /// Returns no reason when the results are clear.
    /// </summary>
    public static (bool Stop, string? Reason) CheckFailureStreak(IReadOnlyList<GuardToolResult> results, LoopGuardThresholds? thresholds = null)
    {
        thresholds ??= DefaultLoopGuardThresholds;
        var streak = 0;
        for (var index = results.Count - 1; index >= 0 && results[index].Failed; index--)
        {
            streak++;
        }

        var reason = "the last " + streak.ToString(CultureInfo.InvariantCulture) + " actions failed in a row";
        return streak >= thresholds.FailureStop ? (true, reason) : streak >= thresholds.FailureWarn ? (false, reason) : (false, null);
    }

    /// <summary>How many times the last <paramref name="period"/> calls repeat one after the other.</summary>
    private static int TrailingRepeats(IReadOnlyList<(string Name, string Input)> calls, int period)
    {
        if (calls.Count < period)
        {
            return 0;
        }

        var last = calls.Count - period;

        // A cycle of one call repeated is period 1's, and period 1 applies the stricter policy.
        if (period > 1 && Enumerable.Range(1, period - 1).All(index => calls[last + index] == calls[last]))
        {
            return 0;
        }

        var repeats = 1;
        for (var offset = period; last - offset >= 0; offset += period)
        {
            if (Enumerable.Range(0, period).Any(index => calls[last + index] != calls[last - offset + index]))
            {
                break;
            }

            repeats++;
        }

        return repeats;
    }

    private static string Describe(IReadOnlyList<(string Name, string Input)> calls, int period, int repeats)
    {
        var names = string.Join(", ", calls.Skip(calls.Count - period).Select(call => call.Name));
        var count = repeats.ToString(CultureInfo.InvariantCulture);
        return period == 1
            ? "the same \"" + names + "\" call with identical input was issued " + count + " times in a row"
            : "the same " + period.ToString(CultureInfo.InvariantCulture) + "-call sequence (" + names + ") repeated " + count + " times with identical inputs";
    }
}
