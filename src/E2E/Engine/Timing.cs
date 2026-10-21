// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine;

/// <summary>Cancellation with the engine error taxonomy (upstream <c>engine/timing.ts</c>).</summary>
internal static class Timing
{
    /// <summary>
    /// Checks cancellation before dispatching the work, then abandons the wait on abort. The work an abort abandons keeps running,
    /// and its late failure is absorbed so it never surfaces as an unobserved exception.
    /// </summary>
    public static async Task<T> RaceAbortAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken, string label)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            throw Cancelled(label);
        }

        var running = work();
        try
        {
            return await running.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw Abandoned(running, label);
        }
    }

    /// <inheritdoc cref="RaceAbortAsync{T}(Func{Task{T}}, CancellationToken, string)"/>
    public static Task RaceAbortAsync(Func<Task> work, CancellationToken cancellationToken, string label) =>
        RaceAbortAsync(async () =>
        {
            await work().ConfigureAwait(false);
            return true;
        }, cancellationToken, label);

    private static EngineException Abandoned(Task work, string label)
    {
        _ = work.ContinueWith(static task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        return Cancelled(label);
    }

    private static EngineException Cancelled(string label) =>
        new(EngineErrorCodes.Cancelled, label + " cancelled", retryable: false);
}
