// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Internal;

// The public report types share their names with the internal records of the run, so this lives beside them.
namespace E2E.Reports;

/// <summary>What a rerun's collection found: the tests it collected by id, and the files it discovered but could not collect (upstream's <c>RerunCollection</c>).</summary>
/// <param name="TestIds">The id of every test collection found, a setup no selected test needed among them.</param>
/// <param name="UncollectedFiles">The files the run found but could not collect. The port finds tests by reflection, so it has none.</param>
internal sealed record RerunCollection(IReadOnlySet<string> TestIds, IReadOnlySet<string> UncollectedFiles);

/// <summary>
/// The report a <c>--last-failed</c> rerun selected from, with the JSON it was read from (.NET-only: the typed <see cref="Report"/> is what upstream
/// holds; the port writes the <c>carried</c> block from the previous document's own JSON, so a carried result is the run before's, byte for byte, and the
/// typed form need not write the wire form back).
/// </summary>
/// <param name="Report">The typed report.</param>
/// <param name="Document">The document it was read from.</param>
internal sealed record PreviousRun(Report Report, JsonObject Document)
{
    /// <summary>The <c>carried</c> block as the schema spells it: the wire form the previous document had for each result and error of <paramref name="carried"/>, which <see cref="LastRun.CarryForward"/> took from it.</summary>
    public JsonObject Carried(ReportCarried carried)
    {
        ArgumentNullException.ThrowIfNull(carried);
        var run = (JsonObject)Document["run"]!;
        var before = run["carried"] as JsonObject;
        return new JsonObject
        {
            ["results"] = new JsonArray([.. carried.Results.Select(result => Locate(result, Report.Run.Results, run["results"]!.AsArray(), Report.Run.Carried?.Results, before?["results"]?.AsArray()))]),
            ["serialGroups"] = new JsonArray(),
            ["errors"] = new JsonArray([.. carried.Errors.Select(error => Locate(error, Report.Run.Errors, run["errors"]!.AsArray(), Report.Run.Carried?.Errors, before?["errors"]?.AsArray()))]),
        };
    }

    // The node at the place the item has in the typed list; the lists were read from the document in order.
    private static JsonNode? Locate<T>(T item, IReadOnlyList<T> list, JsonArray nodes, IReadOnlyList<T>? carriedList, JsonArray? carriedNodes)
        where T : class
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (ReferenceEquals(list[index], item))
            {
                return nodes[index]!.DeepClone();
            }
        }

        for (var index = 0; carriedList is not null && index < carriedList.Count; index++)
        {
            if (ReferenceEquals(carriedList[index], item))
            {
                return carriedNodes![index]!.DeepClone();
            }
        }

        throw new InvalidOperationException("A carried item is not one of the previous run's.");
    }
}

/// <summary>
/// What <c>--last-failed</c> reads back from the previous run's report (upstream's <c>run/last-run.ts</c>). The port has no serial groups, so a
/// carried block's <c>serialGroups</c> is always empty, and no pruning of the results tree, so <c>reportArtifactPaths</c> has no caller.
/// Not ported: <c>previousFileDurations</c> and <c>fileDurationKey</c> (they order the files of a parallel run, and the test framework owns that order here).
/// </summary>
internal static class LastRun
{
    /// <summary>Skips that stand for a test the run could not carry out, as opposed to one it chose not to.</summary>
    private static readonly HashSet<SkipCause> UndeliveredSkips =
    [
        SkipCause.SetupFailed,
        SkipCause.SerialPredecessorFailed,
        SkipCause.HookFailed,
        SkipCause.InfrastructureUnavailable,
        SkipCause.FailureLimit,
        SkipCause.TimeLimit,
    ];

    /// <summary>The id <c>--last-failed</c> names a result by: its test, target, and agent, whichever its repeat.</summary>
    private static string RerunId(ReportResult result) => Ids.ResultId(result.TestId, result.TargetId, result.Agent);

    /// <summary>Whether a run error is a failed <c>beforeAll</c> or <c>afterAll</c>, the failures <c>--last-failed</c> runs the scope of again.</summary>
    private static bool IsSuiteHookFailure(ReportError error) => error.Phase is ErrorPhase.BeforeAll or ErrorPhase.AfterAll;

    /// <summary>Whether a result is one <c>--last-failed</c> runs again: it did not pass, or the run never got to it.</summary>
    private static bool DidNotPass(ReportResult result) => result.Status switch
    {
        ResultStatus.Failed or ResultStatus.TimedOut or ResultStatus.Interrupted => true,
        ResultStatus.Skipped => result.Skip is { } skip && UndeliveredSkips.Contains(skip.Cause),
        _ => false,
    };

    /// <summary>
    /// Whether a failed suite hook covered a result: same file and target, and declared inside the hook's describe. A hook failure is a run
    /// error no result carries, so a test whose body passed there still has to run again. An error that names no scope covers every test,
    /// since nothing narrower is known to be safe.
    /// </summary>
    private static bool InHookScope(ReportResult result, ReportError error) =>
        error.Scope is not { } scope
        || (result.File == scope.File
            && result.TargetId == scope.TargetId
            && result.TitlePath.Count > scope.TitlePath.Count
            && scope.TitlePath.Select((title, index) => result.TitlePath[index] == title).All(same => same));

    private static ConfigurationException NoLastRun(string message, Exception? cause = null) =>
        cause is null ? new ConfigurationException("NO_LAST_RUN", message) : new ConfigurationException("NO_LAST_RUN", message, cause);

    /// <summary>
    /// The previous run's report, read from where this run will write its own. It is what <c>--last-failed</c> selects from, and reporters receive it as
    /// <see cref="FinishedRun.LastRun"/> so one can fold the rerun into it. A missing or unreadable report is <c>NO_LAST_RUN</c>.
    /// </summary>
    public static async Task<Report> ReadLastRunAsync(string reportPath, CancellationToken cancellationToken = default) =>
        (await ReadPreviousRunAsync(reportPath, cancellationToken).ConfigureAwait(false)).Report;

    /// <summary><see cref="ReadLastRunAsync"/> with the document it read (.NET-only, see <see cref="PreviousRun"/>).</summary>
    public static async Task<PreviousRun> ReadPreviousRunAsync(string reportPath, CancellationToken cancellationToken = default)
    {
        string text;
        try
        {
            text = await File.ReadAllTextAsync(reportPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw NoLastRun($"--last-failed needs the report of a previous run, and none is at {reportPath}; run once without the flag first");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw NoLastRun($"--last-failed could not read the previous run's report at {reportPath}: {ex.Message}", ex);
        }

        JsonNode? document;
        try
        {
            document = JsonNode.Parse(text);
        }
        catch (JsonException ex)
        {
            throw NoLastRun($"--last-failed found no report in {reportPath}: {ex.Message}", ex);
        }

        var holdsSomethingElse = $"--last-failed needs a report-1 document at {reportPath}, which holds something else; run once without the flag to write one";
        if (document is not JsonObject root || !IsReport(root))
        {
            throw NoLastRun(holdsSomethingElse);
        }

        try
        {
            return new PreviousRun(ReportSerializer.Read(root), root);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException or FormatException)
        {
            throw NoLastRun(holdsSomethingElse, ex);
        }
    }

    /// <summary>
    /// The result ids of a report's tests that did not pass: failed, timed out, interrupted, or skipped because a setup, a serial predecessor, a hook, the
    /// worker, or the failure limit stopped it, every selected test in the scope of a <c>beforeAll</c> or <c>afterAll</c> that failed, until a run where that hook
    /// passes, and every test the report carries from an earlier run that it did not run again. An explicit skip and a filtered test passed in the sense that
    /// matters here: nothing to run again. The ids are <c>ResultId(testId, target, agent)</c>, so a test is named per target and agent whichever of its
    /// <c>--repeat-each</c> runs did not pass.
    /// </summary>
    public static IReadOnlySet<string> LastFailedIds(Report document)
    {
        var hookFailures = document.Run.Errors.Where(IsSuiteHookFailure).ToList();
        bool Rerun(ReportResult result) => DidNotPass(result) || (result.Selected && hookFailures.Exists(error => InHookScope(result, error)));
        var ids = new HashSet<string>(document.Run.Results.Where(Rerun).Select(RerunId), StringComparer.Ordinal);
        foreach (var result in document.Run.Carried?.Results ?? [])
        {
            ids.Add(RerunId(result));
        }

        return ids;
    }

    /// <summary>Whether two suite hook failures are the same hook's: one phase, one scope.</summary>
    private static bool SameHook(ReportError a, ReportError b)
    {
        if (a.Phase != b.Phase)
        {
            return false;
        }

        if (a.Scope is null || b.Scope is null)
        {
            return a.Scope is null && b.Scope is null;
        }

        return a.Scope.File == b.Scope.File && a.Scope.TargetId == b.Scope.TargetId && a.Scope.TitlePath.SequenceEqual(b.Scope.TitlePath);
    }

    /// <summary>
    /// What a <c>--last-failed</c> rerun carries forward from the report it selected from: every result that report owed a rerun and this run did not run,
    /// as the run that last ran it reported it; and the suite hook failures whose scope still holds one of them. A result is owed when it did not pass, or when
    /// it is in the scope of a failed <c>beforeAll</c> or <c>afterAll</c> this run did not see pass: a hook whose scope this run ran again without it failing
    /// is resolved, and one that failed again is this run's own error, which still makes its left-out tests owed. A test this run did not run is one it
    /// collected (<paramref name="collected"/>, by test id), on a target the config still has, that it did not select: left out by a filter, on a target or
    /// agent the run did not ask for, or a setup no selected test needed, which the report lists no row for. A test in a file the run found but could not
    /// collect is still there and stays carried. A test the run no longer collects cannot run again and is dropped. A test this run selected is no longer
    /// carried, whatever its outcome: its own result says it now. The result and error objects returned are those of <paramref name="lastRun"/>. Null when
    /// nothing is carried.
    /// </summary>
    public static ReportCarried? CarryForward(Report lastRun, Report current, RerunCollection collected)
    {
        var selected = new HashSet<string>(current.Run.Results.Where(result => result.Selected).Select(RerunId), StringComparer.Ordinal);
        var targets = new HashSet<string>(current.Run.Targets.Select(target => target.Id), StringComparer.Ordinal);
        bool StillThere(ReportResult result) => collected.TestIds.Contains(result.TestId) || collected.UncollectedFiles.Contains(result.File);
        bool NotRun(ReportResult result) => !selected.Contains(RerunId(result)) && StillThere(result) && targets.Contains(result.TargetId);

        var failedAgain = current.Run.Errors.Where(IsSuiteHookFailure).ToList();
        var hooks = lastRun.Run.Errors.Where(IsSuiteHookFailure).Concat(lastRun.Run.Carried?.Errors ?? []).ToList();
        bool ReportedAgain(ReportError error) => failedAgain.Exists(again => SameHook(again, error));
        bool PassedAgain(ReportError error) =>
            !ReportedAgain(error) && current.Run.Results.Any(result => result.Selected && result.Status != ResultStatus.Skipped && InHookScope(result, error));
        var unresolved = hooks.Where(error => !PassedAgain(error)).ToList();

        // A test the run before left out is known by the row it carried, not by its filtered one.
        var history = lastRun.Run.Results.Where(result => result.Selected).Concat(lastRun.Run.Carried?.Results ?? []);
        var results = history.Where(result => NotRun(result) && (DidNotPass(result) || unresolved.Exists(error => InHookScope(result, error)))).ToList();
        if (results.Count == 0)
        {
            return null;
        }

        var errors = unresolved.Where(error => !ReportedAgain(error) && results.Exists(result => InHookScope(result, error))).ToList();
        return new ReportCarried { Results = results, Errors = errors };
    }

    /// <summary>
    /// The shape the read relies on: a report-1 envelope whose every result has the fields read here. A hand-edited or truncated file that lacks them is
    /// not a report, rather than a crash on the first bad entry.
    /// </summary>
    internal static bool IsReport(JsonObject document) =>
        Text(document, "schemaVersion") == "report-1"
        && document["run"] is JsonObject run
        && IsRunPart(run)
        && (!run.ContainsKey("carried") || (run["carried"] is JsonObject carried && IsRunPart(carried)));

    // A run, or what one carries: results and errors of the shape read here, and its serial groups.
    private static bool IsRunPart(JsonObject part) =>
        part["results"] is JsonArray results
        && results.All(result => result is JsonObject row && IsResult(row))
        && part["errors"] is JsonArray errors
        && errors.All(error => error is JsonObject row && IsError(row))
        && part["serialGroups"] is JsonArray groups
        && groups.All(group => group is JsonObject row && HasAttempts(row));

    // Whether a result or serial group lists its attempts, each with its artifacts, the way a rerun reads them.
    private static bool HasAttempts(JsonObject value) => value["attempts"] is JsonArray attempts && attempts.All(attempt => attempt is JsonObject row && HasArtifacts(row));

    // Whether an attempt lists its artifacts as records whose path, when there is one, is a string.
    private static bool HasArtifacts(JsonObject attempt) =>
        attempt["artifacts"] is JsonArray artifacts
        && artifacts.All(artifact => artifact is JsonObject row && (!row.ContainsKey("path") || row["path"] is JsonValue { } path && path.TryGetValue<string>(out _)));

    private static bool IsResult(JsonObject result) =>
        Text(result, "id") is not null
        && Text(result, "status") is not null
        && Text(result, "file") is not null
        && result["titlePath"] is JsonArray
        && HasAttempts(result)
        && (!result.ContainsKey("skip") || (result["skip"] is JsonObject skip && Text(skip, "cause") is not null));

    private static bool IsError(JsonObject error) =>
        (!error.ContainsKey("phase") || Text(error, "phase") is not null)
        && (!error.ContainsKey("scope")
            || (error["scope"] is JsonObject scope && Text(scope, "file") is not null && Text(scope, "targetId") is not null && scope["titlePath"] is JsonArray));

    // The string a property holds; null when it is absent or holds something else.
    private static string? Text(JsonObject node, string name) => node[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
