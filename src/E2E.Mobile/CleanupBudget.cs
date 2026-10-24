// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Mobile;

/// <summary>Best-effort cleanup: abandon the wait at cancellation or timeout, absorbing late failures.</summary>
internal static class CleanupBudget
{
    /// <summary>Runs the work under a token that is cancelled when the budget or the caller's token runs out, and waits no longer than that. <paramref name="time"/> is the clock the budget runs on; a test moves it where upstream uses fake timers.</summary>
    public static async Task WithinAsync(Func<CancellationToken, Task> work, TimeSpan timeout, CancellationToken cancellationToken, TimeProvider? time = null)
    {
        var budget = timeout < TimeSpan.Zero ? TimeSpan.Zero : timeout;
        using var elapsed = new CancellationTokenSource(budget, time ?? TimeProvider.System);
        using var expiry = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, elapsed.Token);
        var expired = new TaskCompletionSource();
        await using var registration = expiry.Token.Register(() => expired.TrySetResult());
        var finished = work(expiry.Token).ContinueWith(static _ => { }, TaskScheduler.Default);
        await Task.WhenAny(finished, expired.Task).ConfigureAwait(false);
    }
}

