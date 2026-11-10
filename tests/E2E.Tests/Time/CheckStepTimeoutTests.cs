// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using Microsoft.Extensions.Time.Testing;
using InternalTime = E2E.Internal.Time;

namespace E2E.Tests.Time;

/// <summary>
/// Upstream's <c>describe('checkStepTimeout')</c> (<c>tests/unit/time.test.ts</c>). A <see cref="TimeSpan"/> is always a finite number of
/// milliseconds, so NaN, infinite, and non-number timeouts cannot be written, and "refuses negative, NaN, infinite, and non-number timeouts"
/// is partial. "Gives withTimeout no timer for the infinite budget of 0" is partial too: see its doc comment.
/// </summary>
public sealed class CheckStepTimeoutTests
{
    [Fact]
    public void Takes_no_timeout_0_and_finite_positive_milliseconds()
    {
        foreach (var timeout in new TimeSpan?[] { null, TimeSpan.Zero, TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(2.5), TimeSpan.FromMilliseconds(60_000) })
        {
            InternalTime.CheckStepTimeout(timeout, "tap");
        }
    }

    [Fact]
    public void Partial_Refuses_negative_NaN_infinite_and_non_number_timeouts_as_INVALID_ARGUMENT()
    {
        foreach (var timeout in new[] { TimeSpan.FromMilliseconds(-1), Timeout.InfiniteTimeSpan })
        {
            var error = Assert.Throws<TestException>(() => InternalTime.CheckStepTimeout(timeout, "tap"));
            Assert.Equal("INVALID_ARGUMENT", error.Code);
            Assert.Contains("tap timeout", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Partial_Turns_0_into_a_deadline_only_the_phase_caps()
    {
        var clock = new FakeTimeProvider();
        var phase = new Deadline(TimeSpan.FromMilliseconds(1000), clock);
        var fallback = TimeSpan.FromMilliseconds(30);

        Assert.Same(phase, InternalTime.StepDeadline(TimeSpan.Zero, "tap", fallback, phase, clock));
        Assert.Equal(clock.GetUtcNow().AddMilliseconds(250), InternalTime.StepDeadline(TimeSpan.FromMilliseconds(250), "tap", fallback, phase, clock).EndsAt);
        Assert.Equal(clock.GetUtcNow().AddMilliseconds(30), InternalTime.StepDeadline(null, "tap", fallback, phase, clock).EndsAt);
        Assert.Equal(TimeSpan.FromMilliseconds(1000), InternalTime.StepBudget(TimeSpan.Zero, "tap", fallback, phase, clock));
        Assert.Equal(InternalTime.Never, InternalTime.StepDeadline(TimeSpan.Zero, "tap", fallback, null, clock).EndsAt);
    }

    /// <summary>
    /// Partial: upstream's <c>stepTimeout(0)</c> is an infinite budget, which <c>withTimeout</c> runs with no timer. The port's
    /// <c>WithTimeout</c> takes <see cref="Timeout.InfiniteTimeSpan"/> for that budget, and a test cannot count its timers, so this
    /// checks only that the infinite budget waits for the work and returns its value.
    /// </summary>
    [Fact]
    public async Task Partial_Gives_withTimeout_no_timer_for_the_infinite_budget_of_0()
    {
        var work = new TaskCompletionSource<string>();
        var pending = InternalTime.WithTimeout(work.Task, Timeout.InfiniteTimeSpan, () => new InvalidOperationException("timed out"));
        work.SetResult("done");

        Assert.Equal("done", await pending);
    }
}
