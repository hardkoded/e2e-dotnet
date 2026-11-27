// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop;
using E2E.Engine;

namespace E2E.Desktop.Tests.Operation;

/// <summary>.NET-only: <see cref="DesktopOperation"/> carries what upstream's <c>withDeadline</c> getter gives an <c>OperationContext</c>.</summary>
public sealed class DesktopOperationTests
{
    [Fact]
    public async Task Counts_its_budget_down_from_the_call_and_never_below_zero()
    {
        var operation = new DesktopOperation(OperationOrigin.Test, 300, CancellationToken.None);
        Assert.InRange(operation.TimeoutMs, 200, 300);
        await Task.Delay(400);
        Assert.Equal(0, operation.TimeoutMs);
    }

    [Fact]
    public async Task Keeps_what_is_left_when_it_is_given_a_deadline_again()
    {
        var operation = new DesktopOperation(OperationOrigin.Agent, 1_000, CancellationToken.None);
        await Task.Delay(300);
        var again = operation.WithDeadline();
        Assert.Equal(OperationOrigin.Agent, again.Origin);
        Assert.InRange(again.TimeoutMs, 1, 750);
    }

    [Fact]
    public void Takes_its_budget_from_the_context_else_the_fallback_and_its_token_from_the_call()
    {
        using var source = new CancellationTokenSource();
        var given = DesktopOperation.Of(
            OperationContext.Agent with
            {
                Timeout = TimeSpan.FromSeconds(5),
            },
            TimeSpan.FromSeconds(1),
            source.Token
        );
        Assert.InRange(given.TimeoutMs, 4_000, 5_000);
        Assert.Equal(source.Token, given.Signal);
        var fallback = DesktopOperation.Of(OperationContext.Test, TimeSpan.FromSeconds(2), CancellationToken.None);
        Assert.InRange(fallback.TimeoutMs, 1_500, 2_000);
        Assert.Equal(OperationOrigin.Test, fallback.Origin);
    }
}
