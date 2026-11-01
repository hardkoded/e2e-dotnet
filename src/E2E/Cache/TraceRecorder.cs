// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;
using static E2E.Agent;

namespace E2E.Cache;

/// <summary>
/// Upstream's <c>TraceRecorder</c> (<c>cache/recorder.ts</c>): records every grammar action a step commits, with the target
/// described at commit time, the moment the harness resolved the node, so replay can find it again in a fresh look. A scroll
/// repeated in the same direction on the same target is one action that ran several times. Every string that could carry screen
/// content passes through the redactor as it is recorded. The port records no truncation flag, action cap, or summary, and keeps
/// the recorded actions in the order they ran for <see cref="Finalize"/>.
/// </summary>
/// <param name="redactor">The step's redactor, read at each record: a secret the step resolves late masks what it recorded after.</param>
/// <param name="time">The clock <see cref="LastActionAtMs"/> reads. No upstream counterpart: upstream reads <c>Date.now()</c>.</param>
internal sealed class TraceRecorder(Func<Redactor> redactor, TimeProvider? time = null)
{
    private readonly List<RecordedAction> _actions = [];
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private long? _lastActionAtMs;

    /// <summary>Number of actions recorded so far, gaps included.</summary>
    public int RecordedCount => _actions.Count;

    /// <summary>When the last action or gap was recorded, in Unix milliseconds; null before the first.</summary>
    public long? LastActionAtMs => _lastActionAtMs;

    /// <summary>The recorded actions in order. No upstream equivalent: upstream hands them over only in <see cref="Finalize"/>.</summary>
    public IReadOnlyList<RecordedAction> Actions => _actions;

    /// <summary>Records one committed grammar action on <paramref name="node"/>, described now. Returns the action as recorded.</summary>
    public RecordedAction Record(string kind, SemanticNode node, string? value = null, string? key = null)
    {
        var target = DescribeTarget(node, redactor());
        return Record(new RecordedAction { Kind = kind, Role = target.Role, Name = target.Name, TestId = target.TestId, Value = value, Key = key });
    }

    /// <summary>Records one committed grammar action as given, replayed ones included. Returns it.</summary>
    public RecordedAction Record(RecordedAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _actions.Add(action);
        _lastActionAtMs = _time.GetUtcNow().ToUnixTimeMilliseconds();
        return action;
    }

    /// <summary>
    /// Records one live scroll. A scroll that repeats the one before it, in the same direction on the same target, is one action
    /// that ran several times, so it folds into it. Upstream folds in its <c>push</c>; the port folds only live scrolls, since a
    /// replay commits the entry's own actions, which are already folded.
    /// </summary>
    public void RecordScroll(RecordedAction scroll)
    {
        ArgumentNullException.ThrowIfNull(scroll);
        if (_actions.Count > 0
            && _actions[^1] is { Kind: "scroll" } last
            && string.Equals(last.Direction, scroll.Direction, StringComparison.Ordinal)
            && string.Equals(last.Role, scroll.Role, StringComparison.Ordinal)
            && string.Equals(last.Name, scroll.Name, StringComparison.Ordinal)
            && string.Equals(last.TestId, scroll.TestId, StringComparison.Ordinal))
        {
            // The smallest coverage of the repeats decides the viewport fallback, so a list that shrank on the way is never
            // promoted by its first size.
            last.Times = (last.Times ?? 1) + 1;
            last.Spans = last.Spans is { } previous && scroll.Spans is { } current ? Math.Min(previous, current) : null;
            _lastActionAtMs = _time.GetUtcNow().ToUnixTimeMilliseconds();
            return;
        }

        _actions.Add(scroll);
        _lastActionAtMs = _time.GetUtcNow().ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// Records one mutating project-tool call as a gap. Replay ends here rather than skipping it: the grammar cannot reproduce the
    /// mutation, and a replay that silently dropped a state change would prove nothing.
    /// </summary>
    public RecordedAction RecordGap(string toolName) => Record(new RecordedAction { Kind = "tool", Text = redactor().Redact(toolName) });

    /// <summary>
    /// Forgets every recorded action. No upstream equivalent: a replay that misses before it ran an action leaves nothing to
    /// keep, and the port's replay commits through this recorder.
    /// </summary>
    public void Clear()
    {
        _actions.Clear();
        _lastActionAtMs = null;
    }

    /// <summary>
    /// Concludes recording into the entry of <paramref name="step"/>, or null when nothing was recorded: a step that committed no
    /// actions has no flow worth replaying. The texts are recorded raw, as the step saw them (the routes redacted), and the caller
    /// templates the finished entry with <see cref="CacheKeys.TemplateTrace"/>.
    /// </summary>
    public CacheEntry? Finalize(CacheEntry step, string route, string endRoute, List<RecordedTarget> appeared, List<RecordedTarget> gone, string? recordedBy = null, long? endWaitMs = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (_actions.Count == 0)
        {
            return null;
        }

        var entry = new CacheEntry { Schema = step.Schema };
        entry.SetStep(step);
        entry.RecordedBy = CacheEntry.ReadRecordedBy(recordedBy is null ? null : redactor().Redact(recordedBy));
        entry.Route = route;
        entry.EndRoute = endRoute;
        entry.Actions = [.. _actions];
        entry.Appeared = appeared;
        entry.Gone = gone;
        entry.EndWaitMs = endWaitMs is { } wait ? (int)Math.Min(Trace.MaxTraceEndWaitMs, Math.Max(0, wait)) : null;
        return entry;
    }

    /// <summary>
    /// Upstream's <c>describeTarget</c> (<c>agent/actions.ts</c>): how a replay will find the node again, by its role, its label,
    /// and its test id, redacted. Never the text the node holds or the value a field holds.
    /// </summary>
    public static RecordedTarget DescribeTarget(SemanticNode node, Redactor redactor)
    {
        ArgumentNullException.ThrowIfNull(node);
        return new RecordedTarget { Role = node.Role, Name = RedactLabel(node, redactor), TestId = Redact(node.TestId, redactor) };
    }
}
