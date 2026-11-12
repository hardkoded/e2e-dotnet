// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using E2E.Engine;

namespace E2E.Tests.LocatorEngine;

/// <summary>
/// The port reads a locator over one observation per try and has no engine <c>read</c> by reference, so the count of
/// resolves is the count of observations. The other
/// upstream its of this describe are not ported: the shared deadline after a stale read, the re-resolve of a superseded
/// ref, and the non-retryable stale read all need the engine reference API the port does not have.
/// </summary>
public sealed class LocatorEngineReadRetryContractTests
{
    private static Screen ScreenOver(Func<Observation> observe, Action onObserve, TimeSpan actionTimeout) => new(
        _ =>
        {
            onObserve();
            return Task.FromResult(observe());
        },
        (_, _, _) => Task.CompletedTask,
        () => CancellationToken.None,
        () => { },
        actionTimeout,
        actionTimeout);

    [Fact]
    public async Task Polls_zero_matches_until_the_node_appears_then_reads_it()
    {
        var observations = 0;
        var node = new SemanticNode { Ref = "node-1", Role = "status", Name = "Total", Text = "42" };
        var screen = ScreenOver(
            () => new Observation { Route = "/", Roots = observations >= 3 ? [node] : [] },
            () => observations++,
            TimeSpan.FromSeconds(5));

        Assert.Equal("42", await screen.GetByRole("status").TextContentAsync());
        Assert.Equal(3, observations);
    }

    [Fact]
    public async Task Gives_zero_matches_the_action_timeout_then_LOCATOR_NOT_FOUND()
    {
        var screen = ScreenOver(() => new Observation { Route = "/", Roots = [] }, () => { }, TimeSpan.FromMilliseconds(350));
        var watch = Stopwatch.StartNew();

        var error = await Assert.ThrowsAsync<TestException>(() => screen.GetByRole("status").TextContentAsync());

        Assert.Equal("LOCATOR_NOT_FOUND", error.Code);
        Assert.True(watch.Elapsed >= TimeSpan.FromMilliseconds(300), "failed after " + watch.Elapsed);
    }
}
