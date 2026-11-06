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
    public RecordedAction Record(string kind, SemanticNode node, string? value = null, string? key = null, string? within = null, TracePosition? position = null)
    {
        var action = ActionOf(kind, DescribeTarget(node, redactor(), within, position));
        action.Value = value;
        action.Key = key;
        return Record(action);
    }

    /// <summary>
    /// Records one committed <c>tapAt</c> or <c>hoverAt</c> (upstream's <c>record</c> of a point action): the point and the viewport it was placed in,
    /// and, when a node with a durable descriptor contained it, its place inside that node's box. Returns the action as recorded.
    /// </summary>
    public RecordedAction Record(string kind, PlacedPoint point, TraceViewport viewport)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(viewport);
        return Record(new RecordedAction { Kind = kind, Point = point.Point, Viewport = viewport, Under = WithinOf(point) });
    }

    /// <summary>Records one committed <c>dragAt</c> (upstream's <c>record</c> of a <c>dragAt</c>): both points with the viewport, each placed inside its node.</summary>
    public RecordedAction Record(PlacedPoint from, PlacedPoint to, TraceViewport viewport)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(viewport);
        return Record(new RecordedAction
        {
            Kind = "dragAt",
            Viewport = viewport,
            From = new RecordedPointEnd { Point = from.Point, Under = WithinOf(from) },
            To = new RecordedPointEnd { Point = to.Point, Under = WithinOf(to) },
        });
    }

    /// <summary>
    /// Upstream's <c>withinOf</c>: a bare point's place inside the box of the listed node that contained it. The point replays as given on a
    /// same-sized viewport; when a node with a durable descriptor contained it, its place inside that node's box lets replay follow the node
    /// instead. An anonymous container, a group among groups on a screen merged into one accessibility node, could not be re-found and is not
    /// kept: the point alone is the honest record. The twin position the node sat at among look-alikes is kept with the container key.
    /// </summary>
    private RecordedPointPlace? WithinOf(PlacedPoint placed)
    {
        if (placed.Under is not { Rect: { Width: > 0, Height: > 0 } box } under)
        {
            return null;
        }

        var target = DescribeTarget(under, redactor(), placed.Within, placed.Position);
        if (target.Name is null && target.TestId is null)
        {
            return null;
        }

        return new RecordedPointPlace { Target = target, Fx = Fraction((placed.Point.X - box.X) / box.Width), Fy = Fraction((placed.Point.Y - box.Y) / box.Height) };
    }

    /// <summary>Upstream's <c>fraction</c>: a place inside a box as a fraction of its side, clamped to the box and rounded so the entry stays small.</summary>
    private static double Fraction(double value) => Math.Round(Math.Clamp(value, 0, 1) * 10_000, MidpointRounding.AwayFromZero) / 10_000;

    /// <summary>
    /// An action on <paramref name="target"/>, not yet recorded: the descriptor's fields flat on the action. No upstream counterpart: upstream
    /// nests the descriptor as <c>target</c>; the port's actions carry its fields (.NET-only shape, kept so stored recordings stay readable).
    /// </summary>
    internal static RecordedAction ActionOf(string kind, RecordedTarget target) => new()
    {
        Kind = kind,
        Role = target.Role,
        Name = target.Name,
        TestId = target.TestId,
        Within = target.Within,
        Text = target.Text,
        Placeholder = target.Placeholder,
        InputPurpose = target.InputPurpose,
        Position = target.Position,
    };

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

    /// <summary>The summary a run-time value gap records (upstream's <c>DERIVED_GAP_SUMMARY</c>, <c>tool type (run-time value)</c>), as the text after <c>tool</c>.</summary>
    private const string DerivedGapSummary = "type (run-time value)";

    /// <summary>
    /// Upstream's <c>recordDerivedGap</c>: records a fill whose value was this run's data as a gap, with the rule that said so
    /// (<c>agent/derived.ts</c>). Replay hands the step over before it, so the executor derives the value afresh, and the report can say why.
    /// </summary>
    public RecordedAction RecordDerivedGap(DerivedReason reason) => Record(new RecordedAction { Kind = "tool", Text = DerivedGapSummary, Derived = reason });

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
    /// its text when that differs from the label, its test id, its placeholder, and its input purpose, redacted. Never the value a field
    /// holds. <paramref name="within"/> is the key of the container it sat in and <paramref name="position"/> its place among its twins
    /// (upstream's <c>describePlaced</c>); the key is redacted again and bounded.
    /// </summary>
    public static RecordedTarget DescribeTarget(SemanticNode node, Redactor redactor, string? within = null, TracePosition? position = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        var name = RedactLabel(node, redactor);
        // The label of a node with no name is its text (upstream records that text as the name's absence and a text), so a text is a second
        // field only beside a name of its own, and only when it says something else.
        var text = node.Name is null ? null : NonEmpty(RedactField(node.Text, ObservationLimits.Text, redactor));
        return new RecordedTarget
        {
            Role = node.Role is null ? null : AgentObservation.RedactText(node.Role, redactor),
            Name = name,
            Text = text == name ? null : text,
            TestId = Redact(node.TestId, redactor),
            Placeholder = NonEmpty(RedactField(node.Placeholder, ObservationLimits.Text, redactor)),
            InputPurpose = node.InputPurpose is null or "none" ? null : node.InputPurpose,
            Within = within is null ? null : TextRules.Bound(redactor.Redact(within), AgentActions.MaxWithinChars),
            Position = position,
        };
    }

    private static string? NonEmpty(string? text) => string.IsNullOrEmpty(text) ? null : text;
}
