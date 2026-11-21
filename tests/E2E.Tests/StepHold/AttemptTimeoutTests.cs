// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.StepHold;

/// <summary>
/// <c>describe('AttemptTimeout')</c> of upstream's <c>tests/unit/step-hold.test.ts</c> (tester-army/e2e#1921). Each <c>it</c> is <c>Partial_</c>: upstream
/// races a promise against the timeout (<c>race</c>), where the port enforces the timeout with the test's cancellation source, so these check that
/// source and the deadline the session's steps are capped with. The <c>describe('AttemptHolds')</c> <c>it</c>s of that file need
/// <c>e2e run --step</c>, which the port does not have (hardkoded/e2e-dotnet#2002).
/// </summary>
public sealed class AttemptTimeoutTests
{
    private static (AttemptTimeout Timeout, CancellationTokenSource Test, List<Deadline> Moved) Create(TimeSpan timeout)
    {
        var test = new CancellationTokenSource();
        var moved = new List<Deadline>();
        return (new AttemptTimeout(test, timeout, TimeProvider.System, moved.Add), test, moved);
    }

    [Fact]
    public async Task Partial_Times_the_race_out_at_once_when_set_below_the_time_already_spent()
    {
        var (timeout, test, _) = Create(TimeSpan.FromSeconds(10));
        await Task.Delay(20);
        var set = DateTimeOffset.UtcNow;
        Assert.True(timeout.Set(TimeSpan.FromMilliseconds(10)));

        // Not at the 10 s the race started with.
        Assert.True(test.IsCancellationRequested);
        Assert.True(DateTimeOffset.UtcNow - set < TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.FromMilliseconds(10), timeout.Timeout);
    }

    [Fact]
    public async Task Partial_Lets_a_raised_race_run_past_the_timeout_it_started_with()
    {
        var (timeout, test, _) = Create(TimeSpan.FromMilliseconds(30));
        Assert.True(timeout.Set(TimeSpan.FromSeconds(5)));
        await Task.Delay(80);
        Assert.False(test.IsCancellationRequested);
    }

    [Fact]
    public async Task Partial_Refuses_a_change_once_the_race_settled_also_from_work_a_timeout_left_running_and_tells_the_watchdog_nothing()
    {
        var (timeout, test, moved) = Create(TimeSpan.FromMilliseconds(10));
        test.CancelAfter(TimeSpan.FromMilliseconds(10));
        await Task.Delay(200);
        Assert.True(test.IsCancellationRequested);
        Assert.False(timeout.Set(TimeSpan.FromSeconds(60)));
        Assert.Empty(moved);
        Assert.Equal(TimeSpan.FromMilliseconds(10), timeout.Timeout);
    }
}
