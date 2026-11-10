// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Extensions.Time.Testing;
using InternalTime = E2E.Internal.Time;

namespace E2E.Tests.Time;

/// <summary>
/// Upstream's <c>describe('sleep')</c> (<c>tests/unit/time.test.ts</c>). Upstream waits on Node's timers and reads the timer count and the
/// abort listeners; the port waits on a <see cref="TimeProvider"/> and cannot read either, so those cases are partial.
/// </summary>
public sealed class SleepTests
{
    [Fact]
    public async Task Resolves_after_the_given_duration()
    {
        var clock = new FakeTimeProvider();
        var sleep = InternalTime.Sleep(TimeSpan.FromMilliseconds(500), clock);

        clock.Advance(TimeSpan.FromMilliseconds(499));
        Assert.False(sleep.IsCompleted);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        await sleep;
    }

    [Fact]
    public async Task Rejects_immediately_when_the_signal_is_already_aborted()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var error = await Assert.ThrowsAsync<EngineException>(() => InternalTime.Sleep(TimeSpan.FromMilliseconds(10), new FakeTimeProvider(), cancelled.Token));
        Assert.Equal("CANCELLED", error.Code);
    }

    /// <summary>
    /// Partial: upstream also checks that the abort clears the timer, which the port cannot count. This checks the rejection only.
    /// </summary>
    [Fact]
    public async Task Partial_Rejects_mid_sleep_on_abort_and_clears_the_timer()
    {
        using var aborting = new CancellationTokenSource();
        var sleep = InternalTime.Sleep(TimeSpan.FromSeconds(10), new FakeTimeProvider(), aborting.Token);

        aborting.Cancel();

        var error = await Assert.ThrowsAsync<EngineException>(() => sleep);
        Assert.Equal("CANCELLED", error.Code);
    }

    /// <summary>
    /// Partial: upstream counts the abort listeners on the signal, which the port does not expose. This checks that a cancel after the
    /// sleep resolved leaves it resolved, which is the observable effect of removing the listener.
    /// </summary>
    [Fact]
    public async Task Partial_Removes_its_abort_listener_after_resolving()
    {
        using var aborting = new CancellationTokenSource();
        var clock = new FakeTimeProvider();
        var sleep = InternalTime.Sleep(TimeSpan.FromMilliseconds(1), clock, aborting.Token);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        await sleep;

        aborting.Cancel();
        await sleep;
    }
}
