// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;

namespace E2E.Mobile.Tests.Engine;

public sealed class ObservationTests
{
    private static JsonObject Sparse() => new()
    {
        ["nodes"] = new JsonArray(new JsonObject { ["ref"] = "@e1", ["type"] = "application" }),
        ["snapshotQuality"] = new JsonObject { ["state"] = "sparse" },
    };

    private static SemanticNode Named(SemanticNode root, string name) => Walk(root).First(node => node.Name == name);

    /// <summary>Every node under the root, the root excluded: the device's own tree.</summary>
    private static IEnumerable<SemanticNode> Walk(SemanticNode root)
    {
        foreach (var node in root.Children)
        {
            yield return node;
            foreach (var inner in Walk(node))
            {
                yield return inner;
            }
        }
    }

    private static async Task<DeviceSession> OpenAttempt(Harness h)
    {
        var session = await h.Boot();
        if (h.App.BundleId is not null || h.App.AppPath is not null)
        {
            await session.RestartAsync(CancellationToken.None);
        }

        return session;
    }

    private static JsonObject Snapshot(JsonObject keyboard)
    {
        var snapshot = FakeDeviceClient.SettingsSnapshot();
        snapshot["keyboard"] = keyboard;
        return snapshot;
    }

    [Fact]
    public async Task Reports_the_keyboard_state_the_capture_measured_and_nothing_when_it_measured_none()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        Assert.Null((await session.ObserveAsync(CancellationToken.None)).KeyboardVisible);

        var frame = new JsonObject { ["x"] = 0, ["y"] = 583, ["width"] = 390, ["height"] = 261 };
        h.Fake.Respond("capture.snapshot", _ => Snapshot(new JsonObject { ["kind"] = "visible", ["frame"] = frame }));
        Assert.True((await session.ObserveAsync(CancellationToken.None)).KeyboardVisible);

        h.Fake.Respond("capture.snapshot", _ => Snapshot(new JsonObject { ["kind"] = "absent" }));
        Assert.False((await session.ObserveAsync(CancellationToken.None)).KeyboardVisible);

        h.Fake.Respond("capture.snapshot", _ => Snapshot(new JsonObject { ["kind"] = "unmeasurable", ["reason"] = "keyboard-frame-query-timeout" }));
        Assert.Null((await session.ObserveAsync(CancellationToken.None)).KeyboardVisible);
    }

    [Fact]
    public async Task Projects_the_snapshot_under_one_stable_screen_root_with_a_viewport_and_a_fresh_id_generation_each_time_without_the_tap()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        var first = await session.ObserveAsync(CancellationToken.None);
        Assert.Equal("com.apple.Preferences / General", first.Route);

        // The device's application node sits under a root the engine mints: same id every time, the viewport as its box.
        var root = Assert.Single(first.Roots);
        Assert.Equal("root", root.Ref);
        Assert.Equal("screen", root.Role);
        Assert.Equal(new BoundingBox(0, 0, 390, 844), root.Rect);
        Assert.Equal("application", Assert.Single(root.Children).Role);
        JsonAssert.Args("""{"interactiveOnly":false}""", h.Fake.LastArgs("capture.snapshot"));
        var about = Named(root, "About");

        var second = await session.ObserveAsync(CancellationToken.None);
        Assert.Equal(root.Ref, second.Roots[0].Ref);
        Assert.NotEqual(about.Ref, Named(second.Roots[0], "About").Ref);
    }

    [Fact]
    public async Task Measures_the_screen_in_the_viewport_the_snapshot_publishes_and_in_its_tree_when_it_publishes_none()
    {
        var published = Harness.Create();
        var publishedSession = await OpenAttempt(published);
        // The application node reads 390x844; a published viewport outranks it.
        published.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => WithViewport(402, 874));
        var publishedObservation = await publishedSession.ObserveAsync(CancellationToken.None);
        Assert.Equal(new BoundingBox(0, 0, 402, 874), publishedObservation.Roots[0].Rect);
        Assert.Equal(new TraceViewport(402, 874), publishedObservation.Viewport);
        var tree = Harness.Create();
        var treeSession = await OpenAttempt(tree);
        Assert.Equal(new BoundingBox(0, 0, 390, 844), (await treeSession.ObserveAsync(CancellationToken.None)).Roots[0].Rect);
        Assert.DoesNotContain("capture.screenshot", published.Fake.Methods().Concat(tree.Fake.Methods()));

        // A published size that is not positive is no measurement: the tree decides.
        tree.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => WithViewport(0, 0));
        Assert.Equal(new BoundingBox(0, 0, 390, 844), (await treeSession.ObserveAsync(CancellationToken.None)).Roots[0].Rect);
    }

    private static JsonObject WithViewport(double width, double height)
    {
        var snapshot = FakeDeviceClient.SettingsSnapshot();
        snapshot["viewport"] = new JsonObject { ["width"] = width, ["height"] = height };
        return snapshot;
    }

    [Fact]
    public async Task Keeps_the_known_viewport_for_an_app_drawn_smaller_than_the_screen_and_follows_a_rotation()
    {
        var h = Harness.Create();
        // Calendar behind Notification Center, read live on an iOS 26 simulator: its tree, healthy, clipped to a third of the screen.
        h.Fake.Respond(DeviceMethods.CaptureScreenshot, _ => new JsonObject { ["logicalWidth"] = 390, ["logicalHeight"] = 844, ["width"] = 1170, ["height"] = 2532, ["pixelDensity"] = 3 });
        var session = await OpenAttempt(h);
        Assert.Equal(new BoundingBox(0, 0, 390, 844), (await session.ObserveAsync(CancellationToken.None)).Roots[0].Rect);
        int Screenshots() => h.Fake.Methods().Count(method => method == "capture.screenshot");
        var before = Screenshots();
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
        {
            var snapshot = WithViewport(130, 281.33);
            snapshot["snapshotQuality"] = new JsonObject { ["state"] = "healthy" };
            return snapshot;
        });
        Assert.Equal(new BoundingBox(0, 0, 390, 844), (await session.ObserveAsync(CancellationToken.None)).Roots[0].Rect);
        Assert.Equal(before + 1, Screenshots());

        // A rotation is the known size transposed: taken as it comes, without asking the device.
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => WithViewport(844, 390));
        Assert.Equal(new BoundingBox(0, 0, 844, 390), (await session.ObserveAsync(CancellationToken.None)).Roots[0].Rect);
        Assert.Equal(before + 1, Screenshots());

        // Any other size replaces the known one when the device's screen agrees.
        h.Fake.Respond(DeviceMethods.CaptureScreenshot, _ => new JsonObject { ["logicalWidth"] = 402, ["logicalHeight"] = 874 });
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => WithViewport(402, 874));
        Assert.Equal(new BoundingBox(0, 0, 402, 874), (await session.ObserveAsync(CancellationToken.None)).Roots[0].Rect);
        // A real device's screenshot reports no logical size: the tree's size stands.
        h.Fake.Respond(DeviceMethods.CaptureScreenshot, _ => new JsonObject { ["width"] = 1125, ["height"] = 2001 });
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => WithViewport(375, 667));
        Assert.Equal(new BoundingBox(0, 0, 375, 667), (await session.ObserveAsync(CancellationToken.None)).Roots[0].Rect);
    }

    [Fact]
    public async Task Asks_for_interactive_only_snapshots_when_configured()
    {
        var h = Harness.Create(options => options with { Snapshot = SnapshotMode.Interactive });
        var session = await OpenAttempt(h);
        await session.ObserveAsync(CancellationToken.None);
        JsonAssert.Args("""{"interactiveOnly":true}""", h.Fake.LastArgs("capture.snapshot"));
    }

    [Fact]
    public async Task Retries_a_sparse_snapshot_device_side_before_returning_it()
    {
        var h = Harness.Create();
        var calls = 0;
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => ++calls < 2 ? Sparse() : FakeDeviceClient.SettingsSnapshot());
        var session = await OpenAttempt(h);
        var snapshot = await session.ObserveAsync(CancellationToken.None);
        Assert.Equal(2, calls);
        Assert.Equal(11, Walk(snapshot.Roots[0]).Count());
        Assert.Equal([TimeSpan.FromMilliseconds(1200)], h.Delays);
    }

    [Fact]
    public async Task Flags_a_tree_agent_device_cut_or_one_still_sparse_after_the_retries_as_truncated_a_whole_tree_carries_no_flag()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        Assert.False((await session.ObserveAsync(CancellationToken.None)).Truncated);

        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
        {
            var cut = FakeDeviceClient.SettingsSnapshot();
            cut["truncated"] = true;
            return cut;
        });
        Assert.True((await session.ObserveAsync(CancellationToken.None)).Truncated);

        // One application node after every retry: the screen holds more than the listing shows.
        var calls = 0;
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
        {
            calls++;
            return Sparse();
        });
        var sparse = await session.ObserveAsync(CancellationToken.None);
        Assert.Equal(3, calls);
        Assert.Single(Walk(sparse.Roots[0]));
        Assert.True(sparse.Truncated);
    }

    [Fact]
    public async Task Holds_the_status_bar_in_a_fixed_state_for_a_screenshot_taken_for_comparison_and_only_then()
    {
        var h = Harness.Create();
        var png = PngCodec.Encode(PngCodec.Solid(195, 422, 4, 255));
        var asked = new List<bool?>();
        h.Fake.Respond(DeviceMethods.CaptureScreenshot, options =>
        {
            asked.Add(options?["normalizeStatusBar"]?.GetValue<bool>());
            File.WriteAllBytes(options!["path"]!.GetValue<string>(), png);
            return new JsonObject { ["path"] = options["path"]!.GetValue<string>(), ["logicalWidth"] = 390, ["logicalHeight"] = 844 };
        });

        // No geometry in the tree: the viewport probe is the one capture that is not a comparison.
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.NodesOnly(RawNodes.SettingsNodes.Where(node => node.Password != true && node.Type != "secure-text-field").Select(node => node with { Rect = null })));
        var session = await OpenAttempt(h);
        await session.ObserveAsync(CancellationToken.None);
        await session.ScreenshotAsync(CancellationToken.None);
        Assert.Equal([null, true], asked);
    }

    [Fact]
    public async Task Counts_the_regions_it_paints_over_secure_fields_in_a_screenshot()
    {
        var h = Harness.Create();
        var png = PngCodec.Encode(PngCodec.Solid(195, 422, 4, 255));
        h.Fake.Respond(DeviceMethods.CaptureScreenshot, options =>
        {
            File.WriteAllBytes(options!["path"]!.GetValue<string>(), png);
            return new JsonObject { ["path"] = options["path"]!.GetValue<string>(), ["logicalWidth"] = 390, ["logicalHeight"] = 844 };
        });
        var session = await OpenAttempt(h);
        var observation = await session.ObserveAsync(CancellationToken.None);
        var secure = Walk(observation.Roots[0]).Count(node => node.States.Secure);
        Assert.True(secure > 0);
        Assert.Equal(secure, (await session.ScreenshotAsync(CancellationToken.None)).MaskedRegionCount);
    }

    [Fact]
    public async Task Withholds_the_pixels_of_a_screen_with_a_secure_field_when_the_image_cannot_be_read_or_decoded()
    {
        var h = Harness.Create();

        // A PNG signature and header naming the size, with no image data behind them: the size reads, the decode fails.
        var headerOnly = PngCodec.Encode(PngCodec.Solid(195, 422, 4, 0))[..33];
        byte[] shot = [1, 2, 3];
        h.Fake.Respond(DeviceMethods.CaptureScreenshot, options =>
        {
            File.WriteAllBytes(options!["path"]!.GetValue<string>(), shot);
            return new JsonObject { ["path"] = options["path"]!.GetValue<string>() };
        });
        var session = await OpenAttempt(h);
        foreach (var bytes in new[] { new byte[] { 1, 2, 3 }, headerOnly })
        {
            shot = bytes;
            var observation = await session.ObserveAsync(CancellationToken.None);
            Assert.Single(observation.Roots[0].Children);
            var withheld = await Assert.ThrowsAsync<EngineException>(() => session.ScreenshotAsync(CancellationToken.None));
            Assert.Equal("ENGINE_FAILURE", withheld.Code);
        }
    }

    [Fact]
    public async Task Is_an_empty_screen_before_any_app_is_open_when_no_app_is_pinned_and_a_fault_when_one_is()
    {
        var free = Harness.Create(pinned: false);
        free.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => throw new AgentDeviceError("SESSION_NOT_FOUND", "No active app session"));

        // No tree means no geometry: the viewport comes from the device's screenshot metadata, once.
        free.Fake.Respond(DeviceMethods.CaptureScreenshot, _ => new JsonObject { ["logicalWidth"] = 390, ["logicalHeight"] = 844, ["width"] = 1170, ["height"] = 2532, ["pixelDensity"] = 3 });
        var session = await OpenAttempt(free);
        var empty = await session.ObserveAsync(CancellationToken.None);
        var root = Assert.Single(empty.Roots);
        Assert.Equal("root", root.Ref);
        Assert.Equal("screen", root.Role);
        Assert.Equal(new BoundingBox(0, 0, 390, 844), root.Rect);
        Assert.Empty(root.Children);
        Assert.Equal("", empty.Route);
        Assert.False(empty.Truncated);
        await session.ObserveAsync(CancellationToken.None);
        Assert.Single(free.Fake.Methods(), method => method == "capture.screenshot");

        // A device that reports no geometry at all cannot be observed: an invented viewport would misplace every tap.
        var blind = Harness.Create(pinned: false);
        blind.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => new JsonObject { ["nodes"] = new JsonArray() });
        blind.Fake.Respond(DeviceMethods.CaptureScreenshot, _ => new JsonObject());
        var blindSession = await OpenAttempt(blind);
        var unknown = await Assert.ThrowsAsync<EngineException>(() => blindSession.ObserveAsync(CancellationToken.None));
        Assert.Equal("ENGINE_FAILURE", unknown.Code);
        Assert.Contains("viewport is unknown", unknown.Message, StringComparison.Ordinal);

        var pinned = Harness.Create();
        pinned.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => throw new AgentDeviceError("SESSION_NOT_FOUND", "No active app session"));
        var pinnedSession = await OpenAttempt(pinned);
        var fault = await Assert.ThrowsAsync<EngineException>(() => pinnedSession.ObserveAsync(CancellationToken.None));
        Assert.Equal("INVALID_STATE", fault.Code);
    }

    // Every input entry runs under its budget; one that went out and timed out is possibly committed, so the runner never repeats it.
    [Theory]
    [InlineData("tap", DeviceMethods.InteractionsPress)]
    [InlineData("screen swipe", DeviceMethods.InteractionsScroll)]
    [InlineData("tap at point", DeviceMethods.InteractionsPress)]
    [InlineData("type", DeviceMethods.InteractionsType)]
    [InlineData("press", DeviceMethods.CommandKeyboard)]
    [InlineData("keyboard dismiss", DeviceMethods.CommandKeyboard)]
    [InlineData("back", DeviceMethods.CommandBack)]
    public async Task Ends_an_input_hung_on_its_device_command_at_its_budget_as_possibly_committed(string entry, string method)
    {
        var h = Harness.Create();
        var session = await Screens.OpenAttempt(h, actionTimeout: TimeSpan.FromMilliseconds(200));
        session.Origin = ActionOrigin.Test;
        var root = await Screens.RootOf(session);
        var about = Named(root, "About");
        h.Fake.RespondAsync(method, (_, _) => new TaskCompletionSource<JsonNode?>().Task);
        Task hung = entry switch
        {
            "tap" => session.PerformAsync(about, new LocatorAction.Tap(), CancellationToken.None),
            "screen swipe" => session.PerformAsync(root, new LocatorAction.Swipe(ScrollDirection.Down), CancellationToken.None),
            "tap at point" => session.PerformAtAsync(new Point(10, 10), new PointerAction.Tap()),
            "type" => session.TypeTextAsync("hi", replace: false, CancellationToken.None),
            "press" => session.PressAsync("Enter", CancellationToken.None),
            "keyboard dismiss" => session.DismissKeyboardAsync(CancellationToken.None, session.InputBudget("keyboard dismiss")),
            "back" => session.BackAsync(CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(entry), entry, null),
        };
        var failure = await Assert.ThrowsAsync<EngineException>(() => hung);
        Assert.Equal("ACTION_MAY_HAVE_COMMITTED", failure.Code);
    }

    [Fact]
    public async Task Sends_nothing_once_a_settling_target_spent_the_budget_and_no_keystroke_once_a_focus_tap_used_it_up()
    {
        var h = Harness.Create(options => options with { TransitionMilliseconds = 120 });
        var session = await Screens.OpenAttempt(h, actionTimeout: TimeSpan.FromMilliseconds(100));
        session.Origin = ActionOrigin.Test;
        var about = await Screens.Observed(session, "About");

        // About moves with the tap, so the next tap relocates it first; a slow relocation spends the whole budget.
        await session.PerformAsync(about, new LocatorAction.Tap(), CancellationToken.None);
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SnapshotOf(
            RawNodes.SettingsNodes.Select(node => node.Label == "About" ? node with { Rect = new Rect(0, 400, 390, 44) } : node)));
        var moved = await Screens.Observed(session, "About");
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
        {
            h.Clock.Spend(TimeSpan.FromMilliseconds(400));
            return FakeDeviceClient.SettingsSnapshot();
        });
        int Presses() => h.Fake.Methods().Count(method => method == DeviceMethods.InteractionsPress);
        var before = Presses();
        var late = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(moved, new LocatorAction.Tap(), CancellationToken.None));
        Assert.Equal("OPERATION_TIMEOUT", late.Code);
        Assert.Equal(before, Presses());

        // A key on an unfocused field taps it, then types. The tap answers within its grace but past the budget, so the keystroke is not sent.
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => FakeDeviceClient.SettingsSnapshot());
        var field = await Screens.Observed(session, "Search");
        session.Origin = ActionOrigin.Agent;
        h.Fake.Respond(DeviceMethods.InteractionsPress, _ =>
        {
            h.Clock.Spend(TimeSpan.FromMilliseconds(170));
            return new JsonObject();
        });
        var cut = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(field, new LocatorAction.Press("a"), CancellationToken.None));
        Assert.Equal("ACTION_MAY_HAVE_COMMITTED", cut.Code);
        Assert.DoesNotContain(DeviceMethods.InteractionsType, h.Fake.Methods());
    }

    [Fact]
    public async Task Refuses_a_long_press_that_holds_longer_than_its_timeout_leaves_before_sending_it()
    {
        var h = Harness.Create();
        var session = await Screens.OpenAttempt(h, actionTimeout: TimeSpan.FromSeconds(2));
        session.Origin = ActionOrigin.Test;
        var about = await Screens.Observed(session, "About");
        var onNode = await Assert.ThrowsAsync<EngineException>(() => session.LongPressAsync(about, TimeSpan.FromSeconds(3)));
        Assert.Equal("NOT_ACTIONABLE", onNode.Code);
        var atPoint = await Assert.ThrowsAsync<EngineException>(() => session.PerformAtAsync(new Point(10, 10), new PointerAction.LongPress(TimeSpan.FromSeconds(3))));
        Assert.Equal("NOT_ACTIONABLE", atPoint.Code);
        Assert.DoesNotContain(DeviceMethods.InteractionsLongPress, h.Fake.Methods());
    }

    [Fact]
    public async Task Keeps_a_timed_out_input_in_flight_the_next_attempt_waits_for_the_device_command_to_settle()
    {
        var h = Harness.Create();
        var session = await Screens.OpenAttempt(h, actionTimeout: TimeSpan.FromMilliseconds(200));
        var about = await Screens.Observed(session, "About");
        var pressed = new TaskCompletionSource<JsonNode?>();
        var settled = false;
        h.Fake.RespondAsync(DeviceMethods.InteractionsPress, (_, _) => pressed.Task);
        var failure = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(about, new LocatorAction.Tap(), CancellationToken.None));
        Assert.Equal("ACTION_MAY_HAVE_COMMITTED", failure.Code);
        Assert.False(settled);

        // The attempt ends; the command it abandoned is still running on the device, so the next attempt cannot start yet.
        await session.DisposeAsync();
        var next = h.Boot();
        Assert.False(next.IsCompleted);
        settled = true;
        pressed.SetResult(new JsonObject());
        await next;
        Assert.True(settled);
    }

    [Fact]
    public async Task Cancels_a_snapshot_when_the_operation_aborts()
    {
        var h = Harness.Create();
        h.Fake.RespondAsync(DeviceMethods.CaptureSnapshot, (_, _) => new TaskCompletionSource<JsonNode?>().Task);
        var session = await OpenAttempt(h);
        using var controller = new CancellationTokenSource();
        var pending = session.ObserveAsync(controller.Token);
        await controller.CancelAsync();
        var cancelled = await Assert.ThrowsAsync<EngineException>(() => pending);
        Assert.Equal("CANCELLED", cancelled.Code);
    }

    [Fact]
    public async Task Lets_a_locate_outlive_its_budget_a_poll_keeps_its_last_read_so_a_slow_screen_reads_as_missing_not_as_a_timeout()
    {
        var h = Harness.Create();
        var session = await Screens.OpenAttempt(h);
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
        {
            h.Clock.Advance(TimeSpan.FromSeconds(3));
            return FakeDeviceClient.SettingsSnapshot();
        });
        var about = LocatorExpression.For("text", TextMatch.FromString("About"), new TextMatchOptions(), "value");
        Assert.Single(await session.LocateAsync(about, TimeSpan.FromSeconds(1), CancellationToken.None));
    }
}
