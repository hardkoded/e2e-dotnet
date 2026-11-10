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
/// content passes through the redactor as it is recorded. Replay inputs are verbatim or nothing: a value the redactor would alter, or
/// one over <see cref="Trace.MaxTraceInputChars"/>, poisons the trace (<see cref="CacheEntry.Truncated"/>) instead of being bent to fit, and so does an
/// action past the action cap and a target no replay could re-find. A poisoned trace still documents what happened; it never replays.
/// Each action keeps the readable summary <see cref="AgentActions.DescribeAction"/> gives it, redacted and bounded, and a
/// summary never poisons the trace. The port keeps the recorded actions in the order they ran for <see cref="Finalize"/>.
/// </summary>
/// <param name="redactor">The step's redactor, read at each record: a secret the step resolves late masks what it recorded after.</param>
/// <param name="time">The clock <see cref="LastActionAtMs"/> reads. No upstream counterpart: upstream reads <c>Date.now()</c>.</param>
/// <param name="maxActions">Upstream's <c>maxActions</c> option: the action cap, at most <see cref="Trace.MaxTraceActions"/>.</param>
internal sealed class TraceRecorder(Func<Redactor> redactor, TimeProvider? time = null, int? maxActions = null)
{
    private readonly List<RecordedAction> _actions = [];
    private readonly int _maxActions = Math.Min(maxActions ?? Trace.MaxTraceActions, Trace.MaxTraceActions);
    private bool _truncated;
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
        return Record(new RecordedAction { Kind = kind, Point = point.Point, Viewport = viewport, Under = WithinOf(point) }, DescribePlaced(point));
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
        }, DescribePlaced(from), DescribePlaced(to));
    }

    /// <summary>Upstream's <c>describePlaced</c> for a point: the listed node that held it, described whether or not a replay could re-find it, for the summary alone.</summary>
    private RecordedTarget? DescribePlaced(PlacedPoint placed) =>
        placed.Under is null ? null : DescribeTarget(placed.Under, redactor(), placed.Within, placed.Position);

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
    public RecordedAction Record(RecordedAction action) => Record(action, null, null);

    /// <summary>Records <paramref name="action"/>; <paramref name="target"/> and <paramref name="destination"/> are the point targets its summary reads (see <see cref="AgentActions.DescribeAction"/>).</summary>
    private RecordedAction Record(RecordedAction action, RecordedTarget? target, RecordedTarget? destination = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        var recorded = Stored(action, target, destination);
        Push(recorded);
        _lastActionAtMs = _time.GetUtcNow().ToUnixTimeMilliseconds();
        return recorded;
    }

    /// <summary>
    /// Upstream's <c>toRecorded</c>: the stored variant of one action. A targeted commit whose node yields no descriptor a replay could re-find
    /// it by (none at all, or an unnamed twin with no named row to tell it apart) cannot replay, so the trace stays honest by poisoning instead
    /// of guessing; and a replay input is kept by <see cref="Verbatim"/>. A secret fill holds its marker, a name, and is not an input.
    /// </summary>
    private RecordedAction Stored(RecordedAction action, RecordedTarget? target = null, RecordedTarget? destination = null)
    {
        var stored = action.Copy();
        stored.Summary = Summarized(action, target, destination);
        if (TargetedKinds.Contains(action.Kind))
        {
            Require(TargetOf(action));
            if (action.Kind == "drag")
            {
                Require(action.Destination);
            }
        }

        switch (action.Kind)
        {
            case "fill" when Trace.IsSecretMarker(action):
                break;
            case "fill" or "select" or "typeText":
                stored.Value = action.Value is null ? null : Verbatim(action.Value);
                break;
            case "press" or "pressKey":
                stored.Key = action.Key is null ? null : Verbatim(action.Key);
                break;
            case "navigate":
                stored.Url = action.Url is null ? null : Verbatim(action.Url);
                break;
            case "scrollUntil":
                stored.Text = action.Text is null ? null : Verbatim(action.Text);
                break;
            case "upload" when action.Paths is { } paths:
                stored.Paths = [.. paths.Select(Verbatim)];
                break;
        }

        return stored;
    }

    /// <summary>
    /// The summary an action is stored with: the one it brought (a replayed action holds the entry's own, templated), else the one
    /// <see cref="AgentActions.DescribeAction"/> reads off it. Either way it is redacted and bounded again, so no text reaches the entry unredacted.
    /// </summary>
    private string Summarized(RecordedAction action, RecordedTarget? target, RecordedTarget? destination)
    {
        var current = redactor();
        var summary = action.Summary ?? AgentActions.DescribeAction(action, current, target, destination);
        // Redacted again as a whole: a secret resolved after the node was described still masks the descriptor text the prose holds.
        return TextRules.Bound(current.Redact(summary), Trace.MaxTraceSummaryChars);
    }

    /// <summary>Upstream's node verbs: the actions that name the node they act on, so they replay only by re-finding it.</summary>
    internal static readonly HashSet<string> TargetedKinds =
        ["tap", "doubleTap", "secondaryTap", "longPress", "hover", "scrollTo", "fill", "clear", "press", "select", "check", "uncheck", "upload", "drag"];

    /// <summary>The descriptor an action holds flat (the port's shape; upstream nests it as <c>target</c>).</summary>
    internal static RecordedTarget TargetOf(RecordedAction action) => new()
    {
        Role = action.Role,
        Name = action.Name,
        TestId = action.TestId,
        Within = action.Within,
        Text = action.Text,
        Placeholder = action.Placeholder,
        InputPurpose = action.InputPurpose,
        Position = action.Position,
    };

    /// <summary>Upstream's <c>require</c>: poisons the trace when <paramref name="descriptor"/> is missing or is one no replay could re-find a node by.</summary>
    private void Require(RecordedTarget? descriptor)
    {
        if (descriptor is null || !Relocate.IsRelocatableDescriptor(descriptor))
        {
            _truncated = true;
        }
    }

    /// <summary>
    /// Upstream's <c>verbatim</c>: a replay input, verbatim or poisoned. A value the redactor alters carried a registered secret; an oversized
    /// one cannot be stored whole. Either way the trace is marked non-replayable and a redacted, bounded copy is kept for documentation only.
    /// </summary>
    private string Verbatim(string value)
    {
        var redacted = redactor().Redact(value);
        if (string.Equals(redacted, value, StringComparison.Ordinal) && value.Length <= Trace.MaxTraceInputChars)
        {
            return value;
        }

        _truncated = true;
        return TextRules.Bound(redacted, Trace.MaxTraceInputChars);
    }

    /// <summary>
    /// Upstream's <c>push</c>, past the fold of a scroll: an action past the cap is not kept, and the trace is marked truncated so it never replays,
    /// instead of the flag being dropped with the action.
    /// </summary>
    private void Push(RecordedAction action)
    {
        if (_actions.Count >= _maxActions)
        {
            _truncated = true;
            return;
        }

        _actions.Add(action);
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

        scroll.Summary = Summarized(scroll, null, null);
        Push(scroll);
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
        _truncated = false;
        _lastActionAtMs = null;
    }

    /// <summary>
    /// Concludes recording into the entry of <paramref name="step"/>, or null when nothing was recorded: a step that committed no
    /// actions has no flow worth replaying. The texts are recorded raw, as the step saw them, and the caller templates the finished entry with
    /// <see cref="CacheKeys.TemplateTrace"/>. The routes are replay preconditions, so they follow the input rule (upstream's <c>anchorPath</c>): a
    /// route the redactor alters carried a secret (a token in a query string), and one too long to keep whole cannot be compared, so either marks
    /// the trace non-replayable rather than storing it bent.
    /// </summary>
    public CacheEntry? Finalize(CacheEntry step, string route, string endRoute, List<RecordedTarget> appeared, List<RecordedTarget> gone, string? recordedBy = null, long? endWaitMs = null, string? summary = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (_actions.Count == 0)
        {
            return null;
        }

        var entry = new CacheEntry { Schema = step.Schema };
        entry.SetStep(step);
        entry.RecordedBy = CacheEntry.ReadRecordedBy(recordedBy is null ? null : redactor().Redact(recordedBy));
        // Upstream's conclusion.summary: the recorded run's verdict, redacted and bounded, "step passed" when blank.
        var verdict = TextRules.Bound(redactor().Redact(summary ?? ""), Trace.MaxTraceSummaryChars);
        entry.Summary = string.IsNullOrWhiteSpace(verdict) ? "step passed" : verdict;
        entry.Route = AnchorPath(route);
        entry.EndRoute = AnchorPath(endRoute);
        entry.Actions = [.. _actions];
        entry.Appeared = appeared;
        entry.Gone = gone;
        entry.EndWaitMs = endWaitMs is { } wait ? (int)Math.Min(Trace.MaxTraceEndWaitMs, Math.Max(0, wait)) : null;
        entry.Truncated = _truncated ? true : null;
        return entry;
    }

    private string AnchorPath(string value) => value.Length == 0 ? value : Verbatim(value);

    /// <summary>
    /// Upstream's <c>describeTarget</c> (<c>agent/actions.ts</c>): how a replay will find the node again, by its role, its label,
    /// its text when that differs from the label, its test id, its placeholder, and its input purpose, redacted. Never the value a field
    /// holds. <paramref name="within"/> is the key of the container it sat in and <paramref name="position"/> its place among its twins
    /// (upstream's <c>describePlaced</c>); the key is redacted again and bounded.
    /// </summary>
    public static RecordedTarget DescribeTarget(SemanticNode node, Redactor redactor, string? within = null, TracePosition? position = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        // Each field is bounded to the descriptor budget at record time, as upstream's describeTarget bounds it (`field`), so a recording
        // never holds a field the reader would reject. A blank field is absent, as upstream's `field` makes it.
        // The label of a node with no name is its text (upstream records that text as the name's absence and a text), so a text is a second
        // field only beside a name of its own, and only when it says something else. "Says something else" is compared before the bound, so two
        // texts that agree in their first characters are still two fields.
        var label = RedactLabel(node, redactor);
        var text = node.Name is null ? null : NonEmpty(RedactField(node.Text, ObservationLimits.Text, redactor));
        return new RecordedTarget
        {
            Role = node.Role is null ? null : NonEmpty(Trace.BoundDescriptor(AgentObservation.RedactText(node.Role, redactor))),
            Name = NonEmpty(Trace.BoundDescriptor(label)),
            Text = text == label ? null : NonEmpty(Trace.BoundDescriptor(text)),
            TestId = NonEmpty(Trace.BoundDescriptor(Redact(node.TestId, redactor))),
            Placeholder = NonEmpty(Trace.BoundDescriptor(RedactField(node.Placeholder, ObservationLimits.Text, redactor))),
            InputPurpose = node.InputPurpose is null or "none" or "" ? null : node.InputPurpose,
            Within = within is null ? null : TextRules.Bound(redactor.Redact(within), AgentActions.MaxWithinChars),
            Position = position,
        };
    }

    private static string? NonEmpty(string? text) => string.IsNullOrEmpty(text) ? null : text;

}
