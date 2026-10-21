// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;
using Microsoft.Extensions.Time.Testing;

namespace E2E.Tests.Time;

/// <summary>Upstream's <c>describe('Deadline')</c> (<c>tests/unit/time.test.ts</c>).</summary>
public sealed class DeadlineTests
{
    [Fact]
    public void Computes_remaining_budget_and_never_goes_negative()
    {
        var clock = new FakeTimeProvider();
        var start = clock.GetUtcNow();
        var deadline = new Deadline(TimeSpan.FromMilliseconds(1000), clock);

        Assert.Equal(start.AddMilliseconds(1000), deadline.EndsAt);
        Assert.Equal(TimeSpan.FromMilliseconds(1000), deadline.Remaining(start));
        Assert.Equal(TimeSpan.FromMilliseconds(100), deadline.Remaining(start.AddMilliseconds(900)));
        Assert.Equal(TimeSpan.Zero, deadline.Remaining(start.AddMilliseconds(2000)));
        Assert.False(deadline.Expired(start.AddMilliseconds(999)));
        Assert.True(deadline.Expired(start.AddMilliseconds(1000)));
    }
}
