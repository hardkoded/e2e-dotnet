// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine;

/// <summary>
/// Who acts: a test's own deterministic step, or the agent acting on a screen it observed. A deterministic step verifies its outcome with
/// <c>expect</c>, so an engine may act as soon as the target holds still. An agent reads the screen right after acting, so an engine may wait
/// for the transition to end first. .NET-only: upstream's <c>origin</c> is the string union <c>'test' | 'agent'</c>.
/// </summary>
public enum OperationOrigin
{
    /// <summary>A test's own step.</summary>
    Test,

    /// <summary>The agent.</summary>
    Agent,
}

/// <summary>
/// What an engine call carries about the call itself. Upstream's <c>OperationContext</c>. Only <c>origin</c> is a member here: upstream's
/// <c>signal</c> is the <see cref="CancellationToken"/> every engine call takes, <c>timeoutMs</c> is the per-call timeout (such as
/// <see cref="IEngineSession.ObserveAsync(TimeSpan, CancellationToken)"/>), and <c>runId</c> and <c>attemptId</c> are start options.
/// </summary>
/// <param name="Origin">Who asked. Upstream's <c>origin</c>.</param>
public sealed record OperationContext(OperationOrigin Origin)
{
    /// <summary>A test's own step: an engine may act without settling.</summary>
    public static OperationContext Test { get; } = new(OperationOrigin.Test);

    /// <summary>The agent: an engine may wait for the transition to end first.</summary>
    public static OperationContext Agent { get; } = new(OperationOrigin.Agent);
}
