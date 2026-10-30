// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using E2E.Cache;
using E2E.Engine;
using E2E.Internal;
using static E2E.Agent;

namespace E2E;

/// <summary>
/// Upstream's <c>StepOutcome</c>: how a step settled, as the write-side decision sees it. <see cref="NoVerdict"/> is a step
/// that ended without anything judging the app: it was cancelled, or no model answered. Neither says the recorded flow is
/// wrong, so neither implicates its entry.
/// </summary>
internal enum StepOutcome
{
    Passed,
    Failed,
    NoVerdict,
}

/// <summary>
/// Upstream's <c>StepCacheHost</c> (<c>agent/step-cache.ts</c>): the replay host plus what the session reads off the step's
/// screens. <see cref="FirstActedOn"/> has no upstream counterpart. The port reads a recording's delta from the screen the
/// step's first action was resolved against, where upstream reads it from the first screen on the route the step ended on.
/// </summary>
internal interface IStepCacheHost : IReplayHost
{
    /// <summary>The screen the step's first committed action was resolved against, if it took one.</summary>
    Observation? FirstActedOn { get; }
}

/// <summary>Upstream's <c>ClaimedKey</c>: the key a step claimed and the step it names, which an entry records as its provenance.</summary>
internal sealed record ClaimedKey(string Key, CacheEntry Step);

/// <summary>
/// Upstream's <c>StepCacheOptions</c>, with the cache context (<c>AgentCacheContext</c>) it holds folded in: the store, the
/// mode, and the claimed key. The port has no params templates, redaction functions, or executor to pass: its
/// act reads params as they are, redacts through the host, and records the agent's configured model, not the executor.
/// </summary>
internal sealed class StepCacheOptions
{
    public required IStepCache Cache { get; init; }

    /// <summary>False in read-only mode: the step replays but does not write or delete recordings.</summary>
    public bool Write { get; init; }

    /// <summary>Upstream's <c>cache.strict</c>.</summary>
    public bool Strict { get; init; }

    /// <summary>Whether this attempt may replay: a first attempt, or any attempt under strict mode.</summary>
    public bool ReplayEligible { get; init; }

    public required ClaimedKey Claim { get; init; }

    /// <summary>The step's index among the attempt's steps. Null when no step is running.</summary>
    public int? StepIndex { get; init; }

    public required string Instruction { get; init; }

    public IReadOnlyDictionary<string, object?>? Params { get; init; }

    /// <summary>The step's recorder, which the step's budget also holds.</summary>
    public required TraceRecorder Recorder { get; init; }

    /// <summary>
    /// The origin the app is served on. .NET-only: upstream maps a screen to its path in the host's <c>observe</c>
    /// (<c>screenOf</c>, <c>agent/act.ts</c>); the port's host returns the observation, so the session maps it.
    /// </summary>
    public string? AppOrigin { get; init; }
}

/// <summary>
/// Upstream's <c>StepTraceSession</c> (<c>agent/step-cache.ts</c>): one step's trace-cache session, the replay attempt, live
/// recording, and the stage-or-evict decision once the step has settled. The dispatch owns budgets, the grammar, and the
/// verdict; this class owns nothing but the cache. The recording is staged as a <see cref="PendingAct"/>, which the
/// attempt settles at its end (upstream's <c>settleStagedTraces</c>, here <c>E2ESession.Flush</c>).
/// </summary>
internal sealed class StepTraceSession
{
    // Quotes a label in a detail as JSON.stringify does: text stays readable, not escaped to \u codes.
    private readonly IStepCacheHost _host;
    private readonly StepCacheOptions _options;
    private readonly TraceRecorder _recorder;
    private CacheInfo? _info;
    private string? _recordedBy;
    private bool _changedNothing;
    private ReplayedPrefix? _prefix;
    private Observation? _start;
    private CacheEntry? _wholeReplay;
    private int _actionsAtEndMismatch = -1;
    private long _startedMs;
    private long? _handedOffMs;
    private long? _executorCompletedMs;

    // Upstream's END_WAIT_MARGIN_MS: room for a slower day on top of how long the app took to show its end state.
    private const int EndWaitMarginMs = 10_000;

    public StepTraceSession(IStepCacheHost host, StepCacheOptions options)
    {
        _host = host;
        _options = options;
        _recorder = options.Recorder;
        Pending = new PendingAct
        {
            Key = options.Claim.Key,
            ParamCollision = CacheKeys.Collides(options.Params),
            Step = options.Claim.Step,
            StepIndex = options.StepIndex,
        };
    }

    /// <summary>The act as the attempt settles it: upstream's staged entry.</summary>
    public PendingAct Pending { get; }

    /// <summary>The screen the step began on, if it could be read. The executor's first look can read it.</summary>
    public Observation? Start => _start;

    /// <summary>How the cache participated, for the step's report detail.</summary>
    public CacheInfo? CacheInfo => _info is null || _recordedBy is null ? _info : _info.WithRecordedBy(_recordedBy);

    /// <summary>The mid-step hand-off, when a replay diverged after real progress.</summary>
    public ReplayedPrefix? ReplayedPrefix => _prefix;

    /// <summary>Upstream's <c>failedStepOutcome</c>: the outcome of a step that threw <paramref name="cause"/>.</summary>
    public static StepOutcome FailedStepOutcome(Exception? cause) =>
        cause is AgentException { Code: "CANCELLED" or "MODEL_UNAVAILABLE" or "MODEL_PROVIDER_FAILED" or "REPLAY_STALE" } or AgentException { FromActionHook: true } ? StepOutcome.NoVerdict : StepOutcome.Failed;

    private const string ReplayNoticeStart = "replayed ";
    private const string RecordedVerdictSeparator = "; recorded verdict: ";

    /// <summary>
    /// Upstream's <c>recordedVerdictOf</c>: the recorded verdict alone, for the ledger later steps read. How the step was served is
    /// the report's business, not context a model should reason about. A summary that is not a replay notice is returned as it is.
    /// </summary>
    public static string RecordedVerdictOf(string summary)
    {
        if (!summary.StartsWith(ReplayNoticeStart, StringComparison.Ordinal))
        {
            return summary;
        }

        var at = summary.IndexOf(RecordedVerdictSeparator, StringComparison.Ordinal);
        return at == -1 ? summary : summary[(at + RecordedVerdictSeparator.Length)..];
    }

    /// <summary>Records one committed grammar action into the step trace.</summary>
    public void Record(RecordedAction action) => _recorder.Record(action);

    /// <summary>
    /// Opens the step: reads the cache entry, captures the start screen, then attempts a zero-turn replay of a hit. Returns
    /// the self-finalized verdict on a full replay whose postcondition holds; null dispatches the executor, after a miss
    /// from the top, or after a divergence mid-step with <see cref="ReplayedPrefix"/> set. Every failure to replay is a
    /// miss, never an error; only runtime hard stops propagate, and under strict mode a stale recording.
    /// </summary>
    public async Task<StepVerdict?> BeginAsync()
    {
        // Outside strict mode, a retry records like any step but never replays; the report says so instead of looking
        // like a step that ran with caching off.
        // A screen that cannot be read is the executor's business: the step runs, nothing is replayed or recorded.
        _startedMs = NowMs();
        _start = await ProbeAsync(SettleMode.HeldStill).ConfigureAwait(false);
        if (_start is null)
        {
            return null;
        }

        var read = _options.ReplayEligible ? ReadEntry() : new EntryRead(null, "retry");
        if (read.Entry is null)
        {
            _info = Missed(read.Reason!, 0);
            if (!Pending.ReadFailed)
            {
                FailIfStale();
            }

            if (string.Equals(read.Reason, "no-entry", StringComparison.Ordinal))
            {
                FailIfRekeyed();
            }

            return null;
        }

        var verdict = await ReplayEntryAsync(read.Entry).ConfigureAwait(false);
        if (verdict is null)
        {
            FailIfStale();
        }

        return verdict;
    }

    /// <summary>
    /// Closes the session once the step has settled. In read-write mode a passed step stages its recording for the attempt's
    /// settlement: the entry it replayed whole, to keep, or the trace it recorded. A passed step that an end-mismatch hand-off
    /// had to repair evicts the entry: every recorded action ran and the effect was still missing, so the recorded flow is
    /// proven not to produce it. A step that failed or had no verdict stages nothing; the attempt's settlement evicts what a
    /// failed step consumed, and spares it when nothing judged the app.
    /// </summary>
    public async Task ConcludeAsync(StepOutcome outcome, string? recordedBy = null)
    {
        if (_handedOffMs is not null)
        {
            _executorCompletedMs = NowMs();
        }

        if (outcome != StepOutcome.Passed)
        {
            return;
        }

        if (_wholeReplay is not null)
        {
            Pending.Completed = true;
            Pending.ReplayedWhole = true;
            Pending.Entry = _wholeReplay;
            return;
        }

        if (_actionsAtEndMismatch >= 0 && _recorder.RecordedCount > _actionsAtEndMismatch)
        {
            if (_options.Write)
            {
                // A store that throws did not delete the entry, so nothing is reported as evicted.
                try
                {
                    _options.Cache.Delete(Pending.Key);
                    Pending.Decided = CacheWrite.Evicted;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The cache is disposable: the settlement decides what becomes of the entry.
                }
            }

            return;
        }

        Pending.Completed = true;
        // Only a cache that writes keeps the recording, so only it waits for the screen to settle: after the last action's
        // change, held still. A step that took no action has nothing to record.
        Pending.Entry = _options.Write && _start is not null && _recorder.RecordedCount > 0 ? await StageAsync(CacheEntry.ReadRecordedBy(recordedBy)).ConfigureAwait(false) : null;
        if (_changedNothing && !Pending.ReadEntry)
        {
            // A step that read an entry and recorded nothing evicts it at the settlement instead.
            Pending.Decided = CacheWrite.NoChange;
        }

        if (_info is not null && Pending.ParamCollision && Pending.Entry is not null)
        {
            // The recording cannot be templated safely, so it is not written, and the result says why.
            _info = new CacheInfo { Mode = _info.Mode, Reason = _info.Reason, ReplayedActions = _info.ReplayedActions, TotalActions = _info.TotalActions, Detail = _info.Detail, NotRecorded = "param-collision", Entry = _info.Entry };
        }
    }

    // A store that cannot be read is a miss, never a failed step: the cache is disposable.
    private EntryRead ReadEntry()
    {
        CacheLookup lookup;
        try
        {
            lookup = _options.Cache.Read(Pending.Key);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Pending.ReadFailed = true;
            return new EntryRead(null, "invalid-entry");
        }

        if (lookup.Entry is not { } entry)
        {
            return new EntryRead(null, lookup.Reason ?? "no-entry");
        }

        Pending.ReadEntry = true;

        // Any store can return a hit, so its shape is checked here, at the one read site.
        return entry.Schema != FileStepCache.SchemaVersion || entry.Actions is not { Count: > 0 } || entry.Appeared is null || entry.Gone is null || !entry.HasValidEndWait() || !entry.Actions.All(action => action.HasValidModifiers() && action.HasValidSpans())
            ? new EntryRead(null, "invalid-entry")
            : new EntryRead(entry, null);
    }

    // Under strict mode, ends a step whose recording exists but no longer replays, rather than handing it to the executor: a
    // committed recording that went stale is a change to re-record in review, not a model call to absorb on every run.
    // A recording that is absent, or that stops at a project tool, is no stale recording.
    private void FailIfStale()
    {
        var reason = _info?.Reason;
        if (!_options.Strict || reason is null or "no-entry" or "gap")
        {
            return;
        }

        throw new AgentException(
            "REPLAY_STALE",
            "The recording for '" + _options.Instruction + "' no longer matches (" + reason + "). Strict cache mode does not run it live; re-record it without cache.strict.");
    }

    // Under strict mode, ends a step whose key found no entry while the cache directory holds a recording made for the same
    // step under another key: the runner, the engine, the app, or the agent's context changed since, and the recording no longer replays. A step
    // whose instruction or params changed is a new step and still runs live. A custom IStepCache is not checked.
    private void FailIfRekeyed()
    {
        if (!_options.Strict
            || _options.Cache is not FileStepCache files
            || files.UnderAnotherKey(Pending.Key, Pending.Step!) is not { } previous)
        {
            return;
        }

        var changes = Pending.Step!.KeyedBy is { } now ? Identity.KeyContextChanges(previous.KeyedBy, now) : [];
        var why = changes.Count == 0 ? "the runner, the engine, the app, or the agent's context changed" : string.Join(", ", changes) + " changed";

        throw new AgentException(
            "REPLAY_STALE",
            "The recording for '" + _options.Instruction + "' sits under another cache key (" + previous.Key + ".json), since " + why + " after it was recorded. Strict cache mode does not run it live; re-record it without cache.strict.");
    }

    // Replays one hit: decides it against the start route, runs the recorded actions, then checks the postcondition. Actions
    // that all ran prove the clicks happened; only the postcondition proves the save took. A flow whose destination changed,
    // or whose effect is not on screen again, hands off like any other divergence.
    private async Task<StepVerdict?> ReplayEntryAsync(CacheEntry entry)
    {
        Pending.RecordedActions = entry.Actions.Count;
        _recordedBy = CacheEntry.ReadRecordedBy(entry.RecordedBy);
        if (Decide.DecideTraceReplay(entry, _start is null ? null : ScreenPath(_start), _options.Params) is { Reason: { } missReason })
        {
            _info = Missed(missReason, entry.Actions.Count);
            return null;
        }

        Pending.ConsumedReplay = true;

        // Every screen the replay looks at, in order, from the start: the end check measures the recorded delta from the first
        // of them on the route the replay ends on.
        var screens = new List<Observation>();
        if (_start is not null)
        {
            screens.Add(_start);
        }

        var endRoute = Recorded(entry.EndRoute, _options.Params);
        var watched = new WatchedHost(_host, screens);
        var outcome = await Replay.ReplayTrace(
            watched,
            entry.Actions,
            new ReplayOptions { Initial = _start, LooksBeforeFree = () => screens.Count == 0 || !CacheRoute.Same(ScreenPath(screens[^1]), endRoute) }).ConfigureAwait(false);

        // Where the replay stopped, for the report: the recorded action, one-based, and what went wrong there when
        // more than the reason says.
        string StoppedAt(string? why = null) =>
            "at action " + (_recorder.RecordedCount + 1).ToString(CultureInfo.InvariantCulture) + " of " + entry.Actions.Count.ToString(CultureInfo.InvariantCulture) + ", " + DefaultAgent.Describe(entry.Actions[_recorder.RecordedCount]) + (why is null ? "" : ": " + why);

        if (!outcome.Completed)
        {
            var reason = ReplayHandOffReasons.Name(outcome.StopReason!.Value);
            if (outcome.Executed == 0)
            {
                // A prefix that performed nothing is a miss with a name, not a hand-off: the executor starts from the top and owes
                // the notice nothing, whatever stopped the replay.
                _recorder.Clear();
                _info = Missed(reason, entry.Actions.Count, StoppedAt(outcome.StopReason is ReplayHandOffReason.ActionUncertain or ReplayHandOffReason.ActionFailed ? outcome.Detail : null));
                return null;
            }

            switch (outcome.StopReason)
            {
                case ReplayHandOffReason.ActionUncertain:
                    HandOff(reason, StoppedAt(outcome.Detail), outcome.UncertainAction);
                    return null;
                case ReplayHandOffReason.ActionFailed:
                    HandOff(reason, StoppedAt(outcome.Detail));
                    return null;
                default:
                    HandOff(reason, StoppedAt());
                    return null;
            }
        }

        var end = await WaitForEndAsync(entry, screens).ConfigureAwait(false);
        if (!end.Matches)
        {
            HandOff("end-mismatch", StopDetail(entry, end));
            return null;
        }

        _wholeReplay = entry;
        _info = new CacheInfo { Mode = "self-finalized", Entry = Pending.Key, ReplayedActions = _recorder.RecordedCount, TotalActions = entry.Actions.Count };
        return new StepVerdict(StepVerdictStatus.Passed, "Replayed recorded actions.");
    }

    private long NowMs() => _host.Time.GetUtcNow().ToUnixTimeMilliseconds();

    // Upstream's endWaitBaselineMs: the app-settling baseline, the executor's verdict or, if it acted, its last action. The
    // replay's and the executor's own turns are not app settling time.
    private long EndWaitBaselineMs()
    {
        var lastActionAtMs = _recorder.LastActionAtMs ?? _startedMs;
        return _handedOffMs is null || (_recorder.LastActionAtMs ?? 0) > _handedOffMs ? lastActionAtMs : _executorCompletedMs ?? _handedOffMs.Value;
    }

    private void HandOff(string reason, string? detail, string? uncertain = null)
    {
        _handedOffMs = NowMs();
        if (string.Equals(reason, "end-mismatch", StringComparison.Ordinal))
        {
            _actionsAtEndMismatch = _recorder.RecordedCount;
        }

        _prefix = new ReplayedPrefix(_recorder.Actions.Select(DefaultAgent.Describe).ToList(), Pending.RecordedActions, ReplayHandOffReasons.Parse(reason), uncertain);
        _info = new CacheInfo { Mode = "agent-concluded", Entry = Pending.Key, Reason = reason, ReplayedActions = _recorder.RecordedCount, TotalActions = Pending.RecordedActions, Detail = detail };
    }

    private CacheInfo Missed(string reason, int totalActions, string? detail = null) =>
        new() { Mode = "missed", Entry = Pending.Key, Reason = reason, ReplayedActions = _recorder.RecordedCount, TotalActions = totalActions, Detail = detail };

    // The last action may start a navigation or a slow render, so the end route and anchors get the replay timeout to show up.
    // The replay must also have produced the delta: an outcome already on screen before the actions proves nothing.
    // A failure says which of these failed, for the report: an outcome that already showed, or the anchors that
    // did not hold on the last look the wait took on the recorded end route.
    private async Task<EndStateCheck> WaitForEndAsync(CacheEntry entry, List<Observation> screens)
    {
        var parameters = _options.Params;

        // As a recording reads its delta, the evidence counts from the screen the first action was resolved against.
        // A screen a scroll to a text marked while it paged is not among the screens; the stretch then opens with it.
        var actedOn = _host.FirstActedOn ?? _start;
        var from = actedOn is null ? -1 : screens.IndexOf(actedOn);
        List<Observation> stretch = from >= 0 ? screens.GetRange(from, screens.Count - from) : actedOn is null ? screens : [actedOn, .. screens.Skip(1)];
        var baseline = Baseline(stretch, Recorded(entry.EndRoute, parameters));
        var baselineAnchors = baseline is null ? null : Project(baseline);
        var inputTargets = entry.Actions.Where(HasTarget).Select(action => new RecordedTarget { Role = action.Role, Name = action.Name, TestId = action.TestId });
        if (!Anchors.Evidenced(entry, baselineAnchors, inputTargets))
        {
            // A recording with no delta fails the evidence too, but nothing of it showed before the replay
            // acted: that is the generic detail.
            return entry.Appeared.Count > 0 || entry.Gone.Count > 0 ? new EndStateCheck(false, AlreadyShowing: true) : new EndStateCheck(false);
        }

        var before = baselineAnchors ?? (actedOn is null ? [] : Project(actedOn));
        var endRoute = Recorded(entry.EndRoute, parameters);

        // The last action may start a navigation, so the end route gets the replay timeout to show up. A route that never matches is
        // another screen, whatever it shows.
        var arrived = await EndScreenAsync(endRoute).ConfigureAwait(false);
        if (arrived is null)
        {
            return new EndStateCheck(false);
        }

        // Every look the wait takes must still be on the recorded end route: a screen that moved on after the route first matched
        // is another screen.
        DeltaMismatch? mismatch = null;
        bool Holds(Observation screen)
        {
            if (!CacheRoute.Same(endRoute, screen.Route))
            {
                return false;
            }

            mismatch = Anchors.Mismatch(entry, Project(screen), before);
            return mismatch is null;
        }

        var matches = await Replay.VerifyEndState(_host, Holds, new VerifyEndStateOptions { WaitMs = entry.EndWaitMs, Initial = arrived }).ConfigureAwait(false);
        return matches ? new EndStateCheck(true) : new EndStateCheck(false, Failure: mismatch);
    }

    // Upstream's endScreen: captures the end state once its route matches the recording, polling while a navigation the last
    // action started commits. Null when the route never matches within the replay timeout.
    private async Task<Observation?> EndScreenAsync(string? endRoute)
    {
        var deadline = _host.Time.GetUtcNow() + _host.ReplayTimeout;
        while (true)
        {
            // Upstream's probeScreen: a surface that cannot be observed right now is an end-state miss, not a failure.
            var observation = await ProbeAsync(SettleMode.Raw).ConfigureAwait(false);
            if (observation is null)
            {
                return null;
            }

            if (CacheRoute.Same(endRoute, ScreenPath(observation)))
            {
                return observation;
            }

            // Upstream also stops once the step clock cannot cover another delay, so the miss hands off with time left.
            if (_host.Time.GetUtcNow() >= deadline || _host.RemainingMs() <= 20)
            {
                return null;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), _host.Time, _host.Signal).ConfigureAwait(false);
        }
    }

    // The cache is disposable: a passing step whose end screen cannot be read records nothing, and still passes.
    private async Task<CacheEntry?> StageAsync(string? recordedBy)
    {
        var end = await ProbeAsync(SettleMode.HeldStill).ConfigureAwait(false);
        return end is null ? null : BuildEntry(_host.FirstActedOn ?? _start!, end, recordedBy);
    }

    // A step that changed nothing a replay could check, no node and no route, records nothing:
    // its recording would replay on mechanics alone.
    private CacheEntry? BuildEntry(Observation baseline, Observation end, string? recordedBy)
    {
        var routeMoved = !CacheRoute.Same(ScreenPath(baseline), ScreenPath(end));
        var (appeared, gone) = Anchors.Describe(Project(baseline), Project(end), routeMoved);
        if (appeared.Count == 0 && gone.Count == 0 && !routeMoved)
        {
            _changedNothing = true;
            return null;
        }

        // How long the app took to show its end state after the last action, plus room for a slower day: the budget a replay waits
        // for the recorded end state to return. Measured from the last action, not the step's start: the model's thinking time before
        // that action is no reason for a replay, which does not think, to wait.
        return _recorder.Finalize(Pending.Step!, Template(ScreenPath(_start!)), Template(ScreenPath(end)), appeared, gone, recordedBy, NowMs() - EndWaitBaselineMs() + EndWaitMarginMs);
    }

    // One cache capture, settled as far as mode asks, or null when the surface cannot be observed right now, which is the
    // executor's business, not the cache's. Runtime hard stops (timeout, cancellation) are the step's truth even when they
    // land during cache bookkeeping, and propagate.
    private async Task<Observation?> ProbeAsync(SettleMode mode)
    {
        try
        {
            return await _host.ObserveAsync(mode).ConfigureAwait(false);
        }
        catch (EngineException ex) when (!StepExecutors.IsRuntimeHardStop(ex) && !_host.Signal.IsCancellationRequested)
        {
            return null;
        }
    }

    // Upstream's ObservedScreen.path: where the screen is, as the cache compares it (CacheRoute.AppLocation).
    private string ScreenPath(Observation observation) => CacheRoute.AppLocation(observation.Route, _options.AppOrigin);

    // A route is recorded redacted, with each unique() value as its slot, and read back with this call's values.
    private string Template(string route) => CacheKeys.Template(_host.Redact.Redact(route), _options.Params);

    private static string? Recorded(string? route, IReadOnlyDictionary<string, object?>? parameters) =>
        route is null ? null : CacheKeys.Detemplate(route, parameters);

    // Anchors as the recording stores them: a name or text holding a unique param is skipped, every field has
    // secrets redacted, and a name, text, or value has each unique param as its slot. The secrets are read as
    // they are now, so an unchanged node is no delta.
    private List<AnchorNode> Project(Observation observation)
    {
        var redactor = _host.Redact;
        var parameters = _options.Params;
        return Anchors.Project(
            observation,
            text => ContainsParam(text, parameters),
            value => CacheKeys.Template(redactor.Redact(value), parameters),
            redactor.Redact,
            (text, limit) => AgentObservation.RedactText(text, redactor, limit));
    }

    private static bool ContainsParam(string name, IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters is null)
        {
            return false;
        }

        foreach (var value in parameters.Values)
        {
            if (value is UniqueValue unique && unique.Value.Length > 0 && name.Contains(unique.Value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // The first screen of the replay's last stretch on the end route, or null when its last screen before the end
    // was on another route, so the last action moved it.
    private Observation? Baseline(List<Observation> screens, string? endRoute)
    {
        Observation? baseline = null;
        foreach (var screen in screens)
        {
            baseline = CacheRoute.Same(ScreenPath(screen), endRoute) ? baseline ?? screen : null;
        }

        return baseline;
    }

    // Where and why a replay handed the step back, for the report: for an end state that did not hold, why: the
    // outcome already showed before the replay acted, or the anchors that failed on the last look. With neither
    // known, the screen was never read on the recorded route, and the detail says what the recording expected there.
    private string StopDetail(CacheEntry entry, EndStateCheck end)
    {
        var route = Recorded(entry.EndRoute, _options.Params);
        var where = string.IsNullOrEmpty(route) ? "" : " on " + route;
        if (end.AlreadyShowing)
        {
            return "every recorded action ran, but the recorded outcome already showed" + where + " before the replay acted there, so nothing proves the replay produced it";
        }

        var what = end.Failure is { } failure
            ? MismatchParts(failure)
            : entry.Appeared.Count > 0 ? "expected " + string.Join(", ", entry.Appeared.Take(2).Select(DescribeAnchor)) : "";
        return "every recorded action ran, but the recorded end state did not show" + where + (what.Length == 0 ? "" : ": " + what);
    }

    // A recorded anchor as the detail names it: the node, then its text when the name alone would hide the state
    // the recording expects (status "Marker" text="saved").
    private static string DescribeAnchor(RecordedTarget anchor)
    {
        return anchor.Text is not null && anchor.Name is not null && anchor.Text != anchor.Name
            ? RecordedTarget.TargetLabel(anchor) + " text=" + Quote(TextRules.Bound(anchor.Text, 40))
            : RecordedTarget.TargetLabel(anchor);
    }

    private static string Quote(string text) => LocatorExpect.Quote(text);

    // The anchors a mismatch names, two of each kind at most: those the replay did not bring up, those it did not
    // clear, and alerts it raised that the recording never saw. Kinds are joined with commas, never semicolons: a
    // report line already separates its own parts with those.
    private static string MismatchParts(DeltaMismatch mismatch)
    {
        static IEnumerable<string> Part(string label, IReadOnlyList<RecordedTarget> anchors)
        {
            if (anchors.Count == 0)
            {
                yield break;
            }

            var more = anchors.Count > 2 ? " (+" + (anchors.Count - 2).ToString(CultureInfo.InvariantCulture) + " more)" : "";
            yield return label + " " + string.Join(" and ", anchors.Take(2).Select(DescribeAnchor)) + more;
        }

        return string.Join(", ", Part("missing", mismatch.Missing).Concat(Part("still showing", mismatch.Present)).Concat(Part("unrecorded", mismatch.UnrecordedAlerts)));
    }

    /// <summary>One store read: an entry, or why none was read.</summary>
    private readonly record struct EntryRead(CacheEntry? Entry, string? Reason);

    /// <summary>
    /// The replay host, watched: every look the replay takes is kept, in order, for the end check. The port's replay also needs
    /// the host's action slots and recording, which pass straight through.
    /// </summary>
    private sealed class WatchedHost(IStepCacheHost inner, List<Observation> screens) : IReplayHost
    {
        public IExecutorActions Actions => inner.Actions;

        public CancellationToken Signal => inner.Signal;

        public Redactor Redact => inner.Redact;

        public TimeSpan ReplayTimeout => inner.ReplayTimeout;

        public int RemainingMs() => inner.RemainingMs();

        public TimeProvider Time => inner.Time;

        public async Task<Observation> ObserveAsync(SettleMode mode)
        {
            var screen = await inner.ObserveAsync(mode).ConfigureAwait(false);
            screens.Add(screen);
            return screen;
        }

        public bool TryReserve() => inner.TryReserve();

        public void MarkActing() => inner.MarkActing();

        public Task CommitAsync(RecordedAction action) => inner.CommitAsync(action);
    }
}

/// <summary>
/// The end check's verdict and, when it is known, why it failed: the recorded outcome already showed before the
/// replay acted, or the anchors that did not hold.
/// </summary>
internal readonly record struct EndStateCheck(bool Matches, bool AlreadyShowing = false, DeltaMismatch? Failure = null);
