// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.CheckActions.CheckPage;

namespace E2E.Tests.Actions;

/// <summary>
/// Upstream's <c>dispatchPointerAction</c> describe. Upstream stubs the page's mouse and reads its calls, and fakes the clock. The port
/// cannot see calls inside the engine, so each test runs a live page that logs the mouse events it receives, at the coordinates the page
/// sees. The ported paced tests read real time, so they are <c>Partial_</c>.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class DispatchPointerActionTests
{
    private const string Page = """
        <style>html, body { margin: 0; height: 100%; }</style>
        <div style="height:5000px"></div>
        <script>
          window.log = [];
          const note = (text) => window.log.push(text);
          addEventListener('mousemove', (e) => note('move ' + e.clientX + ',' + e.clientY));
          addEventListener('mousedown', () => note('down'));
          addEventListener('mouseup', () => note('up'));
          addEventListener('click', (e) => note('click ' + e.clientX + ',' + e.clientY));
          addEventListener('dblclick', (e) => note('dblclick ' + e.clientX + ',' + e.clientY));
          addEventListener('contextmenu', (e) => note('contextmenu ' + e.clientX + ',' + e.clientY));
          addEventListener('wheel', (e) => note('wheel ' + e.deltaX + ',' + e.deltaY), { passive: true });
        </script>
        """;

    /// <summary>The log of the page. A browser on some hosts also reports a move to the origin before the first pointer action; it is not an action's, so it is left out.</summary>
    private static async Task<string[]> LogAsync(IEngineSession session) =>
        [.. (await EvaluateAsync<string[]>(session, "() => window.log")).Where(entry => entry != "move 0,0")];

    /// <summary>The log of a drag. The page also clicks when a drag ends over the element it began on, which upstream's stub does not.</summary>
    private static async Task<string[]> DragLogAsync(IEngineSession session) =>
        [.. (await LogAsync(session)).Where(entry => !entry.StartsWith("click ", StringComparison.Ordinal))];

    [Fact]
    public async Task Taps_double_taps_secondary_taps_and_long_presses_at_the_point_as_given()
    {
        await RunAsync(Page, async session =>
        {
            await session.PerformAtAsync(new Point(10, 20), new PointerAction.Tap(), CancellationToken.None);
            Assert.Contains("click 10,20", await LogAsync(session));

            await session.PerformAtAsync(new Point(10, 20), new PointerAction.DoubleTap(), CancellationToken.None);
            Assert.Contains("dblclick 10,20", await LogAsync(session));

            await session.PerformAtAsync(new Point(10, 20), new PointerAction.SecondaryTap(), CancellationToken.None);
            Assert.Contains("contextmenu 10,20", await LogAsync(session));

            var before = (await LogAsync(session)).Length;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            await session.PerformAtAsync(new Point(10, 20), new PointerAction.LongPress(TimeSpan.FromMilliseconds(700)), CancellationToken.None);
            Assert.True(watch.Elapsed >= TimeSpan.FromMilliseconds(650), "a long press holds the button for its duration");
            Assert.Equal("click 10,20", (await LogAsync(session))[before..].Last());
        });
    }

    [Fact]
    public async Task Rounds_a_fractional_point_to_the_nearest_CSS_pixel_the_browser_would_truncate_it_a_pixel_early()
    {
        await RunAsync(Page, async session =>
        {
            await session.PerformAtAsync(new Point(332, 158.875), new PointerAction.Tap(), CancellationToken.None);
            Assert.Contains("click 332,159", await LogAsync(session));

            var before = (await DragLogAsync(session)).Length;
            await session.PerformAtAsync(new Point(10.4, 20.5), new PointerAction.SwipeTo(new Point(110.6, 20.5)), CancellationToken.None);
            Assert.Equal(["move 10,21", "down", "move 61,21", "move 111,21", "up"], (await DragLogAsync(session))[before..]);
        });
    }

    [Fact]
    public async Task Swipes_along_a_path_as_a_pointer_drag_down_at_the_point_through_the_midpoint_up_at_the_end()
    {
        await RunAsync(Page, async session =>
        {
            await session.PerformAtAsync(new Point(10, 20), new PointerAction.SwipeTo(new Point(110, 20)), CancellationToken.None);

            Assert.Equal(["move 10,20", "down", "move 60,20", "move 110,20", "up"], await DragLogAsync(session));
        });
    }

    [Fact]
    public async Task Partial_Swipes_along_a_path_with_duration_as_a_multi_step_drag_paced_over_time()
    {
        // Partial: upstream moves a fake clock 16 ms at a time and reads the calls after each step. The port reads the whole drag and
        // its real time: three moves and the release, taking about the duration.
        await RunAsync(Page, async session =>
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            await session.PerformAtAsync(new Point(10, 20), new PointerAction.SwipeTo(new Point(70, 20), TimeSpan.FromMilliseconds(48)), CancellationToken.None);

            Assert.True(watch.Elapsed >= TimeSpan.FromMilliseconds(40), "the drag is paced over its duration");
            Assert.Equal(["move 10,20", "down", "move 30,20", "move 50,20", "move 70,20", "up"], await DragLogAsync(session));
        });
    }

    [Fact]
    public async Task Partial_Stops_dispatching_moves_when_aborted_and_releases_the_mouse()
    {
        // Partial: upstream aborts after the first paced move on a fake clock. The port aborts once the page sees the first move after the
        // button went down, with an action budget and a swipe duration of one hour so only the abort ends it, and reads that the drag did
        // not reach its end and the button is up.
        await RunAsync(Page, async session =>
        {
            using var cancel = new CancellationTokenSource();
            var swipe = session.PerformAtAsync(new Point(10, 20), new PointerAction.SwipeTo(new Point(1010, 20), TimeSpan.FromHours(1)), cancel.Token);
            await WaitForMoveAfterDownAsync(session);

            await cancel.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => swipe.WaitAsync(TimeSpan.FromSeconds(30)));

            var log = await DragLogAsync(session);
            Assert.Equal("up", log[^1]);
            Assert.DoesNotContain("move 1010,20", log);
        }, TimeSpan.FromHours(1));
    }

    /// <summary>.NET-only: upstream's abort test has no case for a drag that is behind its schedule, where a step has no wait.</summary>
    [Fact]
    public async Task Stops_dispatching_moves_when_aborted_while_the_drag_is_behind_its_schedule()
    {
        // Each move takes the page about 50 ms, so a 16 ms step is always late and no step waits. An abort must still end the drag.
        const string SlowPage = Page + "<script>addEventListener('mousemove', () => { const end = performance.now() + 50; while (performance.now() < end); });</script>";
        await RunAsync(SlowPage, async session =>
        {
            using var cancel = new CancellationTokenSource();
            var swipe = session.PerformAtAsync(new Point(10, 20), new PointerAction.SwipeTo(new Point(1010, 20), TimeSpan.FromSeconds(10)), cancel.Token);
            await WaitForMoveAfterDownAsync(session);

            await cancel.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => swipe.WaitAsync(TimeSpan.FromSeconds(30)));

            var log = await DragLogAsync(session);
            Assert.Equal("up", log[^1]);
            Assert.True(log.Count(entry => entry.StartsWith("move ", StringComparison.Ordinal)) < 20, "the drag stopped soon after the abort");
        }, TimeSpan.FromHours(1));
    }

    /// <summary>Waits until the page has seen a move after the button went down, so the abort never depends on the clock.</summary>
    private static async Task WaitForMoveAfterDownAsync(IEngineSession session)
    {
        while (true)
        {
            var log = await LogAsync(session);
            var down = Array.IndexOf(log, "down");
            if (down >= 0 && log.Skip(down + 1).Any(entry => entry.StartsWith("move ", StringComparison.Ordinal)))
            {
                return;
            }

            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task Swipes_in_a_direction_as_a_wheel_gesture_over_the_point_sized_by_the_viewport()
    {
        await RunAsync(Page, async session =>
        {
            var height = await EvaluateAsync<int>(session, "() => innerHeight");

            await session.PerformAtAsync(new Point(10, 20), new PointerAction.Swipe(ScrollDirection.Down), CancellationToken.None);

            Assert.Equal(["move 10,20", $"wheel 0,{height / 2}"], await LogAsync(session));
        });
    }

    [Fact]
    public async Task Partial_Resolves_a_swipe_only_once_the_frame_after_the_wheel_rendered()
    {
        // Partial: upstream holds the render promise open and checks the swipe is pending. The port reads that the page scrolled by the
        // time the swipe resolved, which the wheel's frame wait makes true.
        await RunAsync(Page, async session =>
        {
            await session.PerformAtAsync(new Point(10, 20), new PointerAction.Swipe(ScrollDirection.Down), CancellationToken.None);

            Assert.True(await EvaluateAsync<double>(session, "() => scrollY") > 0);
        });
    }
}
