// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using E2E.Cache;
using E2E.Engine;
using E2E.Internal;

namespace E2E;

// The harness of an act step (upstream's agent/act.ts, step-cache.ts): the budget, the cache replay and recording, and the
// verdict the step ends with. What thinks is the step's executor (see Executor.cs), which a built-in tool loop is.
public sealed partial class Agent
{
    private async Task<ActResult> RunActStepAsync(string instruction, ActOptions? options, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instruction);
        CheckInstruction(instruction);
        var agent = _scope.Select(options?.Agent);
        var maxCalls = Budget(options?.MaxModelCalls, agent.MaxModelCalls, "MaxModelCalls");
        var budget = new StepAccounting(Budget(options?.MaxSteps, agent.MaxSteps, "MaxSteps"), maxCalls);
        var timeout = ResolveTimeout(options?.Timeout, _scope.StepTimeout);
        ActParams.Validate(options?.Params);
        var callsBefore = _scope.ModelCalls;
        var projectTools = DefaultAgent.ProjectToolsFor(agent, _scope.EnginePlatform);
        var offeredProject = projectTools.Select(pair => AgentTools.Project(pair.Key, pair.Value)).ToList();
        using var linked = Link(cancellationToken, timeout);
        var token = linked.Token;
        _scope.Remember(options?.Params);
        var key = _scope.ClaimKey(instruction, options?.Params, agent, out var callIndex);
        var pending = new PendingAct
        {
            Key = key,
            ParamCollision = CacheKeys.Collides(options?.Params),
            Step = Provenance(instruction, options?.Params, callIndex, agent.Name),
        };
        if (_scope.CacheEnabled)
        {
            _scope.Acts.Add(pending);
        }

        // The baseline a recording's delta is read from, the screen a replay starts on, and the model's first look:
        // a settled screen, as upstream's start capture is.
        _feed = new ObservationFeed(_scope.Session, TimeProvider.System);
        var start = await _feed.ObserveAsync(SettleMode.HeldStill, token).ConfigureAwait(false);
        var actions = budget.Recorded;
        var dispatcher = new ActionDispatcher(_scope, _feed, budget, options?.Params);
        CacheInfo? info = null;
        var handoff = false;
        var actionsAtEndMismatch = -1;
        if (_scope.CacheEnabled && _scope.ReplayEligible && _scope.Cache is not null)
        {
            var replay = await TryReplayAsync(pending, start, actions, budget, dispatcher, options?.Params, token).ConfigureAwait(false);
            info = new CacheInfo { Mode = replay.Info!.Mode, Reason = replay.Info.Reason, ReplayedActions = actions.Count, TotalActions = pending.RecordedActions, Detail = replay.Info.Detail };
            handoff = replay.Handoff;
            // A store that could not be read says nothing about a recording, so strict mode runs the step live too.
            // So does a recording that stops at a project tool: as upstream, a gap is no stale recording, only a step the cache cannot finish.
            if (_scope.CacheStrict && !replay.Completed && !pending.ReadFailed && info?.Reason is not null and not ("no-entry" or "gap"))
            {
                throw new AgentException(
                    "REPLAY_STALE",
                    "The recording for '" + instruction + "' no longer matches (" + info.Reason + "). Strict cache mode does not run it live; re-record it without cache.strict.");
            }

            if (_scope.CacheStrict && string.Equals(info?.Reason, "no-entry", StringComparison.Ordinal))
            {
                FailIfRekeyed(pending, instruction);
            }

            if (replay.Completed)
            {
                pending.Completed = true;
                pending.ReplayedWhole = true;
                pending.Entry = replay.Entry;
                _scope.Completed.Add("Replayed: " + instruction);
                return new ActResult { Summary = "Replayed recorded actions.", Cache = info, ModelCalls = 0, Actions = budget.Used };
            }

            if (handoff && string.Equals(info?.Reason, "end-mismatch", StringComparison.Ordinal))
            {
                actionsAtEndMismatch = actions.Count;
            }
        }
        else if (_scope.CacheEnabled && _scope.Cache is not null)
        {
            // Outside strict mode, a retry records like any attempt but never replays.
            _scope.Missed++;
            info = ReplayAttempt.Miss("retry").Info;
        }

        var executor = DefaultAgent.CreateBuiltInAgent(new BuiltInAgentOptions
        {
            Host = this,
            Scope = _scope,
            Feed = _feed,
            Agent = agent,
            ProjectTools = projectTools,
            OfferedProject = offeredProject,
            CancellationToken = cancellationToken,
        });
        var verdict = await executor.RunStepAsync(
            new StepExecutorContext
            {
                Step = new ExecutorStep { Kind = ExecutorStepKind.Act, Instruction = instruction, Params = options?.Params },
                ReplayedPrefix = handoff ? new ReplayedPrefix(actions.ToList(), ReplayHandOffReasons.Parse(info?.Reason)) : null,
                Start = new ExecutorObservation(SnapshotText.Render(start, _scope.Redactor, _scope.AppOrigin)),
                Actions = dispatcher,
                Budgets = budget,
            },
            token).ConfigureAwait(false);

        var summary = verdict.Summary;
        var code = verdict.ErrorCode;
        if (budget.Exhausted && verdict.Status == StepVerdictStatus.Passed)
        {
            // The model cannot declare success over the runtime's own accounting.
            throw budget.Stop(summary);
        }

        if (verdict.Status == StepVerdictStatus.Passed)
        {
            if (actionsAtEndMismatch >= 0 && actions.Count > actionsAtEndMismatch)
            {
                // The recorded actions ran but did not reach the recorded end, so they are proven
                // not to produce it. Evict the entry and let a clean run record the flow again.
                if (_scope.CacheWrite)
                {
                    _scope.Cache!.Delete(key);
                }

                _scope.Completed.Add(summary.Length == 0 ? instruction : summary);
                return new ActResult { Summary = summary, Cache = info, ModelCalls = _scope.ModelCalls - callsBefore, Actions = budget.Used };
            }

            pending.Completed = true;
            // Only a cache that writes keeps the recording, so only it waits for the screen to settle: after the
            // last action's change, held still.
            pending.Entry = _scope.CacheWrite
                ? await StageAsync(pending.Step!, start, actions, options?.Params, token).ConfigureAwait(false)
                : null;
            _scope.Completed.Add(summary.Length == 0 ? instruction : summary);
            if (info is not null && pending.ParamCollision && pending.Entry is not null)
            {
                // The recording cannot be templated safely, so it is not written, and the result says why.
                info = new CacheInfo { Mode = info.Mode, Reason = info.Reason, ReplayedActions = info.ReplayedActions, TotalActions = info.TotalActions, Detail = info.Detail, NotRecorded = "param-collision" };
            }

            return new ActResult { Summary = summary, Cache = info, ModelCalls = _scope.ModelCalls - callsBefore, Actions = budget.Used };
        }

        if (verdict.Status == StepVerdictStatus.Blocked)
        {
            throw new AgentException(code!, summary.Length == 0 ? "The step is blocked." : summary, blocked: true);
        }

        // A failure the model gave no code for inherits the exhausted budget, so the report names it.
        if (code is null && budget.Exhausted)
        {
            throw budget.Stop(summary);
        }

        throw new AgentException(code ?? "ACTION_FAILED", summary.Length == 0 ? "The step failed." : summary);
    }

    private async Task<ReplayAttempt> TryReplayAsync(
        PendingAct pending,
        Observation start,
        List<RecordedAction> actions,
        StepAccounting budget,
        ActionDispatcher dispatcher,
        IReadOnlyDictionary<string, object?>? parameters,
        CancellationToken token)
    {
        CacheLookup lookup;
        try
        {
            lookup = _scope.Cache!.Read(pending.Key);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The cache is disposable: a store that cannot be read is a miss, never a failed step.
            pending.ReadFailed = true;
            _scope.Missed++;
            return ReplayAttempt.Miss("invalid-entry");
        }

        if (lookup.Entry is null)
        {
            _scope.Missed++;
            return ReplayAttempt.Miss(lookup.Reason ?? "no-entry");
        }

        var entry = lookup.Entry;
        pending.ReadEntry = true;

        // Any store can return a hit, so its shape is checked here, at the one read site.
        if (entry.Schema != FileStepCache.SchemaVersion || entry.Actions is not { Count: > 0 } || entry.Appeared is null || entry.Gone is null)
        {
            _scope.Missed++;
            return ReplayAttempt.Miss("invalid-entry");
        }

        pending.RecordedActions = entry.Actions.Count;
        // A recording that opens with navigate sets up its own start screen.
        var opensWithNavigate = string.Equals(entry.Actions[0].Kind, "navigate", StringComparison.Ordinal);
        if (!opensWithNavigate && !CacheRoute.Same(Recorded(entry.Route, parameters), start.Route))
        {
            _scope.Missed++;
            return ReplayAttempt.Miss("wrong-context");
        }

        pending.ConsumedReplay = true;

        // Every screen the replay looks at, in order. The end check measures the recorded delta from the first of them
        // on the route the replay ends on.
        var screens = new List<Observation> { start };
        var started = false;
        RecordedAction? previous = null;

        // The look before an action: the start serves the first one; after that, the previous action's settle policy
        // says how far a fresh capture settles.
        SettleMode? Look() => previous is null ? null : SettlePolicy.For(previous).Look;
        ReplayAttempt Lost(string? reason)
        {
            if (!started)
            {
                _scope.Missed++;
                actions.Clear();
                return ReplayAttempt.Miss(reason ?? "target-not-found");
            }

            _scope.HandedOff++;
            return ReplayAttempt.Hand(reason ?? "target-not-found");
        }

        foreach (var action in entry.Actions)
        {
            // A project tool ran here, and the grammar cannot reproduce it: the replay ends and the model takes over.
            if (string.Equals(action.Kind, "tool", StringComparison.Ordinal))
            {
                return Lost("gap");
            }

            // An action with no target reads no screen, so the screen the previous action left would go unseen.
            // Look at it, settled as the previous action's policy asks, until one on the recorded end route has been seen.
            if (Look() is { } before && !HasTarget(action) && !CacheRoute.Same(screens[^1].Route, Recorded(entry.EndRoute, parameters)))
            {
                screens.Add(await _feed.ObserveAsync(before, token).ConfigureAwait(false));
            }

            if (string.Equals(action.Kind, "navigate", StringComparison.Ordinal))
            {
                if (!budget.TryReserve())
                {
                    _scope.HandedOff++;
                    return ReplayAttempt.Hand("action-budget");
                }

                await _scope.Session.OpenAsync(Routes.Resolve(_scope.BaseUrl, CacheKeys.Detemplate(action.Url ?? "/", parameters)), token).ConfigureAwait(false);
                _feed.ArmAfter(action);
                actions.Add(action);
                started = true;
                previous = action;
                continue;
            }

            // A replayed action draws on the step's action budget like a live one. The first always fits.
            // An action with a target reserves its slot once the target is found, so a lost target is a miss.
            if ((action.Kind is "back" or "scroll" or "scrollUntil" or "press") && !HasTarget(action) && !budget.TryReserve())
            {
                _scope.HandedOff++;
                return ReplayAttempt.Hand("action-budget");
            }

            try
            {
                // back, a viewport scroll, and a key press on the focused control target nothing, so they replay as given.
                // A scroll on a list re-finds the list before each repeat.
                switch (action.Kind)
                {
                    case "back":
                        await _scope.Session.BackAsync(token).ConfigureAwait(false);
                        break;
                    case "press" when !HasTarget(action):
                        await _scope.Session.PressAsync(action.Key ?? "Enter", token).ConfigureAwait(false);
                        break;
                    case "scroll" or "scrollUntil" when !TryDirection(action.Direction, out _):
                        return Lost("invalid-entry");
                    case "scroll" when !HasTarget(action):
                        // A viewport scroll relocates nothing, so each later repeat takes a settled look of its own, as
                        // the live loop did between them.
                        for (var repeat = 0; repeat < (action.Times ?? 1); repeat++)
                        {
                            if (repeat > 0)
                            {
                                screens.Add(await _feed.ObserveAsync(SettleMode.HeldStill, token).ConfigureAwait(false));
                            }

                            await _scope.Session.SwipeAsync(Direction(action.Direction), token).ConfigureAwait(false);
                            _feed.ArmAfter(action);
                        }

                        break;
                    case "scroll":
                        // A scroll on a list is paced by the relocation before each repeat.
                        for (var repeat = 0; repeat < (action.Times ?? 1); repeat++)
                        {
                            var list = await WaitForTargetAsync(action, repeat == 0 ? Look() : SettleMode.HeldStill, screens, token).ConfigureAwait(false);
                            if (list.Node is null)
                            {
                                return Lost(list.Reason);
                            }

                            // One slot for the whole scroll, taken once its list is found.
                            if (repeat == 0 && !budget.TryReserve())
                            {
                                _scope.HandedOff++;
                                return ReplayAttempt.Hand("action-budget");
                            }

                            await _scope.Session.PerformAsync(list.Node, new LocatorAction.Swipe(Direction(action.Direction)), token).ConfigureAwait(false);
                            _feed.ArmAfter(action);
                            started = true;
                        }

                        break;
                    case "scrollUntil":
                        if (HasTarget(action))
                        {
                            var list = await WaitForTargetAsync(action, Look(), screens, token).ConfigureAwait(false);
                            if (list.Node is null)
                            {
                                return Lost(list.Reason);
                            }

                            if (!budget.TryReserve())
                            {
                                _scope.HandedOff++;
                                return ReplayAttempt.Hand("action-budget");
                            }
                        }

                        var text = CacheKeys.Detemplate(action.Text ?? "", parameters);
                        await dispatcher.PageUntilAsync(text, Direction(action.Direction), HasTarget(action) ? action : null, token).ConfigureAwait(false);
                        break;
                    default:
                        var found = await WaitForTargetAsync(action, Look(), screens, token).ConfigureAwait(false);
                        if (found.Node is null)
                        {
                            return Lost(found.Reason);
                        }

                        if (!budget.TryReserve())
                        {
                            _scope.HandedOff++;
                            return ReplayAttempt.Hand("action-budget");
                        }

                        var performed = ResolveSecret(Detemplate(action, parameters));
                        await PerformRecordedAsync(found.Node, performed, token).ConfigureAwait(false);
                        break;
                }

                if (action.Kind is not "scroll")
                {
                    _feed.ArmAfter(action);
                }

                actions.Add(action);
                started = true;
                previous = action;
            }
            catch (E2EException ex) when (!StepExecutors.IsRuntimeHardStop(ex) && !token.IsCancellationRequested)
            {
                // A step or test timeout cancels the token. That is the step's own end, not a stale recording.
                _scope.HandedOff++;
                return ReplayAttempt.Hand("action-failed");
            }
        }

        var end = await WaitForEndAsync(entry, start, screens, parameters, token).ConfigureAwait(false);
        if (!end.Matches)
        {
            _scope.HandedOff++;
            return ReplayAttempt.Hand("end-mismatch", StopDetail(entry, end, parameters));
        }

        _scope.Replayed++;
        return ReplayAttempt.Done(entry);
    }

    // The last action may start a navigation or a slow render, so the end route and anchors get the replay timeout to show up.
    // The replay must also have produced the delta: an outcome already on screen before the actions proves nothing.
    // A failure says which of these failed, for the report: an outcome that already showed, or the anchors that
    // did not hold on the last look the wait took on the recorded end route.
    private async Task<EndStateCheck> WaitForEndAsync(
        CacheEntry entry,
        Observation start,
        List<Observation> screens,
        IReadOnlyDictionary<string, object?>? parameters,
        CancellationToken token)
    {
        // As a recording reads its delta, the evidence counts from the screen the first action was resolved against.
        // A screen a scroll to a text marked while it paged is not among the screens; the stretch then opens with it.
        var actedOn = _feed.FirstActedOn ?? start;
        var from = screens.IndexOf(actedOn);
        List<Observation> stretch = from >= 0 ? screens.GetRange(from, screens.Count - from) : [actedOn, .. screens.Skip(1)];
        var baseline = Baseline(stretch, Recorded(entry.EndRoute, parameters));
        var baselineAnchors = baseline is null ? null : Project(baseline, parameters);
        var inputTargets = entry.Actions.Where(HasTarget).Select(action => new RecordedTarget { Role = action.Role, Name = action.Name, TestId = action.TestId });
        if (!Anchors.Evidenced(entry, baselineAnchors, inputTargets))
        {
            // A recording with no delta fails the evidence too, but nothing of it showed before the replay
            // acted: that is the generic detail.
            return entry.Appeared.Count > 0 || entry.Gone.Count > 0 ? new EndStateCheck(false, AlreadyShowing: true) : new EndStateCheck(false);
        }

        var before = baselineAnchors ?? Project(actedOn, parameters);
        var deadline = DateTime.UtcNow + _scope.ReplayTimeout;
        DeltaMismatch? mismatch = null;
        while (true)
        {
            // Every look must still be on the recorded end route: a screen that moved on is another screen.
            var end = await _feed.ObserveAsync(SettleMode.Raw, token).ConfigureAwait(false);
            if (CacheRoute.Same(Recorded(entry.EndRoute, parameters), end.Route))
            {
                mismatch = Anchors.Mismatch(entry, Project(end, parameters), before);
                if (mismatch is null)
                {
                    return new EndStateCheck(true);
                }
            }

            if (DateTime.UtcNow >= deadline)
            {
                return new EndStateCheck(false, Failure: mismatch);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), token).ConfigureAwait(false);
        }
    }

    // Where and why a replay handed the step back, for the report: for an end state that did not hold, why: the
    // outcome already showed before the replay acted, or the anchors that failed on the last look. With neither
    // known, the screen was never read on the recorded route, and the detail says what the recording expected there.
    private static string StopDetail(CacheEntry entry, EndStateCheck end, IReadOnlyDictionary<string, object?>? parameters)
    {
        var route = Recorded(entry.EndRoute, parameters);
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
            ? TargetLabel(anchor) + " text=" + Quote(TextRules.Bound(anchor.Text, 40))
            : TargetLabel(anchor);
    }

    // A node as every report line that names one reads it: its role, then its label in quotes.
    private static string TargetLabel(RecordedTarget target)
    {
        var label = target.Name ?? target.Text ?? target.Placeholder ?? target.TestId ?? "";
        var role = target.Role ?? "node";
        return label.Length == 0 ? role : role + " " + Quote(TextRules.Bound(label, 40));
    }

    private static string Quote(string text)
    {
        return JsonSerializer.Serialize(text, JsonQuote);
    }

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

    // The first screen of the replay's last stretch on the end route, or null when its last screen before the end
    // was on another route, so the last action moved it.
    private static Observation? Baseline(List<Observation> screens, string? endRoute)
    {
        Observation? baseline = null;
        foreach (var screen in screens)
        {
            baseline = CacheRoute.Same(screen.Route, endRoute) ? baseline ?? screen : null;
        }

        return baseline;
    }

    // Re-finds a recorded target. The first look is settled as far as <paramref name="look"/> asks, or is the newest
    // screen, in hand, when it is null; the looks after it read the screen as it is.
    private async Task<(SemanticNode? Node, string? Reason)> WaitForTargetAsync(RecordedAction action, SettleMode? look, List<Observation> screens, CancellationToken token)
    {
        var deadline = DateTime.UtcNow + _scope.ReplayTimeout;
        var first = true;
        while (true)
        {
            Observation observation;
            if (first && look is null && _feed.Latest is { } latest)
            {
                // The first action reads the start screen, which the replay has already seen.
                observation = latest;
            }
            else
            {
                observation = await _feed.ObserveAsync(first ? look ?? SettleMode.Raw : SettleMode.Raw, token).ConfigureAwait(false);
                screens.Add(observation);
            }

            first = false;
            var matches = Find(observation, action.Role, action.Name, action.TestId, null, _scope.Redactor);
            if (matches.Count == 1)
            {
                _feed.MarkActing();
                return (matches[0], null);
            }

            if (matches.Count > 1)
            {
                return (null, "target-ambiguous");
            }

            if (DateTime.UtcNow >= deadline)
            {
                return (null, "target-not-found");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), token).ConfigureAwait(false);
        }
    }

    private async Task PerformRecordedAsync(SemanticNode node, RecordedAction action, CancellationToken token)
    {
        LocatorAction locatorAction = action.Kind switch
        {
            "tap" => new LocatorAction.Tap(),
            "doubleTap" => new LocatorAction.DoubleTap(),
            "fill" => new LocatorAction.Fill(action.Value ?? "", false),
            "press" => new LocatorAction.Press(action.Key ?? "Enter"),
            "select" => new LocatorAction.Select(action.Value ?? ""),
            "check" => new LocatorAction.Check(),
            "uncheck" => new LocatorAction.Uncheck(),
            "clear" => new LocatorAction.Clear(),
            "scrollTo" => new LocatorAction.ScrollIntoView(),
            _ => throw new AgentException("AUTOMATION_UNSUPPORTED", "Cannot replay " + action.Kind + "."),
        };
        await _scope.Session.PerformAsync(node, locatorAction, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Under strict mode, ends a step whose key found no entry while the cache directory holds a
    /// recording made for the same step under another key: the runner or the agent's context changed
    /// since, and the recording no longer replays. A step whose instruction or params changed is a new
    /// step and still runs live. A custom <see cref="IStepCache"/> is not checked.
    /// </summary>
    private void FailIfRekeyed(PendingAct pending, string instruction)
    {
        if (_scope.Cache is not FileStepCache files
            || files.UnderAnotherKey(pending.Key, pending.Step!) is not { } previous)
        {
            return;
        }

        throw new AgentException(
            "REPLAY_STALE",
            "The recording for '" + instruction + "' sits under another cache key (" + previous + ".json), since the runner or the agent's context changed after it was recorded. Strict cache mode does not run it live; re-record it without cache.strict.");
    }

    // A route is recorded redacted, with each unique() value as its slot, and read back with this call's values.
    private string Template(string route, IReadOnlyDictionary<string, object?>? parameters) =>
        CacheKeys.Template(_scope.Redactor.Redact(route), parameters);

    private static string? Recorded(string? route, IReadOnlyDictionary<string, object?>? parameters) =>
        route is null ? null : CacheKeys.Detemplate(route, parameters);

    // The cache is disposable: a passing step whose end screen cannot be read records nothing, and still passes.
    private async Task<CacheEntry?> StageAsync(
        CacheEntry step,
        Observation start,
        List<RecordedAction> actions,
        IReadOnlyDictionary<string, object?>? parameters,
        CancellationToken token)
    {
        Observation end;
        try
        {
            end = await _feed.ObserveAsync(SettleMode.HeldStill, token).ConfigureAwait(false);
        }
        catch (EngineException)
        {
            return null;
        }

        return BuildEntry(step, start.Route, _feed.FirstActedOn ?? start, end, actions, parameters);
    }

    // A step that changed nothing a replay could check, no node and no route, records nothing:
    // its recording would replay on mechanics alone.
    private CacheEntry? BuildEntry(
        CacheEntry step,
        string route,
        Observation baseline,
        Observation end,
        List<RecordedAction> actions,
        IReadOnlyDictionary<string, object?>? parameters)
    {
        var routeMoved = !CacheRoute.Same(baseline.Route, end.Route);
        var (appeared, gone) = Anchors.Describe(Project(baseline, parameters), Project(end, parameters), routeMoved);
        if (appeared.Count == 0 && gone.Count == 0 && !routeMoved)
        {
            return null;
        }

        var entry = new CacheEntry { Schema = step.Schema };
        entry.SetStep(step);
        entry.Route = Template(route, parameters);
        entry.EndRoute = Template(end.Route, parameters);
        entry.Actions = actions.ToList();
        entry.Appeared = appeared;
        entry.Gone = gone;
        return entry;
    }

    /// <summary>
    /// The step an entry is recorded for, as the entry stores it. None of it is replay input, so a
    /// secret value in the test title, instruction, or agent name is masked like any other recorded string.
    /// </summary>
    private CacheEntry Provenance(string instruction, IReadOnlyDictionary<string, object?>? parameters, int callIndex, string agent)
    {
        return new CacheEntry
        {
            Schema = FileStepCache.SchemaVersion,
            Test = _scope.Redactor.Redact(_scope.TestTitle),
            Instruction = _scope.Redactor.Redact(instruction.Trim()),
            Engine = _scope.EnginePlatform,
            ParamsDigest = CacheKeys.ParamsDigest(parameters),
            CallIndex = callIndex,
            Agent = _scope.Redactor.Redact(agent),
        };
    }

    // Anchors as the recording stores them: a name or text holding a unique param is skipped, every field has
    // secrets redacted, and a name, text, or value has each unique param as its slot. The secrets are read as
    // they are now, so an unchanged node is no delta.
    private List<AnchorNode> Project(Observation observation, IReadOnlyDictionary<string, object?>? parameters)
    {
        var redactor = _scope.Redactor;
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

    private static RecordedAction Detemplate(RecordedAction action, IReadOnlyDictionary<string, object?>? parameters)
    {
        return new RecordedAction
        {
            Kind = action.Kind,
            Role = action.Role,
            Name = action.Name,
            TestId = action.TestId,
            Key = action.Key,
            Url = action.Url is null ? null : CacheKeys.Detemplate(action.Url, parameters),
            Value = action.Value is null ? null : CacheKeys.Detemplate(action.Value, parameters),
            Direction = action.Direction,
            Times = action.Times,
            Text = action.Text is null ? null : CacheKeys.Detemplate(action.Text, parameters),
        };
    }

    private RecordedAction ResolveSecret(RecordedAction action)
    {
        if (action.Value is null || !action.Value.StartsWith("<secret:", StringComparison.Ordinal) || !action.Value.EndsWith('>'))
        {
            return action;
        }

        var name = action.Value["<secret:".Length..^1];
        var secret = _scope.Secrets.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal));
        if (secret is null)
        {
            throw new AgentException("AUTH_CREDENTIAL_UNAVAILABLE", "Replay needs secret '" + name + "', and this step did not declare it.");
        }

        return new RecordedAction
        {
            Kind = action.Kind,
            Role = action.Role,
            Name = action.Name,
            TestId = action.TestId,
            Key = action.Key,
            Url = action.Url,
            Value = secret.Value,
            Direction = action.Direction,
            Times = action.Times,
            Text = action.Text,
        };
    }
}
