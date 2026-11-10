// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

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
    private CancellationTokenSource _current;

    /// <summary>Starts with the test's own signal; the budget owns it and disposes it.</summary>
    public AttemptBudget(CancellationTokenSource test)
    {
        ArgumentNullException.ThrowIfNull(test);
        _sources = [test];
        _current = test;
    }

    /// <summary>The signal the running phase's operations are cancelled with.</summary>
    public CancellationToken Token
    {
        get
        {
            lock (_gate)
            {
                return _current.Token;
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
            foreach (var source in _sources)
            {
                source.Dispose();
            }

            _sources.Clear();
        }
    }
}
