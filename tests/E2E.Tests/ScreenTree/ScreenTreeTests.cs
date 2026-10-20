// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.Tests.ScreenTree;

/// <summary>
/// Upstream's <c>describe('screen.tree')</c> (<c>tests/unit/screen-tree.test.ts</c>). Not ported: "refuses on a locator, before observing"
/// and "refuses an unknown option" (a <see cref="Locator"/> has no <c>TreeAsync</c>, and <see cref="ActionOptions"/> is a typed class, so
/// neither call compiles), and <c>sdk-types.ts</c>'s type checks for the same reason.
/// </summary>
public sealed class ScreenTreeTests
{
    [Fact]
    public async Task Renders_one_observation_children_indented_as_a_screen_step()
    {
        var fixture = new ScreenTreeFixture();

        var tree = await fixture.Screen.TreeAsync();

        var lines = tree.Split('\n');
        Assert.Equal(4, lines.Length);
        Assert.Matches(new Regex(@"^#root root", RegexOptions.CultureInvariant), lines[0]);
        Assert.Matches(new Regex(@"^ #list list ""Todos""", RegexOptions.CultureInvariant), lines[1]);
        Assert.Matches(new Regex(@"^ {2}#milk checkbox ""Milk"" \[checked\]", RegexOptions.CultureInvariant), lines[2]);
        Assert.Matches(new Regex(@"^ #add button ""Add""", RegexOptions.CultureInvariant), lines[3]);
        Assert.Equal(1, fixture.Observed);
        var step = Assert.Single(fixture.Steps.All());
        Assert.Equal("screen.tree", step.Api);
        Assert.Equal(StepStatus.Passed, step.Status);
    }

    /// <summary>.NET-only: upstream passes the timeout to the engine operation; the port bounds the read itself.</summary>
    [Fact]
    public async Task Fails_with_an_operation_timeout_when_the_screen_does_not_answer()
    {
        var failure = await Assert.ThrowsAsync<EngineException>(() => Hanging().TreeAsync(new ActionOptions { Timeout = TimeSpan.FromMilliseconds(50) }));

        Assert.Equal(EngineErrorCodes.OperationTimeout, failure.Code);
    }

    /// <summary>.NET-only: the wait ends with the timeout even when the engine ignores cancellation.</summary>
    [Fact]
    public async Task Stops_waiting_at_the_timeout_when_the_engine_ignores_cancellation()
    {
        var release = new TaskCompletionSource<E2E.Engine.Observation>();
        var screen = new Screen(
            _ => release.Task,
            (_, _, _) => Task.CompletedTask,
            () => CancellationToken.None,
            () => { },
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2));

        var failure = await Assert.ThrowsAsync<EngineException>(() => screen.TreeAsync(new ActionOptions { Timeout = TimeSpan.FromMilliseconds(50) }));
        release.SetException(new InvalidOperationException("late"));

        Assert.Equal(EngineErrorCodes.OperationTimeout, failure.Code);
    }

    /// <summary>.NET-only: a caller's cancel surfaces as cancellation, not as a timeout.</summary>
    [Fact]
    public async Task Surfaces_a_caller_cancel_as_cancellation()
    {
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Hanging().TreeAsync(cancellationToken: cancel.Token));
    }

    private static Screen Hanging() => new(
        async token =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return new E2E.Engine.Observation { Route = "/", Roots = [] };
        },
        (_, _, _) => Task.CompletedTask,
        () => CancellationToken.None,
        () => { },
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(2));
}
