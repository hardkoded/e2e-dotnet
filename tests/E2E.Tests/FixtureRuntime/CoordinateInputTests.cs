// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Engine;

namespace E2E.Tests.FixtureRuntime;

/// <summary>
/// Upstream's <c>fixture-runtime.test.ts</c>, "coordinate input", over a screen whose engine declares the <c>tap</c> and
/// <c>scrollIntoView</c> actions and the <c>tap</c> and <c>swipeTo</c> pointer actions. Not ported: the unknown option of "rejects a
/// point off the plane and an unknown option before any engine call" (options are typed), the points of other shapes (a missing or
/// string coordinate; a <see cref="Point"/> is two numbers), and, of "screen.swipe({ from, to })...", the options that name both a
/// direction and a path or lack one (a call takes one of the two option classes). A duration is a <see cref="TimeSpan"/>, so the
/// duration cases that need a fraction of a millisecond are checked with it.
/// </summary>
public sealed class CoordinateInputTests
{
    private sealed record Pointer(Screen Screen, List<string> Performed, List<string> PerformedAt, List<int> Locates);

    private static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static Pointer PointerScreen(bool rect = true, bool hidden = false, bool pointer = true, TimeSpan? timeout = null)
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
        var screen = new Screen(
            _ => Task.FromResult(new Observation { Route = "/", Roots = [node] }),
            (_, action, _) =>
            {
                performed.Add(EngineActionKinds.Of(action));
                return Task.CompletedTask;
            },
            () => CancellationToken.None,
            () => { },
            timeout ?? TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2),
            locate: (_, _, _) =>
            {
                locates.Add(1);
                return Task.FromResult<IReadOnlyList<SemanticNode>>([node]);
            },
            performAt: (point, action, _) =>
            {
                var path = action is PointerAction.SwipeTo swipe
                    ? $" -> {Format(swipe.Target.X)},{Format(swipe.Target.Y)}{(swipe.Duration is { } duration ? $" for {Format(duration.TotalMilliseconds)}ms" : "")}"
                    : "";
                performedAt.Add($"{EngineActionKinds.Of(action)} @ {Format(point.X)},{Format(point.Y)}{path}");
                return Task.CompletedTask;
            })
        {
            ActionKinds = ["tap", "scrollIntoView"],
            PointerActionKinds = pointer ? ["tap", "swipeTo"] : [],
            Steps = new StepRecorder("attempt"),
        };
        return new Pointer(screen, performed, performedAt, locates);
    }

    [Fact]
    public async Task Screen_tapAt_dispatches_a_pointer_tap_at_the_viewport_point_with_no_locate_and_records_the_step()
    {
        var (screen, _, performedAt, locates) = PointerScreen();

        await screen.TapAtAsync(new Point(12.5, 40));

        Assert.Equal(["tap @ 12.5,40"], performedAt);
        Assert.Empty(locates);
        var step = screen.Steps!.All()[^1];
        Assert.Equal((StepKind.Screen, "screen.tapAt", "(12.5, 40)", StepStatus.Passed), (step.Kind, step.Api, step.Label, step.Status));
    }

    [Fact]
    public async Task Rejects_a_point_off_the_plane_and_an_unknown_option_before_any_engine_call()
    {
        var (screen, _, performedAt, _) = PointerScreen();

        foreach (var point in new[] { new Point(double.NaN, 1), new Point(double.PositiveInfinity, 1) })
        {
            var error = await Assert.ThrowsAsync<TestException>(() => screen.TapAtAsync(point));
            Assert.Equal("INVALID_ARGUMENT", error.Code);
            Assert.Equal("tapAt requires a point { x, y } of finite numbers", error.Message);
        }

        var negative = await Assert.ThrowsAsync<TestException>(() => screen.TapAtAsync(new Point(-1, 0)));
        Assert.Equal("INVALID_ARGUMENT", negative.Code);
        Assert.Equal("tapAt requires a point { x, y } of non-negative numbers", negative.Message);

        var position = Assert.IsType<TestException>(Record.Exception((Action)(() => screen.GetByLabel("Pad").TapAsync(new TapOptions { Position = new Point(-1, 0) }))));
        Assert.Equal("INVALID_ARGUMENT", position.Code);
        Assert.Equal("tap position requires a point { x, y } of non-negative numbers", position.Message);
        Assert.Empty(performedAt);
    }

    [Fact]
    public async Task Fails_UNSUPPORTED_CAPABILITY_without_a_pointer_tap_before_the_locator_is_resolved()
    {
        var (screen, _, _, locates) = PointerScreen(pointer: false);
        const string tapAt = "the \"tap\" action at a point is not available on this target: its engine declares no pointer actions";

        var tapped = await Assert.ThrowsAsync<EngineException>(() => screen.TapAtAsync(new Point(1, 1)));
        var positioned = await Assert.ThrowsAsync<EngineException>(() => screen.GetByLabel("Pad").TapAsync(new TapOptions { Position = new Point(1, 1) }));
        var swiped = await Assert.ThrowsAsync<EngineException>(() => screen.SwipeAsync(new SwipePathOptions { From = new Point(1, 1), To = new Point(1, 9) }));

        Assert.Equal((EngineErrorCodes.UnsupportedCapability, tapAt), (tapped.Code, tapped.Message));
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
        var (screen, _, performedAt, _) = PointerScreen(rect: state != "has no box", hidden: state == "hidden");

        var error = await Assert.ThrowsAsync<TestException>(() => screen.GetByLabel("Pad").TapAsync(new TapOptions { Position = new Point(1, 1), Timeout = TimeSpan.FromMilliseconds(250) }));

        Assert.Equal("LOCATOR_NOT_FOUND", error.Code);
        Assert.Contains("did not become visible with a box", error.Message, StringComparison.Ordinal);
        Assert.NotNull(error.Details?.WaitedMs);
        Assert.Empty(performedAt);
    }

    [Fact]
    public async Task Taps_the_point_of_the_box_the_position_names_once_the_node_is_scrolled_into_view()
    {
        var (screen, performed, performedAt, _) = PointerScreen();

        await screen.GetByLabel("Pad").TapAsync(new TapOptions { Position = new Point(5, 5) });

        Assert.Equal(["scrollIntoView"], performed);
        Assert.Equal(["tap @ 105,205"], performedAt);
    }

    [Fact]
    public async Task Screen_swipe_from_to_dispatches_a_pointer_swipe_along_the_path_a_direction_still_needs_the_swipe_action()
    {
        var (screen, _, performedAt, _) = PointerScreen();

        await screen.SwipeAsync(new SwipePathOptions { From = new Point(10, 20), To = new Point(10, 300) });
        Assert.Equal(["swipeTo @ 10,20 -> 10,300"], performedAt);
        var step = screen.Steps!.All()[^1];
        Assert.Equal((StepKind.Screen, "screen.swipe", "(10, 20) → (10, 300)", StepStatus.Passed), (step.Kind, step.Api, step.Label, step.Status));

        await screen.SwipeAsync(new SwipePathOptions { From = new Point(10, 20), To = new Point(10, 300), Duration = TimeSpan.FromMilliseconds(400) });
        Assert.Equal("swipeTo @ 10,20 -> 10,300 for 400ms", performedAt[^1]);

        await screen.SwipeAsync(new SwipePathOptions { From = new Point(540, 1560), To = new Point(540, 840), Duration = TimeSpan.FromMilliseconds(300) });
        Assert.Equal("swipeTo @ 540,1560 -> 540,840 for 300ms", performedAt[^1]);
        step = screen.Steps!.All()[^1];
        Assert.Equal((StepKind.Screen, "screen.swipe", "(540, 1560) → (540, 840)", StepStatus.Passed), (step.Kind, step.Api, step.Label, step.Status));

        foreach (var (duration, shown) in new[] { (0.0, "0"), (15.0, "15"), (10_001.0, "10001"), (1.5, "1.5") })
        {
            var error = await Assert.ThrowsAsync<TestException>(() => screen.SwipeAsync(new SwipePathOptions { From = new Point(1, 1), To = new Point(2, 2), Duration = TimeSpan.FromMilliseconds(duration) }));
            Assert.Equal("INVALID_ARGUMENT", error.Code);
            Assert.Equal("swipe duration must be an integer from 16 through 10000, got " + shown, error.Message);
        }

        var path = await Assert.ThrowsAsync<TestException>(() => screen.SwipeAsync(new SwipePathOptions { From = new Point(1, 1), To = new Point(double.NaN, 1) }));
        Assert.Equal("INVALID_ARGUMENT", path.Code);
        Assert.Equal("swipe to requires a point { x, y } of finite numbers", path.Message);

        var direction = await Assert.ThrowsAsync<EngineException>(() => screen.SwipeAsync(new SwipeOptions { Direction = ScrollDirection.Up }));
        Assert.Equal(EngineErrorCodes.UnsupportedCapability, direction.Code);
        Assert.Equal(
            ["swipeTo @ 10,20 -> 10,300", "swipeTo @ 10,20 -> 10,300 for 400ms", "swipeTo @ 540,1560 -> 540,840 for 300ms"],
            performedAt);
    }
}
