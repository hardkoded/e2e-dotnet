// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.TraceReplay.TestHost;

namespace E2E.Tests.TraceReplay;

/// <summary>
/// <c>describe('replayTrace')</c> of <c>trace-replay.test.ts</c>, against <c>Replay.ReplayTrace</c> and a <see cref="TestHost"/>.
/// The port's recording has no drag, bare point (<c>tapAt</c>, <c>hoverAt</c>), <c>hover</c>, <c>longPress</c>,
/// <c>secondaryTap</c>, <c>upload</c>, <c>typeText</c>, recorded position, or <c>derived</c> reason, and its relocation finds a
/// test id exactly, so these <c>it</c>s are not ported: "names the rule behind a run-time value gap, so the report can cite
/// it", "re-finds both ends of a drag on one screen before dragging", "hands a drag off when either end cannot be re-found,
/// never dragging half of it", "replays a bare-point hover through hoverAt, on the recorded viewport only", "keeps looking
/// while a positioned target is ambiguous, since a form still rendering shows fewer twins", and "relocates by semantic
/// identity when a recorded test id churned". The port records no <c>derived</c>, so "ends the prefix at a gap without
/// executing it" has nothing to check there.
/// </summary>
public sealed class ReplayTraceTests
{
    internal static RecordedAction Scroll(string? role, string? name, string direction, int? times) =>
        new() { Kind = "scroll", Role = role, Name = name, Direction = direction, Times = times };

    [Fact]
    public async Task Replays_a_full_trace_and_reports_completion()
    {
        var host = MakeHost();

        var outcome = await Replay.ReplayTrace(
            host,
            [new RecordedAction { Kind = "navigate", Url = "/" }, TapUpgrade, TypeEmail, Scroll(null, null, "down", null)]);

        Assert.True(outcome.Completed);
        Assert.Equal((4, 4), (outcome.Executed, outcome.Total));
        Assert.Equal(["navigate", "tap", "type", "scroll"], host.Calls);
        Assert.Equal(4, outcome.Summaries.Count);
    }

    [Fact]
    public async Task Reads_the_start_capture_for_the_first_look_instead_of_observing_the_same_screen_again()
    {
        var host = MakeHost();

        var outcome = await Replay.ReplayTrace(host, [TapUpgrade], new ReplayOptions { Initial = Screen([Upgrade, Email]) });

        Assert.True(outcome.Completed);
        Assert.Equal(1, outcome.Executed);
        Assert.Equal(["tap"], host.Calls);
        Assert.Equal(0, host.Observations);
    }

    /// <summary>Partial: the port has no <c>typeText</c>, so the look after it is not checked.</summary>
    [Fact]
    public async Task Partial_settles_each_look_as_far_as_the_previous_actions_policy_asks_held_still_after_a_tap_after_the_change_after_a_fill()
    {
        var host = MakeHost();
        var secret = new RecordedAction { Kind = "fill", Role = "textbox", Name = "Email", Value = "<secret:password>" };
        var select = new RecordedAction { Kind = "select", Role = "textbox", Name = "Email", Value = "Pro" };

        var outcome = await Replay.ReplayTrace(host, [TapUpgrade, TypeEmail, TapUpgrade, secret, TapUpgrade, select, TapUpgrade]);

        Assert.True(outcome.Completed);
        Assert.Equal(7, outcome.Executed);
        Assert.Equal(["tap", "type", "tap", "typeSecret", "tap", "select", "tap"], host.Calls);
        // The first look holds still; so does the look after each tap or select. The look after a typed fill waits for its
        // value only. A secret fill is masked out of the tree, so the look after it holds still: there is no change to wait for.
        Assert.Equal(
            [SettleMode.HeldStill, SettleMode.HeldStill, SettleMode.AfterChange, SettleMode.HeldStill, SettleMode.HeldStill, SettleMode.HeldStill, SettleMode.HeldStill],
            host.Looks);
    }

    [Fact]
    public async Task Takes_one_look_per_repeat_of_a_folded_scroll_on_a_list()
    {
        var list = new SemanticNode { Ref = "g1", Role = "group", Name = "Rows 1 to 12" };
        var host = MakeHost([list, Email]);

        var outcome = await Replay.ReplayTrace(host, [Scroll("group", "Rows 1 to 12", "down", 3)]);

        Assert.True(outcome.Completed);
        Assert.Equal(1, outcome.Executed);
        Assert.Equal(["scroll", "scroll", "scroll"], host.Calls);
        Assert.Equal(3, host.Observations);
        Assert.Equal([SettleMode.HeldStill, SettleMode.HeldStill, SettleMode.HeldStill], host.Looks);
    }

    [Fact]
    public async Task Ends_the_prefix_at_a_gap_without_executing_it()
    {
        var host = MakeHost();

        var outcome = await Replay.ReplayTrace(host, [TapUpgrade, new RecordedAction { Kind = "tool" }, TapUpgrade]);

        Assert.False(outcome.Completed);
        Assert.Equal(1, outcome.Executed);
        Assert.Equal(ReplayHandOffReason.Gap, outcome.StopReason);
        Assert.Equal(["tap"], host.Calls);
    }

    [Fact]
    public async Task Hands_off_with_action_failed_when_the_look_before_the_second_action_throws_leaving_the_first_action_done()
    {
        var host = MakeHost();
        host.Capture = () => throw new EngineException("ACTION_FAILED", "the surface went away");

        var outcome = await Replay.ReplayTrace(host, [TapUpgrade, TypeEmail], new ReplayOptions { Initial = Screen([Upgrade, Email]) });

        Assert.False(outcome.Completed);
        Assert.Equal(1, outcome.Executed);
        Assert.Equal(ReplayHandOffReason.ActionFailed, outcome.StopReason);
        Assert.Equal(["tap button \"Upgrade\""], outcome.Summaries);
        Assert.Equal(["tap"], host.Calls);
        // The first look was the start capture; the failed one was the fill's.
        Assert.Equal([SettleMode.HeldStill], host.Looks);
    }

    /// <summary>Partial: the port has no <c>hover</c>, <c>longPress</c>, <c>secondaryTap</c>, or <c>upload</c>.</summary>
    [Fact]
    public async Task Partial_replays_every_node_verb_through_its_own_grammar_call_with_the_recorded_state_and_files()
    {
        var details = new List<object?>();
        var host = MakeHost(onAction: (_, detail) => details.Add(detail));

        var outcome = await Replay.ReplayTrace(
            host,
            [
                new RecordedAction { Kind = "doubleTap", Role = "button", Name = "Upgrade" },
                new RecordedAction { Kind = "scrollTo", Role = "button", Name = "Upgrade" },
                new RecordedAction { Kind = "uncheck", Role = "button", Name = "Upgrade" },
                new RecordedAction { Kind = "back" },
            ]);

        Assert.True(outcome.Completed);
        Assert.Equal((4, 4), (outcome.Executed, outcome.Total));
        Assert.Equal(["doubleTap", "scrollTo", "check", "back"], host.Calls);
        Assert.Equal(("n1", false), details[2]);
    }

    [Fact]
    public async Task Diverges_with_target_not_found_when_relocation_never_matches()
    {
        var host = MakeHost([Email]);

        var outcome = await Replay.ReplayTrace(host, [TapUpgrade]);

        Assert.False(outcome.Completed);
        Assert.Equal(0, outcome.Executed);
        Assert.Equal(ReplayHandOffReason.TargetNotFound, outcome.StopReason);
    }

    [Fact]
    public async Task Still_diverges_at_once_on_ambiguity_for_a_target_with_no_recorded_position()
    {
        var twin = new SemanticNode { Ref = "n9", Role = "button", Name = "Upgrade" };
        var host = MakeHost([Upgrade, twin, Email]);

        var outcome = await Replay.ReplayTrace(host, [TapUpgrade]);

        Assert.False(outcome.Completed);
        Assert.Equal(0, outcome.Executed);
        Assert.Equal(ReplayHandOffReason.TargetAmbiguous, outcome.StopReason);
        Assert.Empty(host.Calls);
        Assert.True(host.Observations < 3);
    }

    [Fact]
    public async Task Absorbs_an_action_failure_as_divergence_never_as_a_step_failure()
    {
        var host = MakeHost(onAction: (name, _) =>
        {
            if (name == "tap")
            {
                throw new AgentException("ACTION_FAILED", "element detached");
            }
        });

        var outcome = await Replay.ReplayTrace(host, [new RecordedAction { Kind = "navigate", Url = "/" }, TapUpgrade]);

        Assert.False(outcome.Completed);
        Assert.Equal(1, outcome.Executed);
        Assert.Equal(ReplayHandOffReason.ActionFailed, outcome.StopReason);
    }

    [Fact]
    public async Task Surfaces_an_unknown_commit_state_as_action_uncertain_with_the_action_named()
    {
        var host = MakeHost(onAction: (name, _) =>
        {
            if (name == "tap")
            {
                throw new EngineException(EngineErrorCodes.ActionMayHaveCommitted, "input may have reached the app");
            }
        });

        var outcome = await Replay.ReplayTrace(host, [new RecordedAction { Kind = "navigate", Url = "/" }, TapUpgrade]);

        Assert.False(outcome.Completed);
        Assert.Equal(1, outcome.Executed);
        Assert.Equal(ReplayHandOffReason.ActionUncertain, outcome.StopReason);
        Assert.Equal("tap button \"Upgrade\"", outcome.UncertainAction);
    }

    [Fact]
    public async Task Rethrows_runtime_hard_stops_untouched()
    {
        var host = MakeHost(onAction: (_, _) => throw new AgentException("STEP_BUDGET_EXHAUSTED", "out of actions"));

        var error = await Assert.ThrowsAsync<AgentException>(() => Replay.ReplayTrace(host, [TapUpgrade]));

        Assert.Equal("STEP_BUDGET_EXHAUSTED", error.Code);
    }
}
