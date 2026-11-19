// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's <c>notify</c> (<c>util/notify.ts</c>).</summary>
internal static class Notify
{
    /// <summary>
    /// Runs every callback with the event in parallel. An error in a callback does not break the call. .NET-only: the wait observes
    /// <paramref name="cancellationToken"/> (the callbacks take no token), so a caller's cancel surfaces as cancellation instead of
    /// waiting for a callback that never completes.
    /// </summary>
    internal static async Task NotifyAsync<TEvent>(
        TEvent @event,
        IEnumerable<Func<TEvent, Task>?> callbacks,
        CancellationToken cancellationToken
    )
    {
        await Task.WhenAll(
                callbacks
                    .Where(callback => callback is not null)
                    .Select(callback => IsolatedAsync(callback!, @event, cancellationToken))
                    .ToList()
            )
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task IsolatedAsync<TEvent>(
        Func<TEvent, Task> callback,
        TEvent @event,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await callback(@event).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // As the AI SDK: an error in a callback does not break the call.
            return;
        }
    }
}
