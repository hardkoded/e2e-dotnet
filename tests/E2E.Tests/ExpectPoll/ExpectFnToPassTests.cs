// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.ExpectPoll;

/// <summary>
/// Ported from the <c>expect(fn).toPass()</c> describe of upstream's <c>expect-poll.test.ts</c>. Not ported, because C# types the options: "refuses a
/// non-plain toPass options object", "names intervals when toPass receives interval", "quotes a string in the interval list". "stops a nested poll at
/// the block deadline outside a test" and "cancels a negated check when the attempt aborts during a try" are partial: the port has no ambient signal,
/// so the block hands its token to the nested poll, and the caller's token stands for the attempt.
/// </summary>
public sealed class ExpectFnToPassTests
{
    private readonly SteppingTime _time = new();

    [Fact]
    public async Task Retries_failures_with_other_error_codes_and_thrown_values()
    {
        var tries = 0;
        await _time.RunAsync(
            Expect
                .That(() =>
                {
                    tries++;
                    if (tries == 1)
                    {
                        throw new TestException("INVALID_ARGUMENT", "not ready");
                    }

                    if (tries == 2)
                    {
                        throw new InvalidOperationException("still waiting");
                    }

                    if (tries == 3)
                    {
                        throw new TimeoutException("the block's own timeout is a failed try, not the deadline");
                    }
                })
                .ToPassAsync(Options(1000, 10))
        );
        Assert.Equal(4, tries);
    }

    [Fact]
    public async Task Reports_the_custom_message_and_last_thrown_error_at_the_deadline()
    {
        var tries = 0;
        var last = new InvalidOperationException("still waiting");
        var error = await FailsWith(
            Expect
                .That(
                    () =>
                    {
                        if (++tries == 1)
                        {
                            throw new InvalidOperationException("first try");
                        }

                        throw last;
                    },
                    "waiting for the job"
                )
                .ToPassAsync(Options(100, 10))
        );
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Equal(
            "expect(...).toPass() timed out after 100 ms\nwaiting for the job\nlast: still waiting",
            error.Message
        );
        Assert.Same(last, error.InnerException);
    }

    [Fact]
    public async Task Backs_off_by_100_250_500_and_1000_ms_then_repeats_1000_ms()
    {
        var tries = new List<int>();
        await _time.RunAsync(
            Expect
                .That(async () =>
                {
                    await Task.CompletedTask;
                    tries.Add((int)_time.Elapsed.TotalMilliseconds);
                    Expect.That(tries.Count).ToBe(6);
                })
                .ToPassAsync(new PassOptions { Timeout = Ms(5000), Clock = _time })
        );
        Assert.Equal([0, 100, 350, 850, 1850, 2850], tries);
    }

    [Fact]
    public async Task Times_out_when_a_synchronous_block_passes_after_the_deadline()
    {
        var error = await FailsWith(
            Expect.That(() => _time.Advance(Ms(60))).ToPassAsync(new PassOptions { Timeout = Ms(10), Clock = _time })
        );
        Assert.EndsWith("timed out after 10 ms\nlast: no try completed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Requires_a_positive_timeout_outside_a_test()
    {
        var error = await Assert.ThrowsAsync<ConfigurationException>(() => Expect.That(() => { }).ToPassAsync());
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Contains(
            "expect(...).toPass() timeout 0 waits for the test deadline",
            error.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Reports_the_attempt_deadline_when_there_is_no_timeout_on_the_block()
    {
        using var live = new LiveAttempt(Ms(20), Ms(150));
        var error = await Assert.ThrowsAsync<TestException>(() =>
            Expect.That(() => Expect.That(false).ToBe(true)).ToPassAsync()
        );
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Matches(
            @"^expect\(\.\.\.\)\.toPass\(\) stopped at the attempt deadline after 1\d\d ms\n",
            error.Message
        );
        Assert.Matches(@"last: expected false to be true\z", error.Message);
    }

    [Fact]
    public async Task Waits_at_the_phase_deadline_for_the_runner_to_cancel()
    {
        using var live = new LiveAttempt(Ms(20), Ms(150));
        _ = Task.Delay(Ms(165)).ContinueWith(_ => live.Cancel(), TaskScheduler.Default);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Expect.That(() => throw new InvalidOperationException("not ready")).ToPassAsync()
        );
    }

    [Fact]
    public async Task Keeps_the_toPass_timeout_when_the_phase_deadline_moves_during_a_pause()
    {
        using var live = new LiveAttempt(Ms(5000), Ms(80));
        var started = DateTimeOffset.UtcNow;
        _ = Task.Delay(Ms(40)).ContinueWith(_ => live.ShiftDeadline(Ms(1000)), TaskScheduler.Default);
        var error = await Assert.ThrowsAsync<TestException>(() =>
            Expect
                .That(() => Expect.That(false).ToBe(true))
                .ToPassAsync(new PassOptions { Timeout = Ms(200), Intervals = [Ms(500)] })
        );
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Matches(@"^expect\(\.\.\.\)\.toPass\(\) timed out after 200 ms\n", error.Message);
        Assert.InRange(DateTimeOffset.UtcNow - started, Ms(195), Ms(400));
    }

    [Fact]
    public async Task Refuses_call_timeouts_longer_than_a_timer_can_wait()
    {
        var timeout = TimeSpan.FromMilliseconds(Math.Pow(2, 31));
        var poll = Assert.Throws<ConfigurationException>(() =>
            Expect.Poll(() => true, new PollOptions { Timeout = timeout })
        );
        Assert.Equal("INVALID_CONFIG", poll.Code);
        Assert.Equal("expect.poll timeout must be at most 2147483647 milliseconds, got 2147483648", poll.Message);

        var error = await Assert.ThrowsAsync<ConfigurationException>(() =>
            Expect.That(() => { }).ToPassAsync(new PassOptions { Timeout = timeout })
        );
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Equal(
            "expect(...).toPass() timeout must be at most 2147483647 milliseconds, got 2147483648",
            error.Message
        );
    }

    [Fact]
    public async Task Refuses_invalid_options_before_calling_the_block()
    {
        var tries = 0;
        var assertion = Expect.That(() => tries++);
        var negative = await Assert.ThrowsAsync<ConfigurationException>(() =>
            assertion.ToPassAsync(new PassOptions { Timeout = Ms(-1) })
        );
        Assert.Equal("INVALID_CONFIG", negative.Code);
        var empty = await Assert.ThrowsAsync<ConfigurationException>(() =>
            assertion.ToPassAsync(new PassOptions { Timeout = Ms(100), Intervals = [] })
        );
        Assert.Equal("INVALID_CONFIG", empty.Code);
        Assert.Equal(0, tries);
    }

    [Fact]
    public async Task Refuses_a_fractional_pause_in_toPass_intervals()
    {
        var error = await Assert.ThrowsAsync<ConfigurationException>(() =>
            Expect
                .That(() => { })
                .ToPassAsync(new PassOptions { Timeout = Ms(10), Intervals = [TimeSpan.FromMilliseconds(0.5)] })
        );
        Assert.Equal("INVALID_CONFIG", error.Code);
        Assert.Equal(
            "expect(...).toPass() intervals[0] must be a positive safe integer of milliseconds, got 0.5",
            error.Message
        );
    }

    [Fact]
    public async Task Refuses_to_run_a_value_that_is_not_a_function()
    {
        var error = await Assert.ThrowsAsync<TestException>(() =>
            Expect.That("a value").ToPassAsync(new PassOptions { Timeout = Ms(100) })
        );
        Assert.Equal("INVALID_ARGUMENT", error.Code);
        Assert.Equal("expect(...).toPass() requires a function", error.Message);
    }

    [Fact]
    public async Task Passes_a_negated_check_on_the_first_throw()
    {
        var tries = 0;
        await _time.RunAsync(
            Expect
                .That(() =>
                {
                    if (++tries == 3)
                    {
                        throw new InvalidOperationException("stopped");
                    }
                })
                .Not.ToPassAsync(Options(100, 10))
        );
        Assert.Equal(3, tries);
    }

    [Fact]
    public async Task Fails_a_negated_check_at_the_deadline_when_the_block_keeps_passing()
    {
        var error = await FailsWith(Expect.That(() => { }).Not.ToPassAsync(Options(100, 10)));
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Equal("expect(...).not.toPass() timed out after 100 ms\nlast: the block passed", error.Message);
    }

    [Theory]
    [InlineData("toPass")]
    [InlineData("not.toPass")]
    public async Task Keeps_a_late_throw_in_the_timeout_error(string matcher)
    {
        var cause = new InvalidOperationException("too late");
        var assertion = Expect.That(() =>
        {
            _time.Advance(Ms(60));
            throw cause;
        });
        var options = new PassOptions { Timeout = Ms(10), Clock = _time };
        var error = await FailsWith(
            matcher == "toPass" ? assertion.ToPassAsync(options) : assertion.Not.ToPassAsync(options)
        );
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Equal($"expect(...).{matcher}() timed out after 10 ms\nlast: too late", error.Message);
        Assert.Same(cause, error.InnerException);
    }

    [Fact]
    public async Task Stops_waiting_for_a_hung_try_at_the_deadline()
    {
        var error = await FailsWith(
            Expect
                .That((Func<Task>)(() => new TaskCompletionSource().Task))
                .ToPassAsync(new PassOptions { Timeout = Ms(100), Clock = _time })
        );
        Assert.EndsWith("timed out after 100 ms\nlast: no try completed", error.Message, StringComparison.Ordinal);
        Assert.Equal(Ms(100), _time.Elapsed);
    }

    // Partial: the port has no ambient signal, so the block gives its token to the nested poll.
    [Fact]
    public async Task Partial_Stops_a_nested_poll_at_the_block_deadline_outside_a_test()
    {
        var reads = 0;
        await FailsWith(
            Expect
                .That(async token =>
                {
                    await Expect
                        .Poll(
                            () => ++reads > 1000,
                            new PollOptions
                            {
                                Timeout = Ms(600),
                                Intervals = [Ms(50)],
                                Clock = _time,
                            }
                        )
                        .ToBeAsync(true, token);
                })
                .ToPassAsync(new PassOptions { Timeout = Ms(120), Clock = _time })
        );

        var atCutoff = reads;
        Assert.True(atCutoff > 0);
        await Task.Delay(50);
        Assert.Equal(atCutoff, reads);
    }

    // Partial: the caller's token stands for the attempt, which the port does not publish.
    [Fact]
    public async Task Partial_Cancels_a_negated_check_when_the_attempt_aborts_during_a_try()
    {
        using var cancel = new CancellationTokenSource();
        var result = Expect
            .That((Func<Task>)(() => new TaskCompletionSource().Task))
            .Not.ToPassAsync(new PassOptions { Clock = _time }, cancel.Token);
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => result);
    }

    private PassOptions Options(int timeout, int interval) =>
        new()
        {
            Timeout = Ms(timeout),
            Intervals = [Ms(interval)],
            Clock = _time,
        };

    private async Task<E2EException> FailsWith(Task pending) =>
        await Assert.ThrowsAsync<TestException>(() => _time.RunAsync(pending));

    private static TimeSpan Ms(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);
}
