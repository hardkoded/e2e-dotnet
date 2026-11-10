// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using static E2E.Mobile.Tests.RawNodes;
using static E2E.Mobile.Tests.Screens;

namespace E2E.Mobile.Tests.Engine;

public sealed class DeterministicActionsTests
{
    private static readonly TimeSpan ShortTransition = TimeSpan.FromMilliseconds(100);

    private static Harness WithTransition(int milliseconds) =>
        Harness.Create(options => options with { TransitionMilliseconds = milliseconds });

    private static Task Tap(DeviceSession session, SemanticNode node) => session.PerformAsync(node, new LocatorAction.Tap(), OperationContext.Test, CancellationToken.None);

    /// <summary>
    /// Taps with the clock stopped. A press that goes out without asking for a delay is one that neither settled nor
    /// waited out a transition budget. The delay is what stands for the wait, whatever the machine's speed.
    /// </summary>
    private static async Task TapsAtOnce(Harness h, DeviceSession session, SemanticNode node, string expected)
    {
        var delays = h.Delays.Count;
        await Tap(session, node);
        Assert.Equal(delays, h.Delays.Count);
        JsonAssert.Args(expected, h.Fake.LastArgs("interactions.press"));
    }

    private static int Presses(Harness h) => h.Fake.Methods().Count(method => method == "interactions.press");

    private static int Snapshots(Harness h) => h.Fake.Methods().Count(method => method == "capture.snapshot");

    [Fact]
    public async Task Acts_at_once_and_without_settling_on_a_control_that_was_already_on_screen_before_the_last_action()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);

        // Two looks at the screen: the second is the one the action resolves from, the first stands for the screen before the launch.
        await Observed(session, "About");
        await Tap(session, await Observed(session, "Back"));
        var looks = Snapshots(h);
        await TapsAtOnce(h, session, await Observed(session, "About"), """{"ref":"@e4"}""");

        // The observation the tap resolves from is the only new look: nothing is waited out or found again.
        Assert.Equal(looks + 1, Snapshots(h));
    }

    [Fact]
    public async Task Gives_a_control_that_came_with_the_last_action_the_transition_budget_before_acting_on_it()
    {
        var h = WithTransition(120);
        var session = await OpenAttempt(h);
        await Tap(session, await Observed(session, "Back"));

        // The next look shows a control that was not there before the tap: a sheet's button.
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SettingsSnapshotWith(Submit()));
        var submit = await Observed(session, "Submit");
        var delays = h.Delays.Count;
        await Tap(session, submit);
        Assert.True(h.Delays.Skip(delays).Sum(delay => delay.TotalMilliseconds) >= ShortTransition.TotalMilliseconds);
        JsonAssert.Args("""{"ref":"@e11"}""", h.Fake.LastArgs("interactions.press"));
    }

    [Fact]
    public async Task Acts_on_a_control_that_came_with_the_last_action_where_a_fresh_snapshot_lists_it_once_the_budget_has_passed()
    {
        var h = WithTransition(120);
        var session = await OpenAttempt(h);
        await Tap(session, await Observed(session, "Back"));

        // Android reports a sliding modal's frames in flight: the button is first seen below the screen, and lands on
        // it with a new ref by the time the budget has passed.
        JsonObject submit(double y, string reference) =>
            FakeDeviceClient.SettingsSnapshotWith(Submit(reference, y, type: "android.widget.Button", identifier: "submit"));
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => submit(2000, "@e11"));
        var inFlight = await Observed(session, "Submit");
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => submit(600, "@e42"));
        await Tap(session, inFlight);
        JsonAssert.Args("""{"ref":"@e42"}""", h.Fake.LastArgs("interactions.press"));

        // A missing control must be resolved again before any input is dispatched.
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SettingsSnapshot());
        await Tap(session, await Observed(session, "Back"));
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => submit(600, "@e42"));
        var landed = await Observed(session, "Submit");
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SettingsSnapshot());
        var presses = Presses(h);
        var stale = await Assert.ThrowsAsync<EngineException>(() => Tap(session, landed));
        Assert.Equal("NODE_STALE", stale.Code);
        Assert.True(stale.Retryable);
        Assert.Equal(presses, Presses(h));
    }

    [Fact]
    public async Task Does_not_tap_a_recycled_ref_when_a_native_button_becomes_a_tab_during_relocation()
    {
        var h = WithTransition(120);
        var session = await OpenAttempt(h);
        await Tap(session, await Observed(session, "Back"));
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SettingsSnapshotWith(Submit("@e11", 600, "Chat")));
        var chat = await Observed(session, "Chat");
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SettingsSnapshotWith(
            N("@e10", index: 10, parentIndex: 0, depth: 1, type: "tab-bar"),
            N("@e12", index: 11, parentIndex: 10, depth: 2, type: "button", label: "Chat"),
            N("@e11", index: 12, parentIndex: 10, depth: 2, type: "button", label: "Orders")));
        var presses = Presses(h);
        var stale = await Assert.ThrowsAsync<EngineException>(() => Tap(session, chat));
        Assert.Equal("NODE_STALE", stale.Code);
        Assert.True(stale.Retryable);
        Assert.Equal(presses, Presses(h));

        await Tap(session, await Observed(session, "Chat"));
        JsonAssert.Args("""{"ref":"@e12"}""", h.Fake.LastArgs("interactions.press"));
    }

    public static TheoryData<string, string> KeyboardOpens()
    {
        var data = new TheoryData<string, string>();
        foreach (var action in new[] { "tap", "focus", "fill", "clear", "type", "press character", "focused character" })
        {
            foreach (var signal in new[] { "measured", "tree" })
            {
                data.Add(action, signal);
            }
        }

        return data;
    }

    private static readonly JsonObject VisibleKeyboard = new() { ["kind"] = "visible", ["frame"] = new JsonObject { ["x"] = 0, ["y"] = 580, ["width"] = 390, ["height"] = 264 } };

    private static readonly JsonObject AbsentKeyboard = new() { ["kind"] = "absent" };

    /// <summary>The Settings screen with the About cell moved up to a new ref, as the app lays it out once the keyboard transition ends.</summary>
    private static IEnumerable<RawNode> AboutLanded(IEnumerable<RawNode> nodes) =>
        nodes.Select(node => node.Identifier == "ABOUT" || node.Label == "About" ? node with { Ref = "@e42", Rect = new Rect(0, 200, 390, 44) } : node);

    [Theory]
    [MemberData(nameof(KeyboardOpens))]
    public async Task Reobserves_after_action_when_signal_keyboard_state_changes_before_the_control_moves(string action, string signal)
    {
        var h = WithTransition(120);
        var session = await OpenAttempt(h);
        h.Clock.Advance(TimeSpan.FromMilliseconds(150));
        var keyboardNode = N("@keyboard", index: 11, type: "Keyboard", rect: new Rect(0, 580, 390, 264));
        JsonObject Visible(IEnumerable<RawNode> nodes) => signal == "measured"
            ? FakeDeviceClient.SnapshotOf(nodes, ("keyboard", VisibleKeyboard.DeepClone()))
            : FakeDeviceClient.SnapshotOf([.. nodes, keyboardNode]);
        var search = await Observed(session, "Search");
        switch (action)
        {
            case "type":
                await session.TypeTextAsync("blue", replace: false);
                break;
            case "focused character":
                await session.PressAsync("a", CancellationToken.None);
                break;
            case "press character":
                await session.PerformAsync(search, new LocatorAction.Press("a"), OperationContext.Test, CancellationToken.None);
                break;
            case "fill":
                await session.PerformAsync(search, new LocatorAction.Fill("blue"), OperationContext.Test, CancellationToken.None);
                break;
            case "clear":
                await session.PerformAsync(search, new LocatorAction.Clear(), OperationContext.Test, CancellationToken.None);
                break;
            case "focus":
                await session.PerformAsync(search, new LocatorAction.Focus(), OperationContext.Test, CancellationToken.None);
                break;
            default:
                await session.PerformAsync(search, new LocatorAction.Tap(), OperationContext.Test, CancellationToken.None);
                break;
        }

        // The first stale layout already reports the keyboard, as in the live probe.
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Visible(SettingsNodes));
        var stale = await Observed(session, "About");
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Visible(AboutLanded(SettingsNodes)));

        await Tap(session, stale);

        JsonAssert.Args("""{"ref":"@e42"}""", h.Fake.LastArgs("interactions.press"));
    }

    [Theory]
    [InlineData("Enter")]
    [InlineData("focused Enter")]
    [InlineData("dismiss")]
    public async Task Reobserves_after_action_hides_the_keyboard_before_the_control_moves(string action)
    {
        var h = WithTransition(120);
        var session = await OpenAttempt(h);
        h.Clock.Advance(TimeSpan.FromMilliseconds(150));
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SnapshotOf(SettingsNodes, ("keyboard", VisibleKeyboard.DeepClone())));
        var search = await Observed(session, "Search");
        switch (action)
        {
            case "Enter":
                await session.PerformAsync(search, new LocatorAction.Press("Enter"), OperationContext.Test, CancellationToken.None);
                break;
            case "focused Enter":
                await session.PressAsync("Enter", CancellationToken.None);
                break;
            default:
                await session.DismissKeyboardAsync(CancellationToken.None);
                break;
        }

        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SnapshotOf(SettingsNodes, ("keyboard", AbsentKeyboard.DeepClone())));
        var stale = await Observed(session, "About");
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SnapshotOf(AboutLanded(SettingsNodes), ("keyboard", AbsentKeyboard.DeepClone())));

        await Tap(session, stale);

        JsonAssert.Args("""{"ref":"@e42"}""", h.Fake.LastArgs("interactions.press"));
    }

    [Theory]
    [InlineData("type")]
    [InlineData("fill")]
    [InlineData("clear")]
    public async Task Keeps_unchanged_controls_immediate_after_action_when_keyboard_state_is_unchanged(string action)
    {
        foreach (var visible in new[] { false, true })
        {
            var h = WithTransition(120);
            var session = await OpenAttempt(h);
            h.Clock.Advance(TimeSpan.FromMilliseconds(150));
            h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SnapshotOf(SettingsNodes, ("keyboard", (visible ? VisibleKeyboard : AbsentKeyboard).DeepClone())));
            var search = await Observed(session, "Search");
            switch (action)
            {
                case "type":
                    await session.TypeTextAsync("blue", replace: false);
                    break;
                case "fill":
                    await session.PerformAsync(search, new LocatorAction.Fill("blue"), OperationContext.Test, CancellationToken.None);
                    break;
                default:
                    await session.PerformAsync(search, new LocatorAction.Clear(), OperationContext.Test, CancellationToken.None);
                    break;
            }

            await TapsAtOnce(h, session, await Observed(session, "About"), """{"ref":"@e4"}""");
        }
    }

    [Fact]
    public async Task Acts_on_the_duplicate_nearest_where_the_control_was_when_a_fresh_snapshot_lists_several_alike()
    {
        var h = WithTransition(120);
        var session = await OpenAttempt(h);
        await Tap(session, await Observed(session, "Back"));
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SettingsSnapshotWith(Submit("@e11", 600, "Add")));
        var arriving = await Observed(session, "Add");

        // A list of rows that each carry an identical Add button: the row the test meant has settled 10 points lower.
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SettingsSnapshotWith(Submit("@e20", 200, "Add"), Submit("@e21", 610, "Add", index: 11)));
        await Tap(session, arriving);
        JsonAssert.Args("""{"ref":"@e21"}""", h.Fake.LastArgs("interactions.press"));
    }

    [Fact]
    public async Task Gives_a_control_that_moved_with_the_last_action_the_budget_too_and_skips_it_once_the_budget_has_elapsed()
    {
        var h = WithTransition(120);
        var session = await OpenAttempt(h);
        await Tap(session, await Observed(session, "Back"));
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SnapshotOf(
            SettingsNodes.Select(node => node.Identifier == "ABOUT" || node.Label == "About" ? node with { Rect = new Rect(0, 300, 390, 44) } : node)));
        var delays = h.Delays.Count;
        await Tap(session, await Observed(session, "About"));
        Assert.True(h.Delays.Skip(delays).Sum(delay => delay.TotalMilliseconds) >= ShortTransition.TotalMilliseconds);

        // Well after the last action, nothing waits.
        h.Clock.Advance(TimeSpan.FromMilliseconds(150));
        await TapsAtOnce(h, session, await Observed(session, "About"), """{"ref":"@e4"}""");
    }

    [Fact]
    public async Task Fills_goes_back_and_taps_at_a_point_without_settling_the_agent_keeps_the_settle_path()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        var search = await Observed(session, "Search");
        await session.PerformAsync(search, new LocatorAction.Fill("blue"), OperationContext.Test, CancellationToken.None);
        JsonAssert.Args("""{"ref":"@e7","text":"blue"}""", h.Fake.LastArgs("interactions.fill"));
        await session.BackAsync(OperationContext.Test, CancellationToken.None);
        JsonAssert.Args("{}", h.Fake.LastArgs("command.back"));
        var point = new Point(10, 20);
        await session.PerformAtAsync(point, new PointerAction.Tap(), OperationContext.Test, CancellationToken.None);
        JsonAssert.Args("""{"x":10,"y":20}""", h.Fake.LastArgs("interactions.press"));
        await session.PerformAtAsync(point, new PointerAction.DoubleTap(), OperationContext.Test, CancellationToken.None);
        JsonAssert.Args("""{"x":10,"y":20,"count":2}""", h.Fake.LastArgs("interactions.press"));
        await session.PerformAtAsync(point, new PointerAction.LongPress(TimeSpan.FromMilliseconds(700)), OperationContext.Test, CancellationToken.None);
        JsonAssert.Args("""{"x":10,"y":20,"durationMs":700}""", h.Fake.LastArgs("interactions.longPress"));
        await session.PerformAtAsync(point, new PointerAction.LongPress(), OperationContext.Test, CancellationToken.None);
        JsonAssert.Args("""{"x":10,"y":20,"durationMs":1000}""", h.Fake.LastArgs("interactions.longPress"));
        await session.PerformAtAsync(point, new PointerAction.SwipeTo(new Point(10, 300)), OperationContext.Test, CancellationToken.None);
        JsonAssert.Args("""{"from":{"x":10,"y":20},"to":{"x":10,"y":300}}""", h.Fake.LastArgs("interactions.swipe"));
        await session.PerformAtAsync(point, new PointerAction.SwipeTo(new Point(10, 300), TimeSpan.FromMilliseconds(400)), OperationContext.Test, CancellationToken.None);
        JsonAssert.Args("""{"x":10,"y":20,"dx":0,"dy":280,"durationMs":400}""", h.Fake.LastArgs("interactions.pan"));
        Assert.Equal(["tap", "doubleTap", "longPress", "swipeTo"], DeviceActions.PointerActionKinds);

        var about = await Observed(session, "About");
        await session.PerformAsync(about, new LocatorAction.Tap(), OperationContext.Agent, CancellationToken.None);
        JsonAssert.Args("""{"ref":"@e4","settle":true,"settleQuietMs":150}""", h.Fake.LastArgs("interactions.press"));
    }

    // .NET-only: upstream's operation always carries an origin. A caller of the members that take no context (the ones IEngineSession had
    // before it) is the agent, and the obsolete Origin property still names who it is until it is removed.
    [Fact]
    public async Task NET_only_an_action_that_carries_no_context_settles_as_the_agent_unless_the_obsolete_session_origin_says_a_test()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        var about = await Observed(session, "About");

        await session.PerformAsync(about, new LocatorAction.Tap(), CancellationToken.None);
        JsonAssert.Args("""{"ref":"@e4","settle":true,"settleQuietMs":150}""", h.Fake.LastArgs("interactions.press"));

#pragma warning disable CS0618 // The property under test.
        session.Origin = ActionOrigin.Test;
#pragma warning restore CS0618
        await session.PerformAsync(about, new LocatorAction.Tap(), CancellationToken.None);
        JsonAssert.Args("""{"ref":"@e4"}""", h.Fake.LastArgs("interactions.press"));
    }

    [Fact]
    public async Task Does_not_dispatch_an_action_whose_transition_wait_was_cancelled()
    {
        var h = WithTransition(300);
        h.Waits.Wait = (_, token) => Task.Delay(Timeout.Infinite, token);
        var session = await OpenAttempt(h);
        var about = await Observed(session, "About");
        using var controller = new CancellationTokenSource();
        controller.CancelAfter(TimeSpan.FromMilliseconds(20));
        var cancelled = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(about, new LocatorAction.Tap(), OperationContext.Test, controller.Token));
        Assert.Equal("CANCELLED", cancelled.Code);
        Assert.DoesNotContain("interactions.press", h.Fake.Methods());
    }

    [Fact]
    public async Task Treats_every_control_as_arriving_after_a_launch_even_one_the_previous_screen_also_had()
    {
        var h = WithTransition(120);
        var session = await OpenAttempt(h);
        h.Clock.Advance(TimeSpan.FromMilliseconds(150));
        await Observed(session, "About");

        // A relaunch: the same About cell at the same place is on the new screen too.
        await session.RestartAsync(CancellationToken.None);
        var delays = h.Delays.Count;
        await Tap(session, await Observed(session, "About"));
        Assert.True(h.Delays.Skip(delays).Sum(delay => delay.TotalMilliseconds) >= ShortTransition.TotalMilliseconds);
    }

    [Fact]
    public async Task Does_not_count_a_toggle_already_in_the_wanted_state_as_an_action()
    {
        var h = WithTransition(120);
        var session = await OpenAttempt(h);
        h.Clock.Advance(TimeSpan.FromMilliseconds(150));

        // Airplane Mode reads unchecked, so uncheck sends nothing.
        await session.PerformAsync(await Observed(session, "Airplane Mode"), new LocatorAction.Uncheck(), OperationContext.Test, CancellationToken.None);
        Assert.DoesNotContain("interactions.press", h.Fake.Methods());
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SettingsSnapshotWith(Submit("@e11", 600, "Save")));
        await TapsAtOnce(h, session, await Observed(session, "Save"), """{"ref":"@e11"}""");
    }

    private static LocatorAction Swipe(ScrollDirection direction = ScrollDirection.Down) => new LocatorAction.Swipe(direction);

    /// <summary>The screen root as a swipe names it, without a look: a swipe of the screen needs only its ref.</summary>
    private static SemanticNode ScreenRoot() => new() { Ref = SnapshotProjection.RootId };

    private static int Scrolls(Harness h) => h.Fake.Calls.Count(call => call.Method == "interactions.scroll");

    [Fact]
    public async Task Keeps_agent_origin_screen_swipes_immediate_with_driver_settling()
    {
        var h = WithTransition(120);
        var session = await OpenAttempt(h);
        var screen = await RootOf(session);
        var startedAt = h.Clock.GetUtcNow();

        await session.PerformAsync(screen, Swipe(), OperationContext.Agent, CancellationToken.None);

        Assert.Equal(startedAt, h.Clock.GetUtcNow());
        JsonAssert.Args("""{"direction":"down","settle":true,"settleQuietMs":150}""", h.Fake.LastArgs("interactions.scroll"));
    }

    [Fact]
    public async Task Counts_captures_between_consecutive_screen_swipes_toward_the_transition_budget()
    {
        var h = WithTransition(120);
        var session = await OpenAttempt(h);
        h.Clock.Advance(TimeSpan.FromMilliseconds(150));
        var screen = await RootOf(session);
        await session.PerformAsync(screen, Swipe(), OperationContext.Test, CancellationToken.None);
        var firstSwipeAt = h.Clock.GetUtcNow();
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
        {
            h.Clock.Advance(TimeSpan.FromMilliseconds(50));
            return FakeDeviceClient.SettingsSnapshot();
        });
        var nextScreen = await RootOf(session);

        await session.PerformAsync(nextScreen, Swipe(), OperationContext.Test, CancellationToken.None);

        Assert.Equal(TimeSpan.FromMilliseconds(120), h.Clock.GetUtcNow() - firstSwipeAt);
        Assert.Equal(2, Scrolls(h));
    }

    [Fact]
    public async Task Does_not_dispatch_a_screen_swipe_whose_transition_wait_was_cancelled()
    {
        var h = WithTransition(300);
        h.Waits.Wait = (_, token) => Task.Delay(Timeout.Infinite, token);
        var session = await OpenAttempt(h);
        var screen = await RootOf(session);
        using var controller = new CancellationTokenSource();
        controller.CancelAfter(TimeSpan.FromMilliseconds(20));

        var cancelled = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(screen, Swipe(), OperationContext.Test, controller.Token));

        Assert.Equal("CANCELLED", cancelled.Code);
        Assert.DoesNotContain("interactions.scroll", h.Fake.Methods());
    }

    [Fact]
    public async Task Rejects_an_already_cancelled_screen_swipe_without_starting_its_transition_wait()
    {
        var h = WithTransition(300);
        var session = await OpenAttempt(h);
        var screen = await RootOf(session);
        using var controller = new CancellationTokenSource();
        controller.Cancel();
        var startedAt = h.Clock.GetUtcNow();

        var cancelled = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(screen, Swipe(), OperationContext.Test, controller.Token));

        Assert.Equal("CANCELLED", cancelled.Code);
        Assert.Equal(startedAt, h.Clock.GetUtcNow());
        Assert.DoesNotContain("interactions.scroll", h.Fake.Methods());
    }

    [Fact]
    public async Task Ends_a_screen_swipe_transition_at_its_action_budget_without_sending_a_gesture()
    {
        var h = WithTransition(160);
        var session = await OpenAttempt(h, actionTimeout: TimeSpan.FromMilliseconds(20));
        var screen = ScreenRoot();
        var startedAt = h.Clock.GetUtcNow();

        var timeout = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(screen, Swipe(), OperationContext.Test, CancellationToken.None));

        Assert.Equal("OPERATION_TIMEOUT", timeout.Code);
        Assert.Equal(TimeSpan.FromMilliseconds(20), h.Clock.GetUtcNow() - startedAt);
        Assert.DoesNotContain("interactions.scroll", h.Fake.Methods());
    }

    // Partial: upstream's `it` also has the timer wake a sub-millisecond early against a lagging Date.now. The fake clock
    // cannot lag its own delays, so this checks only that a capped swipe the transition outlasts is not sent.
    [Theory]
    [InlineData(20)]
    [InlineData(20.5)]
    public async Task Does_not_send_a_capped_screen_swipe_the_transition_outlasts(double timeoutMs)
    {
        var h = WithTransition(160);
        var session = await OpenAttempt(h, actionTimeout: TimeSpan.FromMilliseconds(timeoutMs));

        // The screen root needs no look: a look is a read the same sub-frame budget bounds, and its first call can outlast it.
        var screen = ScreenRoot();
        var startedAt = h.Clock.GetUtcNow();

        var timeout = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(screen, Swipe(), OperationContext.Test, CancellationToken.None));

        Assert.Equal("OPERATION_TIMEOUT", timeout.Code);
        Assert.False(timeout.Retryable);
        Assert.True(h.Clock.GetUtcNow() - startedAt <= TimeSpan.FromMilliseconds(timeoutMs));
        Assert.DoesNotContain("interactions.scroll", h.Fake.Methods());
    }

    [Fact]
    public async Task Sends_a_screen_swipe_when_its_action_budget_can_accommodate_the_full_transition()
    {
        var h = WithTransition(160);
        var session = await OpenAttempt(h, actionTimeout: TimeSpan.FromMilliseconds(200));
        var screen = await RootOf(session);
        var startedAt = h.Clock.GetUtcNow();

        await session.PerformAsync(screen, Swipe(), OperationContext.Test, CancellationToken.None);

        Assert.Equal(TimeSpan.FromMilliseconds(160), h.Clock.GetUtcNow() - startedAt);
        Assert.Equal(1, Scrolls(h));
    }

    [Fact]
    public async Task Reports_cancellation_when_the_caller_aborts_a_capped_transition_wait()
    {
        var h = WithTransition(160);
        h.Waits.Wait = (_, token) => Task.Delay(Timeout.Infinite, token);
        var session = await OpenAttempt(h, actionTimeout: TimeSpan.FromMilliseconds(20));
        var screen = ScreenRoot();
        using var controller = new CancellationTokenSource();
        controller.CancelAfter(TimeSpan.FromMilliseconds(10));

        var cancelled = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(screen, Swipe(), OperationContext.Test, controller.Token));

        Assert.Equal("CANCELLED", cancelled.Code);
        Assert.DoesNotContain("interactions.scroll", h.Fake.Methods());
    }

    [Fact]
    public async Task Ends_an_arriving_control_transition_at_its_action_budget_without_tapping()
    {
        var h = WithTransition(160);
        var session = await OpenAttempt(h, actionTimeout: TimeSpan.FromMilliseconds(20));
        var about = await Observed(session, "About");
        var startedAt = h.Clock.GetUtcNow();

        var timeout = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(about, new LocatorAction.Tap(), OperationContext.Test, CancellationToken.None));

        Assert.Equal("OPERATION_TIMEOUT", timeout.Code);
        Assert.Equal(TimeSpan.FromMilliseconds(20), h.Clock.GetUtcNow() - startedAt);
        Assert.DoesNotContain("interactions.press", h.Fake.Methods());
    }
}
