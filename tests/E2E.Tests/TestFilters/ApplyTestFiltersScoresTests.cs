// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Tests.TestFilters.TestFiltersKit;
using Filters = E2E.TestFilters;

namespace E2E.Tests.TestFilters;

/// <summary>Upstream's <c>describe('scores')</c> inside <c>describe('applyTestFilters')</c> (<c>tests/unit/test-filters.test.ts</c>).</summary>
public sealed class ApplyTestFiltersScoresTests
{
    [Fact]
    public async Task Collects_the_score_each_filter_gives_a_test_keeps_the_highest_across_filters_and_drops_a_collected_test_it_was_not_handed()
    {
        var candidates = CandidatesOf("a", "b", "c");
        var (a, b, c) = (IdOf(candidates, "a"), IdOf(candidates, "b"), IdOf(candidates, "c"));
        const string Outside = "tests/z.e2e.ts::outside%20the%20shard";
        var (scope, _, _) = Scope(
            collectedIds: new HashSet<string>([.. candidates.Select(candidate => candidate.Id), Outside])
        );
        var applied = await Filters.ApplyTestFilters([FilterOf.Of("areas", _ => new TestFilterResult
                {
                    Exclude = new Dictionary<string, string> { [c] = "unaffected" },
                    Relevance = new Dictionary<string, double>
                    {
                        [a] = 0.9,
                        [b] = 0.3,
                        [Outside] = 1,
                    },
                }), FilterOf.Of("jev", _ => new TestFilterResult
                {
                    Exclude = new Dictionary<string, string>(),
                    Relevance = new Dictionary<string, double>
                    {
                        [a] = 0.4,
                        [b] = 0.6,
                        [c] = 1,
                    },
                })], candidates, scope);

        // `jev` was never handed `c`, which `areas` left out, and `areas` never `outside`.
        Assert.Equal([KeyValuePair.Create(a, 0.9), KeyValuePair.Create(b, 0.6)], applied.Relevance);
        Assert.Equal([new FilterDecision("areas", 3), new FilterDecision("jev", 2)], applied.Decisions);
    }

    /// <summary>Partial: C# has no object without a prototype, so that part of the upstream <c>it</c> has no counterpart.</summary>
    [Fact]
    public async Task Partial_takes_0_and_1_and_no_relevance_at_all_which_scores_nothing()
    {
        var candidates = CandidatesOf("a", "b");
        var (scope, _, _) = Scope();
        var scored = await Filters.ApplyTestFilters([FilterOf.Of("areas", _ => new TestFilterResult
                {
                    Exclude = new Dictionary<string, string>(),
                    Relevance = new Dictionary<string, double>
                    {
                        [IdOf(candidates, "a")] = 0,
                        [IdOf(candidates, "b")] = 1,
                    },
                })], candidates, scope);
        Assert.Equal(
            [KeyValuePair.Create(IdOf(candidates, "a"), 0.0), KeyValuePair.Create(IdOf(candidates, "b"), 1.0)],
            scored.Relevance
        );
        var silent = await Filters.ApplyTestFilters([FilterOf.Of("areas", _ => Excluding())], candidates, scope);
        Assert.Empty(silent.Relevance);
        var empty = await Filters.ApplyTestFilters(
            [
                FilterOf.Of("areas", _ => new TestFilterResult
                {
                    Exclude = new Dictionary<string, string>(),
                    Relevance = new Dictionary<string, double>(),
                }),
            ],
            candidates,
            scope
        );
        Assert.Empty(empty.Relevance);
    }

    /// <summary>Partial: the throw that carries scores is a <see cref="TestFilterException"/>; a rejected non-error value has no C# counterpart, so the third filter throws without one.</summary>
    [Fact]
    public async Task Partial_scores_nothing_for_a_filter_that_throws_without_scores_or_is_refused_even_when_its_scores_were_valid()
    {
        var candidates = CandidatesOf("a", "b");
        var a = IdOf(candidates, "a");
        var (scope, _, _) = Scope();
        var applied = await Filters.ApplyTestFilters([FilterOf.Of("refused", _ => new TestFilterResult
                {
                    Exclude = new Dictionary<string, string> { ["nope"] = "x" },
                    Relevance = new Dictionary<string, double> { [a] = 1 },
                }), new FilterOf("threw", (_, _) => throw new InvalidOperationException("no diff")), new FilterOf("thrown text", (_, _) => Task.FromException<TestFilterResult>(new TestFilterException("no key")))], candidates, scope);
        Assert.Empty(applied.Relevance);
    }
}
