// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Engine;

namespace E2E.Tests.FixtureRuntime;

/// <summary>
/// Upstream's <c>fixture-runtime.test.ts</c>, "coordinate input", over a screen whose engine declares the <c>tap</c> and
/// <c>scrollIntoView</c> actions and the <c>tap</c>, <c>doubleTap</c>, and <c>swipeTo</c> pointer actions. Not ported: the unknown option of "rejects a
/// point off the plane and an unknown option before any engine call" (options are typed), the points of other shapes (a missing or
/// boolean coordinate; a <see cref="ScreenPoint"/> is two coordinates), and, of "screen.swipe({ from, to })...", the options that name both a
/// direction and a path or lack one (a call takes one of the two option classes). A duration is a <see cref="TimeSpan"/>, so the
/// duration cases that need a fraction of a millisecond are checked with it.
/// </summary>
public sealed class CoordinateInputTests
{
    private sealed record Pointer(
        Screen Screen,
        List<string> Performed,
        List<string> PerformedAt,
        List<int> Locates,
        List<TimeSpan?> PointTimeouts,
        List<int> Observes
    );

    private static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static Pointer PointerScreen(
        bool rect = true,
        bool hidden = false,
        bool pointer = true,
        TimeSpan? timeout = null
    )
    {
        var node = new SemanticNode
        {
            Ref = "pad",
            Role = "img",
            Name = "Pad",
            Rect = rect ? new BoundingBox(100, 200, 50, 20) : null,
            States = new NodeStates { Hidden = hidden },
        };
        var performed = new List<string>();
        var performedAt = new List<string>();
        var locates = new List<int>();
        var pointTimeouts = new List<TimeSpan?>();
        var observes = new List<int>();
        var screen = new Screen(
            new global::E2E.LocatorEngine(
                new global::E2E.LocatorEngineOptions
                {
                    Observe = _ =>
                    {
                        observes.Add(1);
                        return Task.FromResult(
                            new Observation
                            {
                                Route = "/",
                                Roots = [node],
                                Viewport = new TraceViewport(1280, 720),
                            }
                        );
                    },
                    Perform = (_, action, _, _) =>
                    {
                        performed.Add(EngineActionKinds.Of(action));
                        return Task.CompletedTask;
                    },
                    Cancellation = () => CancellationToken.None,
                    ActionTimeout = timeout ?? TimeSpan.FromSeconds(2),
                    AssertionTimeout = TimeSpan.FromSeconds(2),
                    Locate = (_, _, _) =>
                    {
                        locates.Add(1);
                        return Task.FromResult<IReadOnlyList<SemanticNode>>([node]);
                    },
                    PerformAt = (point, action, operation, _) =>
                    {
                        pointTimeouts.Add(operation.Timeout);
                        var path = action is PointerAction.SwipeTo swipe
                            ? $" -> {Format(swipe.Target.X)},{Format(swipe.Target.Y)}{(swipe.Duration is { } duration ? $" for {Format(duration.TotalMilliseconds)}ms" : "")}"
                            : "";
                        performedAt.Add($"{EngineActionKinds.Of(action)} @ {Format(point.X)},{Format(point.Y)}{path}");
                        return Task.CompletedTask;
                    },
                }
            ),
            () => { }
        )
        {
            ActionKinds = ["tap", "scrollIntoView"],
            PointerActionKinds = pointer ? ["tap", "doubleTap", "swipeTo"] : [],
            Steps = new StepRecorder("attempt"),
        };
        return new Pointer(screen, performed, performedAt, locates, pointTimeouts, observes);
    }

    [Fact]
    public async Task Screen_tapAt_dispatches_a_pointer_tap_at_the_viewport_point_with_no_locate_and_records_the_step()
    {
        var (screen, _, performedAt, locates, _, observes) = PointerScreen();

        await screen.TapAtAsync(new Point(12.5, 40));

        Assert.Equal(["tap @ 12.5,40"], performedAt);
        Assert.Empty(locates);
        Assert.Empty(observes);
        var step = screen.Steps!.All()[^1];
        Assert.Equal(
            (StepKind.Screen, "screen.tapAt", "(12.5, 40)", StepStatus.Passed),
            (step.Kind, step.Api, step.Label, step.Status)
        );
    }

    [Fact]
    public async Task Resolves_percentages_against_one_viewport_read_and_labels_the_step_as_written()
    {
        // The fake engine's viewport is 1280 by 720 (tests/helpers/snapshot.ts upstream).
        var (screen, _, performedAt, _, _, observes) = PointerScreen();

        await screen.TapAtAsync(new ScreenPoint("50%", "25%"));

        var step = screen.Steps!.All()[^1];
        Assert.Equal(
            (StepKind.Screen, "screen.tapAt", "(50%, 25%)", StepStatus.Passed),
            (step.Kind, step.Api, step.Label, step.Status)
        );
        Assert.Contains(
            step.Events,
            e => e.Kind == StepEventKind.Engine && e.Detail == "tap at (640, 180)"
        );
        Assert.Equal([1], observes);
        await screen.DoubleTapAtAsync(new ScreenPoint("12.5%", 40));
        await screen.TapAtAsync(new ScreenPoint("100%", "100%"));
        await screen.SwipeAsync(new SwipePathOptions { From = new ScreenPoint("50%", "90%"), To = new ScreenPoint("50%", 72) });
        Assert.Equal(
            ["tap @ 640,180", "doubleTap @ 160,40", "tap @ 1279,719", "swipeTo @ 640,648 -> 640,72"],
            performedAt
        );
        Assert.Equal([1, 1, 1, 1], observes);
    }

    [Fact]
    public async Task Refuses_a_malformed_or_out_of_range_percentage_before_reading_the_screen()
    {
        var (screen, _, performedAt, _, _, observes) = PointerScreen();

        foreach (var x in new[] { "50", "120%", "-5%", "1e2%" })
        {
            var error = await Assert.ThrowsAsync<TestException>(() => screen.TapAtAsync(new ScreenPoint(x, 1)));
            Assert.Equal("INVALID_ARGUMENT", error.Code);
            Assert.Equal($"tapAt takes a percentage from '0%' through '100%', got \"{x}\"", error.Message);
        }

        Assert.Empty(observes);
        Assert.Empty(performedAt);
    }

    [Fact]
    public async Task Screen_doubleTapAt_dispatches_a_bare_double_tap_and_records_the_step()
    {
        var (screen, _, performedAt, locates, pointTimeouts, _) = PointerScreen(timeout: TimeSpan.FromSeconds(5));

        await screen.DoubleTapAtAsync(new Point(12.5, 40), new ActionOptions { Timeout = TimeSpan.FromSeconds(1) });

        Assert.Equal(["doubleTap @ 12.5,40"], performedAt);
        Assert.True(pointTimeouts[0] > TimeSpan.Zero);
        Assert.True(pointTimeouts[0] <= TimeSpan.FromSeconds(1));
        Assert.Empty(locates);
        var step = screen.Steps!.All()[^1];
        Assert.Equal(
            (StepKind.Screen, "screen.doubleTapAt", "(12.5, 40)", StepStatus.Passed),
            (step.Kind, step.Api, step.Label, step.Status)
        );
    }

    [Fact]
    public async Task Partial_rejects_a_point_off_the_plane_before_any_engine_call()
    {
        var (screen, _, performedAt, _, _, _) = PointerScreen();

        foreach (
            var (method, call) in new (string, Func<ScreenPoint, Task>)[]
            {
                ("tapAt", point => screen.TapAtAsync(point)),
                ("doubleTapAt", point => screen.DoubleTapAtAsync(point)),
            }
        )
        {
            foreach (var point in new[] { new Point(double.NaN, 1), new Point(double.PositiveInfinity, 1) })
            {
                var error = await Assert.ThrowsAsync<TestException>(() => call(point));
                Assert.Equal("INVALID_ARGUMENT", error.Code);
                Assert.Equal($"{method} requires a point {{ x, y }} of finite numbers or percentages", error.Message);
            }

            var text = await Assert.ThrowsAsync<TestException>(() => call(new ScreenPoint("1", 2)));
            Assert.Equal("INVALID_ARGUMENT", text.Code);
            Assert.Equal($"{method} takes a percentage from '0%' through '100%', got \"1\"", text.Message);

            var negative = await Assert.ThrowsAsync<TestException>(() => call(new Point(-1, 0)));
            Assert.Equal("INVALID_ARGUMENT", negative.Code);
            Assert.Equal($"{method} requires a point {{ x, y }} of non-negative numbers", negative.Message);
        }

        var position = Assert.IsType<TestException>(
            Record.Exception(
                (Action)(() => screen.GetByLabel("Pad").TapAsync(new TapOptions { Position = new Point(-1, 0) }))
            )
        );
        Assert.Equal("INVALID_ARGUMENT", position.Code);
        Assert.Equal("tap position requires a point { x, y } of non-negative numbers", position.Message);
        Assert.Empty(performedAt);
    }

    [Fact]
    public void NET_only_a_position_on_a_tap_family_verb_other_than_tap_is_refused_not_dropped()
    {
        var (screen, _, performedAt, _, _, _) = PointerScreen();
        var options = new TapOptions { Position = new Point(1, 1) };

        var error = Assert.IsType<TestException>(
            Record.Exception((Action)(() => screen.GetByLabel("Pad").DoubleTapAsync(options)))
        );

        Assert.Equal("INVALID_ARGUMENT", error.Code);
        Assert.Equal("doubleTap options has no key \"position\"; it takes timeout, modifiers", error.Message);
        Assert.Empty(performedAt);
    }

    [Fact]
    public async Task Fails_UNSUPPORTED_CAPABILITY_without_a_pointer_tap_before_the_locator_is_resolved()
    {
        var (screen, _, _, locates, _, observes) = PointerScreen(pointer: false);
        const string tapAt =
            "the \"tap\" action at a point is not available on this target: its engine declares no pointer actions";

        var tapped = await Assert.ThrowsAsync<EngineException>(() => screen.TapAtAsync(new Point(1, 1)));
        var doubleTapped = await Assert.ThrowsAsync<EngineException>(() => screen.DoubleTapAtAsync(new Point(1, 1)));
        var positioned = await Assert.ThrowsAsync<EngineException>(() =>
            screen.GetByLabel("Pad").TapAsync(new TapOptions { Position = new Point(1, 1) })
        );
        var swiped = await Assert.ThrowsAsync<EngineException>(() =>
            screen.SwipeAsync(new SwipePathOptions { From = new Point(1, 1), To = new Point(1, 9) })
        );

        Assert.Equal((EngineErrorCodes.UnsupportedCapability, tapAt), (tapped.Code, tapped.Message));
        await Assert.ThrowsAsync<EngineException>(() => screen.TapAtAsync(new ScreenPoint("50%", "50%")));
        Assert.Empty(observes);
        Assert.Equal(EngineErrorCodes.UnsupportedCapability, doubleTapped.Code);
        Assert.Contains("the \"doubleTap\" action at a point", doubleTapped.Message, StringComparison.Ordinal);
        Assert.Equal((EngineErrorCodes.UnsupportedCapability, tapAt), (positioned.Code, positioned.Message));
        Assert.Equal(EngineErrorCodes.UnsupportedCapability, swiped.Code);
        Assert.Contains("the \"swipeTo\" action at a point", swiped.Message, StringComparison.Ordinal);
        Assert.Empty(locates);
        await screen.GetByLabel("Pad").TapAsync();
        Assert.NotEmpty(locates);
    }

    [Theory]
    [InlineData("hidden")]
    [InlineData("has no box")]
    public async Task Waits_for_a_node_that_is_x_and_fails_LOCATOR_NOT_FOUND_at_the_deadline(string state)
    {
        var (screen, _, performedAt, _, _, _) = PointerScreen(rect: state != "has no box", hidden: state == "hidden");

        var error = await Assert.ThrowsAsync<TestException>(() =>
            screen
                .GetByLabel("Pad")
                .TapAsync(new TapOptions { Position = new Point(1, 1), Timeout = TimeSpan.FromMilliseconds(250) })
        );

        Assert.Equal("LOCATOR_NOT_FOUND", error.Code);
        Assert.Contains("did not become visible with a box", error.Message, StringComparison.Ordinal);
        Assert.NotNull(error.Details?.WaitedMs);
        Assert.Empty(performedAt);
    }

    [Fact]
    public async Task Taps_the_point_of_the_box_the_position_names_once_the_node_is_scrolled_into_view()
    {
        var (screen, performed, performedAt, _, _, _) = PointerScreen();

        await screen.GetByLabel("Pad").TapAsync(new TapOptions { Position = new Point(5, 5) });

        Assert.Equal(["scrollIntoView"], performed);
        Assert.Equal(["tap @ 105,205"], performedAt);
    }

    [Fact]
    public async Task Screen_swipe_from_to_dispatches_a_pointer_swipe_along_the_path_a_direction_still_needs_the_swipe_action()
    {
        var (screen, _, performedAt, _, _, observes) = PointerScreen();

        await screen.SwipeAsync(new SwipePathOptions { From = new Point(10, 20), To = new Point(10, 300) });
        Assert.Equal(["swipeTo @ 10,20 -> 10,300"], performedAt);
        Assert.Empty(observes);
        var step = screen.Steps!.All()[^1];
        Assert.Equal(
            (StepKind.Screen, "screen.swipe", "(10, 20) → (10, 300)", StepStatus.Passed),
            (step.Kind, step.Api, step.Label, step.Status)
        );

        await screen.SwipeAsync(
            new SwipePathOptions
            {
                From = new Point(10, 20),
                To = new Point(10, 300),
                Duration = TimeSpan.FromMilliseconds(400),
            }
        );
        Assert.Equal("swipeTo @ 10,20 -> 10,300 for 400ms", performedAt[^1]);

        await screen.SwipeAsync(
            new SwipePathOptions
            {
                From = new Point(540, 1560),
                To = new Point(540, 840),
                Duration = TimeSpan.FromMilliseconds(300),
            }
        );
        Assert.Equal("swipeTo @ 540,1560 -> 540,840 for 300ms", performedAt[^1]);
        step = screen.Steps!.All()[^1];
        Assert.Equal(
            (StepKind.Screen, "screen.swipe", "(540, 1560) → (540, 840)", StepStatus.Passed),
            (step.Kind, step.Api, step.Label, step.Status)
        );

        foreach (var (duration, shown) in new[] { (0.0, "0"), (15.0, "15"), (10_001.0, "10001"), (1.5, "1.5") })
        {
            var error = await Assert.ThrowsAsync<TestException>(() =>
                screen.SwipeAsync(
                    new SwipePathOptions
                    {
                        From = new Point(1, 1),
                        To = new Point(2, 2),
                        Duration = TimeSpan.FromMilliseconds(duration),
                    }
                )
            );
            Assert.Equal("INVALID_ARGUMENT", error.Code);
            Assert.Equal("swipe duration must be an integer from 16 through 10000, got " + shown, error.Message);
        }

        var path = await Assert.ThrowsAsync<TestException>(() =>
            screen.SwipeAsync(new SwipePathOptions { From = new Point(1, 1), To = new Point(double.NaN, 1) })
        );
        Assert.Equal("INVALID_ARGUMENT", path.Code);
        Assert.Equal("swipe to requires a point { x, y } of finite numbers or percentages", path.Message);

        var direction = await Assert.ThrowsAsync<EngineException>(() =>
            screen.SwipeAsync(new SwipeOptions { Direction = ScrollDirection.Up })
        );
        Assert.Equal(EngineErrorCodes.UnsupportedCapability, direction.Code);
        Assert.Equal(
            [
                "swipeTo @ 10,20 -> 10,300",
                "swipeTo @ 10,20 -> 10,300 for 400ms",
                "swipeTo @ 540,1560 -> 540,840 for 300ms",
            ],
            performedAt
        );
    }
}
