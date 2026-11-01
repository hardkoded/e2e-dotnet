// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;
using InternalTime = E2E.Internal.Time;

namespace E2E.Tests.Time;

/// <summary>
/// Upstream tests <c>pollCondition</c> on fake timers. The port polls inside the
/// locator matchers, so these run a matcher over a screen whose every read is
/// scripted, on a fake clock that moves to each timer the poll starts. The port
/// samples every 50 ms, upstream every 100 ms.
/// </summary>
public sealed class PollConditionTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromMilliseconds(1000);

    private static readonly SemanticNode Shown = new() { Ref = "s", Role = "status", Name = "Saving" };

    private static readonly SemanticNode Unchecked = new() { Ref = "c", Role = "checkbox", Name = "Terms" };

    private readonly SteppingTime _time = new();

    [Fact]
    public async Task Throws_the_caller_error_at_the_deadline()
    {
        var screen = Scripted(_ => []);
        var error = await Assert.ThrowsAsync<TestException>(() => Drive(Expect.That(screen.GetByRole("status")).ToBeVisibleAsync(timeout: TimeSpan.FromMilliseconds(350))));
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.InRange(_time.Elapsed, TimeSpan.FromMilliseconds(350), TimeSpan.FromMilliseconds(1000));
    }

    [Fact]
    public async Task Takes_its_last_read_at_the_deadline_not_a_poll_tick_past_it()
    {
        // 330 ms is not a multiple of the 50 ms sample: the state arrives just after it.
        var budget = TimeSpan.FromMilliseconds(330);
        var readsAt = new List<double>();
        var screen = Over(_ =>
        {
            readsAt.Add(_time.Elapsed.TotalMilliseconds);
            return Task.FromResult<SemanticNode[]>(_time.Elapsed > budget ? [Shown] : []);
        });
        var error = await Assert.ThrowsAsync<TestException>(() => Drive(Expect.That(screen.GetByRole("status")).ToBeVisibleAsync(timeout: budget)));
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.Equal([0, 50, 100, 150, 200, 250, 300, 330], readsAt);
    }

    [Fact]
    public async Task Negated_a_budget_shorter_than_the_grace_window_is_still_satisfiable()
    {
        var screen = Scripted(_ => []);
        await Drive(Expect.That(screen.GetByRole("status")).Not.ToBeVisibleAsync(timeout: TimeSpan.FromMilliseconds(400)));
        Assert.InRange(_time.Elapsed, TimeSpan.FromMilliseconds(400), Grace);
    }

    [Fact]
    public async Task Negated_passes_only_after_the_grace_window_holds_continuously()
    {
        var screen = Scripted(_ => []);
        await Drive(Expect.That(screen.GetByRole("status")).Not.ToBeVisibleAsync(timeout: TimeSpan.FromSeconds(60)));
        Assert.InRange(_time.Elapsed, Grace, Grace * 2);
    }

    [Fact]
    public async Task Negated_an_undefined_evaluation_resets_the_grace_window()
    {
        // Unchecked for a while, no node to judge on the 8th read, then unchecked again.
        var screen = Scripted(call => call == 8 ? [] : [Unchecked]);
        await Drive(Expect.That(screen.GetByRole("checkbox")).Not.ToBeCheckedAsync(timeout: TimeSpan.FromSeconds(60)));
        Assert.InRange(_time.Elapsed, Grace + TimeSpan.FromMilliseconds(300), Grace * 3);
    }

    [Fact]
    public async Task Negated_a_true_evaluation_clears_accumulated_grace()
    {
        var screen = Scripted(call => call == 8 ? [Shown] : []);
        await Drive(Expect.That(screen.GetByRole("status")).Not.ToBeVisibleAsync(timeout: TimeSpan.FromSeconds(60)));
        Assert.InRange(_time.Elapsed, Grace + TimeSpan.FromMilliseconds(300), Grace * 3);
    }

    [Fact]
    public async Task Negated_a_slow_first_read_counts_toward_a_budget_shorter_than_the_grace_window()
    {
        var issued = new List<TimeSpan>();
        var screen = Slow(issued, first: 80, rest: 10);
        await Drive(Expect.That(screen.GetByRole("status")).Not.ToBeVisibleAsync(timeout: TimeSpan.FromMilliseconds(500)));
        Assert.Equal(TimeSpan.FromMilliseconds(500), _time.Elapsed);
        Assert.All(issued, at => Assert.True(at < TimeSpan.FromMilliseconds(500), "a read was issued at " + at));
    }

    [Fact]
    public async Task Negated_a_slow_first_read_counts_toward_the_grace_window_under_a_longer_budget()
    {
        var screen = Slow([], first: 80, rest: 10);
        await Drive(Expect.That(screen.GetByRole("status")).Not.ToBeVisibleAsync(timeout: TimeSpan.FromMilliseconds(1500)));
        Assert.InRange(_time.Elapsed, Grace, TimeSpan.FromMilliseconds(1500));
    }

    [Fact]
    public async Task Negated_fast_reads_under_a_short_budget_pass_at_the_deadline_not_before()
    {
        var screen = Slow([], first: 0, rest: 0);
        await Drive(Expect.That(screen.GetByRole("status")).Not.ToBeVisibleAsync(timeout: TimeSpan.FromMilliseconds(500)));
        Assert.Equal(TimeSpan.FromMilliseconds(500), _time.Elapsed);
    }

    /// <summary>
    /// Partial: upstream counts exactly 5 reads on its 100 ms sample. The port samples every 50 ms, so this checks
    /// that the poll fails and that its reads stop at the deadline (8 to 11), not the exact count.
    /// </summary>
    [Fact]
    public async Task Negated_a_budget_shorter_than_the_grace_window_fails_after_the_positive_state_was_seen_and_reads_once_per_50_ms_sample_up_to_the_deadline()
    {
        var calls = 0;
        var screen = Scripted(call =>
        {
            calls = call;
            return call == 3 ? [Shown] : [];
        });
        var error = await Assert.ThrowsAsync<TestException>(() => Drive(Expect.That(screen.GetByRole("status")).Not.ToBeVisibleAsync(timeout: TimeSpan.FromMilliseconds(500))));
        Assert.Equal("ASSERTION_FAILED", error.Code);

        // It decided at the deadline instead of reading past it: one read per 50 ms sample, the last at the deadline.
        Assert.InRange(calls, 8, 11);
    }

    [Fact]
    public async Task Returns_once_the_positive_condition_holds()
    {
        var calls = 0;
        var screen = Scripted(call =>
        {
            calls = call;
            return call >= 3 ? [Shown] : [];
        });
        await Drive(Expect.That(screen.GetByRole("status")).ToBeVisibleAsync(timeout: TimeSpan.FromSeconds(5)));
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task Stops_polling_when_the_signal_aborts_between_evaluations()
    {
        var screen = Scripted(_ => []);
        using var abort = new CancellationTokenSource();
        var poll = Expect.That(screen.GetByRole("status")).ToBeVisibleAsync(timeout: TimeSpan.FromSeconds(60), cancellationToken: abort.Token);
        while (!_time.HasTimer && !poll.IsCompleted)
        {
            await Task.Delay(1);
        }

        await abort.CancelAsync();
        var error = await Assert.ThrowsAsync<TestException>(() => poll);
        Assert.Equal("CANCELLED", error.Code);
    }

    [Fact]
    public async Task Ends_on_the_last_sample_when_the_deadline_cuts_a_read_off_and_fails_on_a_read_the_deadline_did_not_cut()
    {
        var cut = new EngineException("OPERATION_TIMEOUT", "locate timed out");
        var budget = TimeSpan.FromMilliseconds(350);
        var calls = 0;
        var screen = Over(_ =>
        {
            calls++;
            return _time.Elapsed < budget ? Task.FromResult<SemanticNode[]>([]) : throw cut;
        });
        var error = await Assert.ThrowsAsync<TestException>(() => Drive(Expect.That(screen.GetByRole("status")).ToBeVisibleAsync(timeout: budget)));
        Assert.Equal("ASSERTION_FAILED", error.Code);
        Assert.True(calls > 1);

        var early = Over(_ => throw cut);
        var timedOut = await Assert.ThrowsAsync<TestException>(() => Drive(Expect.That(early.GetByRole("status")).ToBeVisibleAsync(timeout: TimeSpan.FromSeconds(5))));
        Assert.Equal("ACTION_FAILED", timedOut.Code);
        Assert.Same(cut, timedOut.InnerException);
    }

    [Fact]
    public async Task Keeps_a_read_that_hung_most_of_the_wait_a_failure_and_never_lets_a_negation_pass_on_it()
    {
        var cut = new EngineException("OPERATION_TIMEOUT", "locate timed out");
        var budget = TimeSpan.FromMilliseconds(2_000);
        var calls = 0;
        var screen = Over(async token =>
        {
            calls++;
            if (calls == 1)
            {
                return [];
            }

            // The page froze: this read takes the rest of the wait and is cut at its deadline.
            var rest = budget - _time.Elapsed;
            await Task.Delay(rest > TimeSpan.Zero ? rest : TimeSpan.Zero, _time, token);
            throw cut;
        });
        var timedOut = await Assert.ThrowsAsync<TestException>(() => Drive(Expect.That(screen.GetByRole("status")).Not.ToBeVisibleAsync(timeout: budget)));
        Assert.Equal("ACTION_FAILED", timedOut.Code);
        Assert.Same(cut, timedOut.InnerException);
    }

    [Fact]
    public async Task Negated_a_read_the_deadline_cut_off_ends_the_poll_on_what_held_until_it_was_cut()
    {
        var cut = new EngineException("OPERATION_TIMEOUT", "locate timed out");
        Task Run(int cutAfterMs)
        {
            var budget = TimeSpan.FromMilliseconds(1_950);
            var from = _time.Elapsed;

            // Visible for the first cutAfterMs, then gone; the read that starts with under a 100 ms tick left is cut.
            var screen = Over(_ =>
            {
                if (budget - (_time.Elapsed - from) < TimeSpan.FromMilliseconds(100))
                {
                    throw cut;
                }

                return Task.FromResult<SemanticNode[]>((_time.Elapsed - from).TotalMilliseconds < cutAfterMs ? [Shown] : []);
            });
            return Drive(Expect.That(screen.GetByRole("status")).Not.ToBeVisibleAsync(timeout: budget));
        }

        // Gone from 900 ms: the grace window had run when the read was cut off at 1900 ms.
        await Run(900);

        // Gone from 1000 ms: it had not, and the poll fails with the cut-off read as its cause.
        var error = await Assert.ThrowsAsync<TestException>(() => Run(1_000));
        Assert.Equal("ASSERTION_FAILED", error.Code);

        // The locator reports the cut-off read as ACTION_FAILED over the engine's OPERATION_TIMEOUT.
        Assert.Equal("ACTION_FAILED", Assert.IsType<TestException>(error.InnerException).Code);
        Assert.Same(cut, error.InnerException.InnerException);
    }

    [Fact]
    public async Task Negated_a_budget_shorter_than_the_grace_window_still_passes_when_its_last_read_is_cut_off()
    {
        var cut = new EngineException("OPERATION_TIMEOUT", "locate timed out");
        var budget = TimeSpan.FromMilliseconds(350);
        var screen = Over(async token =>
        {
            var rest = budget - _time.Elapsed;
            if (rest < TimeSpan.FromMilliseconds(100))
            {
                await Task.Delay(rest > TimeSpan.Zero ? rest : TimeSpan.Zero, _time, token);
                throw cut;
            }

            return [];
        });
        await Drive(Expect.That(screen.GetByRole("status")).Not.ToBeVisibleAsync(timeout: budget));
    }

    [Fact]
    public async Task Lets_the_last_read_run_past_the_deadline_by_the_longest_read_the_poll_finished_up_to_the_window_cap()
    {
        var deadline = new Deadline(TimeSpan.FromMilliseconds(350), _time);
        var timeouts = new List<TimeSpan>();
        var calls = 0;
        var poll = InternalTime.PollReadsAsync(new PollReadsOptions
        {
            Deadline = deadline,
            Reads = new ReadWindow(deadline, _time, new Deadline(TimeSpan.FromMilliseconds(400), _time)),
            Clock = _time,
            Token = CancellationToken.None,
            Negated = false,
            Evaluate = reads => reads.ReadAsync<bool?>(async timeout =>
            {
                timeouts.Add(timeout);
                calls++;
                await Bounded(TimeSpan.FromMilliseconds(calls == 1 ? 200 : 50), timeout);
                return calls == 2;
            }),
            OnTimeout = _ => new InvalidOperationException("poll timed out"),
        });
        await Drive(poll);
        Assert.Equal([TimeSpan.FromMilliseconds(350), TimeSpan.FromMilliseconds(100)], timeouts);
    }

    [Fact]
    public async Task Shares_the_extra_time_past_the_deadline_across_the_reads_that_run_in_it()
    {
        var deadline = new Deadline(TimeSpan.FromMilliseconds(100), _time);
        var reads = new ReadWindow(deadline, _time, new Deadline(TimeSpan.FromSeconds(10), _time));
        await Drive(reads.ReadAsync(async _ =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(300), _time);
            return 0;
        }));
        Assert.Equal(TimeSpan.FromMilliseconds(100), reads.Timeout());
        _time.Advance(TimeSpan.FromMilliseconds(50));
        Assert.Equal(TimeSpan.FromMilliseconds(50), reads.Timeout());
        _time.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(TimeSpan.FromMilliseconds(1), reads.Timeout());
    }

    [Fact]
    public async Task Judges_a_cut_off_read_by_when_that_read_started_not_when_its_sample_did()
    {
        var deadline = new Deadline(TimeSpan.FromMilliseconds(1_000), _time);
        var samples = 0;
        var poll = InternalTime.PollReadsAsync(new PollReadsOptions
        {
            Deadline = deadline,
            Reads = new ReadWindow(deadline, _time, new Deadline(TimeSpan.FromSeconds(10), _time)),
            Clock = _time,
            Token = CancellationToken.None,
            Negated = false,
            Evaluate = async reads =>
            {
                samples++;
                if (samples == 1)
                {
                    await reads.ReadAsync(async _ => await Slept(300));
                    return false;
                }

                // The sample starts with 600 ms left; its second read starts with 100.
                await reads.ReadAsync(async _ => await Slept(500));
                await reads.ReadAsync<int>(timeout => Hung(timeout));
                return true;
            },
            OnTimeout = _ => new InvalidOperationException("poll timed out"),
        });
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Drive(poll));
        Assert.Equal("poll timed out", error.Message);
    }

    [Fact]
    public async Task Counts_a_negation_as_held_only_until_the_deadline_not_through_a_last_read_past_it()
    {
        var deadline = new Deadline(TimeSpan.FromMilliseconds(3_000), _time);
        var samples = 0;
        var poll = InternalTime.PollReadsAsync(new PollReadsOptions
        {
            Deadline = deadline,
            Reads = new ReadWindow(deadline, _time, new Deadline(TimeSpan.FromSeconds(10), _time)),
            Clock = _time,
            Token = CancellationToken.None,
            Negated = true,
            Evaluate = async reads =>
            {
                samples++;

                // The first read sees the condition; the second starts 400 ms before the deadline and ends 1.1 s past it.
                await reads.ReadAsync(async _ => await Slept(samples == 1 ? 2_500 : 1_500));
                return samples == 1;
            },
            OnTimeout = _ => new InvalidOperationException("negation timed out"),
        });
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Drive(poll));
        Assert.Equal("negation timed out", error.Message);
    }

    [Fact]
    public async Task Ends_on_a_read_cut_off_within_the_longest_read_time_of_the_deadline_and_fails_on_one_that_hung_with_more_left()
    {
        var cut = new EngineException("OPERATION_TIMEOUT", "locate timed out");
        Task Poll(int timeoutMs)
        {
            var deadline = new Deadline(TimeSpan.FromMilliseconds(timeoutMs), _time);
            var calls = 0;
            return InternalTime.PollReadsAsync(new PollReadsOptions
            {
                Deadline = deadline,
                Reads = new ReadWindow(deadline, _time),
                Clock = _time,
                Token = CancellationToken.None,
                Negated = false,
                Evaluate = reads => reads.ReadAsync<bool?>(async _ =>
                {
                    calls++;
                    await Slept(300);
                    return calls == 1 ? false : throw cut;
                }),
                OnTimeout = _ => new InvalidOperationException("poll timed out"),
            });
        }

        // The second read starts with 200 ms left: under the 300 ms the first took, so the deadline cut it.
        var within = Assert.ThrowsAsync<InvalidOperationException>(() => Poll(600));

        // With 600 ms left, more than any read took, the read hung.
        var hung = Assert.ThrowsAsync<EngineException>(() => Poll(1_000));
        await Drive(Task.WhenAll(within, hung));
        Assert.Equal("poll timed out", (await within).Message);
        Assert.Same(cut, await hung);
    }

    private Task Drive(Task pending) => _time.RunAsync(pending);

    private async Task<int> Drive(Task<int> pending)
    {
        await _time.RunAsync(pending);
        return await pending;
    }

    private async Task<int> Slept(int milliseconds)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(milliseconds), _time);
        return 0;
    }

    /// <summary>A read that takes <paramref name="duration"/> but is ended at <paramref name="budget"/> by the engine's <c>OPERATION_TIMEOUT</c>, as upstream's <c>withTimeout(sleep(...), timeoutMs, ...)</c>.</summary>
    private async Task Bounded(TimeSpan duration, TimeSpan budget)
    {
        try
        {
            await Task.Delay(duration, _time).WaitAsync(budget, _time);
        }
        catch (TimeoutException)
        {
            throw new EngineException("OPERATION_TIMEOUT", "locate timed out");
        }
    }

    /// <summary>A read that never answers on its own: the engine ends it at <paramref name="budget"/> with <c>OPERATION_TIMEOUT</c>.</summary>
    private async Task<int> Hung(TimeSpan budget)
    {
        await Bounded(TimeSpan.FromSeconds(10), budget);
        return 0;
    }

    /// <summary>A screen whose nth read (from 1) shows the nodes <paramref name="read"/> returns for it.</summary>
    private Screen Scripted(Func<int, SemanticNode[]> read)
    {
        var calls = 0;
        return Over(_ => Task.FromResult(read(Interlocked.Increment(ref calls))));
    }

    /// <summary>A screen showing nothing, whose first read takes <paramref name="first"/> ms and the rest <paramref name="rest"/> ms.</summary>
    private Screen Slow(List<TimeSpan> issued, int first, int rest)
    {
        return Over(async token =>
        {
            issued.Add(_time.Elapsed);
            await Task.Delay(TimeSpan.FromMilliseconds(issued.Count == 1 ? first : rest), _time, token);
            return [];
        });
    }

    private Screen Over(Func<CancellationToken, Task<SemanticNode[]>> read)
    {
        return new Screen(
            async token => new Observation { Route = "/", Roots = await read(token) },
            (_, _, _) => Task.CompletedTask,
            () => CancellationToken.None,
            () => { },
            ScreenFixture.Timeout,
            ScreenFixture.Timeout,
            clock: _time);
    }
}
