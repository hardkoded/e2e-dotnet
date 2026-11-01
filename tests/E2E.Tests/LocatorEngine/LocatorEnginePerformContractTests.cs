// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.LocatorEngine;

/// <summary>
/// Upstream's <c>LocatorEngine perform contract</c>. Three of its seven its are not ported: 're-resolves and retries the action after a
/// retryable stale node' (the port retries no stale node on an action), 'never sends a positioned tap whose target became visible past
/// the deadline' (the port has no positioned tap with a scroll into view), and 'marks every action it performs with the test origin'
/// (the port has no operation context).
/// </summary>
public sealed class LocatorEnginePerformContractTests
{
    private static Screen ScreenThatPerforms(Func<Task> perform, Action onPerform) => new(
        _ => Task.FromResult(new Observation { Route = "/", Roots = [new SemanticNode { Ref = "node-1", Role = "button", Name = "Submit" }] }),
        (_, _, _) =>
        {
            onPerform();
            return perform();
        },
        () => CancellationToken.None,
        () => { },
        TimeSpan.FromMilliseconds(1000),
        TimeSpan.FromMilliseconds(1000));

    [Fact]
    public async Task Never_repeats_an_action_that_may_have_committed()
    {
        var performs = 0;
        var screen = ScreenThatPerforms(() => throw new EngineException(EngineErrorCodes.ActionMayHaveCommitted, "maybe committed", retryable: false), () => performs++);

        var error = await Assert.ThrowsAsync<TestException>(() => screen.GetByRole("button").TapAsync());

        Assert.Equal("ACTION_FAILED", error.Code);
        Assert.Equal(1, performs);
    }

    [Fact]
    public async Task Maps_NOT_ACTIONABLE_to_ACTION_FAILED_without_retrying()
    {
        var performs = 0;
        var screen = ScreenThatPerforms(() => throw new EngineException(EngineErrorCodes.NotActionable, "covered by overlay", retryable: false), () => performs++);

        var error = await Assert.ThrowsAsync<TestException>(() => screen.GetByRole("button").TapAsync());

        Assert.Equal("ACTION_FAILED", error.Code);
        Assert.Equal(1, performs);
    }

    [Fact]
    public async Task Never_sends_an_action_whose_target_resolved_past_the_deadline()
    {
        var resolves = 0;
        var performs = 0;
        var target = new SemanticNode { Ref = "node-1", Role = "button", Name = "Submit" };
        var screen = new Screen(
            _ => Task.FromResult(new Observation { Route = "/", Roots = [] }),
            (_, _, _) =>
            {
                performs++;
                return Task.CompletedTask;
            },
            () => CancellationToken.None,
            () => { },
            TimeSpan.FromMilliseconds(1000),
            TimeSpan.FromMilliseconds(1000),
            locate: (_, budget, _) => SlowLocate.AnswerAsync(TimeSpan.FromMilliseconds(200), ++resolves == 1 ? [] : [target], budget));

        var error = await Assert.ThrowsAsync<TestException>(
            () => screen.GetByRole("button").PerformUntilAsync(new LocatorAction.Tap(), new Deadline(TimeSpan.FromMilliseconds(350), screen.Clock), CancellationToken.None));

        Assert.Equal("ACTION_FAILED", error.Code);
        Assert.Contains("before the tap was sent", error.Message, StringComparison.Ordinal);
        Assert.Equal(2, resolves);
        Assert.Equal(0, performs);
    }
}
