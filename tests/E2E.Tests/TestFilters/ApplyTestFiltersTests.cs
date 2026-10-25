// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using static E2E.Tests.TestFilters.TestFiltersKit;
using Filters = E2E.TestFilters;

namespace E2E.Tests.TestFilters;

/// <summary>Upstream's <c>describe('applyTestFilters')</c> (<c>tests/unit/test-filters.test.ts</c>).</summary>
public sealed partial class ApplyTestFiltersTests
{
    private const string Prefix = "returned an invalid result, so it leaves nothing out: ";

    [GeneratedRegex(@"<secret:[^>]+>")]
    private static partial Regex SecretMarker();

    [Fact]
    public async Task Calls_each_filter_in_order_on_the_tests_the_ones_before_it_kept_inside_its_step_and_records_each_decision()
    {
        var candidates = CandidatesOf("a", "b", "c");
        var seen = new List<string[]>();
        var first = FilterOf.Of("areas", tests =>
        {
            seen.Add([.. tests.Select(TitleOf)]);
            return Excluding((IdOf(candidates, "a"), "the diff misses it"));
        });
        var second = new FilterOf("jev", (tests, _) =>
        {
            seen.Add([.. tests.Select(TitleOf)]);
            return Task.FromResult(new TestFilterResult { Exclude = new Dictionary<string, string> { [IdOf(candidates, "c")] = "Jev: 4% likely affected" }, Note = "one call" });
        });
        var (scope, _, steps) = Scope();
        var applied = await Filters.ApplyTestFilters([first, second], candidates, scope);

        Assert.Equal([["a", "b", "c"], ["b", "c"]], seen);
        Assert.Equal(["areas", "jev"], steps);
        Assert.Equal(
            [KeyValuePair.Create(IdOf(candidates, "a"), new TestExclusion("areas", "the diff misses it")), KeyValuePair.Create(IdOf(candidates, "c"), new TestExclusion("jev", "Jev: 4% likely affected"))],
            applied.Excluded);
        Assert.Equal([new FilterDecision("areas", 3), new FilterDecision("jev", 2, "one call")], applied.Decisions);
    }

    [Fact]
    public async Task Does_not_call_a_filter_once_the_ones_before_it_left_nothing_and_says_so()
    {
        var candidates = CandidatesOf("a", "b");
        var all = FilterOf.Of("all", tests => Excluding([.. tests.Select(entry => (entry.Id, "not needed"))]));
        var calls = 0;
        var later = FilterOf.Of("later", _ =>
        {
            calls++;
            return Excluding();
        });
        var (scope, notices, steps) = Scope();
        var applied = await Filters.ApplyTestFilters([all, later], candidates, scope);
        Assert.Equal(0, calls);
        Assert.Equal(["all"], steps);
        Assert.Equal(["later: no tests left to judge"], notices);
        Assert.Equal(["all"], applied.Decisions.Select(decision => decision.Name));
        Assert.Equal(2, applied.Excluded.Count);
    }

    /// <summary>Partial: a .NET list and context are not frozen, and a context has no key list to compare.</summary>
    [Fact]
    public async Task Partial_hands_a_filter_a_list_and_a_context_of_the_project_root_the_change_the_loaded_config_the_signal_log_and_redact()
    {
        var candidates = CandidatesOf("a");
        using var controller = new CancellationTokenSource();
        IReadOnlyList<TestFilterCandidate>? tests = null;
        TestFilterContext? context = null;
        var (scope, _, _) = Scope(cancellationToken: controller.Token);
        await Filters.ApplyTestFilters(
            [new FilterOf("areas", (received, given) =>
            {
                tests = received;
                context = given;
                return Task.FromResult(Excluding());
            })],
            candidates,
            scope);
        Assert.Equal(candidates, tests);
        Assert.Equal(Root, context!.ProjectRoot);
        Assert.Same(ChangeSet, context.Changes);
        Assert.Equal("e2e.ci.config.ts", context.ConfigFile);

        // The signal follows the run's interrupt only while the call runs: unlike upstream's, it is released with the call.
        Assert.False(context.Signal.IsCancellationRequested);
    }

    /// <summary>Partial: an answer is always a typed result, so a synchronous answer and an <c>exclude</c> without a prototype have no port; the trimmed reason and the dropped empty note do.</summary>
    [Fact]
    public async Task Partial_trims_a_reason_and_drops_an_empty_note()
    {
        var candidates = CandidatesOf("a", "b");
        var (scope, _, _) = Scope();
        var applied = await Filters.ApplyTestFilters(
            [new FilterOf("areas", (_, _) => Task.FromResult(new TestFilterResult { Exclude = new Dictionary<string, string> { [IdOf(candidates, "b")] = "  trimmed  " }, Note = "   " }))],
            candidates,
            scope);
        Assert.Equal([KeyValuePair.Create(IdOf(candidates, "b"), new TestExclusion("areas", "trimmed"))], applied.Excluded);
        Assert.Equal(new FilterDecision("areas", 2), applied.Decisions[0]);
        Assert.Null(applied.Decisions[0].Note);
    }

    [Fact]
    public async Task Redacts_every_static_secret_and_credential_password_in_reasons_notes_log_lines_and_what_redact_returns()
    {
        var candidates = CandidatesOf("a", "b");
        var redacted = "";
        var (scope, notices, _) = Scope();
        var applied = await Filters.ApplyTestFilters(
            [new FilterOf("areas", (_, context) =>
            {
                context.Log($"diffing with key {SecretKey}");
                redacted = context.Redact($"password {Password}");
                return Task.FromResult(new TestFilterResult { Exclude = new Dictionary<string, string> { [IdOf(candidates, "a")] = $"the key {SecretKey} says so" }, Note = $"signed in with {Password}" });
            })],
            candidates,
            scope);
        var written = string.Join('\n', [.. applied.Excluded.Select(pair => pair.Key + pair.Value.Reason), .. applied.Decisions.Select(decision => decision.Note), .. notices, redacted]);
        Assert.DoesNotContain(SecretKey, written, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, written, StringComparison.Ordinal);
        Assert.Matches(@"\Aareas: diffing with key <secret:[^>]+>\z", Assert.Single(notices));
        Assert.Matches(@"\Apassword <secret:[^>]+>\z", redacted);
        Assert.Matches(@"\Athe key <secret:[^>]+> says so\z", applied.Excluded[IdOf(candidates, "a")].Reason);
        Assert.Matches(@"\Asigned in with <secret:[^>]+>\z", applied.Decisions[0].Note!);
    }

    /// <summary>Partial: the port's redactor reads whitespace and a newline alike in a run it masks, so it takes the space before the key text into the marker, where upstream keeps it: <c>@@ ... @@&lt;secret:pem&gt;</c> for <c>@@ ... @@ &lt;secret:pem&gt;</c>.</summary>
    [Fact]
    public async Task Partial_hands_a_filter_a_redact_that_also_masks_every_run_of_8_or_more_characters_of_a_value_so_no_part_of_a_key_survives_a_diff_that_shows_its_lines_only_in_part()
    {
        var keyLines = Enumerable.Range(0, 12).Select(line => Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes(line.ToString(System.Globalization.CultureInfo.InvariantCulture))))[..64]).ToArray();
        var pem = string.Join('\n', ["-----BEGIN PRIVATE KEY-----", .. keyLines, "-----END PRIVATE KEY-----"]);
        var pemLines = pem.Split('\n');
        var diff = string.Join('\n', [$"@@ -13,4 +13,4 @@ {keyLines[0][..30]}", .. pemLines.Select((line, index) => " +-"[index % 3] + (index % 2 == 0 ? line[3..] : line[..^3]))]);
        var redacted = "";
        var (scope, _, _) = Scope(secrets: [("pem", pem)]);
        await Filters.ApplyTestFilters(
            [new FilterOf("jev", (_, context) =>
            {
                redacted = context.Redact(diff);
                return Task.FromResult(Excluding());
            })],
            CandidatesOf("a"),
            scope);
        var runs = pemLines.SelectMany(line => Enumerable.Range(0, line.Length - 7).Select(start => line.Substring(start, 8)));
        Assert.DoesNotContain(runs, run => redacted.Contains(run, StringComparison.Ordinal));
        var first = redacted.Split('\n')[0];
        Assert.StartsWith("@@ -13,4 +13,4 @@", first, StringComparison.Ordinal);
        Assert.EndsWith("<secret:pem>", first, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_a_reason_a_note_and_a_log_line_on_one_line_cut_to_1024_code_points_never_through_an_emoji()
    {
        var candidates = CandidatesOf("a", "b");
        var emoji = string.Concat(Enumerable.Repeat("\U0001F600", 2000));
        var longText = $"Gateway failed:\n\n  no key\n\t{emoji}";
        var (scope, notices, _) = Scope();
        var applied = await Filters.ApplyTestFilters(
            [new FilterOf("areas", (_, context) =>
            {
                context.Log(longText);
                return Task.FromResult(new TestFilterResult { Exclude = new Dictionary<string, string> { [IdOf(candidates, "a")] = longText }, Note = longText });
            })],
            candidates,
            scope);
        var cut = $"Gateway failed: no key {string.Concat(Enumerable.Repeat("\U0001F600", 1000))}…";
        Assert.Equal(cut, applied.Excluded[IdOf(candidates, "a")].Reason);
        Assert.Equal(cut, applied.Decisions[0].Note);
        Assert.Equal([$"areas: {cut}"], notices);
        Assert.Equal(1024, applied.Decisions[0].Note!.EnumerateRunes().Count());

        // At the bound, nothing is cut.
        var exact = new string('x', 1024);
        var kept = await Filters.ApplyTestFilters([FilterOf.Of("areas", _ => Excluding((IdOf(candidates, "b"), exact)))], candidates, Scope().Scope);
        Assert.Equal(exact, kept.Excluded[IdOf(candidates, "b")].Reason);
    }

    [Fact]
    public async Task Drops_a_log_line_a_filter_writes_after_its_call_returned_or_threw()
    {
        var candidates = CandidatesOf("a");
        var logs = new List<Action<string>>();
        var (scope, notices, _) = Scope();
        await Filters.ApplyTestFilters(
            [new FilterOf("areas", (_, context) =>
            {
                logs.Add(context.Log);
                return Task.FromResult(Excluding());
            })],
            candidates,
            scope);
        await Filters.ApplyTestFilters(
            [new FilterOf("jev", (_, context) =>
            {
                logs.Add(context.Log);
                throw new InvalidOperationException("no diff");
            })],
            candidates,
            scope);
        foreach (var log in logs)
        {
            log("too late");
        }

        Assert.Equal(2, logs.Count);
        Assert.Equal(["warning: jev failed, so it leaves nothing out: no diff"], notices);
    }

    [Fact]
    public async Task Ignores_an_id_it_was_not_given_whose_test_is_collected_since_that_tests_fate_is_settled_a_setup_test_a_test_another_flag_left_out_and_one_an_earlier_filter_left_out_which_keeps_that_filters_reason()
    {
        var candidates = CandidatesOf("a", "b");
        var b = IdOf(candidates, "b");
        const string Setup = "tests/auth.setup.e2e.ts::sign%20in";
        const string Sharded = "tests/z.e2e.ts::outside%20the%20shard";
        var (scope, _, _) = Scope(collectedIds: new HashSet<string>([.. candidates.Select(candidate => candidate.Id), Setup, Sharded]));
        ITestFilter[] filters =
        [
            FilterOf.Of("areas", _ => Excluding((Setup, "not needed"), (b, "unaffected"), (Sharded, "unaffected"))),
            FilterOf.Of("jev", _ => Excluding((b, "again"))),
        ];
        var applied = await Filters.ApplyTestFilters(filters, candidates, scope);
        Assert.Equal([KeyValuePair.Create(b, new TestExclusion("areas", "unaffected"))], applied.Excluded);
        Assert.Equal([new FilterDecision("areas", 2), new FilterDecision("jev", 1)], applied.Decisions);
    }

    /// <summary>The note the <c>areas</c> filter leaves when it answers <paramref name="answer"/> over tests signs in, pays, and refunds, checking it failed open: one warning, a failed decision, nothing left out.</summary>
    private static async Task<string> Refusal(Func<IReadOnlyList<TestFilterCandidate>, TestFilterResult?> answer, IReadOnlySet<string>? collectedIds = null)
    {
        var candidates = CandidatesOf("signs in", "pays", "refunds");
        var filter = new FilterOf("areas", (_, _) => Task.FromResult(answer(candidates)!));
        var (scope, notices, _) = Scope(collectedIds: collectedIds);
        var applied = await Filters.ApplyTestFilters([filter], candidates, scope);
        var note = applied.Decisions[0].Note ?? "";
        Assert.Equal([new FilterDecision("areas", 3, note, FilterOutcome.Failed)], applied.Decisions);
        Assert.Empty(applied.Excluded);
        Assert.Equal([$"warning: areas {note}"], notices);
        return note;
    }

    /// <summary>Partial: only a null result and a result with no <c>Exclude</c> reach the check in C#, where the type rules out the other shapes (an array, a string, an unknown key, a map, a numeric note), and the messages are the port's.</summary>
    [Fact]
    public async Task Partial_refuses_a_result_that_is_not_an_exclude_and_a_note_warning_and_leaving_nothing_out()
    {
        Assert.Equal(Prefix + "expected a TestFilterResult, got null", await Refusal(_ => null));
        Assert.Equal(Prefix + "exclude must be a dictionary of test ids to reasons, got null", await Refusal(_ => new TestFilterResult { Exclude = null! }));
    }

    [Fact]
    public async Task Refuses_inside_the_filters_step_which_then_ends_failed_as_it_does_for_a_throw()
    {
        var candidates = CandidatesOf("a");
        var ended = new List<string>();
        var (scope, _, _) = Scope(step: async (name, work, _) =>
        {
            var call = await work();
            ended.Add($"{name}: {(call.Outcome is null ? "answered" : call.Outcome.ToString()!.ToLowerInvariant())}");
            return call;
        });
        ITestFilter[] filters =
        [
            FilterOf.Of("areas", _ => Excluding(("nope", "x"))),
            new FilterOf("jev", (_, _) => throw new InvalidOperationException("no diff")),
            FilterOf.Of("quick", _ => Excluding()),
        ];
        await Filters.ApplyTestFilters(filters, candidates, scope);
        Assert.Equal(["areas: failed", "jev: failed", "quick: answered"], ended);
    }

    /// <summary>Partial: a reason is a string in C#, so only an empty or blank one is refused, not a number or a null.</summary>
    [Fact]
    public async Task Partial_refuses_a_reason_that_is_not_a_non_empty_string_naming_the_first_test()
    {
        foreach (var reason in new[] { "", "   " })
        {
            var note = await Refusal(candidates => Excluding((IdOf(candidates, "pays"), reason)));
            Assert.Matches(new Regex($@"\A{Regex.Escape(Prefix)}the reason for "".+::pays"" must be a non-empty string, got "), note);
        }

        var two = await Refusal(candidates => Excluding((IdOf(candidates, "pays"), ""), (IdOf(candidates, "refunds"), " ")));
        Assert.Contains("::pays\" must be a non-empty string, got \"\"", two, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refuses_an_id_no_collected_test_has_a_title_or_a_mistyped_id_among_them_saying_exclude_takes_test_id()
    {
        const string Stranger = ", which no collected test has; exclude takes test.id";
        var candidates = CandidatesOf("signs in", "pays", "refunds");
        var id = IdOf(candidates, "pays");
        var typo = await Refusal(_ => Excluding(($"{id}x", "nope")), new HashSet<string>(candidates.Select(candidate => candidate.Id)));
        Assert.Equal($"{Prefix}exclude names \"{id}x\"{Stranger}", typo);
        var title = await Refusal(_ => Excluding(("pays", "unaffected")));
        Assert.Equal($"{Prefix}exclude names \"pays\"{Stranger}", title);
        var several = await Refusal(_ => Excluding(("tests/zz.e2e.ts::nothing", "a"), ("tests/zz.e2e.ts::else", "b")));
        Assert.Equal($"{Prefix}exclude names \"tests/zz.e2e.ts::nothing\"{Stranger}", several);
    }

    /// <summary>Partial: a key of the answer is not a free string in C#, so only the test id a filter chose is echoed, not an unknown key or the whole answer.</summary>
    [Fact]
    public async Task Partial_redacts_and_cuts_what_it_echoes_like_a_reason_a_secret_as_an_id_quoting_at_most_200_code_points()
    {
        var longText = string.Concat(Enumerable.Repeat("\U0001F600", 20_000));
        var id = await Refusal(_ => Excluding(($"{SecretKey}{longText}", "r")));
        var marker = SecretMarker().Match(id).Value;
        var capped = $"\"{marker}{string.Concat(Enumerable.Repeat("\U0001F600", 199 - marker.EnumerateRunes().Count()))}…\"";
        Assert.Equal($"{Prefix}exclude names {capped}, which no collected test has; exclude takes test.id", id);
        Assert.DoesNotContain(SecretKey, id, StringComparison.Ordinal);
    }

    public static TheoryData<string, Func<Task<TestFilterResult>>> Throws() => new()
    {
        { "a plain error", () => Task.FromException<TestFilterResult>(new InvalidOperationException(Thrown)) },
        { "an error thrown synchronously", () => throw new ArgumentException(Thrown) },
        { "a model answer the agent could not use, never a test failure", () => Task.FromException<TestFilterResult>(new AgentException("MODEL_OUTPUT_INVALID", Thrown)) },
        { "a configuration error, like any other", () => Task.FromException<TestFilterResult>(new ConfigurationException("MODEL_UNAVAILABLE", Thrown)) },
    };

    private static readonly string Thrown = $"git diff failed with {SecretKey}\n{new string('x', 2000)}";

    /// <summary>Partial: the port has no CLI to keep waiting, so the work a throw leaves behind is not recorded (upstream's <c>leaveWorkBehind</c>).</summary>
    [Theory]
    [MemberData(nameof(Throws))]
    public async Task Partial_leaves_nothing_out_when_a_filter_throws_warning_and_recording_a_redacted_one_line_note_of_at_most_1024_code_points_with_what_it_threw_last_and_calls_the_next_filter_on_every_test_it_was_given(string label, Func<Task<TestFilterResult>> decide)
    {
        Assert.NotEmpty(label);
        var candidates = CandidatesOf("a", "b");
        IReadOnlyList<TestFilterCandidate>? laterGot = null;
        var later = FilterOf.Of("later", tests =>
        {
            laterGot = tests;
            return Excluding();
        });
        var (scope, notices, _) = Scope();
        var applied = await Filters.ApplyTestFilters([new FilterOf("areas", (_, _) => decide()), later], candidates, scope);
        var note = applied.Decisions[0].Note ?? "";
        Assert.Matches(@"\Afailed, so it leaves nothing out: git diff failed with <secret:[^>]+> x+…\z", note);
        Assert.Equal(1024, note.EnumerateRunes().Count());
        Assert.Equal([$"warning: areas {note}"], notices);
        Assert.Equal([new FilterDecision("areas", 2, note, FilterOutcome.Failed), new FilterDecision("later", 2)], applied.Decisions);
        Assert.Equal(candidates, laterGot);
        Assert.Empty(applied.Excluded);
    }

    /// <summary>Partial: an interrupt surfaces as a cancellation, where upstream rejects with <c>INTERRUPTED</c> (category <c>interrupted</c>); the signal has no reason to inspect, and no work is recorded as left behind.</summary>
    [Fact]
    public async Task Partial_abandons_a_filter_that_ignores_its_signal_when_the_run_is_interrupted_with_no_warning_and_drops_what_it_logs_after()
    {
        var candidates = CandidatesOf("a");
        using var controller = new CancellationTokenSource();
        var (slow, seen) = Hanging("slow");
        var (scope, notices, _) = Scope(cancellationToken: controller.Token);
        var pending = Filters.ApplyTestFilters([slow], candidates, scope);
        while (true)
        {
            lock (seen)
            {
                if (seen.Count == 1)
                {
                    break;
                }
            }

            await Task.Delay(5);
        }

        var log = seen[0].Log;
        log("asking about a");
        await controller.CancelAsync();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal("interrupted while filter \"slow\" ran", error.Message);
        log("still asking about a");
        Assert.Equal(["slow: asking about a"], notices);

        // No call began.
        var calls = 0;
        var never = FilterOf.Of("areas", _ =>
        {
            calls++;
            return Excluding();
        });
        var second = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Filters.ApplyTestFilters([never], candidates, Scope(cancellationToken: controller.Token).Scope));
        Assert.Equal("interrupted while filter \"areas\" ran", second.Message);
        Assert.Equal(0, calls);
    }
}
