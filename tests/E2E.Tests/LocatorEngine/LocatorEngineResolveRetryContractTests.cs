// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.LocatorEngine;

/// <summary>
/// The port resolves a locator over one observation per try and has no engine <c>locate</c> to retry, so the count of
/// resolves is the count of observations. The other upstream its of this file are not ported: the retry on a stale or frame miss, the deadline cut of a
/// resolve, the stale read contract, and <c>readAll</c> and <c>readNow</c> by reference, all of which need the engine reference API the
/// port does not have. The reads that take time run through a screen whose engine resolves locators itself, which is how the budget of
/// a read reaches the engine; the port polls every 50 ms where upstream polls every 100 ms, so the budgets differ by that. The perform contract is in <see cref="LocatorEnginePerformContractTests"/>, and <c>translateLocatorError</c> in
/// <see cref="TranslateLocatorErrorMappingTableTests"/>.
/// </summary>
public sealed class LocatorEngineResolveRetryContractTests
{
    private static Screen ScreenOver(Func<Observation> observe, Action onObserve) => new(
        _ =>
        {
            onObserve();
            return Task.FromResult(observe());
        },
        (_, _, _) => Task.CompletedTask,
        () => CancellationToken.None,
        () => { },
        TimeSpan.FromMilliseconds(350),
        TimeSpan.FromMilliseconds(350));

    [Fact]
    public async Task Fails_ambiguous_matches_immediately_with_LOCATOR_AMBIGUOUS()
    {
        var observations = 0;
        var screen = ScreenOver(
            () => new Observation
            {
                Route = "/",
                Roots = [new SemanticNode { Ref = "node-1", Role = "button", Name = "Submit" }, new SemanticNode { Ref = "node-2", Role = "button", Name = "Submit" }],
            },
            () => observations++);

        var error = await Assert.ThrowsAsync<TestException>(() => screen.GetByRole("button").TapAsync());

        Assert.Equal("LOCATOR_AMBIGUOUS", error.Code);
        Assert.Equal(1, observations);
    }

    [Fact]
    public async Task Polls_zero_matches_until_the_deadline_then_LOCATOR_NOT_FOUND()
    {
        var observations = 0;
        var screen = ScreenOver(() => new Observation { Route = "/", Roots = [] }, () => observations++);

        var error = await Assert.ThrowsAsync<TestException>(() => screen.GetByRole("button").TapAsync());

        Assert.Equal("LOCATOR_NOT_FOUND", error.Code);
        Assert.True(observations > 1);
    }

    private static readonly SemanticNode Match = new() { Ref = "node-1", Role = "button", Name = "Submit" };

    /// <summary>A screen whose engine resolves each locate with the next script entry, given the budget of that locate.</summary>
    private static Screen LocatingScreen(List<Func<TimeSpan, Task<IReadOnlyList<SemanticNode>>>> script, List<TimeSpan> budgets, TimeSpan? actionTimeout = null)
    {
        var next = 0;
        return new Screen(
            _ => Task.FromResult(new Observation { Route = "/", Roots = [] }),
            (_, _, _) => Task.CompletedTask,
            () => CancellationToken.None,
            () => { },
            actionTimeout ?? TimeSpan.FromMilliseconds(350),
            actionTimeout ?? TimeSpan.FromMilliseconds(350),
            locate: (_, budget, _) =>
            {
                budgets.Add(budget);
                return script[Math.Min(next++, script.Count - 1)](budget);
            });
    }

    [Fact]
    public async Task Keeps_a_resolve_that_hung_with_budget_to_spare_a_timeout_never_a_missing_node()
    {
        var calls = 0;
        var screen = LocatingScreen(
            [
                budget => { calls++; return SlowLocate.AnswerAsync(TimeSpan.FromMilliseconds(100), [], budget); },
                budget => { calls++; return SlowLocate.HangAsync(budget); },
            ],
            []);
        var deadline = new Deadline(TimeSpan.FromMilliseconds(2_000), screen.Clock);

        var error = await Assert.ThrowsAsync<TestException>(() => screen.GetByRole("button").ResolveExactlyOneAsync(deadline, CancellationToken.None));

        Assert.Equal("ACTION_FAILED", error.Code);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Lets_a_slow_last_resolve_finish_past_the_deadline_a_late_match_resolves()
    {
        var budgets = new List<TimeSpan>();
        var screen = LocatingScreen(
            [
                budget => SlowLocate.AnswerAsync(TimeSpan.FromMilliseconds(200), [], budget),
                budget => SlowLocate.AnswerAsync(TimeSpan.FromMilliseconds(200), [Match], budget),
            ],
            budgets);
        var deadline = new Deadline(TimeSpan.FromMilliseconds(400), screen.Clock);

        var node = await screen.GetByRole("button").ResolveExactlyOneAsync(deadline, CancellationToken.None);

        Assert.Equal(Match.Ref, node.Ref);

        // Upstream's budgets are [450, 350]: the first read gets the deadline, the second what is left of it plus the longest read.
        Assert.Equal(2, budgets.Count);
        Assert.InRange(budgets[0], TimeSpan.FromMilliseconds(380), TimeSpan.FromMilliseconds(400));
        Assert.True(budgets[1] > TimeSpan.FromMilliseconds(250), "the second read got " + budgets[1]);
    }

    [Fact]
    public async Task Ends_a_wait_whose_slow_last_resolve_found_nothing_as_LOCATOR_NOT_FOUND_not_a_timeout()
    {
        var budgets = new List<TimeSpan>();
        var screen = LocatingScreen(
            [
                budget => SlowLocate.AnswerAsync(TimeSpan.FromMilliseconds(200), [], budget),
                budget => SlowLocate.AnswerAsync(TimeSpan.FromMilliseconds(200), [], budget),
            ],
            budgets);
        var deadline = new Deadline(TimeSpan.FromMilliseconds(400), screen.Clock);

        var error = await Assert.ThrowsAsync<TestException>(() => screen.GetByRole("button").ResolveExactlyOneAsync(deadline, CancellationToken.None));

        Assert.Equal("LOCATOR_NOT_FOUND", error.Code);
        Assert.Null(error.InnerException);
        Assert.Equal(2, budgets.Count);
    }

    [Fact]
    public async Task Gives_each_retry_of_a_resolve_what_is_left_of_its_budget_not_the_whole_budget_again()
    {
        var budgets = new List<TimeSpan>();
        var stale = () => Task.FromException<IReadOnlyList<SemanticNode>>(new EngineException(EngineErrorCodes.NodeStale, "stale", retryable: true));
        var screen = LocatingScreen([_ => stale(), _ => stale(), _ => Task.FromResult<IReadOnlyList<SemanticNode>>([Match])], budgets, TimeSpan.FromMilliseconds(1_000));

        await screen.GetByRole("button").ResolveAsync(CancellationToken.None);

        // Upstream's budgets are [1000, 900, 800]: its retries pause 100 ms, the port's 50.
        Assert.Equal(3, budgets.Count);
        Assert.InRange(budgets[0], TimeSpan.FromMilliseconds(950), TimeSpan.FromMilliseconds(1_000));
        Assert.True(budgets[1] < budgets[0] - TimeSpan.FromMilliseconds(40), string.Join(", ", budgets));
        Assert.True(budgets[2] < budgets[1] - TimeSpan.FromMilliseconds(40), string.Join(", ", budgets));
    }
}
