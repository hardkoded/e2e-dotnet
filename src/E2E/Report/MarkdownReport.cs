// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Internal;
using static E2E.FailureText;
using static E2E.MarkdownText;

namespace E2E;

/// <summary>What a markdown page links to and how it names itself (upstream's <c>MarkdownReportOptions</c>).</summary>
internal sealed record MarkdownReportOptions
{
    /// <summary>Where the run's artifacts can be fetched: the workflow run page, or a hosted run page. Evidence links there.</summary>
    public string? ArtifactsUrl { get; init; }

    /// <summary>
    /// The directory the report's artifact paths are relative to, as the reader should see it (<c>.e2e/results</c> for a file the project
    /// root is read from). Without <see cref="ArtifactsUrl"/>, evidence is listed as paths under it.
    /// </summary>
    public string? ArtifactsDir { get; init; }

    /// <summary>A link to a source line, when the commit is known.</summary>
    public Func<string, int, string>? SourceUrl { get; init; }

    /// <summary>Where each traced result's page is, by result id, as the reader should see the path; the block links there.</summary>
    public IReadOnlyDictionary<string, string>? Traces { get; init; }

    /// <summary>Tells this run's page apart from another's on the same pull request in the headline: <c>e2e regression: 77 passed</c>.</summary>
    public string? Title { get; init; }
}

/// <summary>
/// The report-1 document as one markdown page, laid out for a pull request: a headline with the counts and what the run spent, the
/// run-level errors, one block per test that failed with the step it went wrong at and the agent's last word, the flaky tests folded
/// away with the same block each, what the run's test filters left out, folded away with each reason, and every test folded away under
/// that as one table, a row per file with its counts and time above the rows of its tests. A green run is the headline, the folds, and
/// the footer: nothing to read unless the reader opens it. An <c>e2e explore</c> run renders its record instead: the goal, every
/// finding with its evidence, and the assessment. Upstream's <c>report/markdown.ts</c>.
/// Everything a test or the agent wrote (titles, labels, error messages, findings) is untrusted text on its way into markdown a
/// renderer trusts, so it is escaped and clipped, and the whole body never outgrows what GitHub accepts. The port has no reporter
/// extension point, so upstream's <c>markdownReporter</c> (the <c>summary.md</c> writer) is not here.
/// </summary>
internal static class MarkdownReport
{
    /// <summary>GitHub rejects a comment body past 65536 characters. The page stays clear of it with room for what a poster prepends (a marker comment).</summary>
    private const int MaxBodyChars = 60_000;

    // Reader caps, not size guards: past these the page says how many more there are.
    private const int MaxFailureBlocks = 30;
    private const int MaxRunErrors = 20;
    private const int MaxListedTests = 400;
    private const int MaxFooterTargets = 8;
    private const int MaxFindings = 30;
    private const int MaxEvidencePaths = 4;
    private const int MaxAssessmentChars = 2_000;
    private const int MaxGoalChars = 400;
    private const int MaxLeftOutRows = 50;
    private const string SiteUrl = "https://e2e.tester.army";
    private const string TruncatedNote = "_Truncated to fit a pull request comment; the full report is in `report.json`._";

    /// <summary>A result with what it left behind, read once.</summary>
    private sealed record Entry(ReportResult Result, Outcome Final);

    /// <summary>A result's file and target, with the entries of the tests in it.</summary>
    private sealed record FileGroup(string File, string Target, IReadOnlyList<Entry> Entries);

    /// <summary>Screenshots first, then the recording; a kind not listed reads last.</summary>
    private static int KindRank(string kind) => kind switch
    {
        "screenshot" => 0,
        "video" => 1,
        "download" => 2,
        "log" => 3,
        _ => 4,
    };

    private static string Icon(ResultBucket bucket) => bucket switch
    {
        ResultBucket.Failed => "🔴",
        ResultBucket.Interrupted => "⏹️",
        ResultBucket.Flaky => "⚠️",
        ResultBucket.Skipped => "⏭️",
        _ => "🟢",
    };

    /// <summary>Worst first: the order failures are listed, files are sorted, and a file's glyph is chosen in.</summary>
    private static readonly ResultBucket[] WorstFirst = [ResultBucket.Failed, ResultBucket.Interrupted, ResultBucket.Flaky, ResultBucket.Skipped, ResultBucket.Passed];

    /// <summary>The order counts read in: <c>1 failed, 2 interrupted, 1 flaky, 3 passed, 1 skipped</c>.</summary>
    private static readonly ResultBucket[] CountOrder = [ResultBucket.Failed, ResultBucket.Interrupted, ResultBucket.Flaky, ResultBucket.Passed, ResultBucket.Skipped];

    private static ResultBucket WorstBucket(IEnumerable<ReportResult> results)
    {
        var present = results.Select(result => ReportFormat.StatusBucket(result.Status)).ToHashSet();
        return WorstFirst.FirstOrDefault(present.Contains, ResultBucket.Passed);
    }

    /// <summary><c>1 failed, 3 passed</c>, zero counts left out.</summary>
    private static string CountText(Dictionary<ResultBucket, int> counts) =>
        string.Join(", ", CountOrder.Where(key => counts[key] > 0).Select(key => counts[key].ToString(CultureInfo.InvariantCulture) + " " + key.ToString().ToLowerInvariant()));

    /// <summary><c>e2e regression</c>, or <c>e2e</c> when the page has no title.</summary>
    private static string PageName(MarkdownReportOptions options)
    {
        var title = options.Title?.Trim() ?? "";
        return title.Length == 0 ? "e2e" : "e2e " + Cell(title, MaxIdChars);
    }

    /// <summary>
    /// The headline's glyph: red for a failed run or any failed test, the interrupted glyph for a run or a listed test the run stopped,
    /// else green. A <c>--last-failed</c> rerun that passed can still list a test the first pass never finished, and the page must not
    /// read as all green. A run without a status (one only a page reads) counts as passed: its results decide.
    /// </summary>
    private static string RunIcon(ReportRun run, IReadOnlyList<ReportResult> results)
    {
        var status = run.Status ?? ReportRunStatus.Passed;
        if (status is not (ReportRunStatus.Passed or ReportRunStatus.Interrupted))
        {
            return Icon(ResultBucket.Failed);
        }

        var worst = WorstBucket(results);
        if (worst == ResultBucket.Failed)
        {
            return Icon(ResultBucket.Failed);
        }

        return worst == ResultBucket.Interrupted || status == ReportRunStatus.Interrupted ? Icon(ResultBucket.Interrupted) : Icon(ResultBucket.Passed);
    }

    /// <summary>The headline: the counts, and how many tests the test filters left out, so a filtered pull request run never reads as the whole suite passing.</summary>
    private static string Headline(ReportRun run, IReadOnlyList<ReportResult> results, Dictionary<string, int> counts, MarkdownReportOptions options)
    {
        var counted = CountText(ReportFormat.Tally(results.Select(result => result.Status)));
        var summary = counted.Length > 0 ? counted : run.Errors.Count > 0 ? "no tests ran" : "no tests selected";
        var leftOut = TotalLeftOut(counts);
        return "### " + RunIcon(run, results) + " " + PageName(options) + ": " + summary + (leftOut == 0 ? "" : ", " + leftOut.ToString(CultureInfo.InvariantCulture) + " left out by filters");
    }

    /// <summary>How many tests the test filters left out in all, from <c>LeftOutTests</c>' counts: a test is left out by one filter.</summary>
    private static int TotalLeftOut(Dictionary<string, int> counts) => counts.Values.Sum();

    /// <summary><c>`affected` kept 7 of 42 tests, left out 35 (note)</c>: what one filter decided, its tests left out from <paramref name="counts"/>.</summary>
    private static string FilterLine(ReportFilter record, Dictionary<string, int> counts)
    {
        var note = record.Note is null ? "" : " (" + Cell(record.Note) + ")";
        return "**Filter** " + Code(record.Name, MaxIdChars) + " " + ReportFormat.FilterDecision(record.Candidates, counts.GetValueOrDefault(record.Name)) + note;
    }

    /// <summary>What the filters judged (<c>12 files changed since abc1234</c>), or why no filter ran. Nothing for a run without <c>--changed-since</c>.</summary>
    private static string? ChangesLine(ReportChanges? changes)
    {
        if (changes is null)
        {
            return null;
        }

        if (changes.Base is null || changes.Skipped is not null)
        {
            return "**Filters** none ran: " + Cell(changes.Skipped ?? "");
        }

        return "**Change** " + Plural(changes.Files ?? 0, "file") + " changed since " + Code(ReportFormat.ShortCommit(changes.Base));
    }

    /// <summary>
    /// What the run's test filters decided: the change they judged, a line per filter, then what they left out folded away as one table
    /// with each reason, capped, the target named when there is more than one. Every count of tests left out comes from
    /// <paramref name="counts"/>, read off the results. Nothing when the run named no filter.
    /// </summary>
    private static List<string> FiltersSection(ReportRun run, bool manyTargets, Dictionary<string, int> counts)
    {
        var leftOut = run.Results.Where(result => result.Skip?.Filter is not null).ToList();
        var withTarget = manyTargets || leftOut.Select(result => result.TargetId).Distinct(StringComparer.Ordinal).Count() > 1;
        var changes = ChangesLine(run.Changes);
        List<string> said = [.. changes is null ? [] : new[] { changes }, .. run.Filters.Select(record => FilterLine(record, counts))];
        List<string> parts = said.Count == 0 ? [] : [string.Join("  \n", said)];
        if (leftOut.Count == 0)
        {
            return parts;
        }

        List<string> lines = [Row(["Test", "Filter", "Reason"]), Row(["---", "---", "---"])];
        foreach (var result in leftOut.Take(MaxLeftOutRows))
        {
            lines.Add(Row([FileLabel(result.File, result.TargetId, withTarget) + " › " + TestName(result), Code(result.Skip?.Filter ?? "", MaxIdChars), Cell(result.Skip?.Reason ?? "")]));
        }

        if (leftOut.Count > MaxLeftOutRows)
        {
            lines.Add(Row(["and " + (leftOut.Count - MaxLeftOutRows).ToString(CultureInfo.InvariantCulture) + " more in report.json", "", ""]));
        }

        var summary = Plural(TotalLeftOut(counts), "test") + " left out by filters";
        return [.. parts, .. parts.Count == 0 ? [] : new[] { "" }, string.Join('\n', "<details>", "<summary>" + summary + "</summary>", "", string.Join('\n', lines), "</details>")];
    }

    /// <summary>The model calls an agent step made; the step's metrics are the source of truth.</summary>
    private static int ModelCalls(StepRecord step) => step.Metrics?.ModelCalls ?? 0;

    /// <summary>What the agent did over a set of steps.</summary>
    private static (int Steps, int Replayed, int Calls) AgentTotals(IEnumerable<StepRecord> steps)
    {
        var agent = steps.Where(step => step.Kind == StepKind.Agent).ToList();
        return (agent.Count, agent.Count(step => step.Cache?.Mode == "self-finalized"), agent.Sum(ModelCalls));
    }

    /// <summary>
    /// <c>41 agent steps · 12 replayed from cache · 128.4k tokens (62% cached) · $0.31</c>, under the headline of a run that used the
    /// agent; a deterministic run has nothing to say here.
    /// </summary>
    private static string? SpendLine(ReportRun run, IReadOnlyList<Entry> entries)
    {
        var totals = AgentTotals(entries.SelectMany(entry => entry.Final.Final.Steps));
        var tokens = run.Usage?.ModelTokens ?? 0;
        if (totals.Steps == 0 && tokens == 0)
        {
            return null;
        }

        List<string> parts = [Plural(totals.Steps, "agent step")];
        if (totals.Replayed > 0)
        {
            parts.Add(totals.Replayed.ToString(CultureInfo.InvariantCulture) + " replayed from cache");
        }

        if (totals.Calls > 0)
        {
            parts.Add(Plural(totals.Calls, "model call"));
        }

        if (tokens > 0)
        {
            var cached = run.Usage?.ModelCachedTokens ?? 0;
            var share = cached == 0 ? "" : " (" + Math.Round((double)cached / tokens * 100, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + "% cached)";
            parts.Add(ReportFormat.FormatTokens(tokens) + " tokens" + share);
        }

        if (run.Usage?.EstimatedCostUsd is { } cost)
        {
            parts.Add(ReportFormat.FormatCost(cost));
        }

        return string.Join(" · ", parts);
    }

    /// <summary><c>screenshot `.e2e/results/&lt;test&gt;/attempt-1/screenshots/001-failure.png`</c>: each file as the reader finds it under <paramref name="dir"/>, capped.</summary>
    private static List<string> EvidencePaths(IReadOnlyList<ReportArtifact> sorted, string dir)
    {
        var files = sorted.Where(artifact => artifact.Path is not null).Select(artifact => (Artifact: artifact, File: PosixJoin(dir, artifact.Path!))).ToList();
        var shown = files.Take(MaxEvidencePaths).Select(item => item.Artifact.Kind + " " + Code(item.File)).ToList();
        if (files.Count > shown.Count)
        {
            shown.Add("and " + (files.Count - shown.Count).ToString(CultureInfo.InvariantCulture) + " more");
        }

        return shown;
    }

    /// <summary>The artifact path under the directory the reader has the files in; the directory <c>.</c> is the path itself (<c>path.posix.join</c>).</summary>
    private static string PosixJoin(string dir, string artifactPath) => dir is "." or "" ? artifactPath : dir.TrimEnd('/') + "/" + artifactPath;

    /// <summary>
    /// Where the evidence is: each kind linked to the run's artifacts when there is a URL (the upload keeps one directory per test, named
    /// after it, so the reader finds the files without their paths spelled out); listed as paths under <c>ArtifactsDir</c> when the
    /// reader has the files; and named by kind otherwise. Paths are POSIX, as the report keeps them.
    /// </summary>
    private static string Evidence(IReadOnlyList<ReportArtifact> artifacts, MarkdownReportOptions options)
    {
        // A video a hosted service keeps links to itself, wherever the run's files are.
        var hosted = artifacts.Where(artifact => artifact.Url is not null).Select(artifact => Link(artifact.Kind, artifact.Url!)).ToList();
        var local = artifacts.Where(artifact => artifact.Url is null).ToList();
        if (local.Count == 0)
        {
            return string.Join(", ", hosted);
        }

        var sorted = local.OrderBy(artifact => KindRank(artifact.Kind)).ToList();
        var kinds = sorted.Select(artifact => artifact.Kind).Distinct(StringComparer.Ordinal).ToList();
        List<string> shown = options.ArtifactsUrl is { } url
            ? [.. kinds.Select(kind => Link(kind, url))]
            : options.ArtifactsDir is null ? [] : EvidencePaths(sorted, options.ArtifactsDir);
        return string.Join(", ", [.. shown.Count == 0 ? kinds : shown, .. hosted]);
    }

    // --- a test that did not pass ---

    /// <summary>
    /// <c>at step 6 of 6: `expect.toBeHidden getByRole(...)`, after 15.1s and 12 model calls</c>: where a failure happened, how long the
    /// step ran, and what the agent spent on it. A step that is not the agent's reads as the one call it was; an agent step's label is the
    /// sentence the author wrote.
    /// </summary>
    private static string StepClause(IReadOnlyList<StepRecord> steps, FailedStep at)
    {
        var calls = ModelCalls(at.Step);
        var spent = calls == 0 ? "" : " and " + Plural(calls, "model call");
        var call = at.Step.Kind == StepKind.Agent ? Code(at.Step.Api, MaxIdChars) + " " + StepLabel(at.Step, MaxPathChars) : Code(at.Step.Api + " " + at.Step.Label, MaxPathChars);
        return "at step " + (at.Index + 1).ToString(CultureInfo.InvariantCulture) + " of " + steps.Count.ToString(CultureInfo.InvariantCulture) + ": " + call + ", after " + FormatDuration(at.Step.DurationMs) + spent;
    }

    /// <summary><c>**CODE** (phase)</c>: how the error names itself.</summary>
    private static string ErrorName(ReportError error)
    {
        var phase = error.Phase is null or "body" ? "" : " (" + TextRules.Collapse(TextRules.SanitizeText(error.Phase)) + ")";
        return "**" + Cell(error.Code, 128) + "**" + phase;
    }

    /// <summary>
    /// What the error and the agent said, for the quote under the lead. An assertion's message restates the facts the list carries, so
    /// only its first line is quoted, and not when that line just says the api failed. An agent step's explanation is quoted once: skipped
    /// when the message already is the explanation.
    /// </summary>
    private static List<string> QuotedWords(ReportError? error, StepRecord? at)
    {
        List<string> words = [];
        if (error is not null)
        {
            var first = error.Message.Split('\n')[0];
            var message = DetailLines(error).Count > 0 ? first : error.Message;
            if (message.Trim().Length > 0 && !(at is not null && first == at.Api + " failed"))
            {
                words.Add(message);
            }
        }

        var said = at?.Explanation?.Trim() ?? "";
        if (said.Length > 0 && !words.Any(word => word.Contains(said, StringComparison.Ordinal)))
        {
            words.Add(said);
        }

        return [.. words.Select(word => "> " + Cell(word, MaxDetailChars))];
    }

    /// <summary>A run-level error in one quoted line: name, message, facts.</summary>
    private static string RunErrorLine(ReportError error)
    {
        var facts = DetailLines(error);
        var message = facts.Count > 0 ? error.Message.Split('\n')[0] : error.Message;
        return "> " + string.Join(" · ", [(ErrorName(error) + " " + Cell(message)).Trim(), .. facts]);
    }

    /// <summary>
    /// One block per test that failed or was flaky, in paragraphs the eye can rest between: the title, with the line to look at and the
    /// target right under it, where the reader looks first; one lead sentence naming the error and the step it happened at; what the
    /// error and the agent said, quoted; the facts as a list (expected and observed, whether every attempt failed alike, the last turns,
    /// the screen, what the app logged as an error); and the evidence. The file is named once, in the source link. The steps before the
    /// failed one are not retold: the lead says where in the flow it was, and the trace page has the rest. A flaky test's story is its
    /// last failed attempt, not the retry that passed.
    /// </summary>
    private static string FailureBlock(Entry entry, bool manyTargets, MarkdownReportOptions options)
    {
        var (result, final) = entry;
        var kind = ReportFormat.StatusBucket(result.Status);
        var told = ToldAttempt(result, final);
        var at = FailedStepOf(told);
        var error = told.Error;
        var name = error is null ? "**" + Cell(ReportWire.ResultStatusName(result.Status), MaxIdChars) + "**" : ErrorName(error);
        var lead = at is null ? name : name + " " + StepClause(told.Steps, at);
        var alike = AttemptsLine(result, final);
        List<string> facts = [.. DetailLines(error), .. alike is null ? [] : new[] { alike }, .. LastTurnLines(at?.Step), .. ScreenLines(told), .. AppErrorLines(told)];
        var where = Evidence(EvidenceOf(told), options);
        var page = options.Traces is { } traces && traces.TryGetValue(result.Id, out var traced) ? traced : null;
        List<string> about = [SourceText(FailureSource(result, told), options.SourceUrl), .. manyTargets ? new[] { Cell(result.TargetId, MaxIdChars) } : []];
        List<string> tail = [.. where.Length == 0 ? [] : new[] { "Evidence: " + where }, .. page is null ? [] : new[] { "Trace: " + Code(page, MaxPathChars) }];
        string[] paragraphs =
        [
            // Two trailing spaces: a hard break, so the title and its line stay two lines wherever the page is rendered.
            "**" + Icon(kind) + " " + TestName(result) + "**  \n" + string.Join(" · ", about),
            lead,
            string.Join('\n', QuotedWords(error, at?.Step)),
            string.Join('\n', facts.Select(fact => "- " + fact)),
            string.Join(" · ", tail),
        ];
        return string.Join("\n\n", paragraphs.Where(paragraph => paragraph.Length > 0));
    }

    /// <summary><c>file (target)</c>, the target named only when the run has several.</summary>
    private static string FileLabel(string file, string target, bool manyTargets) =>
        Cell(file, MaxPathChars) + (manyTargets ? " (" + Cell(target, MaxIdChars) + ")" : "");

    /// <summary><c>suite › title</c>.</summary>
    private static string TestName(ReportResult result) =>
        string.Join(" › ", result.TitlePath.Select(part => Cell(part, MaxTitleChars))) + ReportFormat.RepeatSuffix(result.Repeat);

    /// <summary>The tests that were flaky, folded: the headline counts them and the run is green, so their blocks are there for the reader who asks, not in the way.</summary>
    private static List<string> FlakyFold(IReadOnlyList<Entry> entries, bool manyTargets, MarkdownReportOptions options)
    {
        if (entries.Count == 0)
        {
            return [];
        }

        var shown = entries.Take(MaxFailureBlocks).Select(entry => FailureBlock(entry, manyTargets, options)).ToList();
        if (entries.Count > shown.Count)
        {
            shown.Add("and " + (entries.Count - shown.Count).ToString(CultureInfo.InvariantCulture) + " more");
        }

        var summary = Icon(ResultBucket.Flaky) + " " + Plural(entries.Count, "flaky test") + " passed on a retry";
        return [string.Join('\n', "<details>", "<summary>" + summary + "</summary>", "", string.Join("\n\n", shown), "</details>")];
    }

    /// <summary>A test that missed a run: flaky when a run failed, else interrupted when the run stopped one, else skipped.</summary>
    private static string MissIcon(RepeatGroup group)
    {
        if (group.Unstable)
        {
            return Icon(ResultBucket.Flaky);
        }

        return group.Runs.Any(run => run.Status == "interrupted") ? Icon(ResultBucket.Interrupted) : Icon(ResultBucket.Skipped);
    }

    /// <summary>A <c>--repeat-each</c> run's tally: how many tests passed every run, then each test that did not, with the runs that failed. Empty for a run that repeated nothing.</summary>
    private static List<string> RepeatsSection(IReadOnlyList<Entry> entries, bool manyTargets)
    {
        var groups = Repeats.RepeatGroups(
            entries,
            entry =>
            {
                var result = entry.Result;
                List<string> label =
                [
                    Code(result.File, MaxPathChars),
                    .. manyTargets ? new[] { "(" + Cell(result.TargetId, MaxIdChars) + ")" } : [],
                    .. result.Agent == "default" ? [] : new[] { "as " + Cell(result.Agent, MaxIdChars) },
                    TestName(result with { Repeat = 0 }),
                ];
                return (
                    result.TestId + "@" + result.Agent + "@" + result.TargetId,
                    string.Join(' ', label),
                    new RepeatRun(result.Repeat, ReportWire.ResultStatusName(result.Status), entry.Final.Final.Error?.Code ?? entry.Final.LastFailed?.Error?.Code));
            });
        if (groups.Count == 0)
        {
            return [];
        }

        // A test is listed when it missed a run: a flake first, then one the run cut short.
        var missed = groups.Where(group => group.Passed < group.Runs.Count).OrderByDescending(group => group.Unstable).ToList();
        return
        [
            "**Repeats:** " + Repeats.RepeatSummary(groups),
            .. missed.Count == 0 ? [] : new[] { "" }.Concat(missed.Select(group => "- " + MissIcon(group) + " " + group.Label + ": " + Cell(Repeats.RepeatLine(group)))),
        ];
    }

    // --- every test, by file ---

    /// <summary>Results grouped by file and target, the worst file first, then by path.</summary>
    private static List<FileGroup> FileGroups(IReadOnlyList<Entry> entries)
    {
        List<(string Target, List<(string File, List<Entry> Entries)> Files)> groups = [];
        foreach (var entry in entries)
        {
            var byTarget = groups.FindIndex(group => group.Target == entry.Result.TargetId);
            if (byTarget < 0)
            {
                groups.Add((entry.Result.TargetId, []));
                byTarget = groups.Count - 1;
            }

            var files = groups[byTarget].Files;
            var byFile = files.FindIndex(file => file.File == entry.Result.File);
            if (byFile < 0)
            {
                files.Add((entry.Result.File, []));
                byFile = files.Count - 1;
            }

            files[byFile].Entries.Add(entry);
        }

        var rows = groups.SelectMany(group => group.Files.Select(file => new FileGroup(file.File, group.Target, file.Entries)));
        int Rank(FileGroup group) => Array.IndexOf(WorstFirst, WorstBucket(group.Entries.Select(entry => entry.Result)));
        return [.. rows
            .OrderBy(Rank)
            .ThenBy(group => group.File, StringComparer.InvariantCulture)
            .ThenBy(group => group.Target, StringComparer.InvariantCulture)];
    }

    /// <summary><c>2 steps · 9 calls</c>: the agent's work over a set of steps, or nothing when it did none.</summary>
    private static string AgentCell(IEnumerable<StepRecord> steps)
    {
        var totals = AgentTotals(steps);
        if (totals.Steps == 0)
        {
            return "";
        }

        List<string> parts = [Plural(totals.Steps, "step")];
        if (totals.Calls > 0)
        {
            parts.Add(Plural(totals.Calls, "call"));
        }

        return string.Join(" · ", parts);
    }

    /// <summary>One table row; every cell has already escaped the separator.</summary>
    private static string Row(IEnumerable<string> cells) => "| " + string.Join(" | ", cells) + " |";

    /// <summary><c>⚠️ | title (1 failed attempt first) | 1 step · 4 calls | 3.4s</c>: one test's row, how it ended noted when it did not simply pass. The file's row above names the target.</summary>
    private static string TestRow(Entry entry, bool withAgent)
    {
        var (result, final) = entry;
        var skipped = result.Status == ResultStatus.Skipped;
        var note = skipped ? "skipped: " + Cell(result.Skip?.Reason ?? "skipped") : result.Status == ResultStatus.Flaky ? Plural(final.FailedAttempts, "failed attempt") + " first" : "";
        var name = TestName(result) + (note.Length == 0 ? "" : " (" + note + ")");
        return Row([Icon(ReportFormat.StatusBucket(result.Status)), name, .. withAgent ? new[] { AgentCell(final.Final.Steps) } : [], skipped ? "" : FormatDuration(final.DurationMs)]);
    }

    /// <summary><c>⚠️ | **tests/billing.e2e.ts** · 1 failed, 2 passed | 3 steps · 19 calls | 41.2s</c>: a file's row above its tests, the glyph its worst result's.</summary>
    private static string FileRow(FileGroup group, bool manyTargets, bool withAgent)
    {
        var results = group.Entries.Select(entry => entry.Result).ToList();
        var time = FormatDuration(group.Entries.Sum(entry => entry.Final.DurationMs));
        List<string> agent = withAgent ? [AgentCell(group.Entries.SelectMany(entry => entry.Final.Final.Steps))] : [];
        return Row([Icon(WorstBucket(results)), "**" + FileLabel(group.File, group.Target, manyTargets) + "** · " + CountText(ReportFormat.Tally(results.Select(result => result.Status))), .. agent, time]);
    }

    /// <summary>
    /// Every test as one table, folded away: a row per file with its counts and time, worst file first, then a row per test under it.
    /// The agent column is there only when the run used the agent, so a deterministic suite is not a grid of empty cells. One block, since
    /// a <c>&lt;details&gt;</c> cannot be cut halfway.
    /// </summary>
    private static List<string> AllTests(IReadOnlyList<FileGroup> groups, int total, bool manyTargets)
    {
        if (total == 0)
        {
            return [];
        }

        var withAgent = groups.Any(group => group.Entries.Any(entry => entry.Final.Final.Steps.Any(step => step.Kind == StepKind.Agent)));
        List<string> header = ["", "Test", .. withAgent ? new[] { "Agent" } : [], "Time"];
        List<string> lines = [Row(header), Row(header.Select(_ => "---"))];
        var budget = MaxListedTests;
        foreach (var group in groups)
        {
            if (budget == 0)
            {
                break;
            }

            var shown = group.Entries.Take(budget).ToList();
            budget -= shown.Count;
            lines.Add(FileRow(group, manyTargets, withAgent));
            lines.AddRange(shown.Select(entry => TestRow(entry, withAgent)));
        }

        if (total > MaxListedTests)
        {
            lines.Add(Row(["", "and " + (total - MaxListedTests).ToString(CultureInfo.InvariantCulture) + " more", .. withAgent ? new[] { "" } : [], ""]));
        }

        // A file that ran on several targets is one file, however many groups it has.
        var files = groups.Select(group => group.File).Distinct(StringComparer.Ordinal).Count();
        var summary = "All " + Plural(total, "test") + " in " + Plural(files, "file");
        return [string.Join('\n', "<details>", "<summary>" + summary + "</summary>", "", string.Join('\n', lines), "</details>")];
    }

    /// <summary>
    /// The targets the page speaks of: the ones a selected result ran on. A run that selected one of several configured targets
    /// (<c>--target android</c>) is about that one, and names it nowhere but the footer.
    /// </summary>
    private static IReadOnlyList<ReportTarget> RanTargets(ReportRun run, IReadOnlyList<ReportResult> selected)
    {
        var ran = selected.Select(result => result.TargetId).ToHashSet(StringComparer.Ordinal);
        var targets = run.Targets.Where(target => ran.Contains(target.Id)).ToList();
        return targets.Count == 0 ? run.Targets : targets;
    }

    /// <summary><c>&lt;sub&gt;Tested by e2e 0.13.0 · 2m 15s · web · run artifacts&lt;/sub&gt;</c>: one line of small print.</summary>
    private static List<string> Footer(ReportRun run, IReadOnlyList<ReportTarget> targets, MarkdownReportOptions options)
    {
        // A run no one timed (a page built by hand) reads as instant, where upstream's date arithmetic would print NaN.
        var duration = FormatDuration(run.StartedAt is { } started && run.FinishedAt is { } finished ? (finished - started).TotalMilliseconds : 0);
        var shown = targets.Take(MaxFooterTargets).Select(target => Cell(target.Id, MaxIdChars)).ToList();
        if (targets.Count > shown.Count)
        {
            shown.Add("and " + (targets.Count - shown.Count).ToString(CultureInfo.InvariantCulture) + " more");
        }

        List<string> parts = ["Tested by " + Link("e2e", SiteUrl) + " " + Cell(run.RunnerVersion, MaxIdChars), duration];
        if (targets.Count == 1)
        {
            parts.Add(shown[0]);
        }
        else if (targets.Count > 1)
        {
            parts.Add(Plural(targets.Count, "target") + " (" + string.Join(", ", shown) + ")");
        }

        if (options.ArtifactsUrl is { } url)
        {
            parts.Add(Link("run artifacts", url));
        }

        return ["<sub>" + string.Join(" · ", parts) + "</sub>"];
    }

    // --- the exploration record ---

    private static string ExploreHeadline(ReportRun run, IReadOnlyList<ReportResult> results, ReportExplore explore, MarkdownReportOptions options)
    {
        var issues = explore.Findings.Count(finding => finding.Kind == ReportExploreFindingKind.Issue);
        var warnings = explore.Findings.Count - issues;
        List<string> counts = [.. issues > 0 ? new[] { Plural(issues, "issue") } : [], .. warnings > 0 ? new[] { Plural(warnings, "warning") } : []];
        var summary = counts.Count > 0
            ? string.Join(", ", counts)
            : run.Status == ReportRunStatus.Blocked ? "explored nothing" : run.Status == ReportRunStatus.Passed ? "no findings" : "did not finish";
        return "### " + RunIcon(run, results) + " " + PageName(options) + " explore: " + summary;
    }

    /// <summary><c>3 of 8 steps · 2 passed · 1 failed · the agent covered the goal</c>.</summary>
    private static string ExploreSteps(ReportExplore explore)
    {
        var counts = ExploreText.StepCountParts(explore.Steps).Select(part => part.Text);
        return string.Join(" · ", [explore.Steps.Count.ToString(CultureInfo.InvariantCulture) + " of " + explore.Budgets.MaxSteps.ToString(CultureInfo.InvariantCulture) + " steps", .. counts, ExploreText.EndedText(explore.Ended)]);
    }

    /// <summary>One finding as a list item: grade and title, where it was seen, what was expected against what the screen showed, the actions that reach it, and its screenshot.</summary>
    private static string FindingBlock(ReportExploreFinding finding, int position, Dictionary<string, ReportArtifact> artifacts, MarkdownReportOptions options)
    {
        List<string> where = [.. finding.Path is null ? [] : new[] { Code(finding.Path, MaxTitleChars) }, .. finding.Step is null ? [] : new[] { "step " + finding.Step.Value.ToString(CultureInfo.InvariantCulture) }];
        var kind = finding.Kind == ReportExploreFindingKind.Issue ? "issue" : "warning";
        List<string> lines =
        [
            position.ToString(CultureInfo.InvariantCulture) + ". **" + ExploreText.SeverityWord(finding.Severity) + " " + kind + "** " + Cell(finding.Title) + (where.Count == 0 ? "" : " · " + string.Join(" · ", where)),
            "   Expected: " + Cell(finding.Expected, MaxDetailChars),
            "   Actual: " + Cell(finding.Actual, MaxDetailChars),
        ];
        if (finding.Reproduction.Count > 0)
        {
            lines.Add("   Steps: " + string.Join(' ', finding.Reproduction.Select((action, index) => (index + 1).ToString(CultureInfo.InvariantCulture) + ". " + Cell(action, MaxPathChars))));
        }

        if (finding.ArtifactId is { } id && artifacts.TryGetValue(id, out var screenshot))
        {
            lines.Add("   Evidence: " + Evidence([screenshot], options));
        }

        return string.Join("  \n", lines);
    }

    /// <summary>The findings, issues before warnings and the most severe first, capped.</summary>
    private static List<string> FindingsSection(ReportRun run, ReportExplore explore, MarkdownReportOptions options)
    {
        if (explore.Findings.Count == 0)
        {
            return [];
        }

        Dictionary<string, ReportArtifact> artifacts = [];
        foreach (var artifact in run.Results.SelectMany(result => result.Attempts).SelectMany(attempt => attempt.Artifacts))
        {
            artifacts[artifact.Id] = artifact;
        }

        var ordered = ExploreText.OrderFindings(explore.Findings);
        var blocks = ordered.Take(MaxFindings).Select((finding, index) => FindingBlock(finding, index + 1, artifacts, options)).ToList();
        if (ordered.Count > blocks.Count)
        {
            blocks.Add((blocks.Count + 1).ToString(CultureInfo.InvariantCulture) + ". and " + (ordered.Count - blocks.Count).ToString(CultureInfo.InvariantCulture) + " more");
        }

        return ["**Findings**", "", .. blocks];
    }

    /// <summary>
    /// Greedy fit: keeps whole parts in order while they fit under the budget, then says what was cut. <paramref name="parts"/> are lines
    /// or indivisible blocks, already separated by blank lines.
    /// </summary>
    private static string Fit(IReadOnlyList<string> head, IReadOnlyList<string> parts, IReadOnlyList<string> tail)
    {
        string Render(IEnumerable<string> body) => string.Join('\n', [.. head, .. body, .. tail]) + "\n";
        var size = Render([TruncatedNote]).Length;
        List<string> kept = [];
        foreach (var part in parts)
        {
            if (size + part.Length + 1 > MaxBodyChars)
            {
                return Render([.. kept, TruncatedNote]);
            }

            kept.Add(part);
            size += part.Length + 1;
        }

        return Render(kept);
    }

    /// <summary>Sections as one flat list of parts with a blank line between sections; empty sections vanish.</summary>
    private static List<string> JoinSections(IEnumerable<IReadOnlyList<string>> sections) =>
        [.. sections.Where(section => section.Count > 0).SelectMany((section, index) => index == 0 ? section : ["", .. section])];

    /// <summary>Renders the run as one markdown page: a pull request comment body, a job summary, or <c>summary.md</c>.</summary>
    public static string RenderMarkdownReport(Report1Document report, MarkdownReportOptions? options = null)
    {
        options ??= new MarkdownReportOptions();
        var run = report.Run;
        var explore = run.Explore;

        // A result the selection left out (another suite's file, a tag filter) is report-only: the JSON keeps it, and the page counts or
        // lists it only when a test filter left it out, in the headline and the filters section.
        var selected = run.Results.Where(result => result.Selected).ToList();
        var entries = selected.Select(result => new Entry(result, Outcome.Of(result))).ToList();
        var targets = RanTargets(run, selected);
        var manyTargets = targets.Count > 1;
        var leftOut = LeftOut.LeftOutTests(run.Results.Select(result => (result.TestId, result.Skip)));

        var errors = run.Errors.Take(MaxRunErrors).Select(RunErrorLine).ToList();
        if (run.Errors.Count > errors.Count)
        {
            errors.Add("> and " + (run.Errors.Count - errors.Count).ToString(CultureInfo.InvariantCulture) + " more");
        }

        // An exploration is the run's one test and its failure is the verdict the findings express, so that block gives way to them; any
        // other failure stays.
        var notPassed = entries.Where(entry => explore is null || !TracePages.IsExploreVerdict(entry.Final, explore)).ToList();
        var failed = notPassed.Where(entry => ReportFormat.StatusBucket(entry.Result.Status) == ResultBucket.Failed).ToList();
        List<IReadOnlyList<string>> failures = [.. failed.Take(MaxFailureBlocks).Select(entry => (IReadOnlyList<string>)[FailureBlock(entry, manyTargets, options)])];
        if (failed.Count > MaxFailureBlocks)
        {
            failures.Add(["and " + (failed.Count - MaxFailureBlocks).ToString(CultureInfo.InvariantCulture) + " more failed"]);
        }

        var flaky = FlakyFold(notPassed.Where(entry => ReportFormat.StatusBucket(entry.Result.Status) == ResultBucket.Flaky).ToList(), manyTargets, options);

        var spend = SpendLine(run, entries);
        List<string> head = [explore is null ? Headline(run, selected, leftOut, options) : ExploreHeadline(run, selected, explore, options), .. spend is null ? [] : new[] { spend }, ""];
        var groups = FileGroups(entries);
        List<IReadOnlyList<string>> sections = explore is null
            ? [errors, .. failures, flaky, RepeatsSection(entries, manyTargets), FiltersSection(run, manyTargets, leftOut), AllTests(groups, selected.Count, manyTargets)]
            :
            [
                [$"**Goal:** {Cell(explore.Goal, MaxGoalChars)}  ", $"**Steps:** {ExploreSteps(explore)}"],
                errors,
                .. failures,
                flaky,
                FindingsSection(run, explore, options),
                explore.Summary is null ? [] : ["**Assessment**", "", Cell(explore.Summary, MaxAssessmentChars)],
            ];
        var body = JoinSections(sections);
        return Fit(head, body.Count == 0 ? [] : [.. body, ""], Footer(run, targets, options));
    }
}
