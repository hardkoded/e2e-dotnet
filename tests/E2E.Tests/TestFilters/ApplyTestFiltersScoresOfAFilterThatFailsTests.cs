// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using static E2E.Tests.TestFilters.TestFiltersKit;
using Filters = E2E.TestFilters;

namespace E2E.Tests.TestFilters;

/// <summary>Upstream's <c>describe('of a filter that fails')</c> inside <c>describe('scores')</c> (<c>tests/unit/test-filters.test.ts</c>).</summary>
public sealed class ApplyTestFiltersScoresOfAFilterThatFailsTests
{
    /// <summary>The error a filter throws when it cannot finish but still knows how likely some tests are to fail.</summary>
    private static TestFilterException Failing(string message, IReadOnlyDictionary<string, double>? relevance) =>
        new(message) { Relevance = relevance };

    /// <summary>An exception whose <c>Relevance</c> cannot be read (upstream's property defined with a getter that throws).</summary>
    private sealed class Unreadable(string message, string getterMessage) : TestFilterException(message)
    {
        public override IReadOnlyDictionary<string, double>? Relevance =>
            throw new InvalidOperationException(getterMessage);
    }

    private static Task<TestFilterResult> Throwing(Exception error) => Task.FromException<TestFilterResult>(error);

    [Fact]
    public async Task Scores_the_tests_its_error_names_keeping_the_highest_across_filters_and_still_warns_and_records_the_failure()
    {
        var candidates = CandidatesOf("a", "b", "c");
        var (a, b, c) = (IdOf(candidates, "a"), IdOf(candidates, "b"), IdOf(candidates, "c"));
        const string Outside = "tests/z.e2e.ts::outside%20the%20shard";
        var (scope, notices, _) = Scope(
            collectedIds: new HashSet<string>([.. candidates.Select(candidate => candidate.Id), Outside])
        );
        var applied = await Filters.ApplyTestFilters([FilterOf.Of("answers", _ => new TestFilterResult
                {
                    Exclude = new Dictionary<string, string>(),
                    Relevance = new Dictionary<string, double> { [c] = 0.9 },
                }), new FilterOf("throws", (_, _) => throw Failing(
                        "no key",
                        new Dictionary<string, double>
                        {
                            [a] = 1,
                            [b] = 0.5,
                            [Outside] = 1,
                        }
                    )), new FilterOf("rejects", (_, _) => Throwing(Failing("offline", new Dictionary<string, double> { [b] = 0.7, [c] = 0.2 })))], candidates, scope);

        // `outside` is collected but was never handed over, so its score is dropped as a result's is.
        Assert.Equal(
            [KeyValuePair.Create(c, 0.9), KeyValuePair.Create(a, 1.0), KeyValuePair.Create(b, 0.7)],
            applied.Relevance
        );
        Assert.Empty(applied.Excluded);
        Assert.Equal(
            [
                new FilterDecision("answers", 3),
                new FilterDecision("throws", 3, "failed, so it leaves nothing out: no key", FilterOutcome.Failed),
                new FilterDecision("rejects", 3, "failed, so it leaves nothing out: offline", FilterOutcome.Failed),
            ],
            applied.Decisions
        );
        Assert.Equal(
            [
                "warning: throws failed, so it leaves nothing out: no key",
                "warning: rejects failed, so it leaves nothing out: offline",
            ],
            notices
        );
    }

    /// <summary>Partial: the type rules out a relevance that is a list or a number, so only a score above 1, a NaN, and an unknown id are dropped.</summary>
    [Theory]
    [InlineData("a score above 1", "a", 2.0, "the relevance of \"{0}\" must be a number from 0 to 1, got 2")]
    [InlineData("a score of NaN", "a", double.NaN, "the relevance of \"{0}\" must be a number from 0 to 1, got NaN")]
    [InlineData(
        "an id no collected test has",
        "nope",
        1.0,
        "relevance names \"nope\", which no collected test has; relevance takes test.id"
    )]
    public async Task Partial_drops_a_relevance_that_fails_the_check_and_says_so_in_the_note_before_the_failure_it_follows(
        string label,
        string key,
        double score,
        string issue
    )
    {
        Assert.NotEmpty(label);
        var candidates = CandidatesOf("a", "b");
        var a = IdOf(candidates, "a");
        var (scope, notices, _) = Scope();
        var applied = await Filters.ApplyTestFilters(
            [
                new FilterOf("areas", (_, _) =>
                    throw Failing("no key", new Dictionary<string, double> { [key == "a" ? a : key] = score })
                ),
            ],
            candidates,
            scope
        );
        var note =
            $"failed, ignoring its relevance ({string.Format(System.Globalization.CultureInfo.InvariantCulture, issue, a)}), so it leaves nothing out: no key";
        Assert.Empty(applied.Relevance);
        Assert.Equal([new FilterDecision("areas", 2, note, FilterOutcome.Failed)], applied.Decisions);
        Assert.Equal([$"warning: areas {note}"], notices);
    }

    [Fact]
    public async Task Drops_a_relevance_that_cannot_be_read_such_as_a_getter_that_throws_and_says_so()
    {
        var candidates = CandidatesOf("a");
        var (scope, _, _) = Scope();
        var applied = await Filters.ApplyTestFilters(
            [new FilterOf("areas", (_, _) => throw new Unreadable("no key", "getter broke"))],
            candidates,
            scope
        );
        Assert.Empty(applied.Relevance);
        Assert.Equal(
            [
                new FilterDecision(
                    "areas",
                    1,
                    "failed, ignoring its relevance (getter broke), so it leaves nothing out: no key",
                    FilterOutcome.Failed
                ),
            ],
            applied.Decisions
        );
    }

    [Fact]
    public async Task Redacts_a_secret_in_why_the_scores_were_dropped_whether_a_score_quotes_it_or_the_error_that_stopped_the_read_does()
    {
        var candidates = CandidatesOf("a");
        var (scope, _, _) = Scope();
        var applied = await Filters.ApplyTestFilters(
            [
                // A score cannot be a string in C#, so the secret goes in as the id the scores name.
                new FilterOf("quotes", (_, _) =>
                    throw Failing("no key", new Dictionary<string, double> { [SecretKey] = 1 })
                ),
                new FilterOf("reads", (_, _) => throw new Unreadable("no key", $"reading it failed with {SecretKey}")),
            ],
            candidates,
            scope
        );
        var (quotes, reads) = (applied.Decisions[0].Note ?? "", applied.Decisions[1].Note ?? "");
        Assert.Matches(
            new Regex(
                @"relevance names ""<secret:[^>]+>"", which no collected test has; relevance takes test\.id\), so it leaves nothing out: no key\z"
            ),
            quotes
        );
        Assert.Matches(
            new Regex(
                @"ignoring its relevance \(reading it failed with <secret:[^>]+>\), so it leaves nothing out: no key\z"
            ),
            reads
        );
        foreach (var note in new[] { quotes, reads })
        {
            Assert.DoesNotContain(SecretKey, note, StringComparison.Ordinal);
        }
    }

    /// <summary>Partial: a score cannot be a long string in C#, so the long reason is the message of the error that stopped the read.</summary>
    [Fact]
    public async Task Partial_keeps_the_failure_in_the_note_when_the_reason_the_scores_were_dropped_is_long_and_cuts_a_long_failure_after_that_reason_to_one_line_of_1024_code_points()
    {
        var candidates = CandidatesOf("a");
        var (scope, _, _) = Scope();
        var longId = new string('x', 5000);
        var applied = await Filters.ApplyTestFilters(
            [
                new FilterOf("quiet", (_, _) => throw new Unreadable("no key", longId)),
                new FilterOf("verbose", (_, _) =>
                    throw Failing(longId, new Dictionary<string, double> { ["nope"] = 1 })
                ),
            ],
            candidates,
            scope
        );
        var (quiet, verbose) = (applied.Decisions[0].Note ?? "", applied.Decisions[1].Note ?? "");

        // The reason is cut to 300 code points, and the failure's message still follows it.
        var match = Regex.Match(
            quiet,
            @"\Afailed, ignoring its relevance \((.*)\), so it leaves nothing out: no key\z"
        );
        Assert.True(match.Success);
        var reason = match.Groups[1].Value;
        Assert.Matches(new Regex(@"\Ax+…\z"), reason);
        Assert.Equal(300, reason.EnumerateRunes().Count());

        // A long message is what the cut shortens, and the reason before it survives.
        Assert.Matches(
            new Regex(
                @"\Afailed, ignoring its relevance \(relevance names ""nope"", which no collected test has; relevance takes test\.id\), so it leaves nothing out: x+…\z"
            ),
            verbose
        );
        Assert.Equal(1024, verbose.EnumerateRunes().Count());
    }

    [Fact]
    public async Task Scores_nothing_for_a_filter_that_outran_its_deadline_even_when_it_then_rejects_with_scores_the_call_was_abandoned_and_what_it_throws_after_is_not_read()
    {
        var candidates = CandidatesOf("a");
        var obedient = new FilterOf("jev", (_, context) =>
        {
            var pending = new TaskCompletionSource<TestFilterResult>();
            context.Signal.Register(() =>
                pending.TrySetException(Failing("late", new Dictionary<string, double> { [IdOf(candidates, "a")] = 1 }))
            );
            return pending.Task;
        });
        var (scope, _, _) = Scope(timeout: TimeSpan.FromMilliseconds(20));
        var applied = await Filters.ApplyTestFilters([obedient], candidates, scope);
        Assert.Empty(applied.Relevance);
        Assert.Equal(
            [
                new FilterDecision(
                    "jev",
                    1,
                    "did not finish within 20ms, so it leaves nothing out",
                    FilterOutcome.TimedOut
                ),
            ],
            applied.Decisions
        );
    }
}
