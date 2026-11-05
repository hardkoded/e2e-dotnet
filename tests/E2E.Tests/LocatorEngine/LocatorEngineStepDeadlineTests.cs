// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.LocatorEngine;

/// <summary>
/// Upstream's <c>describe('LocatorEngine step deadline')</c> (<c>tests/unit/locator-engine.test.ts</c>). Not ported: "keeps an operation
/// budget of 0 an expired one", because the port has no <c>operation(timeoutMs)</c>; its engine calls take a <see cref="TimeSpan"/> budget.
/// "Refuses a negative, NaN, or infinite timeout before resolving" is partial: a <see cref="TimeSpan"/> cannot be NaN or infinite.
/// </summary>
public sealed class LocatorEngineStepDeadlineTests
{
    [Fact]
    public async Task Waits_past_the_action_timeout_with_timeout_0_until_the_phase_deadline()
    {
        var phase = new Deadline(TimeSpan.FromMilliseconds(600), TimeProvider.System);
        var scripted = ScriptedLocatorEngine.Make(Enumerable.Repeat(ScriptedLocatorEngine.Nodes(), 50), actionTimeout: TimeSpan.FromMilliseconds(100));
        scripted.Engine.BudgetDeadline = phase;

        Assert.Equal(phase.EndsAt, scripted.Engine.DeadlineFor(TimeSpan.Zero).EndsAt);
        var started = DateTimeOffset.UtcNow;
        var error = await Assert.ThrowsAsync<TestException>(() => scripted.Engine.PerformAsync(
            ScriptedLocatorEngine.Expression, (_, _) => Task.FromResult<LocatorAction>(new LocatorAction.Tap()), TimeSpan.Zero, CancellationToken.None));

        Assert.Equal("LOCATOR_NOT_FOUND", error.Code);
        Assert.True(DateTimeOffset.UtcNow - started >= TimeSpan.FromMilliseconds(550));
    }

    [Fact]
    public async Task Partial_Refuses_a_negative_NaN_or_infinite_timeout_before_resolving()
    {
        var scripted = ScriptedLocatorEngine.Make();

        foreach (var timeout in new[] { TimeSpan.FromMilliseconds(-1), Timeout.InfiniteTimeSpan })
        {
            var error = await Assert.ThrowsAsync<TestException>(() => scripted.Engine.PerformAsync(
                ScriptedLocatorEngine.Expression, (_, _) => Task.FromResult<LocatorAction>(new LocatorAction.Tap()), timeout, CancellationToken.None));
            Assert.Equal("INVALID_ARGUMENT", error.Code);
        }

        Assert.Equal(0, scripted.Resolves);
    }
}
