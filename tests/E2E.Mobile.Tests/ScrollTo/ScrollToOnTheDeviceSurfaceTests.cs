// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Engine;
using static E2E.Mobile.Tests.Screens;

namespace E2E.Mobile.Tests.ScrollTo;

/// <summary>
/// Upstream's <c>describe('scrollTo on the device surface')</c> (<c>packages/mobile/tests/unit/scroll-to.test.ts</c>): the drag toward an edge,
/// what a region shows, and the loop that stops when a drag moves nothing. The root's ref is the port's screen root node, which a test performs on.
/// </summary>
public sealed class ScrollToOnTheDeviceSurfaceTests
{
    private static readonly OperationContext Test = OperationContext.Test;

    private static JsonObject Viewport() => new() { ["width"] = 390, ["height"] = 844 };

    /// <summary>The Settings screen with every row below the navigation bar moved up by <paramref name="offset"/>, as a scroll leaves it.</summary>
    private static JsonObject Scrolled(double offset, params (string Key, JsonNode? Value)[] more)
    {
        var nodes = RawNodes.SettingsNodes.Select(node =>
            node.Rect is { Y: > 100 } rect ? node with { Rect = rect with { Y = rect.Y - offset } } : node
        );
        return FakeDeviceClient.SnapshotOf(nodes, [("viewport", Viewport()), .. more]);
    }

    /// <summary>Answers each snapshot with the next screen in <paramref name="list"/>, the last one from then on.</summary>
    private static void Screens(Harness h, params JsonObject[] list)
    {
        var taken = 0;
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => list[Math.Min(taken++, list.Length - 1)].DeepClone());
    }

    private static async Task<(Harness H, DeviceSession Session)> OpenedAsync()
    {
        var h = Harness.Create();
        return (h, await OpenAttempt(h));
    }

    private static OperationContext Within(TimeSpan timeout) => Test with { Timeout = timeout };

    private static IEnumerable<JsonObject> Swipes(Harness h, int from = 0) =>
        h
            .Fake.Calls.Skip(from)
            .Where(call => call.Method == DeviceMethods.InteractionsSwipe)
            .Select(call => call.Args!);

    [Fact]
    public async Task Drags_the_screen_until_a_drag_moves_nothing_sending_more_drags_per_read_while_it_moves()
    {
        var (h, session) = await OpenedAsync();
        var root = await RootOf(session);
        Screens(h, Scrolled(0), Scrolled(300), Scrolled(600), Scrolled(600));
        var before = h.Fake.Calls.Count;

        await session.PerformAsync(root, new LocatorAction.ScrollTo(ScrollEdge.Bottom), Test, CancellationToken.None);

        var calls = h.Fake.Calls.Skip(before).Where(call => call.Method != DeviceMethods.CaptureScreenshot).ToList();
        Assert.Equal(
            [
                "capture.snapshot",
                "interactions.swipe",
                "capture.snapshot",
                "interactions.swipe",
                "capture.snapshot",
                "interactions.swipe",
                "capture.snapshot",
            ],
            calls.Select(call => call.Method)
        );
        // The whole 390x844 screen: a quarter in from the bottom to a quarter in from the top, one drag, then two, then three.
        var swipes = calls
            .Where(call => call.Method == DeviceMethods.InteractionsSwipe)
            .Select(call => call.Args)
            .ToList();
        Assert.Equal(3, swipes.Count);
        for (var index = 0; index < 3; index++)
        {
            JsonAssert.Args(
                $$"""{"from":{"x":195,"y":633},"to":{"x":195,"y":211},"count":{{index + 1}},"pauseMs":0}""",
                swipes[index]
            );
        }
    }

    [Fact]
    public async Task Never_reads_a_truncated_capture_as_unchanged_so_a_capped_tree_does_not_end_the_scroll()
    {
        var (h, session) = await OpenedAsync();
        var root = await RootOf(session);
        var cut = Scrolled(0, ("truncated", true));
        Screens(h, Scrolled(0), cut, cut, Scrolled(0), Scrolled(0));
        var before = h.Fake.Calls.Count;

        await session.PerformAsync(root, new LocatorAction.ScrollTo(ScrollEdge.Bottom), Test, CancellationToken.None);

        // Two truncated reads, then two whole ones that agree.
        Assert.Equal(4, Swipes(h, before).Count());
    }

    [Fact]
    public async Task Drags_inside_a_nodes_own_box_and_stops_after_one_drag_when_it_is_at_its_edge_already()
    {
        var (h, session) = await OpenedAsync();
        var root = await RootOf(session);
        var scroller = Named(root, "Scroller");

        await session.PerformAsync(scroller, new LocatorAction.ScrollTo(ScrollEdge.Top), Test, CancellationToken.None);

        // Scroller is 390x400 at y 320: the finger runs down, from y 420 to y 620.
        var swipe = Assert.Single(Swipes(h));
        JsonAssert.Args("""{"from":{"x":195,"y":420},"to":{"x":195,"y":620},"count":1,"pauseMs":0}""", swipe);
    }

    [Fact]
    public async Task Keeps_dragging_the_box_it_measured_when_the_nodes_own_label_changes_as_it_scrolls()
    {
        var (h, session) = await OpenedAsync();
        var root = await RootOf(session);
        var scroller = Named(root, "Scroller");
        // The list names itself after its first visible row, which a drag changes.
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
        {
            var label = Swipes(h).Any() ? "Scroller, row 20" : "Scroller";
            var nodes = RawNodes.SettingsNodes.Select(node =>
                node.Label == "Scroller" ? node with { Label = label } : node
            );
            return FakeDeviceClient.SnapshotOf(nodes, ("viewport", Viewport()));
        });

        await session.PerformAsync(
            scroller,
            new LocatorAction.ScrollTo(ScrollEdge.Bottom),
            Test,
            CancellationToken.None
        );

        var swipes = Swipes(h).ToList();
        Assert.NotEmpty(swipes);
        Assert.All(swipes, swipe => Assert.Equal(620, swipe["from"]!["y"]!.GetValue<double>()));
    }

    public static TheoryData<string, string, string> Refusals =>
        new()
        {
            { "a keyboard that is up with no bounds", "keyboard", "keyboard is up and its bounds are unknown" },
            { "bounds that may be window-local", "warnings", "Nothing was sent" },
        };

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task Refuses_x_before_any_drag(string name, string fact, string message)
    {
        Assert.NotEmpty(name);
        var (h, session) = await OpenedAsync();
        var root = await RootOf(session);
        var scroller = Named(root, "Scroller");
        JsonNode extra =
            fact == "keyboard"
                ? new JsonObject { ["kind"] = "visibility-only", ["visible"] = true }
                : new JsonArray(DeviceErrors.OriginWarning);
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ => Scrolled(0, (fact, extra.DeepClone())));

        var failure = await Assert.ThrowsAsync<EngineException>(() =>
            session.PerformAsync(scroller, new LocatorAction.ScrollTo(ScrollEdge.Bottom), Test, CancellationToken.None)
        );

        Assert.Equal(EngineErrorCodes.NotActionable, failure.Code);
        Assert.Contains(message, failure.Message);
        Assert.Empty(Swipes(h));
    }

    [Fact]
    public async Task Refuses_a_node_with_no_area_on_screen_as_having_no_bounds_not_as_covered_by_a_keyboard()
    {
        var (_, session) = await OpenedAsync();
        var root = await RootOf(session);
        var hidden = Named(root, "Hidden");

        var failure = await Assert.ThrowsAsync<EngineException>(() =>
            session.PerformAsync(hidden, new LocatorAction.ScrollTo(ScrollEdge.Bottom), Test, CancellationToken.None)
        );

        Assert.Equal(EngineErrorCodes.NotActionable, failure.Code);
        Assert.Contains("has no bounds on screen", failure.Message);
    }

    [Fact]
    public async Task Starts_no_pass_the_budget_cannot_finish_so_it_fails_with_its_own_reason_within_the_budget()
    {
        var (h, session) = await OpenedAsync();
        var root = await RootOf(session);
        var loaded = 0;
        // Each drag takes 100 ms and a read 10 ms, so a pass grows with its drag count: 110, 210, then 310 ms.
        h.Fake.Respond(DeviceMethods.InteractionsSwipe, args =>
        {
            h.Clock.Spend(TimeSpan.FromMilliseconds(100 * args!["count"]!.GetValue<int>()));
            return new JsonObject();
        });
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
        {
            h.Clock.Spend(TimeSpan.FromMilliseconds(10));
            var nodes = RawNodes.SettingsNodes.Select(node =>
                node.Label == "About" ? node with { Label = $"About {++loaded}" } : node
            );
            return FakeDeviceClient.SnapshotOf(nodes, ("viewport", Viewport()));
        });
        var started = h.Clock.GetTimestamp();

        var failure = await Assert.ThrowsAsync<EngineException>(() =>
            session.PerformAsync(
                root,
                new LocatorAction.ScrollTo(ScrollEdge.Bottom),
                Within(TimeSpan.FromMilliseconds(600)),
                CancellationToken.None
            )
        );

        Assert.Equal(EngineErrorCodes.OperationTimeout, failure.Code);
        Assert.Equal("the content was still moving after 3 drags when the budget ran out", failure.Message);
        Assert.True(h.Clock.GetElapsedTime(started) <= TimeSpan.FromMilliseconds(600));
    }

    /// <summary>Swipes sent since call <paramref name="from"/>, counting each drag of a multi-drag swipe.</summary>
    private static int DragsSince(Harness h, int from) => Swipes(h, from).Sum(args => args["count"]!.GetValue<int>());

    [Fact]
    public async Task Counts_every_drag_it_sent_in_its_timeout_message_not_the_reads_between_them()
    {
        var (h, session) = await OpenedAsync();
        var root = await RootOf(session);
        var loaded = 0;
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
        {
            h.Clock.Spend(TimeSpan.FromMilliseconds(60));
            var nodes = RawNodes.SettingsNodes.Select(node =>
                node.Label == "About" ? node with { Label = $"About {++loaded}" } : node
            );
            return FakeDeviceClient.SnapshotOf(nodes, ("viewport", Viewport()));
        });
        var before = h.Fake.Calls.Count;

        var failure = await Assert.ThrowsAsync<EngineException>(() =>
            session.PerformAsync(
                root,
                new LocatorAction.ScrollTo(ScrollEdge.Bottom),
                Within(TimeSpan.FromMilliseconds(600)),
                CancellationToken.None
            )
        );

        var drags = DragsSince(h, before);
        Assert.True(drags > 3);
        Assert.Equal(EngineErrorCodes.OperationTimeout, failure.Code);
        Assert.Equal($"the content was still moving after {drags} drags when the budget ran out", failure.Message);
    }

    [Fact]
    public async Task Says_the_screen_could_not_be_read_whole_when_every_capture_is_truncated()
    {
        var (h, session) = await OpenedAsync();
        var root = await RootOf(session);
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
        {
            h.Clock.Spend(TimeSpan.FromMilliseconds(60));
            return Scrolled(0, ("truncated", true));
        });

        var failure = await Assert.ThrowsAsync<EngineException>(() =>
            session.PerformAsync(
                root,
                new LocatorAction.ScrollTo(ScrollEdge.Bottom),
                Within(TimeSpan.FromMilliseconds(600)),
                CancellationToken.None
            )
        );

        Assert.Equal(EngineErrorCodes.OperationTimeout, failure.Code);
        Assert.Contains("read with nodes left out (agent-device truncated it)", failure.Message);
    }
}
