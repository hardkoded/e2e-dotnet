// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E;

/// <summary>
/// The cancellation signal an attempt's operations run under (upstream's <c>run/budget.ts</c> <c>AttemptBudget</c>). The body runs under the
/// test's own signal, which its timeout cancels. Each teardown then <see cref="Enter"/>s a budget of its own, so the session can still drive the
/// app after the body timed out or was cancelled: the new signal follows the run's interrupt, not the test timeout. Operations read the signal
/// at call time, so a fixture acquired in the body keeps working afterwards.
/// </summary>
internal sealed class AttemptBudget : IDisposable
{
    private readonly object _gate = new();
    private readonly List<CancellationTokenSource> _sources;
    private readonly Func<E2EException>? _closed;
    private CancellationTokenSource _current;
    private bool _disposed;
    private bool _cancelledByOwner;

    /// <summary>
    /// Starts with the test's own signal; the budget owns it and disposes it. <paramref name="closed"/> is what an operation fails with once the
    /// session has closed (upstream's <c>StandaloneSessionOptions.closed</c>, the reason the ended signal aborts with); a bare cancellation when absent.
    /// </summary>
    public AttemptBudget(CancellationTokenSource test, Func<E2EException>? closed = null)
    {
        ArgumentNullException.ThrowIfNull(test);
        _sources = [test];
        _current = test;
        _closed = closed;
    }

    /// <summary>True while the test's own signal is the running phase's budget, and the budget's owner has not cancelled it with <see cref="CancelCurrentAsync"/>.</summary>
    public bool TestSignalIsOwn
    {
        get
        {
            lock (_gate)
            {
                return !_cancelledByOwner && !_disposed && _sources.Count > 0 && ReferenceEquals(_current, _sources[0]);
            }
        }
    }

    /// <summary>The signal the running phase's operations are cancelled with.</summary>
    public CancellationToken Token
    {
        get
        {
            lock (_gate)
            {
                // A phase that ended for a reason of its own (a closed session) starts nothing more, and the reason is the error.
                return _disposed
                    ? (_closed is null ? new CancellationToken(canceled: true) : throw _closed())
                    : _current.Token;
            }
        }
    }

    /// <summary>
    /// Switches to a fresh budget that follows <paramref name="interrupt"/>: cancelling the run cancels it, and the test timeout does not. Cancelling
    /// the returned source cancels only that budget's operations.
    /// </summary>
    public CancellationTokenSource Enter(CancellationToken interrupt)
    {
        var budget = CancellationTokenSource.CreateLinkedTokenSource(interrupt);
        lock (_gate)
        {
            _sources.Add(budget);
            _current = budget;
        }

        return budget;
    }

    /// <summary>Cancels the running phase's operations, as upstream's close does for the operations the test left running.</summary>
    public async Task CancelCurrentAsync()
    {
        CancellationTokenSource current;
        lock (_gate)
        {
            current = _current;
            _cancelledByOwner = true;
        }

        try
        {
            await current.CancelAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // The session was released already.
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            foreach (var source in _sources)
            {
                source.Dispose();
            }

            _sources.Clear();
        }
    }
}

/// <summary>
/// The test timeout one attempt runs on, and what <c>test.setTimeout</c> and a step hold do to it (upstream's <c>AttemptTimeout</c>, <c>run/budget.ts</c>).
/// The port enforces the timeout with the test's cancellation source, which upstream's race does with a timer; <see cref="Set"/> moves both the
/// source's timer and the deadline that caps the session's steps, and <see cref="Stop"/>, <see cref="Refill"/>, and <see cref="Start"/> stop that
/// timer while a step or <c>screen.pause()</c> is held, so the time a person spends looking at the app is never charged to the test. Upstream's
/// <c>race</c> is the test's cancellation source here.
/// </summary>
internal sealed class AttemptTimeout(
    CancellationTokenSource test,
    TimeSpan timeout,
    TimeProvider clock,
    Action<TimeSpan, Deadline> onMoved
)
{
    private readonly DateTimeOffset _startedAt = clock.GetUtcNow();
    private readonly Lock _gate = new();
    private TimeSpan _current = timeout;
    private DateTimeOffset? _endsAt = IsTimed(timeout) ? clock.GetUtcNow() + timeout : null;
    private bool _ended;

    // True while the clock is stopped; what was left then is _stoppedLeft (null when the attempt has no timeout).
    private bool _stopped;
    private DateTimeOffset _stoppedAt;
    private TimeSpan? _stoppedLeft;

    /// <summary>Ends this attempt's timeout: a later <see cref="Set"/> changes nothing (upstream's <c>ended</c>), as a serial member's does once the next one starts.</summary>
    public void End()
    {
        lock (_gate)
        {
            _ended = true;
        }
    }

    /// <summary>The timeout in force: the one given, or what <see cref="Set"/> changed it to (upstream's <c>timeoutMs</c>).</summary>
    public TimeSpan Timeout
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    /// <summary>Whether the timeout in force has elapsed since the attempt started, whether or not the test's timer ran yet. A stopped clock has not.</summary>
    public bool Expired
    {
        get
        {
            lock (_gate)
            {
                // A timeout that is zero or infinite never elapses, as the session's own timer is not armed for it.
                return !_stopped && _endsAt is { } ends && clock.GetUtcNow() >= ends;
            }
        }
    }

    /// <summary>
    /// Gives the attempt a timeout of <paramref name="timeout"/>, counted from its start like the one it replaces: the deadline moves with it, and one
    /// moved into the past cancels the test's signal at once. While the clock is stopped, what it hands back moves instead. False, changing nothing,
    /// once the timeout fired: work the test left running cannot move it any more.
    /// </summary>
    public bool Set(TimeSpan timeout)
    {
        var cancelNow = false;
        lock (_gate)
        {
            if (_ended || test.IsCancellationRequested)
            {
                return false;
            }

            var wasTimed = IsTimed(_current);
            var delta = timeout - _current;
            _current = timeout;
            if (_stopped)
            {
                _stoppedLeft =
                    !IsTimed(timeout) ? null
                    : _stoppedLeft is { } left && wasTimed ? TimeSpan.FromTicks(Math.Max(0, (left + delta).Ticks))
                    : TimeSpan.FromTicks(Math.Max(0, (timeout - (_stoppedAt - _startedAt)).Ticks));
                return true;
            }

            if (!IsTimed(timeout))
            {
                // No timeout any more: nothing times the test out, and no deadline caps its steps.
                _endsAt = null;
                onMoved(timeout, new Deadline(DateTimeOffset.MaxValue));
                try
                {
                    test.CancelAfter(System.Threading.Timeout.InfiniteTimeSpan);
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }

                return true;
            }

            _endsAt = _endsAt is { } ends && wasTimed ? ends + delta : _startedAt + timeout;
            var deadline = new Deadline(_endsAt.Value);
            onMoved(timeout, deadline);
            try
            {
                // Armed under the gate, so the last change published is the last one armed.
                var left = deadline.Remaining(clock.GetUtcNow());
                if (left == TimeSpan.Zero)
                {
                    cancelNow = true;
                }
                else
                {
                    test.CancelAfter(left);
                }
            }
            catch (ObjectDisposedException)
            {
                // The attempt ended and released its signal.
                return false;
            }
        }

        if (cancelNow)
        {
            // Outside the gate: cancelling runs the signal's callbacks, which can read Timeout.
            try
            {
                test.Cancel();
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Stops the clock and keeps what is left. False when nothing is left, or the test's signal fired already: the test times out now instead,
    /// and the clock keeps running (upstream's <c>stop</c>).
    /// </summary>
    public bool Stop()
    {
        var cancelNow = false;
        lock (_gate)
        {
            if (_stopped)
            {
                return true;
            }

            if (test.IsCancellationRequested)
            {
                return false;
            }

            var now = clock.GetUtcNow();
            TimeSpan? left = _endsAt is { } ends ? new Deadline(ends).Remaining(now) : null;
            if (left == TimeSpan.Zero)
            {
                cancelNow = true;
            }
            else
            {
                try
                {
                    test.CancelAfter(System.Threading.Timeout.InfiniteTimeSpan);
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }

                _stopped = true;
                _stoppedAt = now;
                _stoppedLeft = left;
                return true;
            }
        }

        if (cancelNow)
        {
            _ = TryCancel();
        }

        return false;
    }

    /// <summary>Gives the running phase what was left when the clock stopped; the clock stays stopped (upstream's <c>refill</c>).</summary>
    public void Refill()
    {
        lock (_gate)
        {
            if (_stopped && _stoppedLeft is { } left)
            {
                _endsAt = clock.GetUtcNow() + left;
                onMoved(_current, new Deadline(_endsAt.Value));
            }
        }
    }

    /// <summary>Runs the clock again from what was left when it stopped (upstream's <c>start</c>).</summary>
    public void Start()
    {
        lock (_gate)
        {
            if (!_stopped)
            {
                return;
            }

            _stopped = false;
            if (_stoppedLeft is not { } left)
            {
                return;
            }

            var now = clock.GetUtcNow();
            _endsAt = now + left;
            var deadline = new Deadline(_endsAt.Value);
            onMoved(_current, deadline);
            _ = left == TimeSpan.Zero ? TryCancel() : TryCancelAfter(left);
        }
    }

    // False once the attempt ended and released its signal.
    private bool TryCancel()
    {
        try
        {
            test.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private bool TryCancelAfter(TimeSpan left)
    {
        try
        {
            test.CancelAfter(left);
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private static bool IsTimed(TimeSpan timeout) =>
        timeout > TimeSpan.Zero && timeout != System.Threading.Timeout.InfiniteTimeSpan;
}
