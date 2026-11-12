// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using InternalTime = E2E.Internal.Time;

namespace E2E.Tests.Time;

/// <summary>
/// Upstream's <c>describe('withTimeout')</c> (<c>tests/unit/time.test.ts</c>). Upstream runs on fake timers and reads the timer count; the port's
/// <c>WithTimeout</c> runs on real timers, so the timer count is not observable and the first case is partial.
/// </summary>
public sealed class WithTimeoutTests
{
    /// <summary>
    /// Partial: upstream also checks that the timer is cleared, which the port cannot count. This checks the resolved value.
    /// </summary>
    [Fact]
    public async Task Partial_Returns_the_resolved_value_and_clears_the_timer()
    {
        var value = await InternalTime.WithTimeout(Task.FromResult(7), TimeSpan.FromSeconds(1), () => new InvalidOperationException("unused"));

        Assert.Equal(7, value);
    }

    [Fact]
    public async Task Throws_the_built_error_when_the_promise_does_not_settle_in_time()
    {
        var never = new TaskCompletionSource<int>();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InternalTime.WithTimeout(never.Task, TimeSpan.FromMilliseconds(250), () => new InvalidOperationException("too slow")));
        Assert.Equal("too slow", error.Message);
    }
}
