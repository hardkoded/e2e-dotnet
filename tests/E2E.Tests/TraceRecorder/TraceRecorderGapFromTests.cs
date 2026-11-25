// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Recorder = E2E.Cache.TraceRecorder;

namespace E2E.Tests.TraceRecorder;

/// <summary>
/// Upstream's <c>describe('gapFrom')</c> inside <c>describe('TraceRecorder')</c> of <c>tests/unit/trace-recorder.test.ts</c>: the recording
/// from a piece on becomes one derived gap. Upstream's <c>tool type (run-time value)</c> action is the port's <c>tool</c> kind with the
/// text <c>type (run-time value)</c>.
/// </summary>
public sealed class TraceRecorderGapFromTests
{
    private static readonly SemanticNode DigitBox = new()
    {
        Ref = "n9",
        Role = "textbox",
        Name = "Digit 1",
    };

    private static readonly SemanticNode UpgradeButton = new()
    {
        Ref = "n42",
        Role = "button",
        Name = "Upgrade",
        Text = "Upgrade",
        TestId = "upgrade-cta",
    };

    private static void TypeDigits(Recorder recorder)
    {
        foreach (var digit in "482913")
        {
            recorder.Record("fill", DigitBox, digit.ToString());
        }
    }

    private static void AssertGap(E2E.RecordedAction action) =>
        Assert.Equal(
            ("tool", "type (run-time value)", DerivedReason.MintedToken),
            (action.Kind, action.Text, action.Derived)
        );

    [Fact]
    public void Turns_the_recording_from_the_first_piece_on_into_one_derived_gap()
    {
        var recorder = TraceRecorderTests.MakeRecorder();
        recorder.Record(new RecordedAction { Kind = "navigate", Url = "http://localhost/one-time-code" });
        TypeDigits(recorder);
        recorder.GapFrom(1, DerivedReason.MintedToken);

        var actions = TraceRecorderTests.Finalize(recorder)!.Actions;

        Assert.Equal(2, actions.Count);
        Assert.Equal("navigate", actions[0].Kind);
        AssertGap(actions[1]);
    }

    [Fact]
    public void Keeps_recording_after_the_gap()
    {
        var recorder = TraceRecorderTests.MakeRecorder();
        TypeDigits(recorder);
        recorder.GapFrom(0, DerivedReason.MintedToken);
        recorder.Record("tap", UpgradeButton);

        Assert.Equal(["tool", "tap"], TraceRecorderTests.Finalize(recorder)!.Actions.Select(action => action.Kind));
    }

    [Fact]
    public void Drops_the_pieces_without_a_new_gap_when_an_earlier_gap_already_hands_replay_off()
    {
        var recorder = TraceRecorderTests.MakeRecorder();
        recorder.RecordGap("seed_cart");
        TypeDigits(recorder);
        recorder.GapFrom(1, DerivedReason.MintedToken);

        var actions = TraceRecorderTests.Finalize(recorder)!.Actions;

        // Replay hands off at seed_cart, so the digits after it never replay and are not kept.
        Assert.Equal(("tool", "seed_cart"), (actions.Single().Kind, actions.Single().Text));
    }

    [Fact]
    public void Changes_nothing_on_a_truncated_recording_which_never_replays()
    {
        var recorder = TraceRecorderTests.MakeRecorder(3);
        TypeDigits(recorder);
        recorder.GapFrom(0, DerivedReason.MintedToken);

        var trace = TraceRecorderTests.Finalize(recorder)!;

        Assert.True(trace.Truncated);
        Assert.Equal(["fill", "fill", "fill"], trace.Actions.Select(action => action.Kind));
    }
}
