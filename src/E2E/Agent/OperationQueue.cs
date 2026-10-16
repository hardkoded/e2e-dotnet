// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>
/// Upstream's <c>OperationQueue</c> (<c>agent/operation-queue.ts</c>): call-order serialization for one step's observations and
/// actions. An executor that issues a second action before the first settles would otherwise resolve both targets against the same
/// pre-action screen, exactly the wrong-node hazard the staleness rule exists to prevent. Queued, the second call sees the newest
/// screen, and a stale id fails loud instead of acting on the wrong node.
/// <para>
/// A queued body must never enqueue another operation itself: the inner call would wait behind its own caller. Action bodies call the
/// engine session directly, and a relocation takes its look directly rather than through the queued look.
/// </para>
/// </summary>
internal sealed class OperationQueue
{
    private readonly object _gate = new();
    private Task _tail = Task.CompletedTask;

    /// <summary>Runs <paramref name="body"/> after every operation queued before it, in call order.</summary>
    public Task<T> RunAsync<T>(Func<Task<T>> body)
    {
        lock (_gate)
        {
            // The tail is a task that never fails, so a failure goes to its own caller and never poisons the queue.
            var run = _tail.ContinueWith(_ => body(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();
            _tail = run.ContinueWith(static _ => { }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            return run;
        }
    }

    public Task RunAsync(Func<Task> body) =>
        RunAsync(async () =>
        {
            await body().ConfigureAwait(false);
            return 0;
        });
}
