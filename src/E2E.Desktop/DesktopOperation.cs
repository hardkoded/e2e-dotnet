// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using E2E.Engine;

namespace E2E.Desktop;

/// <summary>
/// The operation with one deadline: <see cref="TimeoutMs"/> reads what is left of the budget the call started with, so every call an action
/// makes (a press that focuses first, a type that clears first) shares one budget instead of each getting the whole of it. .NET-only: upstream
/// passes an <c>OperationContext</c> whose <c>signal</c> and <c>timeoutMs</c> this carries, and <c>withDeadline</c> in <c>surface.ts</c> makes the
/// getter; the port's <see cref="OperationContext"/> holds a <see cref="TimeSpan"/> and the token comes as an argument.
/// </summary>
public sealed class DesktopOperation
{
    private readonly long _deadline;

    /// <summary>An operation whose budget of <paramref name="timeoutMs"/> counts from now.</summary>
    public DesktopOperation(OperationOrigin origin, int timeoutMs, CancellationToken signal)
    {
        Origin = origin;
        Signal = signal;
        _deadline = Stopwatch.GetTimestamp() + (long)(Math.Max(timeoutMs, 0) * (Stopwatch.Frequency / 1000.0));
    }

    /// <summary>Who acts: the agent settles after an action, a test's own step does not. Upstream's <c>origin</c>.</summary>
    public OperationOrigin Origin { get; }

    /// <summary>The caller's cancellation. Upstream's <c>signal</c>.</summary>
    public CancellationToken Signal { get; }

    /// <summary>What is left of the budget, in milliseconds, never below zero. Upstream's <c>timeoutMs</c>.</summary>
    public int TimeoutMs
    {
        get
        {
            var left = (_deadline - Stopwatch.GetTimestamp()) * 1000.0 / Stopwatch.Frequency;
            return left <= 0 ? 0 : (int)Math.Min(Math.Ceiling(left), int.MaxValue);
        }
    }

    /// <summary>This operation with the budget it has left starting now. Upstream's <c>withDeadline</c>.</summary>
    public DesktopOperation WithDeadline() => new(Origin, TimeoutMs, Signal);

    /// <summary>The operation of a session call: the budget, its origin, and the caller's token.</summary>
    internal static DesktopOperation Of(OperationContext context, TimeSpan fallback, CancellationToken signal)
    {
        ArgumentNullException.ThrowIfNull(context);
        var budget = context.Timeout ?? fallback;
        var token = context.Signal.CanBeCanceled && !signal.CanBeCanceled ? context.Signal : signal;
        return new DesktopOperation(context.Origin, (int)Math.Min(budget.TotalMilliseconds, int.MaxValue), token);
    }
}
