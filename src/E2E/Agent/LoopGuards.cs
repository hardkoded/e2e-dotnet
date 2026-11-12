// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.RegularExpressions;

namespace E2E;

/// <summary>One tool result as the guard reads it (upstream's <c>GuardToolResult</c>): the text the model saw, and whether it reports a failure.</summary>
internal readonly record struct GuardToolResult(string Text, bool Failed);

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
internal static partial class LoopGuards
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
    /// Extracts every tool result in a transcript, in order (upstream's <c>extractToolResults</c>). A text result is read for the failure shape.
    /// A call the SDK refused before dispatch (a tool the step does not offer, an input outside the closed schema) or a tool that threw is a
    /// failure by construction: nothing ran, and the turn is spent. The conclusion tool's own text (an accepted verdict, a blocked one sent
    /// back for its code) is not an action and is skipped, but a refused conclusion counts: the repeat guard never sees conclusion calls, so
    /// this streak is the only bound short of the turn budget on a model that resends one the schema turns away.
    /// </summary>
    public static IReadOnlyList<GuardToolResult> ExtractToolResults(IEnumerable<ModelMessage> messages, string concludeToolName)
    {
        var results = new List<GuardToolResult>();
        foreach (var message in messages)
        {
            if (!string.Equals(message.Role, "tool", StringComparison.Ordinal) || message.Name is null || message.Content is null)
            {
                continue;
            }

            if (message.ErrorText)
            {
                results.Add(new GuardToolResult(message.Content, Failed: true));
                continue;
            }

            if (string.Equals(message.Name, concludeToolName, StringComparison.Ordinal))
            {
                continue;
            }

            results.Add(new GuardToolResult(message.Content, IsFailedResult(message.Name, message.Content)));
        }

        return results;
    }

    /// <summary>
    /// Whether a tool result reports its action failing (upstream's <c>isFailedResult</c>), by the first line's shape: <c>Action failed: …</c>
    /// and <c>Tool "x" failed: …</c> for any tool; for a grammar tool also its own failure lead, the tool's name and what it was aimed at
    /// before <c>failed:</c> (<c>tap #n6 failed: …</c>, <c>tap_at (30, 30) failed: …</c>). A grammar success leads with a capital, so text the
    /// model typed into one never reads as a failure, and a project tool's own text (<c>build failed: 2 red</c>) is its result, not a failed call.
    /// </summary>
    public static bool IsFailedResult(string toolName, string text)
    {
        var first = text.Split('\n', 2)[0];
        if (ActionFailedLead().IsMatch(first))
        {
            return true;
        }

        return ActionNames.GrammarToolNames.Contains(toolName) && GrammarFailedLead().IsMatch(first);
    }

    [GeneratedRegex("^(?:Action |Tool \"[^\"]*\" )failed: ", RegexOptions.CultureInvariant)]
    private static partial Regex ActionFailedLead();

    [GeneratedRegex("^[a-z_]+(?: .*)? failed: ", RegexOptions.CultureInvariant)]
    private static partial Regex GrammarFailedLead();

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
