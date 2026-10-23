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
    private readonly Dictionary<string, SemanticNode> _seen = [];
    private PendingChange? _pending;

    /// <summary>How far each look of the step settled, in order.</summary>
    public List<SettleMode> Looks { get; } = [];

    /// <summary>The newest observation of the step, if any was captured.</summary>
    public Observation? Latest { get; private set; }

    /// <summary>Whether any semantic observation in this step stopped at a capture or byte limit.</summary>
    public bool ObservationTruncated { get; private set; }

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
    public Task<Observation> ObserveAsync(SettleMode mode, CancellationToken token) =>
        (Scope?.Debug ?? DebugTrace.Off).Time(Phases.Observe, () => LookAsync(mode, token));

    // The engine's look, shown to the attempt's trace when the feed belongs to an attempt.
    private Task<Observation> SeeAsync(CancellationToken token) => Scope is { } scope ? scope.ObserveAsync(token) : session.ObserveAsync(token);

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
        ObservationTruncated |= observation.Truncated;
        foreach (var node in LocatorResolver.Walk(observation.Roots))
        {
            _seen[node.Ref] = node;
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
        var observation = await ObserveAsync(SettleMode.HeldStill, token).ConfigureAwait(false);
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
    internal static ExecutorObservation View(Observation observation, AttemptScope scope, ExecutorObserveOptions? options = null) =>
        new(SnapshotText.Render(observation, scope.Redactor, scope.AppOrigin))
        {
            Truncated = observation.Truncated,
            KeyboardVisible = observation.KeyboardVisible,
            Path = scope.Redactor.Redact(observation.Route),
            Tree = options?.Tree == true ? AgentObservation.ProjectTree(observation.Roots, scope.Redactor, scope.AppOrigin) : null,
        };

    /// <summary>The newest node an observation of the step listed under <paramref name="reference"/>, if any.</summary>
    public SemanticNode? LastSeen(string reference) => _seen.GetValueOrDefault(reference);

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
        var observation = await ObserveAsync(SettleMode.Raw, token).ConfigureAwait(false);
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
