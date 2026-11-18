// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E;

/// <summary>Why a test filter left a test out: the filter's name and its reason (upstream's <c>TestExclusion</c>, <c>collect/select.ts</c>).</summary>
internal sealed record TestExclusion(string Filter, string Reason);

/// <summary>One filter's decision: its record before the final selection counts what it left out (upstream's <c>FilterDecision</c>).</summary>
internal sealed record FilterDecision(string Name, int Candidates, string? Note = null, FilterOutcome? Outcome = null);

/// <summary>What the filters decided: every test they left out, by test id, and one decision per filter that ran (upstream's <c>AppliedTestFilters</c>).</summary>
/// <param name="Excluded">Every test the filters left out, by test id.</param>
/// <param name="Relevance">The highest score any filter gave each test it scored, by test id.</param>
/// <param name="Decisions">One decision per filter that ran.</param>
internal sealed record AppliedTestFilters(
    IReadOnlyDictionary<string, TestExclusion> Excluded,
    IReadOnlyDictionary<string, double> Relevance,
    IReadOnlyList<FilterDecision> Decisions
);

/// <summary>
/// How one filter call ended (upstream's <c>FilterCall</c>): with its answer, checked; or thrown, refused, or abandoned past the deadline, with
/// the note that says so and the scores the exception it threw carried, none for the other two.
/// </summary>
internal sealed record FilterCall
{
    /// <summary>How the call ended; <c>null</c> when it answered.</summary>
    public FilterOutcome? Outcome { get; init; }

    /// <summary>The checked answer, when the call answered.</summary>
    public CheckedResult? Result { get; init; }

    /// <summary>The note that says why the call did not answer.</summary>
    public string? Note { get; init; }

    /// <summary>The scores the exception a failed call threw carried; none for a call that answered, was refused, or timed out.</summary>
    public IReadOnlyList<KeyValuePair<string, double>> Relevance { get; init; } = [];
}

/// <summary>A filter's answer once checked: what it left out, with each reason, the scores it gave the tests it was handed, and its note, redacted and cut; no note when it left none (upstream's <c>CheckedResult</c>).</summary>
internal sealed record CheckedResult(
    IReadOnlyList<KeyValuePair<string, string>> Exclude,
    IReadOnlyList<KeyValuePair<string, double>> Relevance,
    string? Note
);

/// <summary>The scores the exception a filter threw carried, and why they were dropped when they failed the check or could not be read (upstream's <c>ThrownScores</c>).</summary>
internal sealed record ThrownScores(IReadOnlyList<KeyValuePair<string, double>> Scores, string? Dropped = null);

/// <summary>What <see cref="TestFilters.ApplyTestFilters"/> hands each filter and where its lines go (upstream's <c>TestFilterScope</c>).</summary>
internal sealed class TestFilterScope
{
    public required string ProjectRoot { get; init; }

    /// <summary>The change every filter judges.</summary>
    public required TestFilterChanges Changes { get; init; }

    /// <summary>The loaded config file relative to <c>Changes.Root</c>, POSIX; null for a config object.</summary>
    public string? ConfigFile { get; init; }

    /// <summary>The fixed secrets and credential passwords to redact (upstream's <c>allSecrets</c>).</summary>
    public IReadOnlyList<(string Name, string Value)> Secrets { get; init; } = [];

    /// <summary>The id of every collected test, setup tests included: an <c>Exclude</c> naming one a filter was not given is ignored, since that test's fate is settled.</summary>
    public IReadOnlySet<string> CollectedIds { get; init; } = new HashSet<string>();

    /// <summary>How long each filter call may take before the run stops waiting for it.</summary>
    public TimeSpan Timeout { get; init; } = TestFilters.FilterTimeout;

    /// <summary>The run's interrupt.</summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>One line, already prefixed and redacted.</summary>
    public required Action<string> Notice { get; init; }

    /// <summary>The host wraps each call in the <c>filter</c> setup step, which ends failed or timed out for a call that did not answer; a bare host calls it directly.</summary>
    public Func<string, Func<Task<FilterCall>>, Task<FilterCall>> Step { get; init; } = (_, work) => work();
}

/// <summary>What <see cref="TestFilters.FilterSelection"/> judges a selection with, and where each line about what the gate and the filters decided goes (upstream's <c>FilterSelectionOptions</c>).</summary>
internal sealed class FilterSelectionOptions
{
    public required ResolvedConfig Config { get; init; }

    /// <summary>The base ref <c>--changed-since</c> named.</summary>
    public required string ChangedSince { get; init; }

    /// <summary>The run's interrupt (upstream's <c>signal</c>).</summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>One line about what the filters judge and what each decided.</summary>
    public required Action<string> Notice { get; init; }

    public required Collection Collection { get; init; }

    /// <summary>The flags the selection was made with, <c>--last-failed</c> among them.</summary>
    public SelectionFilters SelectionFilters { get; init; } = new();

    /// <summary>How each filter call is wrapped; called bare unless set.</summary>
    public Func<string, Func<Task<FilterCall>>, Task<FilterCall>>? Step { get; init; }

    /// <summary>How long each filter call may take; <see cref="TestFilters.FilterTimeout"/> unless set.</summary>
    public TimeSpan? Timeout { get; init; }
}

/// <summary>
/// A selection once the test filters had their say, the report's record of them, the change they judged (none when the config has no filter),
/// and how likely they rated each test to fail, by test id: empty when no filter ran or none scored a test (upstream's <c>FilteredSelection</c>).
/// </summary>
internal sealed record FilteredSelection(
    Selection Selection,
    IReadOnlyList<ReportFilter> Records,
    ReportChanges? Changes,
    IReadOnlyDictionary<string, double> Relevance
);

/// <summary>Test filters: what they are given, calling them, and what they decided (upstream's <c>run/test-filters.ts</c>).</summary>
internal static class TestFilters
{
    /// <summary>How long a filter call may take before the run stops waiting for it (upstream's <c>FILTER_TIMEOUT_MS</c>).</summary>
    public static readonly TimeSpan FilterTimeout = TimeSpan.FromMinutes(5);

    /// <summary>The most code points a stored reason, note, or log line keeps, on one line.</summary>
    private const int MaxFilterText = 1024;

    /// <summary>The most code points an error quotes of a key or test id a filter chose.</summary>
    private const int MaxQuoted = 200;

    /// <summary>The most code points of why a thrown exception's scores were dropped that a note keeps, so the failure's own message still fits after it.</summary>
    private const int MaxDropped = 300;

    /// <summary>What a run with no scored test carries, shared since nothing writes to it (upstream's <c>NO_RELEVANCE</c>).</summary>
    private static readonly IReadOnlyDictionary<string, double> NoRelevance = new Dictionary<string, double>();

    /// <summary>What <c>--changed-since</c> says when the config has no test filter for it to run.</summary>
    private const string NoFilters = "warning: --changed-since: the config has no filters, so every test runs";

    /// <summary>
    /// The second selection pass. <paramref name="first"/> is the selection every flag made, <c>--shard</c> included, so each shard machine
    /// hands the filters its own slice and the cut never depends on what they answer. Without a filter in the config, <paramref name="first"/>
    /// stands, and so it does, with <c>Changes.Skipped</c> saying why, on a <c>--last-failed</c> rerun, whose tests are owed, with no test to
    /// judge (git is asked for neither), and when no file changed since the merge base. Otherwise the exclusions go into a selection with the
    /// same flags, which serial groups and sessions settle like any other filter; an empty result is the filters' doing, or pass 1's under
    /// <c>passWithNoTests</c>, and passes. Each decision's line goes to <c>options.Notice</c> (upstream's <c>filterSelection</c>).
    /// </summary>
    public static async Task<FilteredSelection> FilterSelection(Selection first, FilterSelectionOptions options)
    {
        var (config, collection, selectionFilters, notice) = (
            options.Config,
            options.Collection,
            options.SelectionFilters,
            options.Notice
        );
        if (config.Filters.Count == 0)
        {
            if (!options.CancellationToken.IsCancellationRequested)
            {
                notice(NoFilters);
            }

            return new FilteredSelection(first, [], null, NoRelevance);
        }

        // `first` stands, for `record.Skipped`, said as `message` or `<why>, so no filter ran`.
        FilteredSelection Skip(ReportChanges record, string? message = null)
        {
            notice(message ?? $"{record.Skipped}, so no filter ran");
            return new FilteredSelection(first, [], record, NoRelevance);
        }

        if (selectionFilters.LastFailed is not null)
        {
            return Skip(
                new ReportChanges(Skipped: "--last-failed"),
                "--last-failed: the tests the last run owes run without filters"
            );
        }

        var candidates = FilterCandidates(first);
        if (candidates.Count == 0)
        {
            return Skip(new ReportChanges(Skipped: "no tests to judge"));
        }

        // Spelled as on disk, as `Changes.Root` is, so a filter can join the two.
        var projectRoot = Paths.OnDiskPath(config.ProjectRoot);
        var changes = await Changes
            .FindChangesAsync(
                new ChangeOptions
                {
                    Cwd = projectRoot,
                    ChangedSince = options.ChangedSince,
                    CancellationToken = options.CancellationToken,
                }
            )
            .ConfigureAwait(false);
        if (changes.Files.Count == 0)
        {
            return Skip(
                new ReportChanges(changes.Base, 0, $"no files changed since {ReportFormat.ShortCommit(changes.Base)}")
            );
        }

        notice(
            $"judging {MarkdownText.Plural(changes.Files.Count, "file")} changed since {ReportFormat.ShortCommit(changes.Base)}"
        );
        var applied = await ApplyTestFilters(
                config.Filters,
                candidates,
                new TestFilterScope
                {
                    ProjectRoot = projectRoot,
                    Changes = changes,
                    ConfigFile = config.ConfigPath is null ? null : RepoConfigFile(config.ConfigPath, changes.Root),
                    Secrets = config.AllSecrets,
                    CollectedIds = new HashSet<string>(
                        collection.Tests.Select(test => test.Id),
                        StringComparer.Ordinal
                    ),
                    Timeout = options.Timeout ?? FilterTimeout,
                    CancellationToken = options.CancellationToken,
                    Notice = notice,
                    Step = options.Step ?? ((_, work) => work()),
                }
            )
            .ConfigureAwait(false);
        var selection = Selector.Select(
            collection,
            config,
            selectionFilters with
            {
                Excluded = applied.Excluded,
            },
            passWithNoTests: true
        );
        var records = FilterRecords(
            applied.Decisions,
            selection.Pairs.Select(pair =>
                (
                    pair.Test.Id,
                    pair.Skip is null ? null : new ReportSkip(pair.Skip.Cause, pair.Skip.Reason, pair.Skip.Filter)
                )
            )
        );
        foreach (var record in records)
        {
            if (FilterSummary(record) is { } summary)
            {
                notice(summary);
            }
        }

        return new FilteredSelection(
            selection,
            records,
            new ReportChanges(changes.Base, changes.Files.Count),
            applied.Relevance
        );
    }

    /// <summary>
    /// The run notice for how many of the tests <paramref name="selection"/> runs a filter scored, which the run starts first, likeliest first;
    /// null when the scores leave every test in one relevance band, so the order is the one without them (upstream's <c>rankingNotice</c>).
    /// </summary>
    public static string? RankingNotice(Selection selection, IReadOnlyDictionary<string, double> relevance)
    {
        var tests = FilterCandidates(selection);
        if (tests.Select(test => Units.RelevanceBand(relevance.GetValueOrDefault(test.Id))).Distinct().Count() < 2)
        {
            return null;
        }

        return $"ranked {tests.Count(test => relevance.ContainsKey(test.Id))} of {tests.Count} tests by relevance, likeliest first";
    }

    /// <summary>
    /// The tests a filter may leave out: one candidate per ordinary test with at least one pair the selection runs, in report order, with the
    /// file of the setup test that saves its session when it uses one. Setup tests, explicit and capability skips, and tests filtered on every
    /// target are never candidates (upstream's <c>filterCandidates</c>).
    /// </summary>
    public static IReadOnlyList<TestFilterCandidate> FilterCandidates(Selection selection)
    {
        var producers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var test in selection.Pairs.Select(pair => pair.Test).Where(test => test.Kind == TestKind.Setup))
        {
            foreach (var session in test.Sessions)
            {
                producers[session] = test.File;
            }
        }

        var tests = new Dictionary<string, (CollectedTest Test, string? SetupFile)>(StringComparer.Ordinal);
        foreach (
            var (test, options, disposition) in selection.Pairs.Select(pair =>
                (pair.Test, pair.Options, pair.Disposition)
            )
        )
        {
            if (test.Kind != TestKind.Test || disposition != Disposition.Run || tests.ContainsKey(test.Id))
            {
                continue;
            }

            tests[test.Id] = (test, options.Session is { } session ? producers.GetValueOrDefault(session) : null);
        }

        return
        [
            .. tests
                .Values.OrderBy(entry => entry.Test, Comparer<CollectedTest>.Create(BuildReport.CompareTests))
                .Select(entry => new TestFilterCandidate(
                    entry.Test.Id,
                    entry.Test.File,
                    [.. entry.Test.TitlePath],
                    [.. entry.Test.Tags],
                    entry.SetupFile
                )),
        ];
    }

    /// <summary>
    /// The loaded config as <c>Changes.Files</c> spells it, relative to the repository root: its directory resolved on disk, its own name
    /// kept, since git lists a symlinked config under the link's path. Null outside the repository, where no changed file can name it
    /// (upstream's <c>repoConfigFile</c>).
    /// </summary>
    public static string? RepoConfigFile(string configPath, string root)
    {
        var relative = Path.GetRelativePath(
            root,
            Path.Combine(
                Paths.OnDiskPath(Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? ""),
                Path.GetFileName(configPath)
            )
        );
        if (
            relative.Length == 0
            || relative == "."
            || Path.IsPathRooted(relative)
            || relative.Split(Path.DirectorySeparatorChar)[0] == ".."
        )
        {
            return null;
        }

        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    /// <summary>
    /// Calls each filter in order on the tests the ones before it kept, so the result is their intersection. A filter whose input is empty
    /// is not called. A filter that throws, answers with something invalid, or runs past <c>scope.Timeout</c> leaves nothing out: the run
    /// warns and the next filter is called on every test it was given. A failed filter scores nothing either, except that a
    /// <see cref="TestFilterException"/> it throws may carry <c>Relevance</c>, checked as a result's is; nothing a call throws past its
    /// deadline is read. A test several filters scored keeps its highest score. Reasons, notes, log lines, and what a refused answer quotes are
    /// redacted and cut before anything prints or records them.
    /// </summary>
    public static async Task<AppliedTestFilters> ApplyTestFilters(
        IReadOnlyList<ITestFilter> filters,
        IReadOnlyList<TestFilterCandidate> candidates,
        TestFilterScope scope
    )
    {
        var excluded = new Dictionary<string, TestExclusion>(StringComparer.Ordinal);
        var order = new List<string>();
        var relevance = new OrderedValues<double>();
        var decisions = new List<FilterDecision>();
        var remaining = candidates;
        foreach (var filter in filters)
        {
            var name = filter.Name;
            if (remaining.Count == 0)
            {
                scope.Notice($"{name}: no tests left to judge");
                continue;
            }

            IReadOnlyList<TestFilterCandidate> input = [.. remaining];
            var call = await CallFilter(filter, input, scope).ConfigureAwait(false);
            foreach (var (id, score) in call.Outcome is null ? call.Result!.Relevance : call.Relevance)
            {
                relevance.Set(id, relevance.TryGetValue(id, out var best) ? Math.Max(best, score) : score);
            }

            if (call.Outcome is { } outcome)
            {
                // Said now, not with the summaries: an interrupt during a later filter would drop them.
                scope.Notice($"warning: {name} {call.Note}");
                decisions.Add(new FilterDecision(name, input.Count, call.Note, outcome));
                continue;
            }

            var (exclude, _, note) = call.Result!;
            foreach (var (id, reason) in exclude)
            {
                if (!excluded.ContainsKey(id))
                {
                    order.Add(id);
                }

                excluded[id] = new TestExclusion(name, reason);
            }

            decisions.Add(new FilterDecision(name, input.Count, note));
            remaining = [.. remaining.Where(candidate => !excluded.ContainsKey(candidate.Id))];
        }

        return new AppliedTestFilters(new OrderedValues<TestExclusion>(order, excluded), relevance, decisions);
    }

    /// <summary>
    /// Calls one filter on <paramref name="input"/> inside its step, which checks the answer too, so a refused one ends the step failed as a
    /// throw does. Past <c>scope.Timeout</c> the call is abandoned with its token cancelled. An interrupt abandons the call and surfaces as a
    /// cancellation, whatever the filter does next. The filter's <c>Log</c> does nothing once the call has ended.
    /// </summary>
    private static async Task<FilterCall> CallFilter(
        ITestFilter filter,
        IReadOnlyList<TestFilterCandidate> input,
        TestFilterScope scope
    )
    {
        var name = filter.Name;
        var redactor = Redactor.For(scope.Secrets);
        if (scope.CancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(
                $"interrupted while filter {JsonQuote.Of(name)} ran",
                scope.CancellationToken
            );
        }

        var budget = Time.BudgetText((long)scope.Timeout.TotalMilliseconds);
        var live = true;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(scope.CancellationToken);
        deadline.CancelAfter(scope.Timeout);
        var signal = deadline.Token;
        var context = new TestFilterContext
        {
            ProjectRoot = scope.ProjectRoot,
            Changes = scope.Changes,
            ConfigFile = scope.ConfigFile,
            Signal = signal,
            Log = line =>
            {
                if (Volatile.Read(ref live))
                {
                    scope.Notice($"{name}: {Cut(redactor.Redact(line))}");
                }
            },
            Redact = text => redactor.RedactFragments(text),
        };

        // A call that leaves nothing out, its note ending in the detail, so a long one is what the cut shortens, and scoring what `thrown`
        // kept; why `thrown` dropped any is said before it, cut to `MaxDropped`.
        FilterCall Failed(string problem, Exception detail, ThrownScores? thrown = null)
        {
            var ignored = thrown?.Dropped is null
                ? ""
                : $", ignoring its relevance ({ReportFormat.Ellipsize(TextRules.Collapse(redactor.Redact(thrown.Dropped)), MaxDropped)})";
            return new FilterCall
            {
                Outcome = FilterOutcome.Failed,
                Note = Cut($"{problem}{ignored}, so it leaves nothing out: {redactor.Redact(ErrorMessage(detail))}"),
                Relevance = thrown?.Scores ?? [],
            };
        }

        try
        {
            return await scope
                .Step(name, async () =>
                {
                    TestFilterResult? raw;
                    try
                    {
                        raw = await WithAbort(Task.Run(() => filter.FilterAsync(input, context)), signal)
                            .ConfigureAwait(false);
                    }
                    catch (Exception cause)
                    {
                        if (scope.CancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }

                        return deadline.IsCancellationRequested
                            ? new FilterCall
                            {
                                Outcome = FilterOutcome.TimedOut,
                                Note = Cut($"did not finish within {budget}, so it leaves nothing out"),
                            }
                            : Failed(
                                "failed",
                                cause,
                                ThrownScoresOf(cause, input, scope.CollectedIds, redactor.Redact)
                            );
                    }

                    try
                    {
                        return new FilterCall
                        {
                            Result = CheckedResultOf(raw, input, scope.CollectedIds, redactor.Redact),
                        };
                    }
                    catch (Exception cause) when (cause is not OperationCanceledException)
                    {
                        return Failed("returned an invalid result", cause);
                    }
                })
                .ConfigureAwait(false);
        }
        catch (Exception) when (scope.CancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(
                $"interrupted while filter {JsonQuote.Of(name)} ran",
                scope.CancellationToken
            );
        }
        finally
        {
            Volatile.Write(ref live, false);
        }
    }

    /// <summary>The task's result, or a cancellation as soon as <paramref name="token"/> is: a filter that ignores its signal is abandoned, not awaited.</summary>
    private static async Task<T> WithAbort<T>(Task<T> task, CancellationToken token)
    {
        // Whatever the abandoned call throws later is nobody's to see.
        _ = task.ContinueWith(
            finished => finished.Exception,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously
        );
        var cancelled = new TaskCompletionSource();
        using var registration = token.Register(() => cancelled.TrySetResult());
        if (await Task.WhenAny(task, cancelled.Task).ConfigureAwait(false) != task)
        {
            throw new OperationCanceledException(token);
        }

        return await task.ConfigureAwait(false);
    }

    /// <summary>
    /// The report's record of each filter that ran. <c>Excluded</c> counts the distinct tests the final selection leaves out on the
    /// filter's word, so a test it named that came back with its serial group is not counted (upstream's <c>filterRecords</c>, which reads a
    /// <c>Selection</c>; the port reads the skip of each result).
    /// </summary>
    public static IReadOnlyList<ReportFilter> FilterRecords(
        IReadOnlyList<FilterDecision> decisions,
        IEnumerable<(string TestId, ReportSkip? Skip)> results
    )
    {
        var leftOut = LeftOut.LeftOutTests(results.Select(result => (result.TestId, result.Skip?.Filter)));
        return
        [
            .. decisions.Select(decision => new ReportFilter(
                decision.Name,
                decision.Candidates,
                leftOut.GetValueOrDefault(decision.Name),
                decision.Note,
                decision.Outcome
            )),
        ];
    }

    /// <summary>
    /// One filter's summary line for the run notices: what it kept and left out of what it was given, in tests, and its note. A filter that
    /// failed or outran the deadline has none: <c>ApplyTestFilters</c> warned about it as the call ended.
    /// </summary>
    public static string? FilterSummary(ReportFilter record)
    {
        if (record.Outcome is not null)
        {
            return null;
        }

        var note = record.Note is null ? "" : $" ({record.Note})";
        return $"{record.Name} {ReportFormat.FilterDecision(record.Candidates, record.Excluded)}{note}";
    }

    /// <summary>The text as one line of at most 1024 code points, the last of them an ellipsis when it was longer.</summary>
    private static string Cut(string text) => ReportFormat.Ellipsize(TextRules.Collapse(text), MaxFilterText);

    /// <summary>A key or test id a filter chose, as an error quotes it: redacted, then at most 200 code points.</summary>
    private static string Quoted(string text, Func<string, string> redact) =>
        JsonQuote.Of(ReportFormat.Ellipsize(redact(text), MaxQuoted));

    /// <summary>A value as a refusal names it (upstream's <c>describeValue</c>).</summary>
    private static string Describe(string? value) => value is null ? "null" : JsonQuote.Of(value);

    /// <summary>What <paramref name="error"/> says, as upstream's <c>errorMessage</c> reads a thrown value.</summary>
    private static string ErrorMessage(Exception error) =>
        string.IsNullOrEmpty(error.Message) ? error.GetType().Name : error.Message;

    /// <summary>
    /// Checks a filter's answer: a <see cref="TestFilterResult"/>, every excluded id a collected test with a non-empty reason. An id of a
    /// collected test the filter was not given, such as a setup test or one <c>--shard</c> or an earlier filter left out, is ignored: that
    /// test's fate is settled. Anything else is refused rather than guessed at, and the filter leaves nothing out.
    /// </summary>
    private static CheckedResult CheckedResultOf(
        TestFilterResult? raw,
        IReadOnlyList<TestFilterCandidate> input,
        IReadOnlySet<string> collectedIds,
        Func<string, string> redact
    )
    {
        if (raw is null)
        {
            throw new InvalidOperationException("expected a TestFilterResult, got null");
        }

        if (raw.Exclude is null)
        {
            throw new InvalidOperationException("exclude must be a dictionary of test ids to reasons, got null");
        }

        var ids = input.Select(candidate => candidate.Id).ToHashSet(StringComparer.Ordinal);
        var entries = raw.Exclude.ToList();
        var stranger = entries.FirstOrDefault(entry => !ids.Contains(entry.Key) && !collectedIds.Contains(entry.Key));
        if (stranger.Key is not null)
        {
            throw new InvalidOperationException(
                $"exclude names {Quoted(stranger.Key, redact)}, which no collected test has; exclude takes test.id"
            );
        }

        var unexplained = entries.FirstOrDefault(entry => string.IsNullOrWhiteSpace(entry.Value));
        if (unexplained.Key is not null)
        {
            throw new InvalidOperationException(
                $"the reason for {Quoted(unexplained.Key, redact)} must be a non-empty string, got {Describe(unexplained.Value)}"
            );
        }

        var scores = CheckedRelevance(raw.Relevance, ids, collectedIds, redact);
        var kept = Cut(redact(raw.Note ?? ""));
        return new CheckedResult(
            [
                .. entries
                    .Where(entry => ids.Contains(entry.Key))
                    .Select(entry => KeyValuePair.Create(entry.Key, Cut(redact(entry.Value)))),
            ],
            scores,
            kept.Length == 0 ? null : kept
        );
    }

    /// <summary>
    /// The scores a filter's <c>Relevance</c> gives the tests it was handed, each a number from 0 to 1 by the id of a collected test; none when
    /// it has no <c>Relevance</c>. Scores for a collected test it was not handed are dropped, as <c>Exclude</c> drops them (upstream's
    /// <c>checkedRelevance</c>). Partial: the type rules out a value that is not a number and a relevance that is not a dictionary.
    /// </summary>
    private static List<KeyValuePair<string, double>> CheckedRelevance(
        IReadOnlyDictionary<string, double>? relevance,
        IReadOnlySet<string> ids,
        IReadOnlySet<string> collectedIds,
        Func<string, string> redact
    )
    {
        if (relevance is null)
        {
            return [];
        }

        var entries = relevance.ToList();
        var stranger = entries.FirstOrDefault(entry => !ids.Contains(entry.Key) && !collectedIds.Contains(entry.Key));
        if (stranger.Key is not null)
        {
            throw new InvalidOperationException(
                $"relevance names {Quoted(stranger.Key, redact)}, which no collected test has; relevance takes test.id"
            );
        }

        var unscored = entries.FirstOrDefault(entry => !(entry.Value >= 0 && entry.Value <= 1));
        if (unscored.Key is not null)
        {
            throw new InvalidOperationException(
                $"the relevance of {Quoted(unscored.Key, redact)} must be a number from 0 to 1, got {DescribeNumber(unscored.Value)}"
            );
        }

        return [.. entries.Where(entry => ids.Contains(entry.Key))];
    }

    /// <summary>
    /// The scores in the <c>Relevance</c> of what a filter threw, checked as a result's are: a filter that fails can still know how likely some
    /// tests are to fail, such as the ones the change touches, and the run starts those first. None when it carries none; none, with the
    /// reason, when they fail the check or cannot be read, since the failure is what the warning reports (upstream's <c>thrownScores</c>).
    /// </summary>
    private static ThrownScores ThrownScoresOf(
        Exception cause,
        IReadOnlyList<TestFilterCandidate> input,
        IReadOnlySet<string> collectedIds,
        Func<string, string> redact
    )
    {
        try
        {
            var relevance = (cause as TestFilterException)?.Relevance;
            return new ThrownScores(
                CheckedRelevance(
                    relevance,
                    input.Select(candidate => candidate.Id).ToHashSet(StringComparer.Ordinal),
                    collectedIds,
                    redact
                )
            );
        }
        catch (Exception problem)
        {
            return new ThrownScores([], ErrorMessage(problem));
        }
    }

    /// <summary>A score as a refusal names it, as JavaScript spells a number: <c>NaN</c>, <c>Infinity</c>, <c>-0.01</c>.</summary>
    private static string DescribeNumber(double value) =>
        double.IsNaN(value) ? "NaN"
        : double.IsPositiveInfinity(value) ? "Infinity"
        : double.IsNegativeInfinity(value) ? "-Infinity"
        : value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A dictionary that enumerates in the order its keys were first set, as a JavaScript <c>Map</c> does.</summary>
    private sealed class OrderedValues<TValue> : IReadOnlyDictionary<string, TValue>
    {
        private readonly List<string> _order;
        private readonly Dictionary<string, TValue> _values;

        public OrderedValues()
            : this([], new Dictionary<string, TValue>(StringComparer.Ordinal)) { }

        public OrderedValues(List<string> order, Dictionary<string, TValue> values)
        {
            _order = order;
            _values = values;
        }

        public IEnumerable<string> Keys => _order;

        public IEnumerable<TValue> Values => _order.Select(key => _values[key]);

        public int Count => _order.Count;

        public TValue this[string key] => _values[key];

        /// <summary>Sets a key, which keeps its place when it was set before.</summary>
        public void Set(string key, TValue value)
        {
            if (!_values.ContainsKey(key))
            {
                _order.Add(key);
            }

            _values[key] = value;
        }

        public bool ContainsKey(string key) => _values.ContainsKey(key);

        public bool TryGetValue(string key, out TValue value) => _values.TryGetValue(key, out value!);

        public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator() =>
            _order.Select(key => KeyValuePair.Create(key, _values[key])).GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
