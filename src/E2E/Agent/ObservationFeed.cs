// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E;

/// <summary>
/// One step's looks at the screen. It remembers the newest observation, which an action is resolved against, and the
/// change wait the newest committed action armed, kept until the next settled look has waited for the screen to leave
/// the shape the action was resolved against. Without it, the look after a tap on a link reads the old page, stable and
/// wrong, and a replay acts on a form mid-update.
/// </summary>
internal sealed class ObservationFeed(IEngineSession session, TimeProvider time)
{
    // Whether the executor-facing look in flight asked for pixels, which reserve part of the token ceiling the tree is cut under.
    private bool _pixels;

    // The newest text rendered, which the same look reuses.
    private Rendered? _rendered;

    private sealed record Rendered(Observation Observation, bool Pixels, string Text, bool Cut);

    private readonly Dictionary<string, Resolved> _seen = [];
    private PendingChange? _pending;

    /// <summary>How far each look of the step settled, in order.</summary>
    public List<SettleMode> Looks { get; } = [];

    /// <summary>Whether any semantic observation in this step stopped at a capture or byte limit.</summary>
    private bool _observationTruncated;

    /// <summary>The step's observation record for the report (upstream's <c>reportDetails</c>).</summary>
    public StepAgentDetails ReportDetails() => new() { ObservationTruncated = _observationTruncated ? true : null };

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
    public Task<Observation> ObserveAsync(SettleMode mode, string? name, CancellationToken token) =>
        Phases.InstrumentPhaseAsync(
            new PhaseHost(Scope?.Steps, Scope?.Debug ?? DebugTrace.Off),
            new PhaseSpec(StepEventKind.Observation, Phases.Observe, name),
            () => LookAsync(mode, token),
            Book);

    /// <summary>The size in bytes of the largest observation the step read, as a model reads it (upstream's <c>metrics.observationBytes</c>).</summary>
    public int MaxObservationBytes { get; private set; }

    // What an observation event tells (upstream's observationDetail): the nodes read, and the bytes of the text a model reads.
    private PhaseDetail Book(Observation observation)
    {
        var count = LocatorResolver.Walk(observation.Roots).Count();
        if (Scope is not { } scope)
        {
            return new PhaseDetail(count);
        }

        var bytes = System.Text.Encoding.UTF8.GetByteCount(Text(observation, _pixels));
        MaxObservationBytes = Math.Max(MaxObservationBytes, bytes);
        return new PhaseDetail(count, bytes);
    }

    // The engine's look, shown to the attempt's trace when the feed belongs to an attempt.
    // A settled look takes several; a truncated one among them marks the step even when the last one is whole.
    private async Task<Observation> SeeAsync(CancellationToken token)
    {
        var observation = await (Scope is { } scope ? scope.ObserveAsync(token) : session.ObserveAsync(token)).ConfigureAwait(false);
        if (observation.Truncated)
        {
            _observationTruncated = true;
        }

        return observation;
    }

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

        foreach (var node in LocatorResolver.Walk(observation.Roots))
        {
            _seen[node.Ref] = new Resolved(node, observation);
        }

        return observation;
    }

    /// <summary>
    /// The attempt the looks belong to, for what an executor's look is redacted and cleared with: the secrets, the app origin,
    /// the engine's capture, and the pixel taint. Null for a feed that serves no executor.
    /// </summary>
    public AttemptScope? Scope { get; init; }

    /// <summary>
    /// Upstream's executor-facing <c>observe</c>: the options checked before anything is captured, then one settled look,
    /// redacted and viewed. The text is always there, and the tree and the pixels when asked for. Pixels go out only when the
    /// engine captures them, its masking is proven, and no secret was filled in the attempt; otherwise the look says why not.
    /// </summary>
    public async Task<ExecutorObservation> ObserveAsync(ExecutorObserveOptions? options, CancellationToken token)
    {
        var scope = Scope ?? throw new InvalidOperationException("This feed serves no executor.");
        _pixels = options?.Pixels == true;
        Observation observation;
        try
        {
            observation = await ObserveAsync(SettleMode.HeldStill, token).ConfigureAwait(false);
        }
        finally
        {
            _pixels = false;
        }

        var view = View(observation, scope, options);
        if (options?.Pixels != true)
        {
            return view;
        }

        var tainted = scope.Exposure.WithholdsModelPixels;
        var outcome = AgentObservation.PixelsForModel(observation, tainted ? null : await scope.CaptureAsync(token).ConfigureAwait(false), tainted);
        if (outcome.Pixels is not { } pixels)
        {
            scope.VisionDegraded = true;
            return view with { PixelsWithheld = outcome.Withheld };
        }

        scope.VisionInput = true;
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
            Truncated = observation.Truncated || cut,
            KeyboardVisible = observation.KeyboardVisible,
            Path = scope.Redactor.Redact(observation.Route),
            Revision = observation.Revision.Length > 0 ? observation.Revision : null,
            Tree = options?.Tree == true ? AgentObservation.ProjectTree(observation.Roots, scope.Redactor, scope.AppOrigin) : null,
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
        // One look is read for its size, for the executor, and for the model; it is rendered once.
        if (_rendered is { } last && ReferenceEquals(last.Observation, observation) && last.Pixels == pixels)
        {
            cut = last.Cut;
            return last.Text;
        }

        var scope = Scope ?? throw new InvalidOperationException("This feed serves no executor.");
        var text = SnapshotText.Render(observation, scope.Redactor, scope.AppOrigin, ByteBudget(pixels), out cut);
        if (cut)
        {
            _observationTruncated = true;
        }

        _rendered = new Rendered(observation, pixels, text, cut);
        return text;
    }

    /// <summary>
    /// Bytes one act observation may contribute to a model turn, clamped like a judgment's. The loop's history grows past what any one
    /// observation costs, so the fixed part counted here is what every turn resends: the project context, the prior-step ledger, and the
    /// step's instruction.
    /// </summary>
    private int ByteBudget(bool pixels) =>
        Options is not { } options
            ? int.MaxValue
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
        var node = LocatorResolver.Walk(latest.Roots).FirstOrDefault(candidate => candidate.Ref == id) ?? Refound(id, latest);
        return new Lookup(id, node, latest);
    }

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

        var matches = FindLike(latest, earlier.Node, scope.Redactor);
        return matches.Count == 1 ? matches[0] : null;
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
    /// taken directly, as upstream's is, because this runs inside an action. Upstream matches with the trace recorder's
    /// descriptor rules (<c>cache/relocate.ts</c>), which also read placeholder, input purpose, and container, and
    /// forgive a changed test id. The port matches the role, name, and test id it records.
    /// </summary>
    public async Task<SemanticNode?> Relocate(SemanticNode stale, Redactor redactor, CancellationToken token)
    {
        var observation = await ObserveAsync(SettleMode.Raw, "relocate", token).ConfigureAwait(false);
        var matches = FindLike(observation, stale, redactor);
        return matches.Count == 1 ? matches[0] : null;
    }

    /// <summary>
    /// The nodes that read as <paramref name="node"/> did. A node with no name and no test id has nothing to tell it
    /// from its twins, so it is never found again: a wrong match would act on the wrong control.
    /// </summary>
    public static List<SemanticNode> FindLike(Observation observation, SemanticNode node, Redactor redactor)
    {
        var name = Agent.Redact(Agent.LabelOf(node), redactor);
        var testId = Agent.Redact(node.TestId, redactor);
        return name is null && testId is null ? [] : Agent.Find(observation, node.Role, name, testId, null, redactor);
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
