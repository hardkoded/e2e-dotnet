// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.LocatorEngine;

/// <summary>
/// Upstream's <c>LocatorEngine resolve retry contract</c>. The tests that run on the <see cref="E2E.LocatorEngine"/> use a
/// <see cref="ScriptedLocatorEngine"/>, upstream's <c>makeEngine</c>; the port polls every 50 ms where upstream polls every 100 ms, so the
/// budgets of the retries differ by that. The perform contract is in <see cref="LocatorEnginePerformContractTests"/>, and
/// <c>translateLocatorError</c> in <see cref="TranslateLocatorErrorMappingTableTests"/>.
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
    private static Screen LocatingScreen(List<Func<TimeSpan, Task<IReadOnlyList<SemanticNode>>>> script, List<TimeSpan> budgets, TimeSpan? actionTimeout = null, TimeProvider? clock = null)
    {
        var next = 0;
        return new Screen(
            _ => Task.FromResult(new Observation { Route = "/", Roots = [] }),
            (_, _, _) => Task.CompletedTask,
            () => CancellationToken.None,
            () => { },
            actionTimeout ?? TimeSpan.FromMilliseconds(350),
            actionTimeout ?? TimeSpan.FromMilliseconds(350),
            clock: clock,
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
        // The slow resolves move a fake clock, so no load on the runner can move the deadline.
        var clock = new SteppingTime();
        var budgets = new List<TimeSpan>();
        var screen = LocatingScreen(
            [
                budget => SlowLocate.AnswerAsync(clock, TimeSpan.FromMilliseconds(200), [], budget),
                budget => SlowLocate.AnswerAsync(clock, TimeSpan.FromMilliseconds(200), [Match], budget),
            ],
            budgets,
            clock: clock);
        var deadline = new Deadline(TimeSpan.FromMilliseconds(400), screen.Clock);

        var resolving = screen.GetByRole("button").ResolveExactlyOneAsync(deadline, CancellationToken.None);
        await clock.RunAsync(resolving.ContinueWith(_ => { }, TaskScheduler.Default));
        var node = await resolving;

        Assert.Equal(Match.Ref, node.Ref);

        // Upstream's budgets are [450, 350]: the first read gets the deadline, the second what is left of it plus the longest read.
        Assert.Equal(2, budgets.Count);
        Assert.InRange(budgets[0], TimeSpan.FromMilliseconds(380), TimeSpan.FromMilliseconds(400));
        Assert.True(budgets[1] > TimeSpan.FromMilliseconds(250), "the second read got " + budgets[1]);
    }

    [Fact]
    public async Task Ends_a_wait_whose_slow_last_resolve_found_nothing_as_LOCATOR_NOT_FOUND_not_a_timeout()
    {
        // The slow resolves move a fake clock, so the deadline passes without any waiting.
        var clock = new SteppingTime();
        var budgets = new List<TimeSpan>();
        var screen = LocatingScreen(
            [
                budget => SlowLocate.AnswerAsync(clock, TimeSpan.FromMilliseconds(200), [], budget),
                budget => SlowLocate.AnswerAsync(clock, TimeSpan.FromMilliseconds(200), [], budget),
            ],
            budgets,
            clock: clock);
        var deadline = new Deadline(TimeSpan.FromMilliseconds(400), screen.Clock);

        var resolving = screen.GetByRole("button").ResolveExactlyOneAsync(deadline, CancellationToken.None);
        await clock.RunAsync(resolving.ContinueWith(_ => { }, TaskScheduler.Default));
        var error = await Assert.ThrowsAsync<TestException>(() => resolving);

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

    [Fact]
    public async Task Retries_retryable_frame_misses_until_the_engine_recovers()
    {
        var scripted = ScriptedLocatorEngine.Make([ScriptedLocatorEngine.Frame, ScriptedLocatorEngine.Stale, ScriptedLocatorEngine.Nodes(ScriptedLocatorEngine.Node)]);

        var node = await scripted.Engine.ResolveExactlyOneAsync(ScriptedLocatorEngine.Expression, scripted.Engine.DeadlineFor(), CancellationToken.None);

        Assert.Equal(ScriptedLocatorEngine.Node.Ref, node.Ref);
        Assert.Equal(3, scripted.Resolves);
    }

    [Fact]
    public async Task Translates_a_non_retryable_resolve_failure_immediately_without_retrying()
    {
        var scripted = ScriptedLocatorEngine.Make([ScriptedLocatorEngine.Failure]);
        var engine = scripted.Engine;

        var error = await Assert.ThrowsAnyAsync<Exception>(() => engine.ResolveNowAsync(ScriptedLocatorEngine.Expression, CancellationToken.None, engine.Reads(engine.DeadlineFor())));

        var classified = ErrorSerializer.Classify(error);
        Assert.Equal("infrastructure", classified.Category);
        Assert.Equal("ENGINE_FAILURE", classified.Code);
        Assert.Equal(1, scripted.Resolves);
    }

    [Fact]
    public async Task Gives_up_on_persistent_retryable_errors_at_the_deadline_with_the_translated_error()
    {
        var scripted = ScriptedLocatorEngine.Make(Enumerable.Repeat(ScriptedLocatorEngine.Stale, 50), actionTimeout: TimeSpan.FromMilliseconds(350));
        var engine = scripted.Engine;

        var error = await Assert.ThrowsAnyAsync<Exception>(() => engine.ResolveNowAsync(ScriptedLocatorEngine.Expression, CancellationToken.None, engine.Reads(engine.DeadlineFor())));

        var classified = ErrorSerializer.Classify(error);
        Assert.Equal("test", classified.Category);
        Assert.Equal("LOCATOR_NOT_FOUND", classified.Code);
    }

    [Fact]
    public async Task Reports_a_resolve_the_deadline_cut_off_after_one_that_matched_nothing_as_LOCATOR_NOT_FOUND()
    {
        var deadline = new Deadline(TimeSpan.FromMilliseconds(350), TimeProvider.System);
        ResolveStep cut = _ => deadline.Remaining(DateTimeOffset.UtcNow) < TimeSpan.FromMilliseconds(50) ? ScriptedLocatorEngine.TimedOut(_) : ScriptedLocatorEngine.Nodes()(_);
        var scripted = ScriptedLocatorEngine.Make(Enumerable.Repeat(cut, 50));

        var error = await Assert.ThrowsAsync<TestException>(() => scripted.Engine.ResolveExactlyOneAsync(ScriptedLocatorEngine.Expression, deadline, CancellationToken.None));

        Assert.Equal("LOCATOR_NOT_FOUND", error.Code);
        var cause = Assert.IsType<TestException>(error.InnerException);
        Assert.Equal("ACTION_FAILED", cause.Code);
        Assert.Contains("operation timed out", cause.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ends_a_frame_wait_the_deadline_cut_off_on_the_missing_frame_as_LOCATOR_NOT_FOUND()
    {
        var deadline = new Deadline(TimeSpan.FromMilliseconds(350), TimeProvider.System);
        ResolveStep cut = _ => deadline.Remaining(DateTimeOffset.UtcNow) < TimeSpan.FromMilliseconds(50) ? ScriptedLocatorEngine.TimedOut(_) : ScriptedLocatorEngine.Frame(_);
        var scripted = ScriptedLocatorEngine.Make(Enumerable.Repeat(cut, 50));

        var error = await Assert.ThrowsAsync<TestException>(() => scripted.Engine.ResolveExactlyOneAsync(ScriptedLocatorEngine.Expression, deadline, CancellationToken.None));

        Assert.Equal("LOCATOR_NOT_FOUND", error.Code);
        Assert.Contains("frame missing", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_a_first_resolve_the_deadline_cut_off_a_timeout_because_nothing_was_ever_seen_missing()
    {
        var scripted = ScriptedLocatorEngine.Make([ScriptedLocatorEngine.TimedOut]);

        var error = await Assert.ThrowsAsync<TestException>(() => scripted.Engine.ResolveExactlyOneAsync(ScriptedLocatorEngine.Expression, new Deadline(TimeSpan.Zero, TimeProvider.System), CancellationToken.None));

        Assert.Equal("ACTION_FAILED", error.Code);
    }
}
