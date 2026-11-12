// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Extensions.Time.Testing;

namespace E2E.Tests.LocatorEngine;

/// <summary>
/// The fake engine calls of upstream's <c>locator-engine.test.ts</c> that take time: <c>slowResolve(ms, refs)</c> and the
/// <c>withinBudget</c> wrapper, which ends a call at its operation's budget as an engine must, with <c>OPERATION_TIMEOUT</c>.
/// </summary>
internal static class SlowLocate
{
    /// <summary>A locate that answers <paramref name="nodes"/> after <paramref name="after"/>, ended at <paramref name="budget"/>.</summary>
    public static async Task<IReadOnlyList<SemanticNode>> AnswerAsync(TimeSpan after, IReadOnlyList<SemanticNode> nodes, TimeSpan budget)
    {
        try
        {
            await Task.Delay(after).WaitAsync(budget).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new EngineException(EngineErrorCodes.OperationTimeout, "operation timed out", retryable: false);
        }

        return nodes;
    }

    /// <summary>
    /// <see cref="AnswerAsync(TimeSpan, IReadOnlyList{SemanticNode}, TimeSpan)"/> on a fake clock: the call takes <paramref name="after"/> by moving
    /// <paramref name="clock"/> that far, or the budget's worth when it cannot finish, so the test waits for nothing.
    /// </summary>
    public static Task<IReadOnlyList<SemanticNode>> AnswerAsync(FakeTimeProvider clock, TimeSpan after, IReadOnlyList<SemanticNode> nodes, TimeSpan budget)
    {
        if (after > budget)
        {
            clock.Advance(budget);
            return Task.FromException<IReadOnlyList<SemanticNode>>(new EngineException(EngineErrorCodes.OperationTimeout, "operation timed out", retryable: false));
        }

        clock.Advance(after);
        return Task.FromResult(nodes);
    }

    /// <summary>A locate that never answers on its own, on a fake clock: it takes the whole <paramref name="budget"/> and ends with <c>OPERATION_TIMEOUT</c>.</summary>
    public static Task<IReadOnlyList<SemanticNode>> HangAsync(FakeTimeProvider clock, TimeSpan budget)
    {
        clock.Advance(budget);
        return Task.FromException<IReadOnlyList<SemanticNode>>(new EngineException(EngineErrorCodes.OperationTimeout, "operation timed out", retryable: false));
    }

    /// <summary>A locate that never answers on its own, ended at <paramref name="budget"/>.</summary>
    public static async Task<IReadOnlyList<SemanticNode>> HangAsync(TimeSpan budget)
    {
        await AnswerAsync(Timeout.InfiniteTimeSpan, [], budget).ConfigureAwait(false);
        return [];
    }
}
