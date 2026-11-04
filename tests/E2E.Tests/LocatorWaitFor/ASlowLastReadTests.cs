// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.LocatorEngine;

namespace E2E.Tests.LocatorWaitFor;

/// <summary>
/// Upstream's <c>a slow last read</c>. The port polls every 50 ms where upstream polls every 100 ms, so a read here takes 350 ms and
/// not 300: the last one then starts 200 ms before the deadline, as upstream's does, and finishes after the banner showed up.
/// </summary>
public sealed class ASlowLastReadTests
{
    private static readonly SemanticNode Banner = new() { Ref = "banner", Role = "alert", Name = "Saved", TestId = "banner" };

    /// <summary>
    /// A screen whose every read takes 350 ms, bounded by its operation's budget, and whose banner shows up at 1050 ms. The reads move a
    /// fake clock, so a loaded runner cannot shift the banner past the last read.
    /// </summary>
    private static Locator SlowScreen(SteppingTime clock)
    {
        var screen = new Screen(
            _ => Task.FromResult(new Observation { Route = "/", Roots = [] }),
            (_, _, _) => Task.CompletedTask,
            () => CancellationToken.None,
            () => { },
            TimeSpan.FromMilliseconds(1_000),
            TimeSpan.FromMilliseconds(1_000),
            clock: clock,
            locate: async (_, budget, _) =>
            {
                // The banner is read when the read ends, as a device answers with the screen of its end.
                await SlowLocate.AnswerAsync(clock, TimeSpan.FromMilliseconds(350), [], budget).ConfigureAwait(false);
                return clock.Elapsed >= TimeSpan.FromMilliseconds(1_050) ? [Banner] : [];
            });
        return screen.GetByTestId("banner");
    }

    [Fact]
    public async Task Still_finishes_past_the_deadline_so_waitFor_sees_a_node_that_showed_up_during_it()
    {
        var clock = new SteppingTime();
        await clock.RunAsync(SlowScreen(clock).WaitForAsync());
    }

    [Fact]
    public async Task Still_finishes_past_the_deadline_so_an_assertion_sees_a_node_that_showed_up_during_it()
    {
        var clock = new SteppingTime();
        await clock.RunAsync(Expect.That(SlowScreen(clock)).ToBeVisibleAsync());
    }
}
