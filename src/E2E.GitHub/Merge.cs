// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Reports;

namespace E2E.GitHub;


/// <summary>
/// A <c>--last-failed</c> rerun and the run it selected from, as one report (upstream's <c>merge.ts</c>). The rerun's document lists every test the run
/// before it passed as unselected, so the page would show only the few tests it ran again; folding the two gives the comment the whole suite: carried
/// results for the tests the rerun left out, the rerun's results for the rest, and a test that failed and then passed reads as flaky with both runs'
/// attempts. Rendering only: nothing here is written back to a report file. The port has no serial groups, so the fold has none to join.
/// </summary>
internal static class Merge
{
    /// <summary>Statuses that count as a failure of the run. <c>--last-failed</c> also runs an interrupted test again, but that one reached no verdict.</summary>
    private static readonly HashSet<Reports.ResultStatus> FailedStatuses = [Reports.ResultStatus.Failed, Reports.ResultStatus.TimedOut];

    /// <summary>
    /// A test per target and agent, whatever its <c>--repeat-each</c> run: the identity <c>--last-failed</c> selects by, so a rerun without the flag runs
    /// once a test any repeat of which failed.
    /// </summary>
    private static string TestKey(Reports.ReportResult result) => string.Join('\0', result.TestId, result.TargetId, result.Agent);

    /// <summary>
    /// The rerun's result with the run before's attempts in front of its own: the same test's result there, and the further repeats the rerun has no
    /// result of its own for. A pass after a failure there is flaky; an interrupted rerun of a test that failed there keeps that failure.
    /// </summary>
    private static Reports.ReportResult FoldRerun(Reports.ReportResult current, IReadOnlyList<Reports.ReportResult> history)
    {
        if (history.Count == 0)
        {
            return current;
        }

        var before = history.OrderBy(result => result.Repeat).ToList();
        var attempts = before.SelectMany(result => result.Attempts).Concat(current.Attempts).ToList();
        var failure = before.LastOrDefault(result => FailedStatuses.Contains(result.Status));
        var status = failure is null
            ? current.Status
            : current.Status == Reports.ResultStatus.Passed
                ? Reports.ResultStatus.Flaky
                : current.Status == Reports.ResultStatus.Interrupted ? failure.Status : current.Status;
        return current with { Status = status, Attempts = attempts };
    }

    /// <summary>The counts the way the runner tallies them, over the folded results: the status counts are over the selected ones.</summary>
    private static Reports.ReportSummary Summarize(IReadOnlyList<Reports.ReportResult> results, Reports.ReportSummary baseSummary)
    {
        int selected = 0, executed = 0, passed = 0, failed = 0, interrupted = 0, flaky = 0, skipped = 0;
        foreach (var result in results)
        {
            if (result.Attempts.Count > 0)
            {
                executed++;
            }

            if (!result.Selected)
            {
                continue;
            }

            selected++;
            switch (result.Status)
            {
                case Reports.ResultStatus.Passed:
                    passed++;
                    break;
                case Reports.ResultStatus.Flaky:
                    flaky++;
                    break;
                case Reports.ResultStatus.Interrupted:
                    interrupted++;
                    break;
                case var status when FailedStatuses.Contains(status):
                    failed++;
                    break;
                default:
                    skipped++;
                    break;
            }
        }

        return baseSummary with { Discovered = results.Count, Selected = selected, Executed = executed, Passed = passed, Failed = failed, Interrupted = interrupted, Flaky = flaky, Skipped = skipped };
    }

    /// <summary>
    /// The rerun folded into the run it selected from. A result the rerun has under the same id is folded with it; a test the rerun left out keeps what the
    /// run before said, or what the rerun carries for it when an earlier run left it owed (<c>run.carried</c>). Results the rerun has no id for at all, the
    /// further repeats of a <c>--repeat-each</c> first pass a plain rerun ran once, fold into the rerun's first result of that test, or stay as carried rows
    /// when the rerun left the test out. A run before of another project leaves the rerun as it is. The outcome is the rerun's unless a carried result
    /// failed, as a test another filter kept out of the rerun can have, or the rerun carries a test it owed and never ran, a failure limit's skip among them,
    /// or a suite hook failure whose scope it did not run again (<c>run.carried</c>). Then the page is red, since the suite is not known to pass. A carried
    /// test the first pass interrupted reached no verdict: it leaves the outcome alone and the page reads interrupted, not green. The start is the run
    /// before's, so the footer's duration spans both. The rerun takes the run before's filter records and the change they judged, since the rows those
    /// filters left out are the run before's too. An <c>e2e explore</c> run before leaves the rerun as it is.
    /// </summary>
    public static Report FoldLastRun(Report current, Report lastRun)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(lastRun);

        // An exploration that wrote to the same output is not a pass of the suite.
        if (lastRun.Run.Project.Id != current.Run.Project.Id || lastRun.Run.Explore is not null)
        {
            return current;
        }

        var carried = current.Run.Carried;
        var carriedById = new OrderedDictionary<string, Reports.ReportResult>(StringComparer.Ordinal);
        foreach (var result in carried?.Results ?? [])
        {
            carriedById[result.Id] = result;
        }

        var currentIds = new HashSet<string>(current.Run.Results.Select(result => result.Id), StringComparer.Ordinal);

        // The run before's own carried rows stand for the tests it left out, over their filtered rows.
        var beforeById = new OrderedDictionary<string, Reports.ReportResult>(StringComparer.Ordinal);
        foreach (var result in lastRun.Run.Results.Concat(lastRun.Run.Carried?.Results ?? []))
        {
            beforeById[result.Id] = result;
        }

        var leftovers = new OrderedDictionary<string, List<Reports.ReportResult>>(StringComparer.Ordinal);
        foreach (var result in beforeById.Values.Where(result => !currentIds.Contains(result.Id)))
        {
            var key = TestKey(result);
            if (!leftovers.TryGetValue(key, out var list))
            {
                leftovers[key] = list = [];
            }

            list.Add(result);
        }

        var firstOfKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var result in current.Run.Results.OrderBy(result => result.Repeat))
        {
            firstOfKey.TryAdd(TestKey(result), result.Id);
        }

        var results = new List<Reports.ReportResult>();
        foreach (var result in current.Run.Results)
        {
            beforeById.TryGetValue(result.Id, out var previous);
            if (!result.Selected)
            {
                results.Add(carriedById.GetValueOrDefault(result.Id) ?? previous ?? result);
                continue;
            }

            var key = TestKey(result);
            List<Reports.ReportResult> extra = firstOfKey[key] == result.Id && leftovers.TryGetValue(key, out var found) ? found : [];
            if (extra.Count > 0)
            {
                leftovers.Remove(key);
            }

            results.Add(FoldRerun(result, previous is null ? extra : [previous, .. extra]));
        }

        foreach (var extra in leftovers.Values)
        {
            results.AddRange(extra);
        }

        var carriedErrors = carried?.Errors ?? [];
        var carriedFailure =
            carriedErrors.Count > 0
            || carriedById.Values.Any(result => result.Status != Reports.ResultStatus.Interrupted)
            || results.Exists(result => result.Selected && FailedStatuses.Contains(result.Status));
        return current with
        {
            Run = current.Run with
            {
                Status = carriedFailure && current.Run.Status == ReportRunStatus.Passed ? ReportRunStatus.Failed : current.Run.Status,
                ExitCode = carriedFailure && current.Run.ExitCode == 0 ? 1 : current.Run.ExitCode,
                StartedAt = lastRun.Run.StartedAt,
                Results = results,
                Errors = [.. current.Run.Errors, .. carriedErrors],
                Summary = Summarize(results, current.Run.Summary),
                Filters = lastRun.Run.Filters,
                Changes = lastRun.Run.Changes,
            },
        };
    }
}
