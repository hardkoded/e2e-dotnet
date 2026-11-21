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
/// The test timeout one attempt runs on, and what <c>test.setTimeout</c> does to it (upstream's <c>AttemptTimeout</c>, <c>run/budget.ts</c>). The
/// port enforces the timeout with the test's cancellation source, which upstream's race does with a timer; <see cref="Set"/> moves both the
/// source's timer and the deadline that caps the session's steps. Upstream's <c>stop</c>, <c>refill</c>, <c>start</c>, and <c>race</c> serve
/// <c>e2e run --step</c> and the runner's own race, which the port does not have.
/// </summary>
internal sealed class AttemptTimeout(
    CancellationTokenSource test,
    TimeSpan timeout,
    TimeProvider clock,
    Action<Deadline> onMoved
)
{
    private readonly DateTimeOffset _startedAt = clock.GetUtcNow();
    private readonly object _gate = new();
    private TimeSpan _current = timeout;

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

    /// <summary>
    /// Gives the attempt a timeout of <paramref name="timeout"/>, counted from its start like the one it replaces: the deadline moves with it, and one
    /// moved into the past cancels the test's signal at once. False, changing nothing, once the timeout fired: work the test left running
    /// cannot move it any more.
    /// </summary>
    public bool Set(TimeSpan timeout)
    {
        lock (_gate)
        {
            if (test.IsCancellationRequested)
            {
                return false;
            }

            _current = timeout;
            var deadline = new Deadline(_startedAt + timeout);
            onMoved(deadline);
            var left = deadline.Remaining(clock.GetUtcNow());
            if (left == TimeSpan.Zero)
            {
                test.Cancel();
            }
            else
            {
                test.CancelAfter(left);
            }

            return true;
        }
    }
}
