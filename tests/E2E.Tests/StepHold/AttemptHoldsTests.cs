// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.StepHold;

/// <summary>
/// <c>describe('AttemptHolds')</c> of upstream's <c>tests/unit/step-hold.test.ts</c> (tester-army/e2e#1972). <c>Partial_</c> tests: upstream races a
/// promise against the timeout (<c>race</c>), where the port enforces the timeout with the test's cancellation source, so they check that source.
/// </summary>
public sealed class AttemptHoldsTests
{
    private static readonly CancellationToken Signal = new CancellationTokenSource().Token;

    private static (AttemptTimeout Timeout, CancellationTokenSource Test, List<Deadline> Moved) Create(TimeSpan timeout)
    {
        var test = new CancellationTokenSource();
        var moved = new List<Deadline>();
        return (
            new AttemptTimeout(test, timeout, TimeProvider.System, (_, deadline) => moved.Add(deadline)),
            test,
            moved
        );
    }

    [Fact]
    public async Task Partial_Times_the_body_out_at_a_hold_that_comes_past_the_deadline_without_asking()
    {
        var asked = 0;
        var (timeout, test, _) = Create(TimeSpan.FromMilliseconds(20));
        var holds = new AttemptHolds(
            new AttemptHoldsOptions
            {
                Timeout = timeout,
                Signal = Signal,
                Ask = (_, _) =>
                {
                    Interlocked.Increment(ref asked);
                    return new TaskCompletionSource().Task;
                },
                OnResume = () => { },
            }
        );

        // Synchronous work blocks the thread past the deadline.
        Thread.Sleep(40);
        await holds.HoldAsync(
            new StepHoldRequest
            {
                Kind = StepHoldKind.Step,
                Number = 1,
                Api = "app.open",
                Label = "/",
            },
            Signal
        );

        Assert.True(test.IsCancellationRequested);
        Assert.Equal(0, asked);
    }

    [Fact]
    public async Task Partial_Stops_a_clock_test_setTimeout_moved_while_screen_pause_waits_and_runs_it_again_from_what_was_left()
    {
        var (timeout, test, moved) = Create(TimeSpan.FromSeconds(10));

        // The pause waits twice as long as the lowered timeout.
        var holds = new AttemptHolds(
            new AttemptHoldsOptions
            {
                Timeout = timeout,
                Signal = Signal,
                Ask = (_, _) => Task.Delay(300),
                OnResume = () => { },
            }
        );
        Assert.True(timeout.Set(TimeSpan.FromMilliseconds(150)));
        await holds.HoldAsync(
            new StepHoldRequest
            {
                Kind = StepHoldKind.Pause,
                Number = 0,
                Api = "screen.pause",
                Label = "",
            },
            Signal
        );
        var leftAfterPause = moved[^1].Remaining(DateTimeOffset.UtcNow);

        // The clock ran again on what was left of the 150 ms when the pause began, and told the session.
        Assert.False(test.IsCancellationRequested);
        Assert.True(leftAfterPause > TimeSpan.Zero);
        Assert.True(leftAfterPause <= TimeSpan.FromMilliseconds(150));
    }

    [Fact]
    public async Task Moves_a_held_clock_by_what_test_setTimeout_changed_and_the_release_announces_it()
    {
        var release = new TaskCompletionSource();
        var (timeout, _, moved) = Create(TimeSpan.FromSeconds(10));
        var holds = new AttemptHolds(
            new AttemptHoldsOptions
            {
                Timeout = timeout,
                Signal = Signal,
                Ask = (_, _) => release.Task,
                OnResume = () => { },
            }
        );
        var held = holds.HoldAsync(
            new StepHoldRequest
            {
                Kind = StepHoldKind.Step,
                Number = 1,
                Api = "app.open",
                Label = "/",
            },
            Signal
        );

        // Held: the clock is stopped, so the change moves what the release hands back and announces nothing yet.
        Assert.True(timeout.Set(TimeSpan.FromSeconds(60)));
        Assert.Empty(moved);
        release.SetResult();
        await held;
        Assert.Single(moved);
        Assert.True(moved[0].Remaining(DateTimeOffset.UtcNow) > TimeSpan.FromSeconds(59));

        // Running: the phase's deadline moves at once, and the move is announced.
        Assert.True(timeout.Set(TimeSpan.FromSeconds(30)));
        Assert.Equal(2, moved.Count);
        Assert.True(moved[1].Remaining(DateTimeOffset.UtcNow) < TimeSpan.FromMilliseconds(30_001));
    }
}
