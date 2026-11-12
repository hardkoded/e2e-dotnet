// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Time.Testing;

namespace E2E.Tests.TraceReplay;

/// <summary>
/// Upstream's <c>vi.useFakeTimers()</c> with <c>vi.setTimerTickMode('nextTimerAsync')</c> (<c>trace-replay.test.ts</c>): a clock whose
/// every sleep ends at once, with the time it asked for added to the time it reads, so a replay that waits half a minute takes no
/// time and reads the same instants every run.
/// </summary>
internal sealed class AutoAdvanceClock : FakeTimeProvider
{
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = base.CreateTimer(callback, state, dueTime, period);
        if (dueTime > TimeSpan.Zero && dueTime != Timeout.InfiniteTimeSpan)
        {
            Advance(dueTime);
        }

        return timer;
    }
}
