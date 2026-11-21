// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.RegularExpressions;

namespace E2E;

/// <summary>One tool result as the guard reads it (upstream's <c>GuardToolResult</c>): the text the model saw, and whether it reports a failure.</summary>
internal readonly record struct GuardToolResult(string Text, bool Failed);

/// <summary>One tool call as identity (upstream's <c>GuardToolCall</c>): the name plus its exact serialized input.</summary>
/// <param name="ToolName">The tool called.</param>
/// <param name="Input">The call's input, serialized.</param>
/// <param name="SawChange">Set on a call to a waiting tool whose result showed the listing or the tabs change; not part of the identity.</param>
internal readonly record struct GuardToolCall(string ToolName, string Input, bool SawChange = false);

/// <summary>What a loop guard decided (upstream's <c>LoopGuardVerdict</c> kinds).</summary>
internal enum LoopGuardKind
{
    /// <summary>Nothing to say.</summary>
    Clear,

    /// <summary>Tell the model it is going in circles.</summary>
    Warn,

    /// <summary>Force the verdict.</summary>
    Stop,
}

/// <summary>A loop guard's decision (upstream's <c>LoopGuardVerdict</c>).</summary>
/// <param name="Kind">Whether the calls are clear, warned about, or stopped.</param>
/// <param name="Reason">Why, for a warning or a stop.</param>
/// <param name="Waiting">True when the calls are looks of the waiting tools (the built-in observe and screenshot): a wait, not yet a circle.</param>
/// <param name="Key">Names the wait a warning belongs to, so each wait in a step is warned once; the reason stays the same.</param>
internal sealed record LoopGuardVerdict(
    LoopGuardKind Kind,
    string? Reason = null,
    bool Waiting = false,
    string? Key = null
)
{
    /// <summary>The verdict for calls that need no notice.</summary>
    public static LoopGuardVerdict Clear { get; } = new(LoopGuardKind.Clear);
}

/// <summary>
/// Guard thresholds (upstream's <c>LoopGuardThresholds</c>). Identical-call (period 1) counts are consecutive repetitions of one call;
/// cycle (period 2 to 4) counts are whole repetitions of the sequence. Each warns first, then forces the conclusion.
/// </summary>
/// <param name="RepeatWarn">Consecutive identical calls that warn.</param>
/// <param name="RepeatStop">Consecutive identical calls that force the conclusion.</param>
/// <param name="ObserveRepeatWarn">
/// Calls in a row to waiting tools (the built-in <c>observe</c> and plain <c>screenshot</c>), in any order, since the last one whose result showed
/// a change that warn: reading the screen again while a save or a load is still in progress is waiting, not a loop, so it gets more room than an action.
/// </param>
/// <param name="ObserveRepeatStop">The same run that forces the conclusion.</param>
/// <param name="WaitingWarn">Calls in a row to waiting tools however many showed a change that warn: a screen that ticks forever still ends the wait.</param>
/// <param name="WaitingStop">The same run that forces the conclusion.</param>
/// <param name="CycleWarn">Repetitions of a 2 to 4 call sequence that warn.</param>
/// <param name="CycleStop">Repetitions of a 2 to 4 call sequence that force the conclusion.</param>
/// <param name="FailureWarn">Consecutive failed tool results that warn.</param>
/// <param name="FailureStop">Consecutive failed tool results that force the conclusion.</param>
internal sealed record LoopGuardThresholds(
    int RepeatWarn,
    int RepeatStop,
    int ObserveRepeatWarn,
    int ObserveRepeatStop,
    int WaitingWarn,
    int WaitingStop,
    int CycleWarn,
    int CycleStop,
    int FailureWarn,
    int FailureStop
);

/// <summary>
/// Detects a model going in circles: the same call again and again, a short sequence of calls
/// repeated, or a run of failed calls. Identity is the tool name plus its exact arguments, so two taps on different targets are
/// never a repeat. A warning lets the model react; a stop forces the verdict.
/// </summary>
internal static partial class LoopGuards
{
    private const int MaxCyclePeriod = 4;

    /// <summary>The thresholds the built-in agent ships with (upstream's <c>DEFAULT_LOOP_GUARD_THRESHOLDS</c>).</summary>
    public static readonly LoopGuardThresholds DefaultLoopGuardThresholds = new(
        RepeatWarn: 3,
        RepeatStop: 5,
        ObserveRepeatWarn: 6,
        ObserveRepeatStop: 10,
        WaitingWarn: 12,
        WaitingStop: 20,
        CycleWarn: 2,
        CycleStop: 3,
        FailureWarn: 3,
        FailureStop: 5
    );

    // Set on the handler of a tool that only waits on the screen; see ObserveRepeatWarn. The mark is on the handler, not the tool: a caller's
    // own tool under the same name, or the built-in tool with its handler replaced, may write and keeps the ordinary thresholds. The marks
    // live in a process-wide slot, not a static (upstream's registry symbol): a custom executor can get the built-in tools from another
    // installed copy of this assembly, and the table is a framework type both copies share.
    private static readonly E2E.Internal.RealmSlot<System.Runtime.CompilerServices.ConditionalWeakTable<
        object,
        object
    >> WaitingHandlerSlot = new(E2E.Internal.Brands.WaitingHandlerBrand);

    private static System.Runtime.CompilerServices.ConditionalWeakTable<object, object> WaitingHandler()
    {
        lock (E2E.Internal.RealmSlot<System.Runtime.CompilerServices.ConditionalWeakTable<object, object>>.Gate)
        {
            var table = WaitingHandlerSlot.Get();
            if (table is null)
            {
                table = new System.Runtime.CompilerServices.ConditionalWeakTable<object, object>();
                WaitingHandlerSlot.Set(table);
            }

            return table;
        }
    }

    // The waiting notices' reasons: the same on every turn of a wait, so each wait is warned once.
    private const string WaitingReason = "the screen was looked at several times in a row with no change";
    private const string TickingReason = "the screen was looked at many times in a row while it kept changing";

    /// <summary>
    /// Marks a tool as one that only reads the screen again, so a run of calls to it gets the waiting thresholds (upstream's <c>markWaitingTool</c>).
    /// Only the built-in <c>observe</c> and the plain <c>screenshot</c> (not the variant that saves a file) are marked.
    /// </summary>
    public static ToolLoopTool MarkWaitingTool(ToolLoopTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        WaitingHandler().AddOrUpdate(tool.ExecuteAsync, tool.ExecuteAsync);
        return tool;
    }

    /// <summary>The names under which a tool set offers a tool whose handler <see cref="MarkWaitingTool"/> marked (upstream's <c>waitingToolNames</c>).</summary>
    public static IReadOnlySet<string> WaitingToolNames(IEnumerable<ToolLoopTool> tools) =>
        tools
            .Where(tool => WaitingHandler().TryGetValue(tool.ExecuteAsync, out _))
            .Select(tool => tool.Definition.Name)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// The calls of a transcript in order, without the verdict tool: ending the step is never a loop (upstream's <c>extractGuardCalls</c>).
    /// A call to a <paramref name="waiting"/> tool whose result reported a change on screen carries <see cref="GuardToolCall.SawChange"/>.
    /// </summary>
    /// <param name="messages">The history the model reads.</param>
    /// <param name="concludeToolName">The verdict tool, whose calls are skipped.</param>
    /// <param name="waiting">The tools that only look.</param>
    /// <param name="seen">What each look's result showed, by call id, read once when the result was new and kept past compaction.</param>
    /// <param name="resultMessages">Where the looks' results are read: the calls come from the history the model reads, the results may come from an earlier, uncut one.</param>
    public static IReadOnlyList<GuardToolCall> ExtractGuardCalls(
        IEnumerable<ModelMessage> messages,
        string concludeToolName,
        IReadOnlySet<string>? waiting = null,
        Dictionary<string, bool>? seen = null,
        IEnumerable<ModelMessage>? resultMessages = null
    )
    {
        var moved =
            waiting is null || waiting.Count == 0
                ? []
                : WaitsThatSawChange(resultMessages ?? messages, waiting, seen ?? []);
        return messages
            .Where(message => message.ToolCalls is not null)
            .SelectMany(message => message.ToolCalls!)
            .Where(call => !string.Equals(call.Name, concludeToolName, StringComparison.Ordinal))
            .Select(call => new GuardToolCall(call.Name, call.Arguments.GetRawText(), moved.Contains(call.Id)))
            .ToList();
    }

    // The call ids of waiting tools whose result reported a change on screen. A result is read once and its answer kept in `seen`: transcript
    // compaction later elides old full screens, lead line and all, and a look that showed the listing come back must stay one.
    private static HashSet<string> WaitsThatSawChange(
        IEnumerable<ModelMessage> messages,
        IReadOnlySet<string> waiting,
        Dictionary<string, bool> seen
    )
    {
        var moved = new HashSet<string>(StringComparer.Ordinal);
        foreach (var message in messages)
        {
            if (
                !string.Equals(message.Role, "tool", StringComparison.Ordinal)
                || message.Name is null
                || message.ToolCallId is null
                || !waiting.Contains(message.Name)
            )
            {
                continue;
            }

            if (!seen.TryGetValue(message.ToolCallId, out var changed))
            {
                changed = ScreenPresenter.ReportsScreenChange(message.Content ?? "");
                seen[message.ToolCallId] = changed;
            }

            if (changed)
            {
                moved.Add(message.ToolCallId);
            }
        }

        return moved;
    }

    /// <summary>
    /// Checks the trailing calls for exact repetition (period 1) and short exact cycles (periods 2 to 4). Identity includes the serialized
    /// input, so two taps on different targets never count as a repeat, only literally re-issuing the same work does. A trailing run of calls to
    /// the <paramref name="waiting"/> tools is a wait and is counted on its own, in calls and in any order; a window with any other call in it
    /// gets the ordinary thresholds.
    /// </summary>
    public static LoopGuardVerdict CheckLoopGuards(
        IReadOnlyList<GuardToolCall> calls,
        LoopGuardThresholds? thresholds = null,
        IReadOnlySet<string>? waiting = null
    )
    {
        thresholds ??= DefaultLoopGuardThresholds;
        LoopGuardVerdict? warning = null;
        var (looks, unchanged) = TrailingWait(calls, waiting);
        if (looks >= thresholds.WaitingStop)
        {
            return new LoopGuardVerdict(
                LoopGuardKind.Stop,
                "the screen was looked at "
                    + looks.ToString(CultureInfo.InvariantCulture)
                    + " times in a row while it kept changing"
            );
        }

        if (unchanged >= thresholds.ObserveRepeatStop)
        {
            return new LoopGuardVerdict(
                LoopGuardKind.Stop,
                "the screen was looked at "
                    + unchanged.ToString(CultureInfo.InvariantCulture)
                    + " times in a row with no change"
            );
        }

        // Keyed by where the wait or its unchanged run began: a later wait in the step is warned again.
        var started = calls.Count - looks;
        if (unchanged >= thresholds.ObserveRepeatWarn)
        {
            warning = new LoopGuardVerdict(
                LoopGuardKind.Warn,
                WaitingReason,
                Waiting: true,
                Key: "wait@" + (calls.Count - unchanged).ToString(CultureInfo.InvariantCulture)
            );
        }
        else if (looks >= thresholds.WaitingWarn)
        {
            warning = new LoopGuardVerdict(
                LoopGuardKind.Warn,
                TickingReason,
                Waiting: true,
                Key: "ticking@" + started.ToString(CultureInfo.InvariantCulture)
            );
        }

        for (var period = 1; period <= MaxCyclePeriod; period++)
        {
            // A window of waiting calls alone is the wait above, not a circle.
            if (period <= looks)
            {
                continue;
            }

            var repeats = TrailingRepeats(calls, period);
            var (warnAt, stopAt) =
                period > 1
                    ? (thresholds.CycleWarn, thresholds.CycleStop)
                    : (thresholds.RepeatWarn, thresholds.RepeatStop);
            if (repeats >= stopAt)
            {
                return new LoopGuardVerdict(LoopGuardKind.Stop, Describe(calls, period, repeats));
            }

            if (repeats >= warnAt && warning is null)
            {
                warning = new LoopGuardVerdict(LoopGuardKind.Warn, Describe(calls, period, repeats));
            }
        }

        return warning ?? LoopGuardVerdict.Clear;
    }

    // The trailing run of calls to waiting tools: how many there are, and how many came after the last one that showed a change.
    private static (int Looks, int Unchanged) TrailingWait(
        IReadOnlyList<GuardToolCall> calls,
        IReadOnlySet<string>? waiting
    )
    {
        var looks = 0;
        int? unchanged = null;
        for (var index = calls.Count - 1; index >= 0; index--)
        {
            var call = calls[index];
            if (waiting is null || !waiting.Contains(call.ToolName))
            {
                break;
            }

            if (call.SawChange && unchanged is null)
            {
                unchanged = looks;
            }

            looks++;
        }

        return (looks, unchanged ?? looks);
    }

    /// <summary>
    /// Extracts every tool result in a transcript, in order (upstream's <c>extractToolResults</c>). A text result is read for the failure shape.
    /// A call the SDK refused before dispatch (a tool the step does not offer, an input outside the closed schema) or a tool that threw is a
    /// failure by construction: nothing ran, and the turn is spent. The conclusion tool's own text (an accepted verdict, a blocked one sent
    /// back for its code) is not an action and is skipped, but a refused conclusion counts: the repeat guard never sees conclusion calls, so
    /// this streak is the only bound short of the turn budget on a model that resends one the schema turns away.
    /// </summary>
    public static IReadOnlyList<GuardToolResult> ExtractToolResults(
        IEnumerable<ModelMessage> messages,
        string concludeToolName
    )
    {
        var results = new List<GuardToolResult>();
        foreach (var message in messages)
        {
            if (
                !string.Equals(message.Role, "tool", StringComparison.Ordinal)
                || message.Name is null
                || message.Content is null
            )
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
    public static (bool Stop, string? Reason) CheckFailureStreak(
        IReadOnlyList<GuardToolResult> results,
        LoopGuardThresholds? thresholds = null
    )
    {
        thresholds ??= DefaultLoopGuardThresholds;
        var streak = 0;
        for (var index = results.Count - 1; index >= 0 && results[index].Failed; index--)
        {
            streak++;
        }

        var reason = "the last " + streak.ToString(CultureInfo.InvariantCulture) + " actions failed in a row";
        return streak >= thresholds.FailureStop ? (true, reason)
            : streak >= thresholds.FailureWarn ? (false, reason)
            : (false, null);
    }

    /// <summary>How many times the last <paramref name="period"/> calls repeat one after the other.</summary>
    private static int TrailingRepeats(IReadOnlyList<GuardToolCall> calls, int period)
    {
        if (calls.Count < period)
        {
            return 0;
        }

        var last = calls.Count - period;

        // A cycle of one call repeated is period 1's, and period 1 applies the stricter policy.
        if (period > 1 && Enumerable.Range(1, period - 1).All(index => Same(calls[last + index], calls[last])))
        {
            return 0;
        }

        var repeats = 1;
        for (var offset = period; last - offset >= 0; offset += period)
        {
            if (Enumerable.Range(0, period).Any(index => !Same(calls[last + index], calls[last - offset + index])))
            {
                break;
            }

            repeats++;
        }

        return repeats;
    }

    // Identity is the name and the exact input; SawChange is not part of it.
    private static bool Same(GuardToolCall left, GuardToolCall right) =>
        string.Equals(left.ToolName, right.ToolName, StringComparison.Ordinal)
        && string.Equals(left.Input, right.Input, StringComparison.Ordinal);

    private static string Describe(IReadOnlyList<GuardToolCall> calls, int period, int repeats)
    {
        var names = string.Join(", ", calls.Skip(calls.Count - period).Select(call => call.ToolName));
        var count = repeats.ToString(CultureInfo.InvariantCulture);
        return period == 1
            ? "the same \"" + names + "\" call with identical input was issued " + count + " times in a row"
            : "the same "
                + period.ToString(CultureInfo.InvariantCulture)
                + "-call sequence ("
                + names
                + ") repeated "
                + count
                + " times with identical inputs";
    }
}
