// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.ExpectPoll;

public sealed class ExpectPollTests
{
    [Fact]
    public async Task Passes_once_the_value_settles_and_stops_reading()
    {
        var reads = 0;
        await Expect.Poll(() => ++reads, new PollOptions { Interval = TimeSpan.FromMilliseconds(5) }).ToBeAsync(3);
        Assert.Equal(3, reads);
    }

    [Fact]
    public async Task Accepts_an_asynchronous_read()
    {
        var reads = 0;
        await Expect.Poll(async () =>
        {
            await Task.Yield();
            return ++reads;
        }, PollSoftTests.Fast).ToSatisfyAsync(value => value > 2, "above 2");
        Assert.Equal(3, reads);
    }

    [Fact]
    public async Task Not_flips_the_check()
    {
        var reads = 0;
        await Expect.Poll(() => ++reads > 2 ? "done" : "pending", PollSoftTests.Fast).Not.ToBeAsync("pending");
        Assert.Equal(3, reads);

        var error = await Assert.ThrowsAsync<TestException>(() => Expect.Poll(() => "pending", PollSoftTests.Short).Not.ToBeAsync("pending"));
        Assert.StartsWith("expect.poll(...).not.toBe(...) timed out", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bounds_a_hung_read_by_the_deadline()
    {
        var error = await Assert.ThrowsAsync<TestException>(() =>
            Expect.Poll(async token =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), CancellationToken.None);
                return 1;
            }, new PollOptions { Timeout = TimeSpan.FromMilliseconds(100) }).ToBeAsync(1));
        Assert.EndsWith("last: no read completed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_timeout_or_interval_that_could_never_expire_before_the_first_read()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Expect.Poll(() => 1, new PollOptions { Interval = TimeSpan.Zero }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Expect.Poll(() => 1, new PollOptions { Timeout = TimeSpan.FromSeconds(-1) }));
    }
}
