// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using Microsoft.Extensions.Time.Testing;

namespace E2E.Tests.LocatorEngine;

/// <summary>
/// Upstream's <c>locator failure candidate counts</c> (tester-army/e2e#1416, <c>79e2e13d3365</c>). The engine answers per expression, keyed by
/// the locator as it is described, so a test pins which expressions a failure resolves and how much budget each resolve was given.
/// </summary>
public sealed class LocatorFailureCandidateCountsTests
{
    private const string Heading = "candidates left after each filter step, counted after the failure";

    private static readonly SemanticNode Ref = new() { Ref = "node-1", Role = "listitem", Name = "Order 1042" };

    private readonly Screen _screen = ScreenFixture.Create();
    private readonly LocatorExpression _base;
    private readonly LocatorExpression _texted;
    private readonly LocatorExpression _chain;

    public LocatorFailureCandidateCountsTests()
    {
        var baseLocator = _screen.GetByRole("listitem");
        var texted = baseLocator.Filter("Order 1042");
        _base = baseLocator.Query;
        _texted = texted.Query;
        _chain = texted.Filter(has: _screen.GetByRole("button", "Cancel order")).Query;
    }

    [Fact]
    public async Task Lists_how_many_candidates_each_filter_step_left_counted_after_the_failure()
    {
        var counting = Counting(Answers((_chain, Nodes(0)), (_base, Nodes(6)), (_texted, Nodes(1))));

        var failure = await Assert.ThrowsAsync<TestException>(() => counting.Engine.ReadAsync(_chain, CancellationToken.None));

        Assert.Equal("LOCATOR_NOT_FOUND", failure.Code);
        Assert.Contains(Heading, failure.Message, StringComparison.Ordinal);
        Assert.Contains($"{_base.Describe()}: 6", failure.Message, StringComparison.Ordinal);
        Assert.Contains($"{_texted.Describe()}: 1", failure.Message, StringComparison.Ordinal);
        Assert.Contains($"{_chain.Describe()}: 0", failure.Message, StringComparison.Ordinal);

        // The walk re-resolves the prefixes innermost first, after the failing resolve's polls.
        Assert.Equal(_chain.Describe(), counting.Resolved[0]);
        Assert.Equal([_base.Describe(), _texted.Describe(), _chain.Describe()], counting.Resolved.TakeLast(3));

        // The failing resolve polled on the action deadline; the diagnostic resolves arrived after it, each bounded by the 500 ms diagnostic budget.
        Assert.InRange(counting.Timeouts[0], TimeSpan.FromTicks(1), TimeSpan.FromMilliseconds(20));
        var diagnostic = counting.Timeouts.TakeLast(3).ToList();
        Assert.Equal(3, diagnostic.Count);
        Assert.All(diagnostic, timeout => Assert.InRange(timeout, TimeSpan.FromTicks(1), TimeSpan.FromMilliseconds(500)));
    }

    [Fact]
    public async Task Keeps_a_locator_with_no_filter_step_unlisted()
    {
        var counting = Counting(Answers((_base, Nodes(0))));

        var failure = await Assert.ThrowsAsync<TestException>(() => counting.Engine.ReadAsync(_base, CancellationToken.None));

        Assert.Equal("LOCATOR_NOT_FOUND", failure.Code);
        Assert.DoesNotContain("counted after the failure", failure.Message, StringComparison.Ordinal);

        // Only the failing resolve polled; no walk ran.
        Assert.All(counting.Resolved, key => Assert.Equal(_base.Describe(), key));
    }

    [Fact]
    public async Task Keeps_the_plain_failure_when_the_diagnostic_pass_cannot_read()
    {
        var counting = Counting(Answers((_chain, Nodes(0)), (_base, new EngineException(EngineErrorCodes.EngineFailure, "engine died", retryable: false))));

        var failure = await Assert.ThrowsAsync<TestException>(() => counting.Engine.ReadAsync(_chain, CancellationToken.None));

        Assert.Equal("LOCATOR_NOT_FOUND", failure.Code);
        Assert.DoesNotContain("counted after the failure", failure.Message, StringComparison.Ordinal);

        // The walk stopped at the prefix that refused to read; only the full chain polled.
        Assert.Equal([_base.Describe()], counting.Resolved.Where(key => key != _chain.Describe()));
    }

    [Fact]
    public async Task Lists_the_step_counts_on_an_ambiguous_filtered_locator()
    {
        var counting = Counting(Answers((_chain, Nodes(2)), (_base, Nodes(6)), (_texted, Nodes(3))));

        var failure = await Assert.ThrowsAsync<TestException>(() => counting.Engine.ReadAsync(_chain, CancellationToken.None));

        Assert.Equal("LOCATOR_AMBIGUOUS", failure.Code);
        Assert.Equal(2, failure.Details?.Matches);
        Assert.Contains($"{_base.Describe()}: 6", failure.Message, StringComparison.Ordinal);
        Assert.Equal([_chain.Describe(), _base.Describe(), _texted.Describe(), _chain.Describe()], counting.Resolved);
    }

    [Fact]
    public async Task Counts_an_index_step_between_filters_under_its_own_label()
    {
        var indexed = new Locator(_screen, _texted).First().Query;
        var chained = new Locator(_screen, indexed).Filter("1042").Query;
        var counting = Counting(Answers((chained, Nodes(0)), (_base, Nodes(2)), (_texted, Nodes(1)), (indexed, Nodes(1))));

        var failure = await Assert.ThrowsAsync<TestException>(() => counting.Engine.ReadAsync(chained, CancellationToken.None));

        Assert.Contains($"{_base.Describe()}: 2", failure.Message, StringComparison.Ordinal);
        Assert.Contains($"{_texted.Describe()}: 1", failure.Message, StringComparison.Ordinal);
        Assert.Contains($"{indexed.Describe()}: 1", failure.Message, StringComparison.Ordinal);
        Assert.Contains($"{chained.Describe()}: 0", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A locate the way mobile's behaves: it ignores its budget and every call runs to completion (here by moving a fake clock 300 ms). The walk
    /// shares one deadline, so a three-step chain reads two prefixes and then says it stopped, rather than starting a third full read the budget
    /// cannot cover.
    /// </summary>
    [Fact]
    public async Task Bounds_the_whole_walk_by_one_shared_deadline_when_locate_ignores_the_operation_timeout()
    {
        var clock = new FakeTimeProvider();
        List<string> resolved = [];
        List<TimeSpan> timeouts = [];
        var engine = Engine(
            (expression, budget, _) =>
            {
                resolved.Add(expression.Describe());
                timeouts.Add(budget);
                clock.Advance(TimeSpan.FromMilliseconds(300));
                return Task.FromResult<IReadOnlyList<SemanticNode>>([]);
            },
            () => CancellationToken.None,
            clock);

        var failure = await Assert.ThrowsAsync<TestException>(() => engine.ReadAsync(_chain, CancellationToken.None));

        Assert.Equal("LOCATOR_NOT_FOUND", failure.Code);

        // The failing resolve polls once, then the walk resolves two of the three prefixes inside the shared budget and refuses the third.
        Assert.Equal([_chain.Describe(), _base.Describe(), _texted.Describe()], resolved);
        Assert.Contains("walk stopped after 2 of 3 steps", failure.Message, StringComparison.Ordinal);
        Assert.All(timeouts.Skip(1), timeout => Assert.InRange(timeout, TimeSpan.FromTicks(1), TimeSpan.FromMilliseconds(500)));
    }

    [Fact]
    public async Task Surfaces_cancellation_when_the_signal_aborts_mid_walk()
    {
        using var cancellation = new CancellationTokenSource();
        var engine = Engine(
            (expression, _, _) =>
            {
                // Abort during the walk's second prefix, as a mobile session would when the attempt's signal fires between two reads.
                if (expression.Describe() == _texted.Describe())
                {
                    cancellation.Cancel();
                    throw new InvalidOperationException("boom");
                }

                return Task.FromResult<IReadOnlyList<SemanticNode>>([]);
            },
            () => cancellation.Token,
            TimeProvider.System);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.ReadAsync(_chain, cancellation.Token));
    }

    [Fact]
    public async Task Drops_the_counts_when_a_later_prefix_cannot_be_read()
    {
        var counting = Counting(Answers((_chain, Nodes(0)), (_base, Nodes(1)), (_texted, new EngineException(EngineErrorCodes.EngineFailure, "engine died", retryable: false))));

        var failure = await Assert.ThrowsAsync<TestException>(() => counting.Engine.ReadAsync(_chain, CancellationToken.None));

        Assert.Equal("LOCATOR_NOT_FOUND", failure.Code);
        Assert.DoesNotContain("counted after the failure", failure.Message, StringComparison.Ordinal);

        // The walk read the base, then the next prefix refused: a partial list must not present itself as the per-step counts of a completed walk.
        Assert.Equal([_base.Describe(), _texted.Describe()], counting.Resolved.Where(key => key != _chain.Describe()));
    }

    private static IReadOnlyList<SemanticNode> Nodes(int count) => [.. Enumerable.Repeat(Ref, count)];

    private static Dictionary<string, object> Answers(params (LocatorExpression Expression, object Answer)[] entries) =>
        entries.ToDictionary(entry => entry.Expression.Describe(), entry => entry.Answer);

    private static E2E.LocatorEngine Engine(Func<LocatorExpression, TimeSpan, CancellationToken, Task<IReadOnlyList<SemanticNode>>> locate, Func<CancellationToken> cancellation, TimeProvider clock) =>
        new(new LocatorEngineOptions
        {
            Observe = _ => Task.FromResult(new Observation { Route = "/", Roots = [] }),
            Locate = locate,
            Perform = (_, _, _, _) => throw new InvalidOperationException("unexpected perform"),
            Cancellation = cancellation,
            ActionTimeout = TimeSpan.FromMilliseconds(20),
            AssertionTimeout = TimeSpan.FromMilliseconds(1_000),
            Clock = clock,
        });

    /// <summary>An engine over <paramref name="answers"/>, keyed by the described locator: nodes to answer, or an exception to throw.</summary>
    private static CountingEngine Counting(Dictionary<string, object> answers)
    {
        List<string> resolved = [];
        List<TimeSpan> timeouts = [];
        var engine = Engine(
            (expression, budget, _) =>
            {
                var key = expression.Describe();
                resolved.Add(key);
                timeouts.Add(budget);
                return answers.TryGetValue(key, out var answer)
                    ? answer is Exception failure ? Task.FromException<IReadOnlyList<SemanticNode>>(failure) : Task.FromResult((IReadOnlyList<SemanticNode>)answer)
                    : throw new InvalidOperationException("unexpected locate: " + key);
            },
            () => CancellationToken.None,
            TimeProvider.System);
        return new CountingEngine(engine, resolved, timeouts);
    }

    private sealed record CountingEngine(E2E.LocatorEngine Engine, List<string> Resolved, List<TimeSpan> Timeouts);
}
