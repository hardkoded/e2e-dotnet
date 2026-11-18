// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.LocatorEngine;

/// <summary>
/// The port reads a locator over one observation per try and has no engine <c>read</c> by reference, so the count of
/// resolves is the count of observations. The other
/// upstream its of this describe are not ported: the shared deadline after a stale read, the re-resolve of a superseded
/// ref, and the non-retryable stale read all need the engine reference API the port does not have.
/// </summary>
public sealed class LocatorEngineReadRetryContractTests
{
    private static Screen ScreenOver(Func<Observation> observe, Action onObserve, TimeSpan actionTimeout) =>
        new(
            _ =>
            {
                onObserve();
                return Task.FromResult(observe());
            },
            (_, _, _) => Task.CompletedTask,
            () => CancellationToken.None,
            () => { },
            actionTimeout,
            actionTimeout
        );

    [Fact]
    public async Task Polls_zero_matches_until_the_node_appears_then_reads_it()
    {
        var observations = 0;
        var node = new SemanticNode
        {
            Ref = "node-1",
            Role = "status",
            Name = "Total",
            Text = "42",
        };
        var screen = ScreenOver(
            () => new Observation { Route = "/", Roots = observations >= 3 ? [node] : [] },
            () => observations++,
            TimeSpan.FromSeconds(5)
        );

        Assert.Equal("42", await screen.GetByRole("status").TextContentAsync());
        Assert.Equal(3, observations);
    }

    [Fact]
    public async Task Reads_zero_matches_again_at_20_and_50_ms_then_every_poll_tick_from_the_start()
    {
        var clock = new SteppingTime();
        var budgets = new List<TimeSpan>();
        var node = new SemanticNode
        {
            Ref = "node-1",
            Role = "status",
            Name = "Total",
        };
        var screen = new Screen(
            _ => Task.FromResult(new Observation { Route = "/", Roots = [] }),
            (_, _, _) => Task.CompletedTask,
            () => CancellationToken.None,
            () => { },
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1),
            clock: clock,
            locate: (_, budget, _) =>
            {
                budgets.Add(budget);
                return Task.FromResult<IReadOnlyList<SemanticNode>>(budgets.Count == 5 ? [node] : []);
            }
        );

        var resolving = screen
            .GetByRole("status")
            .ResolveExactlyOneAsync(new Deadline(TimeSpan.FromSeconds(1), clock), CancellationToken.None);
        await clock.RunAsync(resolving.ContinueWith(_ => { }, TaskScheduler.Default));
        await resolving;

        Assert.Equal(
            [
                TimeSpan.FromMilliseconds(1_000),
                TimeSpan.FromMilliseconds(980),
                TimeSpan.FromMilliseconds(950),
                TimeSpan.FromMilliseconds(900),
                TimeSpan.FromMilliseconds(800),
            ],
            budgets
        );
    }

    [Fact]
    public async Task Gives_zero_matches_the_action_timeout_then_LOCATOR_NOT_FOUND()
    {
        var screen = ScreenOver(
            () => new Observation { Route = "/", Roots = [] },
            () => { },
            TimeSpan.FromMilliseconds(350)
        );
        var watch = Stopwatch.StartNew();

        var error = await Assert.ThrowsAsync<TestException>(() => screen.GetByRole("status").TextContentAsync());

        Assert.Equal("LOCATOR_NOT_FOUND", error.Code);
        Assert.True(watch.Elapsed >= TimeSpan.FromMilliseconds(300), "failed after " + watch.Elapsed);
    }
}
