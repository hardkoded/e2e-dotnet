// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cache;
using E2E.Engine;
using E2E.Internal;
using RelocateRules = E2E.Relocate;

namespace E2E;

/// <summary>
/// One step's looks at the screen. It remembers the newest observation, which an action is resolved against, and the
/// change wait the newest committed action armed, kept until the next settled look has waited for the screen to leave
/// the shape the action was resolved against. Without it, the look after a tap on a link reads the old page, stable and
/// wrong, and a replay acts on a form mid-update.
/// </summary>
internal sealed class ObservationFeed(IEngineSession session, TimeProvider time)
{
    // The newest text rendered, which the same look reuses while the redactor it was rendered under is the attempt's current one.
    private Rendered? _rendered;

    private sealed record Rendered(Observation Observation, bool Pixels, Redactor Redactor, string Text, bool Cut);

    private readonly Dictionary<string, Resolved> _seen = [];
    private PendingChange? _pending;

    /// <summary>How far each look of the step settled, in order.</summary>
    public List<SettleMode> Looks { get; } = [];

    /// <summary>Whether any semantic observation in this step stopped at a capture or byte limit.</summary>
    private bool _observationTruncated;

    // True once pixels this feed cleared were handed to the executor as model input, and why requested pixels were not, when they were not.
    private bool _pixelsSent;
    private VisionDegradation? _visionDegraded;

    // A trace may only describe a step observed with semantic evidence throughout (upstream's `semanticHistory`).
    private bool _semanticHistory = true;

    /// <summary>The size in bytes of the largest screenshot handed to the executor in this step (upstream's <c>metrics.pixelBytes</c>); null when none was.</summary>
    public int? PixelBytes { get; private set; }

    /// <summary>A trace may only describe a step observed with semantic evidence throughout (upstream's <c>traceEligible</c>).</summary>
    public bool TraceEligible => _semanticHistory;

    /// <summary>True once a screenshot went to the model in this step; text drawn in it is not in the listing (upstream's <c>pixelsShown</c>).</summary>
    public bool PixelsShown => _pixelsSent;

    // Strings the screens of a step showed, kept for telling a typed value read off the screen from one the model composed
    // (Derived): one per node, the oldest dropped first past this many (upstream's MAX_SHOWN_TEXTS).
    private const int MaxShownTexts = 4_096;

    // What the step's screens have shown, one string per node, oldest first (upstream's `shown`).
    private readonly HashSet<string> _shown = new(StringComparer.Ordinal);
    private readonly Queue<string> _shownOrder = new();

    /// <summary>What the step's screens have shown so far, one string per node; what a typed value is checked against (upstream's <c>shownText</c>).</summary>
    public IReadOnlyCollection<string> ShownText() => _shown;

    // Upstream's noteShown: remembers what a screen displayed, the name and text of every node but those that echo input (an editable
    // field, whose name is its value or its placeholder example; any node carrying a value) and the wrappers that restate a field,
    // which some platforms name after the field they hold.
    // The text is kept redacted, as upstream keeps it: its feed remembers the prepared (redacted) capture.
    internal void NoteShown(Observation observation)
    {
        var redactor = Scope?.Redactor ?? Redactor.None;
        foreach (var node in LocatorResolver.Walk(observation.Roots))
        {
            if (EchoesInput(node) || node.Children.Any(EchoesInput))
            {
                continue;
            }

            foreach (var text in new[] { node.Name, node.Text })
            {
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                var redacted = redactor.Redact(text);
                if (_shown.Add(redacted))
                {
                    _shownOrder.Enqueue(redacted);
                }
            }
        }

        while (_shown.Count > MaxShownTexts)
        {
            _shown.Remove(_shownOrder.Dequeue());
        }
    }

    // Upstream's echoesInput: a node whose name or text is what was put into it, not what the screen says.
    private static bool EchoesInput(SemanticNode node) => Roles.IsEditable(node) || node.Value is not null;

    /// <summary>The step's observation and pixel record for the report (upstream's <c>reportDetails</c>).</summary>
    public StepAgentDetails ReportDetails() => new()
    {
        ObservationTruncated = _observationTruncated ? true : null,
        VisionInput = _pixelsSent ? true : null,
        VisionDegraded = _visionDegraded,
    };

    /// <summary>
    /// The limits the step's observations are cut under, upstream's <c>ObservationFeedOptions</c>. Null for a feed that cuts nothing,
    /// such as a judgment's.
    /// </summary>
    public ObservationFeedOptions? Options { get; init; }

    /// <summary>The newest observation of the step, if any was captured.</summary>
    public Observation? Latest { get; private set; }

    /// <summary>
    /// The screen the step's first committed action was resolved against. A screen still loading when the step began
    /// can hold still long enough to pass a held-still look, so the step's delta is read from here: the page load
    /// before it is not the step's effect.
    /// </summary>
    public Observation? FirstActedOn { get; private set; }

    /// <summary>
    /// One look, settled as far as <paramref name="mode"/> asks. A raw look reads the screen as it is. A settled look
    /// consumes the pending change: it waits for the screen to leave the pre-action shape once, so later looks read the
    /// screen as it is; held still, it then proves the new shape holds for a beat.
    /// </summary>
    public Task<Observation> ObserveAsync(SettleMode mode, CancellationToken token) => ObserveAsync(mode, null, token);

    /// <summary>
    /// <see cref="ObserveAsync(SettleMode, CancellationToken)"/> as one recorded <c>observation</c> event of the running step, which
    /// <paramref name="name"/> tells apart when the look is not the step's own (<c>relocate</c>, <c>page</c>), and which books the
    /// observation's size.
    /// </summary>
    public Task<Observation> ObserveAsync(SettleMode mode, string? name, CancellationToken token) => ObserveAsync(mode, name, false, token);

    // Whether the look asks for pixels is this look's own, so a concurrent look books its size by its own ask. Pixels reserve part
    // of the token ceiling the tree is cut under.
    private Task<Observation> ObserveAsync(SettleMode mode, string? name, bool pixels, CancellationToken token) =>
        Phases.InstrumentPhaseAsync(
            new PhaseHost(Scope?.Steps, Scope?.Debug ?? DebugTrace.Off),
            new PhaseSpec(StepEventKind.Observation, Phases.Observe, name),
            () => LookAsync(mode, token),
            observation => Book(observation, pixels));

    /// <summary>The size in bytes of the largest observation the step read, as a model reads it (upstream's <c>metrics.observationBytes</c>).</summary>
    public int MaxObservationBytes { get; private set; }

    // What an observation event tells (upstream's observationDetail): the nodes read, and the bytes of the text a model reads.
    private PhaseDetail Book(Observation observation, bool pixels)
    {
        var count = LocatorResolver.Walk(observation.Roots).Count();
        if (Scope is not { } scope)
        {
            return new PhaseDetail(count);
        }

        var bytes = System.Text.Encoding.UTF8.GetByteCount(Text(observation, pixels));
        MaxObservationBytes = Math.Max(MaxObservationBytes, bytes);
        return new PhaseDetail(count, bytes);
    }

    // The engine's look, shown to the attempt's trace when the feed belongs to an attempt.
    // A settled look takes several; a truncated one among them marks the step even when the last one is whole.
    private async Task<Observation> SeeAsync(CancellationToken token)
    {
        // Retried at the engine while it reports a race, the step's clock checked each time it fails (upstream's retryingObserve).
        var observation = await Phases.RetryingObserveAsync(
            () => Scope is { } scope ? scope.ObserveAsync(PixelFallbackLook, token) : session.ObserveAsync(token),
            cause => Accounting?.Checkpoint(cause),
            token).ConfigureAwait(false);
        if (observation.Truncated)
        {
            _observationTruncated = true;
        }

        if (observation.TreeUnavailable)
        {
            // No node of an earlier look names anything now, and a recording cannot describe a step that saw only pixels.
            _semanticHistory = false;
            _seen.Clear();
        }

        return observation;
    }

    // The agent lets the engine answer a screen it cannot read with pixels alone (upstream's `pixelFallback: true`).
    private static readonly ObserveOptions PixelFallbackLook = new() { PixelFallback = true };

    private async Task<Observation> LookAsync(SettleMode mode, CancellationToken token)
    {
        Looks.Add(mode);
        Observation observation;
        if (mode == SettleMode.Raw)
        {
            observation = await SeeAsync(token).ConfigureAwait(false);
        }
        else
        {
            var changedFrom = _pending;
            _pending = null;
            observation = await AgentObservation.SettleAsync(
                SeeAsync,
                AgentObservation.Shape,
                new SettleOptions<Observation>
                {
                    ChangedFrom = changedFrom,
                    Transitional = AgentObservation.IsTransitional,
                    StableWait = mode == SettleMode.HeldStill ? SettlePolicy.HeldStill : TimeSpan.Zero,
                },
                time,
                token).ConfigureAwait(false);
        }

        if (Latest is not null && !string.Equals(Latest.Route, observation.Route, StringComparison.Ordinal))
        {
            // Refs are per document, so a ref seen on another page names nothing here.
            _seen.Clear();
        }

        Latest = observation;
        NoteShown(observation);

        foreach (var node in LocatorResolver.Walk(observation.Roots))
        {
            _seen[node.Ref] = new Resolved(node, observation);
        }

        return observation;
    }

    /// <summary>
    /// The attempt the looks belong to, for what an executor's look is redacted and cleared with: the secrets, the app origin,
    /// the engine's capture. Null for a feed that serves no executor.
    /// </summary>
    public AttemptScope? Scope { get; init; }

    /// <summary>The step's clock and budgets (upstream's <c>accounting</c>), which a failed capture is checked against. Null for a feed of no step.</summary>
    public StepAccounting? Accounting { get; init; }

    /// <summary>
    /// Upstream's executor-facing <c>observe</c>: the options checked before anything is captured, then one settled look,
    /// redacted and viewed. The text is always there, and the tree and the pixels when asked for. Pixels go out only when the
    /// engine captures them and its masking is proven; otherwise the look says why not.
    /// </summary>
    public async Task<ExecutorObservation> ObserveAsync(ExecutorObserveOptions? options, CancellationToken token)
    {
        var scope = Scope ?? throw new InvalidOperationException("This feed serves no executor.");
        var observation = await ObserveAsync(SettleMode.HeldStill, null, options?.Pixels == true, token).ConfigureAwait(false);

        var view = View(observation, scope, options);
        if (options?.Pixels != true && !observation.TreeUnavailable)
        {
            return view;
        }

        // A screen with no tree carries its pixels with it; any other look is photographed now.
        var captured = observation.TreeUnavailable ? observation.Pixels : await scope.CaptureAsync(token).ConfigureAwait(false);
        var outcome = AgentObservation.PixelsForModel(observation, captured);
        if (outcome.Pixels is not { } pixels)
        {
            if (observation.TreeUnavailable)
            {
                throw new TestException("UNSUPPORTED_CAPABILITY", "semantic capture is unavailable and no permitted, proven-masked screenshot can replace it");
            }

            scope.VisionDegraded = true;
            _visionDegraded = outcome.Withheld;
            return view with { PixelsWithheld = outcome.Withheld };
        }

        scope.VisionInput = true;
        _pixelsSent = true;
        PixelBytes = Math.Max(PixelBytes ?? 0, pixels.Png.Length);
        var size = Pixels.ReadPngSize(pixels.Png);
        return view with { Pixels = new ExecutorPixels(pixels.Png, "image/png", size?.Width ?? 0, size?.Height ?? 0, pixels.Scale, pixels.MaskedRegionCount ?? 0) };
    }

    /// <summary>
    /// Upstream's <c>view</c>: one capture as the executor sees it, redacted, with the text, the truncation, the keyboard state
    /// and the route, and the tree when asked for. The step's start observation and <see cref="ObserveAsync(ExecutorObserveOptions?, CancellationToken)"/> both read it.
    /// </summary>
    internal ExecutorObservation View(Observation observation, AttemptScope scope, ExecutorObserveOptions? options = null)
    {
        var text = Text(observation, options?.Pixels == true, out var cut);
        return new ExecutorObservation(text)
        {
            TreeUnavailable = observation.TreeUnavailable,
            Truncated = observation.Truncated || cut || observation.TreeUnavailable,
            KeyboardVisible = observation.KeyboardVisible,
            Path = scope.Redactor.Redact(observation.Route),
            Revision = observation.Revision.Length > 0 ? observation.Revision : null,
            Tree = options?.Tree == true ? AgentObservation.ProjectTree(observation.Roots, scope.Redactor, scope.AppOrigin) : null,
            Viewport = observation.Viewport,
        };
    }

    /// <summary>
    /// The screen as the model reads it, cut at <see cref="ByteBudget"/> (upstream's <c>prepareObservation</c> under <c>capture</c>'s
    /// <c>maxBytes</c>). A step with a cut screen reports its observation as truncated.
    /// </summary>
    public string Text(Observation observation, bool pixels = false) => Text(observation, pixels, out _);

    /// <inheritdoc cref="Text(Observation, bool)"/>
    /// <param name="observation">The look to render.</param>
    /// <param name="pixels">Whether a screenshot travels with the text.</param>
    /// <param name="cut">Whether the byte budget cut the screen.</param>
    public string Text(Observation observation, bool pixels, out bool cut)
    {
        // One look is read for its size, for the executor, and for the model; it is rendered once. A secret registered since
        // replaces the attempt's redactor, and text rendered under the old one is never reused.
        var scope = Scope ?? throw new InvalidOperationException("This feed serves no executor.");
        if (observation.TreeUnavailable)
        {
            cut = false;
            return UnavailableTreeMarker;
        }

        var redactor = scope.Redactor;
        if (_rendered is { } last && ReferenceEquals(last.Observation, observation) && last.Pixels == pixels && ReferenceEquals(last.Redactor, redactor))
        {
            cut = last.Cut;
            return last.Text;
        }

        var text = SnapshotText.Render(observation, redactor, scope.AppOrigin, ByteBudget(pixels), out cut);
        if (cut)
        {
            _observationTruncated = true;
        }

        _rendered = new Rendered(observation, pixels, redactor, text, cut);
        return text;
    }

    /// <summary>
    /// Bytes one act observation may contribute to a model turn, clamped like a judgment's. The loop's history grows past what any one
    /// observation costs, so the fixed part counted here is what every turn resends: the project context, the prior-step ledger, and the
    /// step's instruction.
    /// </summary>
    private int ByteBudget(bool pixels) =>
        Options is not { } options
            ? E2EDefaults.MaxObservationBytes
            : ObservationBudget.ObservationByteBudget(
                new ObservationBudgetLimits(options.MaxObservationBytes, options.MaxInputTokens),
                new ObservationBudgetRequest(options.ContextBytes + options.LedgerBytes, options.InstructionBytes, pixels));

    /// <summary>What <paramref name="reference"/> named in the newest observation of the step that carried it, with that observation (upstream's <c>lastSeen</c>).</summary>
    public Resolved? LastSeen(string reference) => _seen.GetValueOrDefault(reference);

    /// <summary>
    /// What a target names on the newest screen, if the screen still lists it or its descriptor re-finds it (upstream's <c>lookup</c>).
    /// The id is the target's without the <c>#</c> the listing prefixes, which a model copies now and then. A caller with its own way of
    /// re-finding a lost node (<see cref="ScrollTarget"/>) starts here.
    /// </summary>
    public Lookup Lookup(ExecutorTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var id = target.Id.TrimStart('#');
        var latest = Latest ?? throw NodeGone(id);
        if (latest.TreeUnavailable)
        {
            throw new TestException("LOCATOR_NOT_FOUND", "semantic capture is unavailable; previous node ids are no longer valid, so use the current screenshot");
        }

        var node = LocatorResolver.Walk(latest.Roots).FirstOrDefault(candidate => candidate.Ref == id) ?? Refound(id, latest);
        return new Lookup(id, node, latest);
    }

    /// <summary>What a screen with no semantic tree lists in place of one (upstream's <c>UNAVAILABLE_TREE_MARKER</c>).</summary>
    internal const string UnavailableTreeMarker = "[semantic capture unavailable: no nodes were read; previous node ids are no longer valid. Use the screenshot, never infer absence from this listing]";

    /// <summary>Upstream's <c>nodeGone</c>: the error for a target id the newest screen no longer lists.</summary>
    public static TestException NodeGone(string id) =>
        new("LOCATOR_NOT_FOUND", "node " + id + " is not on the current screen; it was removed or never existed");

    // The newest node matching the descriptor of what the id named in a recent observation, if exactly one (upstream's refound).
    private SemanticNode? Refound(string id, Observation latest)
    {
        if (Scope is not { } scope || LastSeen(id) is not { } earlier)
        {
            return null;
        }

        return RefindOne(latest, earlier.Node, scope.Redactor);
    }

    /// <summary>
    /// Marks the newest observation as the one the step's next action is taken on, when no action came before. An
    /// action that looks at the screen while it acts, paging a list to a text, marks it before its first page.
    /// </summary>
    public void MarkActing()
    {
        FirstActedOn ??= Latest;
    }

    /// <summary>
    /// Arms the change wait the committed action's settle policy asks for, against the newest observation's shape, so
    /// the next settled look reads the screen after the effect rather than before. An action whose effect the tree
    /// cannot show arms nothing. The newest observation is the one the action was resolved against.
    /// </summary>
    public void ArmAfter(RecordedAction action)
    {
        MarkActing();
        if (Latest is not null && SettlePolicy.For(action).ChangeWait is { } wait)
        {
            _pending = new PendingChange(AgentObservation.Shape(Latest), time.GetUtcNow() + wait);
        }
    }

    /// <summary>
    /// Upstream's <c>relocate</c>: finds a node that went stale in one fresh look, exactly one match or null. The look is
    /// taken directly, as upstream's is, because this runs inside an action. The stale node is matched with the trace recorder's
    /// own descriptor rules (<c>cache/relocate.ts</c>): role, name, text, test id, placeholder, and input purpose, with a changed test id
    /// forgiven when the rest still identifies it. Like upstream's <c>describeTarget</c> it carries no container key and no position.
    /// </summary>
    public async Task<SemanticNode?> Relocate(SemanticNode stale, Redactor redactor, CancellationToken token)
    {
        if (!RelocateRules.NamesAnything(TraceRecorder.DescribeTarget(stale, redactor)))
        {
            return null;
        }

        var observation = await ObserveAsync(SettleMode.Raw, "relocate", token).ConfigureAwait(false);
        return RefindOne(observation, stale, redactor);
    }

    // The one node of the observation that reads as the stale node did, by the recorder's descriptor rules, or null (upstream's relocateDescriptor on describeTarget).
    private static SemanticNode? RefindOne(Observation observation, SemanticNode stale, Redactor redactor)
    {
        var matches = FindLike(observation, stale, redactor);
        return matches.Count == 1 ? matches[0] : null;
    }

    /// <summary>
    /// The nodes that read as <paramref name="node"/> did, by the descriptor rules of <c>cache/relocate.ts</c>: the one it relocates to, or
    /// the several it cannot tell apart. A node with nothing to tell it from its twins is never found again: a wrong match would act on the
    /// wrong control. No upstream counterpart: upstream's callers read the <c>RelocationResult</c>; the port's <c>ResolveIn</c> also names the
    /// candidates of an ambiguity (.NET-only act tools).
    /// </summary>
    public static List<SemanticNode> FindLike(Observation observation, SemanticNode node, Redactor redactor)
    {
        var descriptor = TraceRecorder.DescribeTarget(node, redactor);
        if (!RelocateRules.NamesAnything(descriptor))
        {
            return [];
        }

        return RelocateRules.RelocateDescriptor(descriptor, observation, redactor) switch
        {
            RelocationResult.Found found => RelocateRules.NodeOf(observation, found.Id) is { } relocated ? [relocated] : [],
            RelocationResult.Failed { Candidates: { } candidates } => [.. candidates.Select(id => RelocateRules.NodeOf(observation, id)).OfType<SemanticNode>()],
            _ => [],
        };
    }
}

/// <summary>A target resolved to a node of the observation it was found in (upstream's <c>Resolved</c>).</summary>
internal sealed record Resolved(SemanticNode Node, Observation Observation);

/// <summary>What a target names on the newest screen: the id as the screen lists it, the node or nothing, and the screen looked at (upstream's <c>Lookup</c>).</summary>
internal sealed record Lookup(string Id, SemanticNode? Node, Observation Observation);

/// <summary>What the step's observations are cut under (upstream's <c>ObservationFeedOptions</c>).</summary>
/// <param name="MaxObservationBytes">The agent's observation byte ceiling, clamped per capture by the token limit.</param>
/// <param name="MaxInputTokens">The agent's per-request input token ceiling.</param>
/// <param name="InstructionBytes">Bytes of the step's instruction, which opens the history every turn resends.</param>
/// <param name="ContextBytes">Bytes of the project context every turn resends (upstream reads them from the step's metrics).</param>
/// <param name="LedgerBytes">Bytes of the prior-step ledger every turn resends (upstream reads them from the step's metrics).</param>
internal sealed record ObservationFeedOptions(int MaxObservationBytes, int MaxInputTokens, int InstructionBytes, int ContextBytes, int LedgerBytes);
