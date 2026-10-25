// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using E2E.Engine;

namespace E2E.Tests.ScrollUntilVisible;

/// <summary>
/// Upstream's <c>describe('screen.scrollUntilVisible')</c> (<c>tests/unit/scroll-until-visible.test.ts</c>): polls the target and
/// swipes the viewport between polls, slowly in the requested direction, until the target reads as visible or the deadline
/// passes. Every swipe is bounded by that deadline.
/// <para>
/// Not ported: <c>refuses an option it does not take before any swipe</c>, because the options are a typed class. Partial, with
/// the reason in each name's comment: the port has no observation-root node to address a viewport swipe to (the swipe goes to the
/// engine's <c>SwipeAsync</c>), no <c>translateLocatorError</c> (an engine error reaches the test with the engine's own code, so a
/// locator swipe's timeout is not wrapped in <c>ACTION_FAILED</c>), and a typed <c>Locator</c> parameter that cannot be anything but a locator.
/// </para>
/// </summary>
public sealed class ScreenScrollUntilVisibleTests
{
    private static readonly SemanticNode Target = ScrollScreen.Target;

    private static ScrollUntilVisibleOptions Options(TimeSpan timeout) => new() { Timeout = timeout };

    [Fact]
    public async Task On_a_locator_swipes_that_node_instead_of_the_viewport_so_a_scroll_container_pages()
    {
        var scroll = new ScrollScreen(new ScrollScript { Screens = [[ScrollScreen.Feed()], [ScrollScreen.Feed()], [ScrollScreen.Feed(Target)]] });
        var screen = scroll.Screen;
        await screen.GetByRole("list", "Feed").ScrollUntilVisibleAsync(screen.GetByText("Accept"));
        Assert.Equal(
            [new Swipe("feed", ScrollDirection.Down, Momentum.Slow), new Swipe("feed", ScrollDirection.Down, Momentum.Slow)],
            scroll.Swipes);
    }

    /// <summary>Partial: the viewport swipe is addressed to the engine's <c>SwipeAsync</c>, not to an observation-root node.</summary>
    [Fact]
    public async Task Partial_Takes_a_direction_and_a_momentum_for_the_stride_of_each_step_on_the_screen_and_on_a_locator()
    {
        var viewport = new ScrollScreen(new ScrollScript { Screens = [[], [Target]] });
        await viewport.Screen.ScrollUntilVisibleAsync(viewport.Screen.GetByText("Accept"), new ScrollUntilVisibleOptions { Direction = ScrollDirection.Up, Momentum = Momentum.None });
        Assert.Equal([new Swipe("viewport", ScrollDirection.Up, Momentum.None)], viewport.Swipes);

        var scroll = new ScrollScreen(new ScrollScript { Screens = [[ScrollScreen.Feed()], [ScrollScreen.Feed(Target)]] });
        var screen = scroll.Screen;
        await screen.GetByRole("list", "Feed").ScrollUntilVisibleAsync(screen.GetByText("Accept"), new ScrollUntilVisibleOptions { Direction = ScrollDirection.Right, Momentum = Momentum.None });
        Assert.Equal([new Swipe("feed", ScrollDirection.Right, Momentum.None)], scroll.Swipes);
    }

    [Fact]
    public async Task Returns_at_once_when_the_target_is_already_visible_with_no_swipe()
    {
        var scroll = new ScrollScreen(new ScrollScript { Screens = [[Target]] });
        await scroll.Screen.ScrollUntilVisibleAsync(scroll.Screen.GetByText("Accept"));
        Assert.Empty(scroll.Swipes);
    }

    /// <summary>Partial: the swipe is addressed to the engine's viewport swipe, not to an observation-root node.</summary>
    [Fact]
    public async Task Partial_Swipes_down_slowly_at_the_viewport_until_the_target_appears_then_stops()
    {
        var scroll = new ScrollScreen(new ScrollScript { Screens = [[], [], [Target]] });
        await scroll.Screen.ScrollUntilVisibleAsync(scroll.Screen.GetByText("Accept"));
        Assert.Equal(
            [new Swipe("viewport", ScrollDirection.Down, Momentum.Slow), new Swipe("viewport", ScrollDirection.Down, Momentum.Slow)],
            scroll.Swipes);
    }

    [Fact]
    public async Task Keeps_scrolling_past_a_target_the_engine_reports_hidden()
    {
        var hidden = ScrollScreen.HiddenTarget;
        var scroll = new ScrollScreen(new ScrollScript { Screens = [[hidden], [hidden], [Target]] });
        await scroll.Screen.ScrollUntilVisibleAsync(scroll.Screen.GetByText("Accept"));
        Assert.Equal(2, scroll.Swipes.Count);
    }

    [Fact]
    public async Task Fails_as_LOCATOR_NOT_FOUND_naming_the_locator_once_the_deadline_passes_and_records_the_failed_step()
    {
        var scroll = new ScrollScreen(new ScrollScript { Screens = [[]] });
        var failure = await Assert.ThrowsAsync<TestException>(() => scroll.Screen.ScrollUntilVisibleAsync(scroll.Screen.GetByText("Accept"), Options(TimeSpan.FromMilliseconds(250))));
        Assert.Equal("LOCATOR_NOT_FOUND", failure.Code);
        Assert.Equal("target did not become visible while scrolling: getByText(\"Accept\")", failure.Message);
        Assert.Equal("getByText(\"Accept\")", failure.Details?.Locator);
        Assert.Equal("Accept", failure.Details?.Name);
        Assert.NotNull(failure.Details?.WaitedMs);
        Assert.True(scroll.Swipes.Count >= 1);
        var step = Assert.Single(scroll.Steps.All());
        Assert.Equal(StepKind.Screen, step.Kind);
        Assert.Equal("screen.scrollUntilVisible", step.Api);
        Assert.Equal("getByText(\"Accept\")", step.Label);
        Assert.Equal(StepStatus.Failed, step.Status);
        Assert.Equal("LOCATOR_NOT_FOUND", step.Error?.Code);
    }

    [Fact]
    public async Task Bounds_a_viewport_swipe_by_its_own_deadline_a_hung_swipe_is_LOCATOR_NOT_FOUND_within_the_scroll_timeout_not_the_action_timeout()
    {
        var scroll = new ScrollScreen(new ScrollScript { Screens = [[]], Hang = true, ActionTimeout = TimeSpan.FromSeconds(5) });
        var started = Stopwatch.StartNew();
        var failure = await Assert.ThrowsAsync<TestException>(() => scroll.Screen.ScrollUntilVisibleAsync(scroll.Screen.GetByText("Accept"), Options(TimeSpan.FromMilliseconds(250))));
        Assert.Equal("LOCATOR_NOT_FOUND", failure.Code);
        Assert.Equal("target did not become visible while scrolling: getByText(\"Accept\")", failure.Message);
        Assert.Equal(EngineErrorCodes.OperationTimeout, Assert.IsType<EngineException>(failure.InnerException).Code);
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(2));
        var step = Assert.Single(scroll.Steps.All());
        Assert.Equal("screen.scrollUntilVisible", step.Api);
        Assert.Equal(StepStatus.Failed, step.Status);
    }

    /// <summary>Partial: the cause is the engine's timeout itself; the port wraps no engine error in <c>ACTION_FAILED</c>.</summary>
    [Fact]
    public async Task Partial_Bounds_a_locator_swipe_by_the_same_deadline()
    {
        var scroll = new ScrollScreen(new ScrollScript { Screens = [[ScrollScreen.Feed()]], Hang = true, ActionTimeout = TimeSpan.FromSeconds(5) });
        var screen = scroll.Screen;
        var started = Stopwatch.StartNew();
        var failure = await Assert.ThrowsAsync<TestException>(() =>
            screen.GetByRole("list", "Feed").ScrollUntilVisibleAsync(screen.GetByText("Accept"), Options(TimeSpan.FromMilliseconds(250))));
        Assert.Equal("LOCATOR_NOT_FOUND", failure.Code);
        Assert.Equal("target did not become visible while scrolling: getByText(\"Accept\")", failure.Message);
        Assert.Equal(EngineErrorCodes.OperationTimeout, Assert.IsType<EngineException>(failure.InnerException).Code);
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(2));
        var step = Assert.Single(scroll.Steps.All());
        Assert.Equal(StepStatus.Failed, step.Status);
        Assert.Equal("LOCATOR_NOT_FOUND", step.Error?.Code);
    }

    [Fact]
    public async Task Owns_the_outcome_whichever_timer_fires_first_a_swipe_timing_out_on_the_budget_the_deadline_left_it_is_LOCATOR_NOT_FOUND()
    {
        // A swipe whose budget is the deadline's remainder, ending on the engine's own clock a moment before this one reads the deadline as passed.
        var scroll = new ScrollScreen(new ScrollScript { Screens = [[]], HangFor = TimeSpan.FromMilliseconds(110), ActionTimeout = TimeSpan.FromSeconds(5) });
        var failure = await Assert.ThrowsAsync<TestException>(() => scroll.Screen.ScrollUntilVisibleAsync(scroll.Screen.GetByText("Accept"), Options(TimeSpan.FromMilliseconds(200))));
        Assert.Equal("LOCATOR_NOT_FOUND", failure.Code);
        Assert.Equal(EngineErrorCodes.OperationTimeout, Assert.IsType<EngineException>(failure.InnerException).Code);
    }

    /// <summary>Partial: the swipe is addressed to the engine's viewport swipe, not to an observation-root node.</summary>
    [Fact]
    public async Task Partial_Still_swipes_with_less_than_a_poll_interval_of_the_budget_left()
    {
        var scroll = new ScrollScreen(new ScrollScript { Screens = [[], [Target]] });
        await scroll.Screen.ScrollUntilVisibleAsync(scroll.Screen.GetByText("Accept"), Options(TimeSpan.FromMilliseconds(50)));
        Assert.Equal([new Swipe("viewport", ScrollDirection.Down, Momentum.Slow)], scroll.Swipes);
    }

    /// <summary>Partial: the port has no <c>ACTION_FAILED</c> translation, so the failure is the engine's own <c>OPERATION_TIMEOUT</c>, uncontextualised.</summary>
    [Fact]
    public async Task Partial_Keeps_an_engine_timeout_far_from_the_deadline_as_the_action_failure_it_is()
    {
        var scroll = new ScrollScreen(new ScrollScript { Screens = [[ScrollScreen.Feed()]], HangFor = TimeSpan.FromMilliseconds(20), ActionTimeout = TimeSpan.FromSeconds(5) });
        var screen = scroll.Screen;
        var failure = await Assert.ThrowsAsync<EngineException>(() =>
            screen.GetByRole("list", "Feed").ScrollUntilVisibleAsync(screen.GetByText("Accept"), Options(TimeSpan.FromSeconds(30))));
        Assert.Equal(EngineErrorCodes.OperationTimeout, failure.Code);
        var step = Assert.Single(scroll.Steps.All());
        Assert.Equal(StepStatus.Failed, step.Status);
        Assert.Equal(EngineErrorCodes.OperationTimeout, step.Error?.Code);
    }

    /// <summary>Partial: a typed <c>Locator</c> parameter cannot be anything but a locator, so this checks <c>null</c>, before any step or swipe.</summary>
    [Fact]
    public async Task Partial_Rejects_a_target_that_is_not_an_e2e_locator_before_recording_a_step()
    {
        var scroll = new ScrollScreen(new ScrollScript { Screens = [[Target]] });
        await Assert.ThrowsAsync<ArgumentNullException>(() => scroll.Screen.ScrollUntilVisibleAsync(null!));
        Assert.Empty(scroll.Steps.All());
        Assert.Empty(scroll.Swipes);
    }

    /// <summary>Partial: the port writes a scoped locator description as a chain, <c>parent.child</c>, where upstream writes <c>parent >> child</c>, so the step label differs.</summary>
    [Fact]
    public async Task Partial_On_a_locator_labels_the_step_with_the_target_whose_description_carries_the_scope_once()
    {
        var scroll = new ScrollScreen(new ScrollScript { Screens = [[ScrollScreen.Feed()], [ScrollScreen.Feed(Target)]] });
        var container = scroll.Screen.GetByRole("list", "Feed");
        await container.ScrollUntilVisibleAsync(container.GetByText("Accept"));
        var step = Assert.Single(scroll.Steps.All());
        Assert.Equal("screen.scrollUntilVisible", step.Api);
        // The port writes a scoped description as a chain, `parent.child`, where upstream writes `parent >> child`.
        Assert.Equal("getByRole(\"list\", \"Feed\").getByText(\"Accept\")", step.Label);
        Assert.Equal(StepStatus.Passed, step.Status);
    }

    [Fact]
    public async Task Needs_the_swipe_action_only_once_it_has_to_scroll_a_visible_target_passes_on_an_engine_without_one()
    {
        var visible = new ScrollScreen(new ScrollScript { Screens = [[Target]], Swipeable = false });
        await visible.Screen.ScrollUntilVisibleAsync(visible.Screen.GetByText("Accept"));
        var absent = new ScrollScreen(new ScrollScript { Screens = [[]], Swipeable = false });
        var failure = await Assert.ThrowsAsync<EngineException>(() => absent.Screen.ScrollUntilVisibleAsync(absent.Screen.GetByText("Accept")));
        Assert.Equal("UNSUPPORTED_CAPABILITY", failure.Code);
    }
}
