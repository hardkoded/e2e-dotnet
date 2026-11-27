// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop.MacOs;

namespace E2E.Desktop.Tests.MacOsDriver;

/// <summary>Ported from the <c>tests</c> module of <c>crates/e2e-desktop-napi/src/actions/mod.rs</c>.</summary>
public sealed class ActionsTests
{
    /// <summary>A deadline the test never reaches.</summary>
    private static Instant Later() => Instant.After(TimeSpan.FromSeconds(60));

    private static Failure? Failed(Action work)
    {
        try
        {
            work();
            return null;
        }
        catch (Failure failure)
        {
            return failure;
        }
    }

    private static Failure Landed() =>
        Failure.Unconfirmed(Codes.ActionFailed, "the app's accessibility answered with a failure");

    /// <summary>An AppKit box with mixed state: each press moves it mixed, on, off, mixed.</summary>
    private static (bool Reached, byte State, int Presses) Drive(byte target, byte start)
    {
        var state = start;
        var presses = 0;
        var reached = Actions.PressToState(
            target,
            state,
            () =>
            {
                presses++;
                state = state switch
                {
                    2 => 1,
                    1 => 0,
                    _ => 2,
                };
            },
            () => state
        );
        return (reached, state, presses);
    }

    [Fact]
    public void Unchecks_a_mixed_box_in_two_presses()
    {
        var (reached, state, presses) = Drive(0, 2);
        Assert.True(reached);
        Assert.Equal((0, 2), (state, presses));
    }

    [Fact]
    public void Checks_a_mixed_box_in_one_press_and_leaves_a_checked_box_alone()
    {
        Assert.Equal(1, Drive(1, 2).State);
        Assert.Equal(1, Drive(1, 2).Presses);
        Assert.Equal(0, Drive(1, 1).Presses);
    }

    [Fact]
    public void Steps_a_stepper_up_and_down_to_the_value()
    {
        foreach (var (start, target, presses) in new[] { (1.0, 3.0, 2), (5.0, 2.0, 3), (4.0, 4.0, 0) })
        {
            var value = start;
            var count = 0;
            var reached = Actions.StepTo(
                target,
                start,
                Later(),
                up =>
                {
                    count++;
                    value += up ? 1.0 : -1.0;
                },
                () => value
            );
            Assert.True(reached);
            Assert.Equal((target, presses), (value, count));
        }
    }

    [Fact]
    public void Stops_a_stepper_that_steps_past_the_value_or_does_not_move()
    {
        var value = 0.0;
        var past = Failed(() => Actions.StepTo(3.0, 0.0, Later(), up => value += up ? 2.0 : -2.0, () => value));
        Assert.NotNull(past);
        Assert.Equal(Delivery.Unknown, past.Delivery);
        Assert.Contains("stepped back to where it started", past.Message);
        Assert.DoesNotContain("3", past.Message);
        Assert.Equal(0.0, value);

        var count = 0;
        var stuck = Actions.StepTo(3.0, 0.0, Later(), _ => count++, () => 0.0);
        Assert.False(stuck);
        Assert.Equal(1, count);
    }

    [Fact]
    public void Counts_a_write_the_app_answered_with_anything_but_a_refusal_as_delivered()
    {
        Assert.Equal(Delivery.Unknown, Actions.WriteFailed(AxError.InvalidElement, "set value").Delivery);
        Assert.Equal(Delivery.NotDelivered, Actions.WriteFailed(AxError.AttributeUnsupported, "set value").Delivery);
        Assert.Equal(Delivery.NotDelivered, Actions.WriteFailed(AxError.ApiDisabled, "set value").Delivery);
    }

    [Fact]
    public void Counts_a_failure_after_a_scroll_into_view_as_delivered()
    {
        var refused = () => Failure.Refused(Codes.ActionNotSupported, "no press");
        Assert.Equal(Delivery.NotDelivered, Failed(() => Actions.AfterScroll(false, () => throw refused()))!.Delivery);
        Assert.Equal(Delivery.Unknown, Failed(() => Actions.AfterScroll(true, () => throw refused()))!.Delivery);
        Actions.AfterScroll(true, () => { });
    }

    [Fact]
    public void Stops_stepping_at_the_deadline_and_says_the_steps_landed()
    {
        var value = 0.0;
        var steps = 0;
        var deadline = Instant.After(TimeSpan.FromMilliseconds(30));
        var failure = Failed(() =>
            Actions.StepTo(
                400.0,
                0.0,
                deadline,
                _ =>
                {
                    steps++;
                    Thread.Sleep(10);
                    value += 1.0;
                },
                () => value
            )
        );
        Assert.NotNull(failure);
        Assert.Equal(Codes.Timeout, failure.Code);
        Assert.Equal(Delivery.Unknown, failure.Delivery);
        Assert.True(steps < 10, $"{steps} steps");

        var none = Failed(() =>
            Actions.StepTo(
                3.0,
                0.0,
                Instant.Now,
                _ => throw new InvalidOperationException("no step past the deadline"),
                () => 0.0
            )
        );
        Assert.Equal(Delivery.NotDelivered, none!.Delivery);
    }

    [Fact]
    public void Sends_no_input_once_the_action_was_cancelled_or_ran_out_of_time()
    {
        // Ids near the input tests' ids: tests share one registry, and a run 4096 ids past another drops that one's mark.
        void Go(uint id, Instant deadline, bool cancelFirst, bool expectFailure)
        {
            if (cancelFirst)
            {
                Input.CancelInput(id);
            }

            Actions.Guarded(id, deadline, () =>
            {
                var failure = Failed(() => Actions.Go("press"));
                Assert.Equal(expectFailure, failure is not null);
                return 0;
            });
        }

        Go(1_000_031, Later(), true, true);
        Go(1_000_032, Instant.Now, false, true);
        Go(1_000_033, Later(), false, false);
    }

    [Fact]
    public void Sends_nothing_to_close_a_menu_once_the_caller_cancelled()
    {
        var sent = 0;

        // Cancelled before the cleanup: neither AXCancel nor Escape, and the menu is reported as it is.
        var open = Actions.CloseMenuWith(
            () => true,
            () => false,
            () =>
            {
                sent++;
                return true;
            },
            () => sent++
        );
        Assert.Equal((false, 0), (open, sent));

        // Cancelled while the menu was watched after AXCancel: no Escape follows.
        var calls = 0;
        var escaped = false;
        Assert.False(Actions.CloseMenuWith(() => ++calls > 1, () => false, () => false, () => escaped = true));
        Assert.False(escaped);

        // Not cancelled: a menu that ignored AXCancel gets Escape, and is closed by it.
        var menuOpen = true;
        Assert.True(Actions.CloseMenuWith(() => false, () => !menuOpen, () => false, () => menuOpen = false));
    }

    [Fact]
    public void Brings_no_window_forward_once_the_call_was_cancelled()
    {
        Input.CancelInput(1_000_039);

        // No such process: past the guard, the window lookup would answer APP_NOT_FOUND or WINDOW_NOT_FOUND instead.
        var failure = Assert.Throws<Failure>(() => Actions.FocusWindow(-1, 1, 1_000_039, Later()));
        Assert.Equal((Codes.Timeout, Delivery.NotDelivered), (failure.Code, failure.Delivery));
        var late = Assert.Throws<Failure>(() => Actions.FocusWindow(-1, 1, 1_000_040, Instant.Now));
        Assert.Equal(Codes.Timeout, late.Code);
    }

    [Fact]
    public void Stops_scanning_a_list_once_the_caller_cancelled()
    {
        var read = 0;
        var cancelled = Assert.Throws<Failure>(() =>
            Actions.Guarded(1_000_051, Later(), () => Actions.Scan([1, 2, 3], "read the list's rows", 1, _ =>
                {
                    read++;
                    Input.CancelInput(1_000_051);
                    return AxResult<bool>.Ok(false);
                }))
        );
        Assert.Equal(Codes.Timeout, cancelled.Code);
        Assert.Equal(1, read);

        // Not cancelled: the first matches up to the limit, and a stall on the way is reported with a miss.
        var found = Actions.Guarded(1_000_052, Later(), () =>
            Actions.Scan([1, 2, 3], "scan", 1, item => AxResult<bool>.Ok(item >= 2))
        );
        Assert.Equal([2], found.Found);
        Assert.False(found.Stalled);
        var both = Actions.Guarded(1_000_054, Later(), () =>
            Actions.Scan([1, 2, 3, 4], "scan", 2, item => AxResult<bool>.Ok(item >= 2))
        );
        Assert.Equal([2, 3], both.Found);
        Assert.False(both.Stalled);
        var missed = Actions.Guarded(1_000_053, Later(), () =>
            Actions.Scan([1, 2], "scan", 1, item =>
                item == 1 ? AxResult<bool>.Err(AxError.CannotComplete) : AxResult<bool>.Ok(false)
            )
        );
        Assert.Empty(missed.Found);
        Assert.True(missed.Stalled);
    }

    [Fact]
    public void Refuses_to_uncheck_a_selected_radio_button_before_pressing_it()
    {
        Assert.True(Actions.ClearsOnlyByAnother("radiobutton", false, 1));
        Assert.False(Actions.ClearsOnlyByAnother("radiobutton", false, 0), "already clear: nothing to press");
        Assert.False(Actions.ClearsOnlyByAnother("radiobutton", true, 0), "a check selects it");
        Assert.False(Actions.ClearsOnlyByAnother("checkbox", false, 1), "a box unchecks itself");
    }

    [Fact]
    public void Counts_a_cancel_but_not_a_deadline_as_the_caller_moving_on()
    {
        Assert.False(Actions.Cancelled(), "no action runs");
        bool During(uint id, Instant deadline) => Actions.Guarded(id, deadline, Actions.Cancelled);
        Assert.False(During(1_000_037, Instant.Now), "a deadline that passed is no cancel");
        Input.CancelInput(1_000_038);
        Assert.True(During(1_000_038, Later()));
    }

    [Fact]
    public void Sends_no_click_when_the_action_is_cancelled_during_the_hit_test()
    {
        var failure = Assert.Throws<Failure>(() => Actions.Guarded(1_000_034, Later(), () =>
            {
                Actions.StillAimed(() =>
                {
                    Input.CancelInput(1_000_034);
                    return AxResult<Aim>.Ok(Aim.Target);
                });
                return 0;
            }));
        Assert.Equal((Codes.Timeout, Delivery.NotDelivered), (failure.Code, failure.Delivery));
        var missed = Assert.Throws<Failure>(() => Actions.Guarded(1_000_035, Later(), () =>
            {
                Actions.StillAimed(() => AxResult<Aim>.Ok(Aim.Other));
                return 0;
            }));
        Assert.Equal(Codes.ActionFailed, missed.Code);
        Actions.Guarded(1_000_036, Later(), () =>
        {
            Actions.StillAimed(() => AxResult<Aim>.Ok(Aim.Target));
            return 0;
        });
    }

    [Fact]
    public void Takes_a_control_at_a_point_unless_the_point_is_on_a_node_with_no_click_of_its_own()
    {
        Actions.PointLands(AxResult<Aim>.Ok(Aim.Control), false);
        Actions.PointLands(AxResult<Aim>.Ok(Aim.Target), true);
        var revealed = Assert.Throws<Failure>(() => Actions.PointLands(AxResult<Aim>.Ok(Aim.Control), true));
        Assert.Equal((Codes.ActionFailed, Delivery.NotDelivered), (revealed.Code, revealed.Delivery));
        Assert.Throws<Failure>(() => Actions.PointLands(AxResult<Aim>.Ok(Aim.Other), false));
        Assert.Equal(
            Codes.AppUnresponsive,
            Assert.Throws<Failure>(() => Actions.PointLands(AxResult<Aim>.Err(AxError.CannotComplete), false)).Code
        );
    }

    [Fact]
    public void Sends_a_coordinate_click_only_while_its_window_takes_it()
    {
        Sheet Key(string text) => new Sheet.Key(text);
        Actions.TakesPoint(true, true, new Sheet.None(), new Sheet.None());

        // A sheet that held the focus when the point was taken is where the point was taken, by window id or element.
        Actions.TakesPoint(true, true, Key("7"), Key("7"));
        Actions.TakesPoint(true, true, Key("ax-1f"), Key("ax-1f"));
        foreach (
            var (frontmost, holds, now, then) in new (bool, bool, Sheet, Sheet)[]
            {
                (false, true, new Sheet.None(), new Sheet.None()),
                (true, false, new Sheet.None(), new Sheet.None()),
                (true, true, Key("7"), new Sheet.None()),
                (true, true, new Sheet.None(), Key("7")),
                (true, true, Key("8"), Key("7")),
                (true, true, Key("ax-2a"), Key("7")),
                // A sheet the caller could name no key for is never known to be the same one.
                (true, true, Key("7"), new Sheet.Unnamed()),
            }
        )
        {
            var refused = Assert.Throws<Failure>(() => Actions.TakesPoint(frontmost, holds, now, then));
            Assert.Equal((Codes.ActionFailed, Delivery.NotDelivered), (refused.Code, refused.Delivery));
        }
    }

    private static AxResult<Aim> Walk(params (bool IsElement, string Role, int? Error)[] steps)
    {
        var next = 0;
        return Actions.AimAlong(() =>
        {
            if (next >= steps.Length)
            {
                return null;
            }

            var (isElement, role, error) = steps[next++];
            return error is { } code
                ? AxResult<Actions.WalkStep>.Err(code)
                : AxResult<Actions.WalkStep>.Ok(new Actions.WalkStep(isElement, Actions.Presses(role)));
        });
    }

    [Fact]
    public void Tells_a_stalled_hit_test_and_a_control_inside_the_element_from_a_covering_element()
    {
        // A row's text cell is content of the row; its inline button is a control of its own.
        Assert.Equal(Aim.Target, Walk((false, "AXStaticText", null), (false, "AXCell", null), (true, "", null)).Value);
        Assert.Equal(
            Aim.Control,
            Walk((false, "AXStaticText", null), (false, "AXButton", null), (true, "", null)).Value
        );
        Assert.Equal(Aim.Other, Walk((false, "AXButton", null), (false, "AXWindow", null)).Value);

        // An image is decoration unless it offers a press: then it is a button of its own.
        bool? Image(AxResult<List<string>> actions)
        {
            var read = Actions.TakesClick("AXImage", () => actions);
            return read.IsOk ? read.Value : null;
        }

        Assert.Equal(true, Image(AxResult<List<string>>.Ok(["AXPress"])));
        Assert.Equal(false, Image(AxResult<List<string>>.Ok(["AXShowMenu"])));
        Assert.Equal(false, Image(AxResult<List<string>>.Err(AxError.ActionUnsupported)));
        Assert.Null(Image(AxResult<List<string>>.Err(AxError.CannotComplete)));
        Assert.Equal(
            AxError.CannotComplete,
            Actions.TakesClick("AXImage", () => AxResult<List<string>>.Err(AxError.CannotComplete)).Error
        );
        Assert.False(
            Actions
                .TakesClick("AXSeparator", () =>
                    throw new InvalidOperationException("no action read for a role that tells")
                )
                .Value
        );

        // A text label is content unless the app made it a button of its own.
        Assert.False(Actions.TakesClick("AXStaticText", () => AxResult<List<string>>.Ok([])).Value);
        Assert.True(Actions.TakesClick("AXStaticText", () => AxResult<List<string>>.Ok(["AXPress"])).Value);

        // A custom control drawn in a group, which offers a press, is a control of its own too.
        Assert.True(Actions.TakesClick("AXGroup", () => AxResult<List<string>>.Ok(["AXPress"])).Value);

        // An open or a confirm takes a click as a press does.
        Assert.True(Actions.TakesClick("AXGroup", () => AxResult<List<string>>.Ok(["AXOpen"])).Value);
        Assert.True(Actions.TakesClick("AXGroup", () => AxResult<List<string>>.Ok(["AXConfirm"])).Value);
        Assert.False(Actions.TakesClick("AXGroup", () => AxResult<List<string>>.Ok(["AXScrollToVisible"])).Value);

        // An inline editor of a row takes a double-click of its own.
        foreach (var editor in new[] { "AXTextField", "AXComboBox", "AXTextArea" })
        {
            Assert.Equal(Aim.Control, Walk((false, editor, null), (false, "AXCell", null), (true, "", null)).Value);
        }

        Assert.Equal(
            AxError.CannotComplete,
            Walk((false, "AXStaticText", null), (false, "", AxError.CannotComplete)).Error
        );

        // A click refuses the control, a hover does not; a stall is a retryable stall, not a covering element.
        Assert.Equal(
            Codes.ActionFailed,
            Assert.Throws<Failure>(() => Actions.Aimed(AxResult<Aim>.Ok(Aim.Control), true, "after the move")).Code
        );
        Actions.Aimed(AxResult<Aim>.Ok(Aim.Control), false, "before the move");
        var stall = Assert.Throws<Failure>(() =>
            Actions.Aimed(AxResult<Aim>.Err(AxError.CannotComplete), true, "after the move")
        );
        Assert.Equal((Codes.AppUnresponsive, Delivery.NotDelivered), (stall.Code, stall.Delivery));
    }

    [Fact]
    public void Says_a_select_menu_that_stayed_open_reached_the_app()
    {
        var stall = () => Actions.Unread(AxError.CannotComplete, "read the list's options");
        var closed = Actions.Unclosed(stall(), true);
        Assert.Equal(Delivery.NotDelivered, closed.Delivery);
        Assert.False(closed.Details?.ContainsKey("menu") ?? false);
        var open = Actions.Unclosed(stall(), false);
        Assert.Equal(Delivery.Unknown, open.Delivery);
        Assert.Equal("open", open.Details!["menu"]);
        var missing = Actions.Unclosed(Failure.Refused(Codes.ElementNotFound, "no option"), false);
        Assert.Equal((Codes.ElementNotFound, Delivery.Unknown), (missing.Code, missing.Delivery));
    }

    [Fact]
    public void Stops_a_stepper_that_wraps_around_its_range()
    {
        // 0 to 10, and one step up from 10 goes to 0: 11 is never reached.
        var value = 10.0;
        var presses = 0;
        var wrapped = Failed(() =>
            Actions.StepTo(
                11.0,
                10.0,
                Later(),
                up =>
                {
                    presses++;
                    value = up ? (value >= 10.0 ? 0.0 : value + 1.0) : value - 1.0;
                },
                () => value
            )
        );
        Assert.Equal(Delivery.Unknown, wrapped!.Delivery);
        Assert.Equal(1, presses);
    }

    [Fact]
    public void Steps_by_a_fraction_to_a_value_it_reaches_only_up_to_rounding()
    {
        var value = 0.0;
        var reached = Actions.StepTo(0.3, 0.0, Later(), up => value += up ? 0.1 : -0.1, () => value);
        Assert.True(reached);
    }

    [Fact]
    public void Refuses_a_stepper_target_outside_the_range_it_reports()
    {
        Assert.True(Actions.Outside(11.0, 0.0, 10.0));
        Assert.True(Actions.Outside(-1.0, 0.0, 10.0));

        // The bounds themselves are in range.
        Assert.False(Actions.Outside(10.0, 0.0, 10.0));
        Assert.False(Actions.Outside(0.0, 0.0, 10.0));

        // A bound the stepper does not report limits nothing.
        Assert.False(Actions.Outside(1e9, 0.0, null));
        Assert.False(Actions.Outside(-1e9, null, 10.0));
    }

    [Fact]
    public void Tells_a_large_value_from_the_next_step_after_it()
    {
        Assert.False(Actions.Same(1_800_000_000.0, 1_800_000_001.0, null));

        // Whole numbers past the reach of a relative tolerance (a Unix time in microseconds) still differ by one.
        Assert.False(Actions.Same(1_800_000_000_000_000.0, 1_800_000_000_000_001.0, null));
        Assert.True(Actions.Same(0.1 + 0.1 + 0.1, 0.3, null));

        // A step smaller than the rounding allowance of a large value is still a step, once a press showed its size.
        Assert.True(Actions.Same(1_800_000_000.0, 1_800_000_000.000_01, null));
        Assert.False(Actions.Same(1_800_000_000.0, 1_800_000_000.000_01, 0.000_01));
        var value = 1_800_000_000.0;
        var reached = Actions.StepTo(1_800_000_001.0, value, Later(), up => value += up ? 1.0 : -1.0, () => value);
        Assert.True(reached);
        Assert.Equal(1_800_000_001.0, value);
    }

    [Fact]
    public void Steps_a_large_value_by_a_step_smaller_than_its_rounding_allowance()
    {
        const double small = 0.000_01;
        var value = 1_800_000_000.0;
        var target = 1_800_000_000.0 + small;
        var presses = 0;
        var reached = Actions.StepTo(
            target,
            value,
            Later(),
            up =>
            {
                presses++;
                value += up ? small : -small;
            },
            () => value
        );
        Assert.True(reached);
        Assert.Equal((target, 1), (value, presses));
    }

    [Fact]
    public void Leaves_a_value_that_differs_from_the_target_only_by_rounding_as_it_was()
    {
        var summed = 0.1 + 0.1 + 0.1;
        var value = summed;
        var presses = 0;
        var reached = Actions.StepTo(
            0.3,
            value,
            Later(),
            up =>
            {
                presses++;
                value += up ? 0.1 : -0.1;
            },
            () => value
        );
        Assert.True(reached);

        // One step to learn the step's size, one back.
        Assert.Equal(2, presses);
        Assert.True(Actions.Same(value, summed, 0.1));

        // A stepper already at the exact value gets no press.
        Assert.True(Actions.StepTo(0.3, 0.3, Later(), _ => throw new InvalidOperationException("no press"), () => 0.3));
    }

    [Fact]
    public void Judges_a_press_that_answered_with_an_error_by_the_state_it_led_to()
    {
        byte state = 0;
        var toggled = Actions.PressToState(
            1,
            0,
            () =>
            {
                state = 1;
                throw Landed();
            },
            () => state
        );
        Assert.True(toggled);
        var unchanged = Failed(() => Actions.PressToState(1, 0, () => throw Landed(), () => (byte?)0));
        Assert.Equal(Delivery.Unknown, unchanged!.Delivery);
        var value = 1.0;
        var stepped = Actions.StepTo(
            3.0,
            1.0,
            Later(),
            _ =>
            {
                value += 1.0;
                if (value == 2.0)
                {
                    throw Landed();
                }
            },
            () => value
        );
        Assert.True(stepped);
    }

    private static Failure? Answer(int error, int? then)
    {
        try
        {
            Actions.Answered(error, then, "press");
            return null;
        }
        catch (Failure failure)
        {
            return failure;
        }
    }

    [Fact]
    public void Reads_an_action_answer_by_whether_the_element_stayed()
    {
        foreach (var refused in new[] { AxError.ActionUnsupported, AxError.AttributeUnsupported })
        {
            Assert.Equal(Delivery.NotDelivered, Answer(refused, null)!.Delivery);
        }

        Assert.Null(Answer(AxError.Failure, AxError.InvalidElement)); // gone after the press: it closed its alert
        Assert.Null(Answer(AxError.AttributeUnsupported, AxError.InvalidElement)); // a SwiftUI alert's button, gone after the press
        foreach (var unread in new[] { AxError.CannotComplete, AxError.Failure })
        {
            // Unread after: no proof the element stayed.
            Assert.Equal(Delivery.Unknown, Answer(AxError.AttributeUnsupported, unread)!.Delivery);
        }

        Assert.Equal(Delivery.NotDelivered, Answer(AxError.ActionUnsupported, AxError.InvalidElement)!.Delivery);
        Assert.Null(Answer(AxError.CannotComplete, null)); // a modal runs in the press's handler
        var stalled = Answer(AxError.CannotComplete, AxError.CannotComplete)!;
        Assert.Equal((Codes.AppUnresponsive, Delivery.Unknown), (stalled.Code, stalled.Delivery));
        var failed = Answer(AxError.Failure, null)!;
        Assert.Equal((Codes.ActionFailed, Delivery.Unknown), (failed.Code, failed.Delivery));
        Assert.Equal(Delivery.Unknown, Answer(AxError.InvalidElement, null)!.Delivery);
    }

    [Fact]
    public void Stops_watching_for_a_gone_element_in_time_to_answer()
    {
        var margin = TimeSpan.FromMilliseconds(200);
        var goneWait = TimeSpan.FromMilliseconds(300);
        var now = Instant.Now;
        Assert.Equal(now + goneWait, Actions.GoneWaitEnd(now, null));
        Assert.Equal(now + goneWait, Actions.GoneWaitEnd(now, now + TimeSpan.FromSeconds(5)));
        var soon = now + margin + TimeSpan.FromMilliseconds(40);
        Assert.Equal(now + TimeSpan.FromMilliseconds(40), Actions.GoneWaitEnd(now, soon));
    }

    [Fact]
    public void Fails_a_stepper_value_read_that_stalled_or_went_away_before_any_step()
    {
        Assert.Equal(4.0, Actions.ShownNumber(AxResult<AxValue>.Ok(new AxValue.NumberValue(4.0)), "set value"));
        Assert.Null(Actions.ShownNumber(AxResult<AxValue>.Ok(new AxValue.TextValue("four")), "set value"));
        Assert.Null(Actions.ShownNumber(AxResult<AxValue>.Err(AxError.Failure), "set value"));
        var stall = Assert.Throws<Failure>(() =>
            Actions.ShownNumber(AxResult<AxValue>.Err(AxError.CannotComplete), "set value")
        );
        Assert.Equal((Codes.AppUnresponsive, Delivery.NotDelivered), (stall.Code, stall.Delivery));
        var gone = Assert.Throws<Failure>(() =>
            Actions.ShownNumber(AxResult<AxValue>.Err(AxError.InvalidElement), "set value")
        );
        Assert.Equal((Codes.StaleRef, Delivery.NotDelivered), (gone.Code, gone.Delivery));
    }

    [Fact]
    public void Reads_a_stall_as_one_and_any_other_failure_as_no_answer()
    {
        var kept = Actions.Stalled(AxResult<AxValue>.Ok(new AxValue.NumberValue(3.0)));
        Assert.True(kept.IsOk);
        Assert.Equal(new AxValue.NumberValue(3.0), kept.Value);
        Assert.Equal(AxError.CannotComplete, Actions.Stalled(AxResult<AxValue>.Err(AxError.CannotComplete)).Error);
        foreach (var none in new[] { AxError.Failure, AxError.InvalidElement, AxError.NoValue })
        {
            var read = Actions.Stalled(AxResult<AxValue>.Err(none));
            Assert.True(read.IsOk, none.ToString());
            Assert.Null(read.Value);
        }

        // The stall reaches the engine as one it may retry.
        var failure = Actions.Unread(AxError.CannotComplete, "type");
        Assert.Equal((Codes.AppUnresponsive, Delivery.NotDelivered), (failure.Code, failure.Delivery));
        Assert.Equal(true, failure.Details!["retryable"]);
    }

    [Fact]
    public void Names_no_value_in_any_stepper_failure()
    {
        static void Digitless(Failure? failure)
        {
            Assert.NotNull(failure);
            Assert.DoesNotContain(failure.Message, char.IsAsciiDigit);
        }

        // Wraps: one step up from 42 goes to 0.
        var value = 42.0;
        Digitless(Failed(() => Actions.StepTo(43.0, 42.0, Later(), _ => value = 0.0, () => value)));

        // Steps past and back: 40, 42, 44 past 43, then back to 40.
        value = 40.0;
        Digitless(Failed(() => Actions.StepTo(43.0, 40.0, Later(), up => value += up ? 2.0 : -2.0, () => value)));

        // Steps past and cannot go back.
        value = 40.0;
        Digitless(
            Failed(() =>
                Actions.StepTo(
                    43.0,
                    40.0,
                    Later(),
                    up =>
                    {
                        if (up)
                        {
                            value += 2.0;
                        }
                    },
                    () => value
                )
            )
        );

        // Already showed the target up to rounding, and the step to tell it left it changed.
        var summed = 0.1 + 0.1 + 0.1;
        value = summed;
        Digitless(
            Failed(() =>
                Actions.StepTo(
                    0.3,
                    summed,
                    Later(),
                    up =>
                    {
                        if (!up)
                        {
                            value -= 0.1;
                        }
                    },
                    () => value
                )
            )
        );

        // Out of time before the first step.
        Digitless(Failed(() => Actions.StepTo(43.0, 40.0, Instant.Now, _ => { }, () => 40.0)));
    }

    [Fact]
    public void Takes_only_a_finite_number()
    {
        Assert.Equal(3.0, Actions.Finite(" 3 "));
        foreach (var text in new[] { "NaN", "inf", "-infinity", "three" })
        {
            Assert.Null(Actions.Finite(text));
        }
    }

    [Fact]
    public void Stops_when_a_press_changes_nothing_or_the_state_cannot_be_read()
    {
        var presses = 0;
        var stuck = Actions.PressToState(1, 0, () => presses++, () => 0);
        Assert.False(stuck);
        Assert.Equal(1, presses);
        var unreadable = Actions.PressToState(1, null, () => { }, () => null);
        Assert.False(unreadable);
    }

    [Fact]
    public void Lets_a_scroll_area_scroll_itself_only_with_a_scroll_bar_for_the_axis()
    {
        string[] swiftui = ["AXScrollDownByPage", "AXScrollUpByPage", "AXScrollLeftByPage", "AXScrollRightByPage"];

        // A SwiftUI scroll area offers every page action: only its scroll bar for the axis says it scrolls along it.
        Assert.False(Actions.ScrollsItself("scrollarea", false, swiftui, "down"));
        Assert.True(Actions.ScrollsItself("scrollarea", true, swiftui, "down"));
        Assert.True(Actions.ScrollsItself("scrollarea", true, [], "left"));

        // Any other role scrolls itself by the page action for the direction.
        Assert.True(Actions.ScrollsItself("list", false, ["AXScrollDownByPage"], "down"));
        Assert.False(Actions.ScrollsItself("list", false, ["AXScrollDownByPage"], "right"));
        Assert.False(Actions.ScrollsItself("list", false, [], "up"));
    }

    [Fact]
    public void Confirms_no_secure_fill_by_a_field_that_went_away_or_did_not_answer()
    {
        Assert.Equal((true, (int?)8), Actions.SecureCount(AxResult<AxValue>.Ok(new AxValue.NumberValue(8.0))));

        // A field that counts none is taken at the write's word.
        Assert.Equal((true, (int?)null), Actions.SecureCount(AxResult<AxValue>.Err(AxError.AttributeUnsupported)));
        Assert.False(Actions.SecureCount(AxResult<AxValue>.Err(AxError.CannotComplete)).IsOk);
        Assert.False(Actions.SecureCount(AxResult<AxValue>.Err(AxError.InvalidElement)).IsOk);
    }

    /// <summary>A node of a fake accessibility tree, for the text gathering that names a row.</summary>
    private sealed record Fake(string Role, string? Label, List<Fake> Children);

    private static string? Gathered(Fake row)
    {
        var texts = new List<string>();
        var read = Actions.GatherTexts(
            row,
            0,
            texts,
            node => AxResult<List<Fake>>.Ok(node.Children),
            node => AxResult<string>.Ok(node.Role),
            node => AxResult<string?>.Ok(node.Label)
        );
        Assert.Equal(AxError.Success, read);
        return Snapshot.ContentName(texts);
    }

    [Fact]
    public void Names_a_row_from_the_texts_under_its_texts_as_the_snapshot_does()
    {
        // An untitled heading over "Paid": the snapshot walks into it, and so does the row's name.
        var row = new Fake(
            "row",
            null,
            [new Fake("statictext", "Invoice", []), new Fake("heading", null, [new Fake("statictext", "Paid", [])])]
        );
        Assert.Equal("Invoice Paid", Gathered(row));

        // A titled text comes after the texts inside it, in the snapshot's order.
        var titled = new Fake("row", null, [new Fake("heading", "Total", [new Fake("statictext", "42", [])])]);
        Assert.Equal("42 Total", Gathered(titled));

        // A scroll bar's parts are not read.
        var scrolled = new Fake(
            "row",
            null,
            [new Fake("statictext", "Notes", []), new Fake("scrollbar", null, [new Fake("statictext", "50%", [])])]
        );
        Assert.Equal("Notes", Gathered(scrolled));
    }
}
