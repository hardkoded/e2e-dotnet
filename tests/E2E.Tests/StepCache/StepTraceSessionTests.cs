// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;
using static E2E.Tests.StepCache.StepCacheHarness;

namespace E2E.Tests.StepCache;

/// <summary>
/// Upstream's <c>describe('StepTraceSession')</c> of <c>step-cache.test.ts</c>, driven at the session seam. Not ported, because the
/// port has no such feature: the three tests of an observation that loses semantic evidence (the port reads no pixel-only
/// screens), "misses a truncated entry" (no truncated entries), "never stages a trace with no start anchor" (every screen has a
/// route), the two end-wait tests (an entry records no end wait), and "takes the step for the live progress" (no live progress).
/// "keys on the params with each unique() value as a placeholder" is the dispatcher's and the attempt's key claim in the port.
/// </summary>
public sealed class StepTraceSessionTests
{
    [Fact]
    public async Task Turns_a_rejecting_store_read_into_a_miss_instead_of_failing_the_step()
    {
        var session = MakeSession(FakeContext(() => throw new InvalidOperationException("redis connection refused")), MakeHost(["/"]));

        Assert.Null(await session.BeginAsync());

        Assert.Equal(("missed", "invalid-entry", 0, 0), Info(session));
        Assert.Null(session.ReplayedPrefix);
    }

    [Fact]
    public async Task Names_a_retry_attempt_as_the_miss_reason_without_reading_the_store()
    {
        var context = FakeContext(() => new CacheLookup { Reason = "no-entry" }).Retry();
        var session = MakeSession(context, MakeHost(["/"]));

        Assert.Null(await session.BeginAsync());

        Assert.Equal(0, context.Reads);
        Assert.Equal(("missed", "retry", 0, 0), Info(session));
        Assert.Null(session.ReplayedPrefix);
    }

    [Fact]
    public async Task Degrades_a_hit_that_is_not_a_trace_1_entry_to_a_miss_whichever_store_returned_it()
    {
        var malformed = new CacheEntry { Schema = FileStepCache.SchemaVersion, Actions = [] };
        var session = MakeSession(FakeContext(() => new CacheLookup { Entry = malformed }), MakeHost(["/"]));

        Assert.Null(await session.BeginAsync());

        Assert.Equal(("missed", "invalid-entry", 0, 0), Info(session));
    }

    [Fact]
    public async Task Takes_no_unchanged_node_for_the_delta_when_a_secret_registered_after_the_starting_screen_masks_it()
    {
        var context = NoEntry();
        var status = Node("s", "status", "Code token-2718-value");
        var button = Node("b", "button", "Save marker");
        var host = MakeHost(["/storage"], [status, button], [status, button, SavedMarker]);
        var session = MakeSession(context, host);
        await session.BeginAsync();

        // A provider-backed fill resolves the value only now.
        host.Redact = Redactor.For([("token", "token-2718-value")]);
        session.Record(Tap("button", "Save marker"));
        await session.ConcludeAsync(StepOutcome.Passed);

        Assert.Equal([Anchor(SavedAnchor)], session.Pending.Entry!.Appeared.Select(Anchor));
    }

    [Fact]
    public async Task Stages_the_delta_between_the_starting_and_passing_screens_as_end_anchors()
    {
        var heading = Node("h", "heading", "Storage");
        var button = Node("b", "button", "Save marker");
        var session = MakeSession(NoEntry(), MakeHost(["/storage"], [heading, button], [Node("h2", "heading", "Storage"), Node("b2", "button", "Save marker"), SavedMarker]));
        await session.BeginAsync();

        session.Record(Tap("button", "Save marker"));
        await session.ConcludeAsync(StepOutcome.Passed);

        var staged = session.Pending.Entry!;
        Assert.Equal([Anchor(SavedAnchor)], staged.Appeared.Select(Anchor));
        Assert.Equal("/storage", staged.EndRoute);
    }

    /// <summary>Partial port of "records nothing when a unique() value is spelled by another param, and says so in the step detail": the slot in the staged action is the dispatcher's.</summary>
    [Fact]
    public async Task Records_nothing_and_says_so_when_a_unique_value_is_spelled_by_another_param()
    {
        var spelled = NoEntry();
        var session = MakeSession(spelled, MakeHost(["/reminders", "/reminders/7"]), new Dictionary<string, object?> { ["title"] = Values.Unique("Daily"), ["frequency"] = "Daily" });
        await session.BeginAsync();
        session.Record(Navigate("/reminders/new?title=Daily&frequency=Daily"));
        await session.ConcludeAsync(StepOutcome.Passed);
        Settle(spelled, session, verified: true);

        Assert.Empty(spelled.Written);
        Assert.Equal(("missed", "param-collision"), (session.CacheInfo?.Mode, session.CacheInfo?.NotRecorded));

        // The same call with a title of its own records, and the detail carries no such note.
        var clean = NoEntry();
        var other = MakeSession(clean, MakeHost(["/reminders", "/reminders/8"]), new Dictionary<string, object?> { ["title"] = Values.Unique("Water plants"), ["frequency"] = "Daily" });
        await other.BeginAsync();
        other.Record(Navigate("/reminders/new?title=Water+plants&frequency=Daily"));
        await other.ConcludeAsync(StepOutcome.Passed);
        Settle(clean, other, verified: true);

        Assert.Single(clean.Written);
        Assert.Null(other.CacheInfo?.NotRecorded);
    }

    [Fact]
    public async Task Records_the_new_screen_as_anchors_for_a_step_that_moved_to_another_pathname()
    {
        var session = MakeSession(NoEntry(), MakeHost(["/pricing", "/customers?ref=nav"], [], [SavedMarker]));
        await session.BeginAsync();

        session.Record(Navigate("/customers"));
        await session.ConcludeAsync(StepOutcome.Passed);

        Assert.Equal([Anchor(SavedAnchor)], session.Pending.Entry!.Appeared.Select(Anchor));
        Assert.Equal("/customers?ref=nav", session.Pending.Entry.EndRoute);
    }

    [Fact]
    public async Task Self_finalizes_on_a_created_records_page_whose_minted_id_differs_from_the_recording()
    {
        var context = EntryContext(endPath: "/projects/95488a620f65", endAnchors: [SavedAnchor]);
        var session = MakeSession(context, MakeHost(["/pricing", "/projects/0c1d2e3f4a5b"], [], [SavedMarker]));

        var verdict = await session.BeginAsync();

        Assert.Equal(StepVerdictStatus.Passed, verdict?.Status);
        Assert.Equal("self-finalized", session.CacheInfo?.Mode);
    }

    [Fact]
    public async Task Still_refuses_a_different_page_even_when_the_anchors_happen_to_be_on_it()
    {
        var context = EntryContext(endPath: "/projects/95488a620f65", endAnchors: [SavedAnchor]);
        var session = MakeSession(context, MakeHost(["/pricing", "/customers"], [], [SavedMarker]));

        Assert.Null(await session.BeginAsync());

        Assert.Equal(ReplayHandOffReason.EndMismatch, session.ReplayedPrefix?.StopReason);
    }

    [Fact]
    public async Task Refuses_to_self_finalize_when_a_recorded_end_anchor_is_not_on_screen_again()
    {
        var context = EntryContext(endPath: "/customers", endAnchors: [SavedAnchor]);
        var session = MakeSession(context, MakeHost(["/pricing", "/customers"]));

        Assert.Null(await session.BeginAsync());

        Assert.Equal(ReplayHandOffReason.EndMismatch, session.ReplayedPrefix?.StopReason);
        Assert.Equal(["navigate to \"/customers\""], session.ReplayedPrefix?.ReplayedActions);
        var info = session.CacheInfo!;
        Assert.Equal(("agent-concluded", "end-mismatch", 1, 1), (info.Mode, info.Reason, info.ReplayedActions, info.TotalActions));
        Assert.Equal("every recorded action ran, but the recorded end state did not show on /customers: missing status \"Marker\" text=\"saved\"", info.Detail);
    }

    [Fact]
    public async Task Names_the_anchors_that_did_not_hold_on_an_end_mismatch_not_the_first_ones_recorded()
    {
        var announcer = new RecordedTarget { Role = "alert", Name = "Acme Notes" };
        var declined = Node("d1", "alert", "Card declined");
        var quiet = MakeSession(EntryContext(endPath: "/customers", endAnchors: [announcer, SavedAnchor]), MakeHost(["/pricing", "/customers"], [], [SavedMarker]));
        Assert.Null(await quiet.BeginAsync());
        Assert.Equal("end-mismatch", quiet.CacheInfo?.Reason);
        Assert.Equal("every recorded action ran, but the recorded end state did not show on /customers: missing alert \"Acme Notes\"", quiet.CacheInfo?.Detail);

        var raised = MakeSession(EntryContext(endPath: "/customers", endAnchors: [SavedAnchor]), MakeHost(["/pricing", "/customers"], [], [SavedMarker, declined]));
        Assert.Null(await raised.BeginAsync());
        Assert.Equal("every recorded action ran, but the recorded end state did not show on /customers: unrecorded alert \"Card declined\"", raised.CacheInfo?.Detail);

        RecordedTarget[] links = [.. new[] { "Alpha", "Beta", "Gamma" }.Select(name => new RecordedTarget { Role = "link", Name = name })];
        var kept = MakeSession(EntryContext(endPath: "/customers", endAnchors: links, goneAnchors: [SavedAnchor]), MakeHost(["/pricing", "/customers"], [], [SavedMarker]));
        Assert.Null(await kept.BeginAsync());
        Assert.Equal(
            "every recorded action ran, but the recorded end state did not show on /customers: missing link \"Alpha\" and link \"Beta\" (+1 more), still showing status \"Marker\" text=\"saved\"",
            kept.CacheInfo?.Detail);
    }

    [Fact]
    public async Task Says_the_outcome_already_showed_when_the_replay_cannot_have_produced_it_rather_than_that_it_did_not_show()
    {
        var session = MakeSession(EntryContext(endPath: "/customers", endAnchors: [SavedAnchor]), MakeHost(["/customers", "/customers"], [SavedMarker]));

        Assert.Null(await session.BeginAsync());

        Assert.Equal("end-mismatch", session.CacheInfo?.Reason);
        Assert.Equal("every recorded action ran, but the recorded outcome already showed on /customers before the replay acted there, so nothing proves the replay produced it", session.CacheInfo?.Detail);
    }

    [Fact]
    public async Task Does_not_say_the_outcome_already_showed_when_the_recording_has_no_delta_to_show()
    {
        var session = MakeSession(EntryContext(endPath: "/customers"), MakeHost(["/customers", "/customers"], [SavedMarker]));

        Assert.Null(await session.BeginAsync());

        Assert.Equal("end-mismatch", session.CacheInfo?.Reason);
        Assert.Equal("every recorded action ran, but the recorded end state did not show on /customers", session.CacheInfo?.Detail);
    }

    [Fact]
    public async Task Heals_stale_anchors_when_the_executor_settled_an_end_mismatch_without_acting()
    {
        var context = EntryContext(endPath: "/customers", endAnchors: [SavedAnchor]);
        var session = MakeSession(context, MakeHost(["/pricing", "/customers", "/customers"]));
        await session.BeginAsync();
        Assert.Equal(ReplayHandOffReason.EndMismatch, session.ReplayedPrefix?.StopReason);

        // The executor looked, agreed the step was done, and recorded nothing more.
        await session.ConcludeAsync(StepOutcome.Passed);

        Assert.Equal(["navigate"], session.Pending.Entry!.Actions.Select(action => action.Kind));
    }

    [Fact]
    public async Task Evicts_instead_of_re_staging_when_the_executor_had_to_repair_after_an_end_mismatch()
    {
        var context = EntryContext(endPath: "/customers", endAnchors: [SavedAnchor]);
        var session = MakeSession(context, MakeHost(["/pricing", "/customers", "/customers"]));
        await session.BeginAsync();
        Assert.Equal(ReplayHandOffReason.EndMismatch, session.ReplayedPrefix?.StopReason);

        // The replayed flow did not produce its effect; the executor acted further.
        session.Record(Tap("button", "Save"));
        await session.ConcludeAsync(StepOutcome.Passed);

        Assert.Null(session.Pending.Entry);
        Assert.Equal([Key], context.Deleted);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Evicts_a_consumed_entry_when_the_step_then_fails_and_never_when_nothing_judged_the_app(bool failed)
    {
        var outcome = failed ? StepOutcome.Failed : StepOutcome.NoVerdict;
        var context = EntryContext(endPath: "/customers", endAnchors: [SavedAnchor]);
        var session = MakeSession(context, MakeHost(["/pricing", "/customers"]));
        await session.BeginAsync();
        Assert.Equal(ReplayHandOffReason.EndMismatch, session.ReplayedPrefix?.StopReason);

        await session.ConcludeAsync(outcome);
        Settle(context, session, verified: false, preserve: AttemptScope.KeepsCache(outcome == StepOutcome.NoVerdict ? new AgentException("MODEL_UNAVAILABLE", "x") : null));

        Assert.Equal(failed ? [Key] : [], context.Deleted);
        Assert.Null(session.Pending.Entry);
    }

    [Fact]
    public async Task Evicts_nothing_on_failure_when_no_replay_was_consumed()
    {
        var context = FakeContext(() => new CacheLookup { Reason = "no-entry" });
        var session = MakeSession(context, MakeHost(["/"]));
        await session.BeginAsync();

        await session.ConcludeAsync(StepOutcome.Failed);
        Settle(context, session, verified: false);

        Assert.Empty(context.Deleted);
    }

    [Fact]
    public async Task Neither_stages_nor_evicts_in_read_only_mode()
    {
        var context = EntryContext(endPath: "/customers", endAnchors: [SavedAnchor]).ReadOnly();
        var session = MakeSession(context, MakeHost(["/pricing", "/customers"]));
        await session.BeginAsync();

        await session.ConcludeAsync(StepOutcome.Failed);

        Assert.Empty(context.Deleted);
        Assert.Null(session.Pending.Entry);
    }

    [Fact]
    public async Task Lets_an_engine_independent_executor_run_when_the_baseline_cannot_be_observed_and_stages_nothing()
    {
        var host = MakeHost(["/", "/billing"]);
        host.Capture = _ => throw new EngineException("ENGINE_FAILURE", "no surface to observe");
        var session = MakeSession(FakeContext(() => new CacheLookup { Reason = "no-entry" }), host);

        Assert.Null(await session.BeginAsync());
        session.Record(Navigate("/billing"));
        await session.ConcludeAsync(StepOutcome.Passed);

        Assert.Null(session.Pending.Entry);
    }

    [Fact]
    public async Task Stages_nothing_when_the_passing_screen_cannot_be_observed()
    {
        var host = MakeHost(["/", "/billing"]);
        var looks = 0;
        host.Capture = _ => ++looks > 1 ? throw new EngineException("ENGINE_FAILURE", "surface went away") : new Observation { Route = "/", Roots = [] };
        var session = MakeSession(FakeContext(() => new CacheLookup { Reason = "no-entry" }), host);
        await session.BeginAsync();

        session.Record(Navigate("/billing"));
        await session.ConcludeAsync(StepOutcome.Passed);

        Assert.Null(session.Pending.Entry);
    }

    [Fact]
    public Task Propagates_a_runtime_hard_stop_raised_while_capturing_the_baseline() => PropagatesHardStopAsync("baseline");

    [Fact]
    public Task Propagates_a_runtime_hard_stop_raised_while_capturing_the_passing_screen() => PropagatesHardStopAsync("passing screen");

    private static async Task PropagatesHardStopAsync(string when)
    {
        var host = MakeHost(["/", "/billing"]);
        var looks = 0;
        host.Capture = _ => when == "baseline" || ++looks > 1 ? throw new EngineException(EngineErrorCodes.Cancelled, "the attempt was cancelled") : new Observation { Route = "/", Roots = [] };
        var session = MakeSession(FakeContext(() => new CacheLookup { Reason = "no-entry" }), host);
        if (when == "baseline")
        {
            Assert.Equal(EngineErrorCodes.Cancelled, (await Assert.ThrowsAsync<EngineException>(session.BeginAsync)).Code);
            return;
        }

        await session.BeginAsync();
        session.Record(Navigate("/billing"));
        Assert.Equal(EngineErrorCodes.Cancelled, (await Assert.ThrowsAsync<EngineException>(() => session.ConcludeAsync(StepOutcome.Passed))).Code);
    }

    [Fact]
    public async Task Self_finalizes_when_the_recorded_end_anchors_are_present_again()
    {
        var context = EntryContext(endPath: "/customers", endAnchors: [SavedAnchor]);
        var session = MakeSession(context, MakeHost(["/pricing", "/customers"], [SavedMarker]));

        var verdict = await session.BeginAsync();

        Assert.Equal(StepVerdictStatus.Passed, verdict?.Status);
        Assert.Equal("self-finalized", session.CacheInfo?.Mode);
    }

    [Fact]
    public async Task Refuses_to_self_finalize_when_the_recorded_end_path_no_longer_matches()
    {
        // Start path read, then the post-replay end check landing elsewhere.
        var session = MakeSession(EntryContext(endPath: "/customers"), MakeHost(["/pricing", "/moved-away"]));

        Assert.Null(await session.BeginAsync());

        Assert.Equal(ReplayHandOffReason.EndMismatch, session.ReplayedPrefix?.StopReason);
        Assert.Equal("agent-concluded", session.CacheInfo?.Mode);
    }

    [Fact]
    public async Task Hands_off_on_an_end_route_with_any_literal_that_differs_the_recorded_anchors_on_screen_or_not()
    {
        // A slug the runner cannot recognize, or a link that now lands on a lookalike page: another screen either way.
        foreach (var (start, recorded, replayed) in new[] { ("/products", "/products/summer-sneaker", "/products/winter-boot"), ("/nav", "/settings-page", "/profile"), ("/nav", "/task?mode=safe", "/task?mode=unsafe") })
        {
            var session = MakeSession(EntryContext(endPath: recorded, endAnchors: [SavedAnchor]), MakeHost([start, replayed], [], [SavedMarker]));
            Assert.Null(await session.BeginAsync());
            Assert.Equal(ReplayHandOffReason.EndMismatch, session.ReplayedPrefix?.StopReason);
        }
    }

    [Fact]
    public async Task Hands_off_when_the_recorded_delta_did_not_happen_during_the_replay()
    {
        var draft = Node("d", "status", "Marker", "draft");
        var draftAnchor = new RecordedTarget { Role = "status", Name = "Marker", Text = "draft" };
        var submit = Node("s", "button", "Submit");
        StepTraceSession Replay(RecordedTarget[]? endAnchors, RecordedTarget[]? goneAnchors, params SemanticNode[][] screens) =>
            MakeSession(EntryContext(startPath: "/form", endPath: "/form", endAnchors: endAnchors, goneAnchors: goneAnchors, actions: [Tap("button", "Submit")]), MakeHost(["/form", "/form"], screens));

        // The effect happened: the draft gave way to the saved marker.
        var good = Replay([SavedAnchor], [draftAnchor], [submit, draft], [submit, SavedMarker]);
        Assert.Equal(StepVerdictStatus.Passed, (await good.BeginAsync())?.Status);
        Assert.Equal("self-finalized", good.CacheInfo?.Mode);

        // The saved marker already showed before the submit, which did nothing: no proof.
        var already = Replay([SavedAnchor], [draftAnchor], [submit, SavedMarker]);
        Assert.Null(await already.BeginAsync());
        Assert.Equal(("agent-concluded", "end-mismatch"), (already.CacheInfo?.Mode, already.CacheInfo?.Reason));

        // A removal that did not happen: the vanished node is still there.
        var kept = Replay(null, [draftAnchor], [submit, draft]);
        Assert.Null(await kept.BeginAsync());
        Assert.Equal("end-mismatch", kept.CacheInfo?.Reason);

        // A recording with no delta at all and no move proves nothing.
        var blind = Replay(null, null, [submit]);
        Assert.Null(await blind.BeginAsync());
        Assert.Equal("end-mismatch", blind.CacheInfo?.Reason);
    }

    [Fact]
    public async Task Measures_the_evidence_from_the_page_a_recorded_navigate_opened_not_from_the_screen_the_replay_began_on()
    {
        var home = Node("h", "heading", "Home");
        var off = Node("s", "switch", "Email alerts");
        var on = new SemanticNode { Ref = "s", Role = "switch", Name = "Email alerts", States = new NodeStates { Checked = true } };
        StepTraceSession Replay(params SemanticNode[][] screens) =>
            MakeSession(
                EntryContext(
                    endPath: "/settings",
                    endAnchors: [new RecordedTarget { Role = "switch", Name = "Email alerts", States = ["checked"] }],
                    goneAnchors: [new RecordedTarget { Role = "heading", Name = "Home" }],
                    actions: [Navigate("/settings"), Tap("switch", "Email alerts")]),
                MakeHost(["/home", "/settings"], screens));

        // The switch was off on the settings page and on after the tap: the replay turned it on.
        Assert.Equal(StepVerdictStatus.Passed, (await Replay([home], [off], [on]).BeginAsync())?.Status);

        // It was already on when the page opened, from an earlier run, and the tap did nothing.
        var already = Replay([home], [on]);
        Assert.Null(await already.BeginAsync());
        Assert.Equal(("agent-concluded", "end-mismatch"), (already.CacheInfo?.Mode, already.CacheInfo?.Reason));
    }

    /// <summary>Partial port of "looks at the page a navigate opened before free actions on it, so an outcome already there is no proof": the port's free action is a key press, not typing into the focused field.</summary>
    [Fact]
    public async Task Looks_at_the_page_a_navigate_opened_before_a_free_press_so_an_outcome_already_there_is_no_proof()
    {
        var home = Node("h", "heading", "Home");
        var field = Node("f", "textbox", "Email");
        var subscribed = new SemanticNode { Ref = "ok", Role = "status", Text = "You're subscribed" };
        StepTraceSession Replay(params SemanticNode[][] screens) =>
            MakeSession(
                EntryContext(
                    endPath: "/newsletter",
                    endAnchors: [new RecordedTarget { Role = "status", Text = "You're subscribed" }],
                    goneAnchors: [new RecordedTarget { Role = "heading", Name = "Home" }],
                    actions: [Navigate("/newsletter"), new RecordedAction { Kind = "press", Key = "Enter" }]),
                MakeHost(["/home", "/newsletter"], screens));

        Assert.Equal(StepVerdictStatus.Passed, (await Replay([home], [field], [field, subscribed]).BeginAsync())?.Status);

        var already = Replay([home], [field, subscribed]);
        Assert.Null(await already.BeginAsync());
        Assert.Equal(("agent-concluded", "end-mismatch"), (already.CacheInfo?.Mode, already.CacheInfo?.Reason));
    }

    [Fact]
    public async Task Takes_no_look_of_the_end_wait_as_the_end_state_once_the_screen_moved_off_the_recorded_route()
    {
        var context = EntryContext(endPath: "/customers", endAnchors: [SavedAnchor]);
        // The route matches first with the effect missing; the anchors then show on another page.
        var host = MakeHost(["/pricing", "/customers", "/elsewhere"], [], [], [SavedMarker]);
        host.ReplayTimeout = TimeSpan.FromMilliseconds(300);
        var session = MakeSession(context, host);
        var started = DateTime.UtcNow;

        Assert.Null(await session.BeginAsync());

        Assert.Equal("end-mismatch", session.CacheInfo?.Reason);
        Assert.True(DateTime.UtcNow - started >= TimeSpan.FromMilliseconds(300));
    }

    [Fact]
    public async Task Evicts_an_entry_that_did_not_serve_a_pass_with_nothing_to_record_in_its_place()
    {
        var save = Node("b", "button", "Save");
        var context = EntryContext(startPath: "/form", endAnchors: [SavedAnchor], actions: [Tap("button", "Save")]);
        // The saved marker already shows, so the replay hands off; the agent finds the step done without changing anything.
        var session = MakeSession(context, MakeHost(["/form"], [save, SavedMarker]));
        Assert.Null(await session.BeginAsync());

        session.Record(Tap("button", "Save"));
        await session.ConcludeAsync(StepOutcome.Passed);

        Assert.Null(session.Pending.Entry);
        Assert.Single(context.Deleted);
    }

    [Fact]
    public async Task Never_stages_a_step_that_changed_nothing_a_replay_could_check()
    {
        var button = Node("b", "button", "Copy link");
        var session = MakeSession(NoEntry(), MakeHost(["/share"], [button]));
        await session.BeginAsync();

        session.Record(Tap("button", "Copy link"));
        await session.ConcludeAsync(StepOutcome.Passed);

        Assert.Null(session.Pending.Entry);
    }

    [Fact]
    public async Task Stages_what_a_removal_only_step_made_vanish()
    {
        var item = Node("i", "listitem", "Item A");
        var button = Node("b", "button", "Delete");
        var session = MakeSession(NoEntry(), MakeHost(["/items"], [item, button], [button]));
        await session.BeginAsync();

        session.Record(Tap("button", "Delete"));
        await session.ConcludeAsync(StepOutcome.Passed);

        var staged = session.Pending.Entry!;
        Assert.Equal([("listitem", "Item A")], staged.Gone.Select(target => (target.Role, target.Name)));
        Assert.Empty(staged.Appeared);
    }

    /// <summary>Partial port of "stages the entry it replayed whole to keep, never as a recording, and reports the recorded verdict": an entry records no verdict.</summary>
    [Fact]
    public async Task Stages_the_entry_it_replayed_whole_to_keep_never_as_a_recording()
    {
        var context = EntryContext(endPath: "/customers");
        var host = MakeHost(["/pricing", "/customers#top"]);
        var session = MakeSession(context, host);
        var verdict = await session.BeginAsync();
        Assert.Equal(StepVerdictStatus.Passed, verdict?.Status);

        session.Record(Navigate("/customers"));
        await session.ConcludeAsync(StepOutcome.Passed);

        // The passing screen is never captured for a kept entry: the start and the end check are the only looks.
        Assert.True(session.Pending.ReplayedWhole);
        Assert.Equal(2, host.Looks.Count);
    }

    [Fact]
    public async Task Reads_the_start_capture_for_the_first_relocation_instead_of_capturing_the_same_screen_again()
    {
        var upgrade = Node("u", "button", "Upgrade");
        var context = EntryContext(startPath: "/pricing", endPath: "/pricing", endAnchors: [SavedAnchor], actions: [Tap("button", "Upgrade")]);
        var host = MakeHost(["/pricing", "/pricing"], [upgrade], [upgrade, SavedMarker]);
        var session = MakeSession(context, host);

        var verdict = await session.BeginAsync();

        Assert.Equal(StepVerdictStatus.Passed, verdict?.Status);
        // One settled start capture serves the relocation; the end state is one raw look.
        Assert.Equal([SettleMode.HeldStill, SettleMode.Raw], host.Looks);
    }

    [Fact]
    public async Task Leaves_the_entry_file_untouched_across_replays_and_rewrites_it_after_a_hand_off_the_executor_healed()
    {
        var directory = CoreTests.TempCache();
        var cache = new FileStepCache(directory);
        string File() => Assert.Single(Directory.GetFiles(directory, "*.json"));
        (string Bytes, DateTime Written) Snapshot() => (System.IO.File.ReadAllText(File()), System.IO.File.GetLastWriteTimeUtc(File()));

        async Task<StepTraceSession> Run(string[] paths, SemanticNode[][] screens, bool verified = true)
        {
            var host = MakeHost(paths, screens);
            var session = MakeSession(new CacheContext(cache), host);
            var verdict = await session.BeginAsync();

            // On a miss the executor performs the flow itself.
            if (session.CacheInfo?.Mode == "missed")
            {
                session.Record(Navigate("/customers"));
            }

            await session.ConcludeAsync(StepOutcome.Passed);
            Settle(new CacheContext(cache), session, verified);
            Assert.True(verdict is null || session.CacheInfo?.Mode == "self-finalized");
            return session;
        }

        // The recording run: a miss, then the flow and its effect are written.
        var recorded = await Run(["/pricing", "/customers"], [[], [SavedMarker]]);
        Assert.Equal("missed", recorded.CacheInfo?.Mode);
        var written = Snapshot();
        Assert.Equal([Anchor(SavedAnchor)], cache.Read(Key).Entry!.Appeared.Select(Anchor));

        // Two replays in a row: the file's bytes and write time never move.
        await Task.Delay(20);
        Assert.Equal("self-finalized", (await Run(["/pricing", "/customers"], [[SavedMarker]])).CacheInfo?.Mode);
        Assert.Equal(written, Snapshot());
        Assert.Equal("self-finalized", (await Run(["/pricing", "/customers"], [[SavedMarker]])).CacheInfo?.Mode);
        Assert.Equal(written, Snapshot());

        // A hand-off the executor settled without acting re-records with the live anchors.
        var renamed = Node("m1", "status", "Marker", "stored");
        var healed = await Run(["/pricing", "/customers", "/customers"], [[], [], [renamed]]);
        Assert.Equal(ReplayHandOffReason.EndMismatch, healed.ReplayedPrefix?.StopReason);
        Assert.NotEqual(written.Bytes, Snapshot().Bytes);
        Assert.Equal([("status", "Marker", "stored")], cache.Read(Key).Entry!.Appeared.Select(Anchor));

        // A replay nothing verified afterwards is implicated like a recording would be: evicted.
        var unconfirmed = await Run(["/pricing", "/customers"], [[], [renamed]], verified: false);
        Assert.Equal("self-finalized", unconfirmed.CacheInfo?.Mode);
        Assert.Empty(Directory.GetFiles(directory, "*.json"));
    }

    private static (string?, string?, int?, int?) Info(StepTraceSession session) =>
        (session.CacheInfo?.Mode, session.CacheInfo?.Reason, session.CacheInfo?.ReplayedActions, session.CacheInfo?.TotalActions);
}
