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
internal sealed record AppliedTestFilters(IReadOnlyDictionary<string, TestExclusion> Excluded, IReadOnlyList<FilterDecision> Decisions);

/// <summary>How one filter call ended (upstream's <c>FilterCall</c>): with its answer, checked; or thrown, refused, or abandoned past the deadline, with the note that says so.</summary>
internal sealed record FilterCall
{
    /// <summary>How the call ended; <c>null</c> when it answered.</summary>
    public FilterOutcome? Outcome { get; init; }

    /// <summary>The checked answer, when the call answered.</summary>
    public CheckedResult? Result { get; init; }

    /// <summary>The note that says why the call did not answer.</summary>
    public string? Note { get; init; }
}

/// <summary>A filter's answer once checked: what it left out, with each reason, and its note, redacted and cut; no note when it left none (upstream's <c>CheckedResult</c>).</summary>
internal sealed record CheckedResult(IReadOnlyList<KeyValuePair<string, string>> Exclude, string? Note);

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

/// <summary>Test filters: what they are given, calling them, and what they decided (upstream's <c>run/test-filters.ts</c>).</summary>
internal static class TestFilters
{
    /// <summary>How long a filter call may take before the run stops waiting for it (upstream's <c>FILTER_TIMEOUT_MS</c>).</summary>
    public static readonly TimeSpan FilterTimeout = TimeSpan.FromMinutes(5);

    /// <summary>The most code points a stored reason, note, or log line keeps, on one line.</summary>
    private const int MaxFilterText = 1024;

    /// <summary>The most code points an error quotes of a key or test id a filter chose.</summary>
    private const int MaxQuoted = 200;

    /// <summary>
    /// The loaded config as <c>Changes.Files</c> spells it, relative to the repository root: its directory resolved on disk, its own name
    /// kept, since git lists a symlinked config under the link's path. Null outside the repository, where no changed file can name it
    /// (upstream's <c>repoConfigFile</c>).
    /// </summary>
    public static string? RepoConfigFile(string configPath, string root)
    {
        var relative = Path.GetRelativePath(root, Path.Combine(Paths.OnDiskPath(Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? ""), Path.GetFileName(configPath)));
        if (relative.Length == 0 || relative == "." || Path.IsPathRooted(relative) || relative.Split(Path.DirectorySeparatorChar)[0] == "..")
        {
            return null;
        }

        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    /// <summary>
    /// Calls each filter in order on the tests the ones before it kept, so the result is their intersection. A filter whose input is empty
    /// is not called. A filter that throws, answers with something invalid, or runs past <c>scope.Timeout</c> leaves nothing out: the run
    /// warns and the next filter is called on every test it was given. Reasons, notes, log lines, and what a refused answer quotes are
    /// redacted and cut before anything prints or records them.
    /// </summary>
    public static async Task<AppliedTestFilters> ApplyTestFilters(IReadOnlyList<ITestFilter> filters, IReadOnlyList<TestFilterCandidate> candidates, TestFilterScope scope)
    {
        var excluded = new Dictionary<string, TestExclusion>(StringComparer.Ordinal);
        var order = new List<string>();
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
            if (call.Outcome is { } outcome)
            {
                // Said now, not with the summaries: an interrupt during a later filter would drop them.
                scope.Notice($"warning: {name} {call.Note}");
                decisions.Add(new FilterDecision(name, input.Count, call.Note, outcome));
                continue;
            }

            var (exclude, note) = call.Result!;
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

        return new AppliedTestFilters(new OrderedExclusions(order, excluded), decisions);
    }

    /// <summary>
    /// Calls one filter on <paramref name="input"/> inside its step, which checks the answer too, so a refused one ends the step failed as a
    /// throw does. Past <c>scope.Timeout</c> the call is abandoned with its token cancelled. An interrupt abandons the call and surfaces as a
    /// cancellation, whatever the filter does next. The filter's <c>Log</c> does nothing once the call has ended.
    /// </summary>
    private static async Task<FilterCall> CallFilter(ITestFilter filter, IReadOnlyList<TestFilterCandidate> input, TestFilterScope scope)
    {
        var name = filter.Name;
        var redactor = Redactor.For(scope.Secrets);
        if (scope.CancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException($"interrupted while filter {JsonQuote.Of(name)} ran", scope.CancellationToken);
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

        // A call that leaves nothing out, its note ending in the detail, so a long one is what the cut shortens.
        FilterCall Failed(string problem, Exception detail) =>
            new() { Outcome = FilterOutcome.Failed, Note = Cut($"{problem}, so it leaves nothing out: {redactor.Redact(ErrorMessage(detail))}") };

        try
        {
            return await scope.Step(name, async () =>
            {
                TestFilterResult? raw;
                try
                {
                    raw = await WithAbort(Task.Run(() => filter.FilterAsync(input, context)), signal).ConfigureAwait(false);
                }
                catch (Exception cause)
                {
                    if (scope.CancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }

                    return deadline.IsCancellationRequested
                        ? new FilterCall { Outcome = FilterOutcome.TimedOut, Note = Cut($"did not finish within {budget}, so it leaves nothing out") }
                        : Failed("failed", cause);
                }

                try
                {
                    return new FilterCall { Result = CheckedResultOf(raw, input, scope.CollectedIds, redactor.Redact) };
                }
                catch (Exception cause) when (cause is not OperationCanceledException)
                {
                    return Failed("returned an invalid result", cause);
                }
            }).ConfigureAwait(false);
        }
        catch (Exception) when (scope.CancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException($"interrupted while filter {JsonQuote.Of(name)} ran", scope.CancellationToken);
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
        _ = task.ContinueWith(finished => finished.Exception, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
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
    public static IReadOnlyList<ReportFilter> FilterRecords(IReadOnlyList<FilterDecision> decisions, IEnumerable<(string TestId, ReportSkip? Skip)> results)
    {
        var leftOut = LeftOut.LeftOutTests(results);
        return [.. decisions.Select(decision => new ReportFilter(decision.Name, decision.Candidates, leftOut.GetValueOrDefault(decision.Name), decision.Note, decision.Outcome))];
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
    private static string Quoted(string text, Func<string, string> redact) => JsonQuote.Of(ReportFormat.Ellipsize(redact(text), MaxQuoted));

    /// <summary>A value as a refusal names it (upstream's <c>describeValue</c>).</summary>
    private static string Describe(string? value) => value is null ? "null" : JsonQuote.Of(value);

    /// <summary>What <paramref name="error"/> says, as upstream's <c>errorMessage</c> reads a thrown value.</summary>
    private static string ErrorMessage(Exception error) => string.IsNullOrEmpty(error.Message) ? error.GetType().Name : error.Message;

    /// <summary>
    /// Checks a filter's answer: a <see cref="TestFilterResult"/>, every excluded id a collected test with a non-empty reason. An id of a
    /// collected test the filter was not given, such as a setup test or one <c>--shard</c> or an earlier filter left out, is ignored: that
    /// test's fate is settled. Anything else is refused rather than guessed at, and the filter leaves nothing out.
    /// </summary>
    private static CheckedResult CheckedResultOf(TestFilterResult? raw, IReadOnlyList<TestFilterCandidate> input, IReadOnlySet<string> collectedIds, Func<string, string> redact)
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
            throw new InvalidOperationException($"exclude names {Quoted(stranger.Key, redact)}, which no collected test has; exclude takes test.id");
        }

        var unexplained = entries.FirstOrDefault(entry => string.IsNullOrWhiteSpace(entry.Value));
        if (unexplained.Key is not null)
        {
            throw new InvalidOperationException($"the reason for {Quoted(unexplained.Key, redact)} must be a non-empty string, got {Describe(unexplained.Value)}");
        }

        var kept = Cut(redact(raw.Note ?? ""));
        return new CheckedResult(
            [.. entries.Where(entry => ids.Contains(entry.Key)).Select(entry => KeyValuePair.Create(entry.Key, Cut(redact(entry.Value))))],
            kept.Length == 0 ? null : kept);
    }

    /// <summary>A dictionary that enumerates in the order its keys were first set, as a JavaScript <c>Map</c> does.</summary>
    private sealed class OrderedExclusions(List<string> order, Dictionary<string, TestExclusion> values) : IReadOnlyDictionary<string, TestExclusion>
    {
        public IEnumerable<string> Keys => order;

        public IEnumerable<TestExclusion> Values => order.Select(key => values[key]);

        public int Count => order.Count;

        public TestExclusion this[string key] => values[key];

        public bool ContainsKey(string key) => values.ContainsKey(key);

        public bool TryGetValue(string key, out TestExclusion value) => values.TryGetValue(key, out value!);

        public IEnumerator<KeyValuePair<string, TestExclusion>> GetEnumerator() => order.Select(key => KeyValuePair.Create(key, values[key])).GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

/// <summary>How many tests a run's test filters left out, counted one way everywhere (upstream's <c>report/left-out.ts</c>).</summary>
internal static class LeftOut
{
    /// <summary>How many tests each test filter left out of <paramref name="results"/>, by its name; a test left out on several targets, agents, or repeats counts once.</summary>
    public static Dictionary<string, int> LeftOutTests(IEnumerable<(string TestId, ReportSkip? Skip)> results)
    {
        var tests = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var (testId, skip) in results)
        {
            if (skip?.Filter is { } filter)
            {
                if (!tests.TryGetValue(filter, out var ids))
                {
                    tests[filter] = ids = new HashSet<string>(StringComparer.Ordinal);
                }

                ids.Add(testId);
            }
        }

        return tests.ToDictionary(pair => pair.Key, pair => pair.Value.Count, StringComparer.Ordinal);
    }
}
