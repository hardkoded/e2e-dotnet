// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision;

/// <summary>What one model call came back with: its value, the tokens it used, and the model id the response named (upstream's <c>Recorded</c>).</summary>
public sealed class Recorded<T>
{
    public required T Value { get; init; }

    public int? InputTokens { get; init; }

    public int? OutputTokens { get; init; }

    public string? ModelId { get; init; }
}

/// <summary>Upstream's <c>budget.ts</c>.</summary>
public static class Budget
{
    /// <summary>
    /// Upstream's <c>recorded</c>. Runs one model call against the step budget: cancelled before it starts or after it returns, and
    /// recorded with its timing and tokens even when it throws. Errors propagate untouched for the caller to map.
    /// </summary>
    public static Task<T> RecordedAsync<T>(
        StepExecutorContext ctx,
        string provider,
        string modelId,
        Func<Task<Recorded<T>>> call
    ) => RecordedAsync(ctx, provider, modelId, ctx.Attempt.CancellationToken, call);

    /// <summary>
    /// <see cref="RecordedAsync{T}(StepExecutorContext, string, string, Func{Task{Recorded{T}}})"/> under <paramref name="signal"/>: the
    /// attempt's token linked with the caller's (.NET-only: the runtime hands an executor both).
    /// </summary>
    public static async Task<T> RecordedAsync<T>(
        StepExecutorContext ctx,
        string provider,
        string modelId,
        CancellationToken signal,
        Func<Task<Recorded<T>>> call
    )
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(call);
        signal.ThrowIfCancellationRequested();
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var startedAt = DateTimeOffset.UtcNow;
        Recorded<T>? result = null;
        try
        {
            result = await call().ConfigureAwait(false);
            signal.ThrowIfCancellationRequested();
            return result.Value;
        }
        finally
        {
            ctx.Budgets.RecordModelCall(
                new ExecutorModelCall
                {
                    Provider = provider,
                    ModelId = result?.ModelId ?? modelId,
                    StartedAt = startedAt,
                    DurationMs = (int)System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    InputTokens = result?.InputTokens,
                    OutputTokens = result?.OutputTokens,
                }
            );
        }
    }
}
