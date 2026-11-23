// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.ExpectPoll;

/// <summary>
/// Ported from the <c>inside an attempt</c> describe of upstream's <c>expect-poll.test.ts</c>. A cancelled phase throws
/// <see cref="OperationCanceledException"/> in place of an error with the code CANCELLED. Not ported: "refuses a second live attempt in the same
/// process", because the port names the attempt through the async flow of the test, so attempts side by side are allowed.
/// </summary>
public sealed class InsideAnAttemptTests
{
    [Fact]
    public async Task Stops_reading_the_moment_the_attempt_is_cancelled()
    {
        using var cancel = new CancellationTokenSource();
        var reads = 0;
        var options = new PollOptions
        {
            Timeout = TimeSpan.FromSeconds(15),
            Intervals = [TimeSpan.FromMilliseconds(5)],
        };
        var poll = Expect
            .Poll(
                () =>
                {
                    reads++;
                    return "running";
                },
                options
            )
            .ToBeAsync("done", cancel.Token);

        cancel.CancelAfter(TimeSpan.FromMilliseconds(30));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => poll);

        await Task.Delay(20);
        var readsAtCancel = reads;
        await Task.Delay(50);
        Assert.Equal(readsAtCancel, reads);
    }

    [Fact]
    public async Task Defaults_the_timeout_to_the_attempt_assertionTimeout_until_the_attempt_ends()
    {
        var live = new LiveAttempt(TimeSpan.FromMilliseconds(80), TimeSpan.FromSeconds(10));
        try
        {
            var error = await Assert.ThrowsAsync<TestException>(() =>
                Expect.Poll(() => "running", new PollOptions { Intervals = [TimeSpan.FromMilliseconds(10)] }).ToBeAsync("done")
            );
            Assert.Contains("timed out after 80 ms", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            live.Dispose();
        }

        Assert.Null(Attempts.CurrentAttempt());
    }

    [Fact]
    public async Task Caps_the_poll_at_the_attempt_deadline()
    {
        using var live = new LiveAttempt(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(80));
        var started = DateTimeOffset.UtcNow;
        var error = await Assert.ThrowsAsync<TestException>(() =>
            Expect
                .Poll(
                    () => "running",
                    new PollOptions { Timeout = TimeSpan.FromSeconds(15), Intervals = [TimeSpan.FromMilliseconds(10)] }
                )
                .ToBeAsync("done")
        );
        Assert.Matches(@"stopped at the attempt deadline after \d+ ms", error.Message);
        Assert.Contains("last: received \"running\", expected \"done\"", error.Message, StringComparison.Ordinal);
        Assert.True(DateTimeOffset.UtcNow - started < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Polls_with_timeout_0_past_the_assertion_timeout_until_the_attempt_deadline()
    {
        using var live = new LiveAttempt(TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(150));
        var started = DateTimeOffset.UtcNow;
        var error = await Assert.ThrowsAsync<TestException>(() =>
            Expect
                .Poll(
                    () => "running",
                    new PollOptions { Timeout = TimeSpan.Zero, Intervals = [TimeSpan.FromMilliseconds(10)] }
                )
                .ToBeAsync("done")
        );
        Assert.Matches(@"stopped at the attempt deadline after \d+ ms", error.Message);
        Assert.True(DateTimeOffset.UtcNow - started >= TimeSpan.FromMilliseconds(140));
    }

    [Fact]
    public async Task Partial_Polls_with_timeout_0_past_the_assertion_timeout_until_the_attempt_deadline()
    {
        using var cancel = new CancellationTokenSource();
        var options = new PollOptions { Timeout = TimeSpan.Zero, Intervals = [TimeSpan.FromMilliseconds(10)] };
        cancel.CancelAfter(TimeSpan.FromMilliseconds(150));
        var started = DateTimeOffset.UtcNow;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Expect.Poll(() => "running", options).ToBeAsync("done", cancel.Token)
        );

        Assert.True(DateTimeOffset.UtcNow - started >= TimeSpan.FromMilliseconds(140));
    }

    [Fact]
    public async Task Cuts_a_read_in_flight_when_the_attempt_is_cancelled()
    {
        using var cancel = new CancellationTokenSource();
        var options = new PollOptions { Timeout = TimeSpan.FromSeconds(15) };
        var poll = Expect.Poll(_ => new TaskCompletionSource<string>().Task, options).ToBeAsync("done", cancel.Token);

        cancel.CancelAfter(TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => poll);
    }
}
