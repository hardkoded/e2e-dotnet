// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using E2E.Internal;
using FinishedRun = E2E.Reports.FinishedRun;
using LastRun = E2E.Reports.LastRun;
using PreviousRun = E2E.Reports.PreviousRun;
using RerunCollection = E2E.Reports.RerunCollection;
using VcsInfo = E2E.Reports.VcsInfo;

namespace E2E;

/// <summary>
/// <c>report.json</c>, the report-1 document of the run. .NET-only: upstream's runner builds the document once, when the run ends
/// (<c>report/build.ts</c>), and writes it with its <c>file</c> reporter. A test framework runs the port's attempts as sessions of its own,
/// with no run around them that ends, so each session adds its attempt to the process's run and the whole document is written again, atomically.
/// The file is complete after every attempt; its <c>finishedAt</c> and <c>status</c> are those of the attempts so far, not final. Sessions
/// that share an output directory but not a process overwrite one another's document.
/// </summary>
internal static class ReportFile
{
    /// <summary>The file the document goes to, in the output directory beside <c>results/</c>.</summary>
    public const string FileName = "report.json";

    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, RunState> Runs = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, Lazy<Task<VcsInfo?>>> Detections = new(StringComparer.Ordinal);

    private sealed class Entry(ReportResult result)
    {
        public ReportResult Result { get; set; } = result;

        /// <summary>The result as the document spells it, redacted; null until the next write builds it.</summary>
        public JsonNode? Node { get; set; }

        public Func<string, string> Redact { get; set; } = text => text;
    }

    private sealed class RunState(string runId, string projectRoot, DateTimeOffset startedAt, Task<VcsInfo?> vcs)
    {
        public string ProjectRoot { get; } = projectRoot;

        public SemaphoreSlim Write { get; } = new(1, 1);

        public string RunId { get; } = runId;

        /// <summary>The earliest start of an attempt added so far: tests that run in parallel finish in any order.</summary>
        public DateTimeOffset StartedAt { get; set; } = startedAt;

        public Task<VcsInfo?> Vcs { get; } = vcs;

        public Dictionary<string, Entry> Results { get; } = new(StringComparer.Ordinal);

        public List<ReportTarget> Targets { get; } = [];

        public ReportExplore? Explore { get; set; }

        /// <summary>The trace page written for each test that kept one, by result id, as a path from the project root (<see cref="FinishedRun.Traces"/>).</summary>
        public Dictionary<string, string> Traces { get; } = new(StringComparer.Ordinal);

        /// <summary>Set once a session of the run was cancelled by the user (upstream's <c>interruptController.signal.aborted</c>); read from the results the run holds.</summary>
        public bool Interrupted { get; set; }

        /// <summary>The run's limits: the largest any test's agents allowed.</summary>
        public ReportLimits? Limits { get; set; }

        /// <summary>The change the test filters judged (<c>run.changes</c>); null when no filter had a say.</summary>
        public ReportChanges? Changes { get; set; }

        /// <summary>
        /// The id of the target the left-out tests named when no session had described one (it has no engine to describe); the first session
        /// takes its place, since a fixture can run on a target of another name than the config's.
        /// </summary>
        public string? Placeholder { get; set; }

        /// <summary>What each test filter decided (<c>run.filters</c>), in order.</summary>
        public IReadOnlyList<ReportFilter> Filters { get; set; } = [];

        /// <summary>The report a <c>--last-failed</c> rerun selected from; null for any other run. Reporters get it as <see cref="FinishedRun.LastRun"/>, and the document carries what it owed.</summary>
        public PreviousRun? Previous { get; set; }

        /// <summary>What the rerun's collection found, which decides what the document still carries; set with <see cref="Previous"/>.</summary>
        public RerunCollection? Collected { get; set; }
    }

    /// <summary>Where the document of the run whose results go to <paramref name="resultsRoot"/> is written.</summary>
    public static string PathFor(string resultsRoot) => Path.Combine(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(resultsRoot))!, FileName);

    /// <summary>
    /// The commit of the project at <paramref name="projectRoot"/>, asked of the version control once per process and root, so a run of many
    /// sessions starts no process per session. A lookup that failed is not kept: the next call asks again. <paramref name="detect"/> is for tests.
    /// </summary>
    internal static async Task<VcsInfo?> DetectVcs(string projectRoot, Func<string, Task<VcsInfo?>>? detect = null)
    {
        // The lazy keeps two sessions that ask together from both starting a lookup; the task inside keeps no exception, so a failure is evicted below.
        var detection = Detections.GetOrAdd(projectRoot, root => new Lazy<Task<VcsInfo?>>(async () => detect is null ? await Vcs.DetectAsync(root, EnvironmentVariables()).ConfigureAwait(false) : await detect(root).ConfigureAwait(false)));
        try
        {
            return await detection.Value.ConfigureAwait(false);
        }
        catch (Exception)
        {
            Detections.TryRemove(new KeyValuePair<string, Lazy<Task<VcsInfo?>>>(projectRoot, detection));
            throw;
        }
    }

    /// <summary>The project's root: the one given, else the parent of the results directory (the output directory).</summary>
    public static string ProjectRootOf(string? projectRoot, string resultsRoot) =>
        projectRoot ?? Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(resultsRoot))!;

    /// <summary>
    /// How a run stands, from its results alone (upstream's <c>statusOf</c> of the exit code that <c>verdictExitCodes</c> implies). A
    /// blocked run reads <c>error</c> here and <c>blocked</c> once <see cref="BuildReport.DeriveRunStatus"/> has read its results.
    /// <paramref name="interrupted"/> is the run's interrupt flag: when set the exit codes include 130, whatever the results say
    /// (upstream's <c>currentExitCode</c> adds 130 when <c>interruptController.signal.aborted</c>).
    /// </summary>
    internal static (ReportRunStatus Status, int ExitCode) StatusOf(IEnumerable<ReportResult> results, bool interrupted = false)
    {
        var exitCode = ErrorSerializer.CombineExitCodes(interrupted ? [.. VerdictExitCodes(results), 130] : VerdictExitCodes(results));
        return (exitCode switch { 0 => ReportRunStatus.Passed, 1 => ReportRunStatus.Failed, 130 => ReportRunStatus.Interrupted, _ => ReportRunStatus.Error }, exitCode);
    }

    /// <summary>Whether a session cancelled by the user ended an attempt of <paramref name="result"/>: a retry the run interrupted counts, though the verdict keeps the failure before it.</summary>
    private static bool WasInterrupted(ReportResult result) => result.Attempts.Any(attempt => attempt.Status == AttemptStatus.Interrupted);

    /// <summary>The exit codes the verdicts imply: a failed result exits 1, or its attempts' error category when that is worse. An attempt whose cleanup failed adds its cleanup errors' codes, even when the test passed (upstream's <c>verdictExitCodes</c>).</summary>
    private static IEnumerable<int> VerdictExitCodes(IEnumerable<ReportResult> results)
    {
        foreach (var result in results)
        {
            switch (result.Status)
            {
                case ResultStatus.Failed or ResultStatus.TimedOut:
                    yield return 1;
                    foreach (var error in result.Attempts.Select(attempt => attempt.Error).OfType<ReportError>())
                    {
                        yield return ErrorSerializer.ExitCodeForCategory(error.Category);
                    }

                    break;
                case ResultStatus.Interrupted:
                    yield return 130;
                    break;
            }

            foreach (var cleanupError in ReportRecords.CleanupErrorsOf(result.Attempts))
            {
                yield return ErrorSerializer.ExitCodeForCategory(cleanupError.Category);
            }
        }
    }

    /// <summary>
    /// The document of the attempt in <paramref name="report"/> (a single-attempt report, as <see cref="AttemptReport.Build"/> returns it) alone, as a
    /// run of one result, redacted by <paramref name="redact"/>. Upstream's <c>writeHostTrace</c> builds the same report for a hosted session and returns it.
    /// </summary>
    public static async Task<JsonObject> SingleAsync(Report1Document report, string? projectRoot, string resultsRoot, Func<string, string> redact)
    {
        var result = report.Run.Results[0];
        projectRoot = ProjectRootOf(projectRoot, resultsRoot);
        var (status, exitCode) = StatusOf([result], WasInterrupted(result));
        var document = BuildReport.Build(new BuildReportOptions
        {
            RunId = report.Run.Id,
            ProjectRoot = projectRoot,
            StartedAt = result.Attempts[0].StartedAt ?? DateTimeOffset.UtcNow,
            Status = status,
            ExitCode = exitCode,
            Results = [result],
            ReportTargets = report.Run.Targets,
            Explore = report.Run.Explore,
            Limits = report.Run.Limits,
            Vcs = await VcsOf(DetectVcs(projectRoot)).ConfigureAwait(false),
            Ci = E2EConfig.IsCi(),
        });
        var node = ReportWire.Result(document.Run.Results[0], result.Platform, Path.TrimEndingDirectorySeparator(Path.GetFullPath(resultsRoot)));
        ReportWire.Redact(node, redact);
        return ReportWire.Document(document, new Dictionary<string, JsonNode>(StringComparer.Ordinal) { [result.Id] = node });
    }

    /// <summary>
    /// Adds the test in <paramref name="report"/> (the single result <see cref="RunReport.Add"/> returned, with every attempt the test has had) to the
    /// run, and writes the document of the run to <c>report.json</c> beside <paramref name="resultsRoot"/>. <paramref name="redact"/> is the
    /// redactor of that test's attempts.
    /// </summary>
    public static async Task WriteAsync(Report1Document report, string projectRoot, string resultsRoot, Func<string, string> redact, CancellationToken cancellationToken = default)
    {
        resultsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(resultsRoot));
        var added = report.Run.Results[0];
        var state = StateOf(resultsRoot, report.Run.Id, projectRoot, added.Attempts.FirstOrDefault()?.StartedAt ?? DateTimeOffset.UtcNow);
        await state.Write.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            state.Results[added.Id] = new Entry(added) { Redact = redact };
            if (added.Attempts.Select(attempt => attempt.StartedAt).Min() is { } earliest && earliest < state.StartedAt)
            {
                state.StartedAt = earliest;
            }

            ReplacePlaceholder(state, report.Run.Targets[0]);
            foreach (var target in report.Run.Targets.Where(target => state.Targets.TrueForAll(known => known.Id != target.Id)))
            {
                state.Targets.Add(target);
            }

            state.Explore = report.Run.Explore ?? state.Explore;
            state.Limits = report.Run.Limits is { } limits ? state.Limits?.Max(limits) ?? limits : state.Limits;
            await WriteStateAsync(state, resultsRoot, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            state.Write.Release();
        }
    }

    /// <summary>
    /// Adds what the test filters left out to the run whose results go to <paramref name="resultsRoot"/>, and writes the document: the skipped,
    /// unselected result of each test left out, <c>run.filters</c>, and <c>run.changes</c> (.NET-only: upstream's runner holds them when it builds the report;
    /// a test left out starts no session, so no session would add it). The run exists from here, so a run whose every test was left out still has its document.
    /// </summary>
    public static async Task WriteLeftOutAsync(HostSelection selection, ReportTarget target, string projectRoot, string resultsRoot, CancellationToken cancellationToken = default)
    {
        resultsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(resultsRoot));
        var state = StateOf(resultsRoot, AttemptReport.RunId, projectRoot, DateTimeOffset.UtcNow);
        await state.Write.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (state.Targets.Count == 0)
            {
                state.Targets.Add(target);
                state.Placeholder = target.Id;
            }

            // A session has described the target already: the tests left out ran on it.
            var named = state.Placeholder is null ? state.Targets[0] : target;
            foreach (var result in selection.Results)
            {
                state.Results[result.Id] = new Entry(result with { TargetId = named.Id, Platform = named.Platform });
            }

            state.Changes = selection.Changes;
            state.Filters = selection.Filters;
            state.Previous = selection.Previous;
            state.Collected = selection.Collected;
            await WriteStateAsync(state, resultsRoot, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            state.Write.Release();
        }
    }

    /// <summary>
    /// Records where the trace page of the result <paramref name="resultId"/> is, as a path from the project root, or that it has none now (null) in the run whose
    /// results go to <paramref name="resultsRoot"/>. The run keeps the union over its tests, which a reporter reads as <see cref="FinishedRun.Traces"/>.
    /// A run that no session has added a result to yet has nothing to record it in.
    /// </summary>
    public static void SetTrace(string resultsRoot, string resultId, string? path)
    {
        resultsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(resultsRoot));
        RunState? state;
        lock (Gate)
        {
            Runs.TryGetValue(resultsRoot, out state);
        }

        if (state is null)
        {
            return;
        }

        lock (state.Traces)
        {
            if (path is null)
            {
                state.Traces.Remove(resultId);
            }
            else
            {
                state.Traces[resultId] = path;
            }
        }
    }

    // The target of the first session stands for the one the left-out tests named: they ran on it, whatever name the config gave.
    private static void ReplacePlaceholder(RunState state, ReportTarget real)
    {
        if (state.Placeholder is not { } placeholder)
        {
            return;
        }

        state.Placeholder = null;
        state.Targets.RemoveAll(each => each.Id == placeholder);
        state.Targets.Insert(0, real with { Index = 0 });
        foreach (var entry in state.Results.Values.Where(entry => entry.Result.TargetId == placeholder))
        {
            entry.Result = entry.Result with { TargetId = real.Id, Platform = real.Platform };
            entry.Node = null;
        }
    }

    private static RunState StateOf(string resultsRoot, string runId, string projectRoot, DateTimeOffset startedAt)
    {
        lock (Gate)
        {
            if (!Runs.TryGetValue(resultsRoot, out var state))
            {
                state = Runs[resultsRoot] = new RunState(runId, projectRoot, startedAt, DetectVcs(projectRoot));
            }

            return state;
        }
    }

    // The caller holds the run's write gate.
    private static async Task WriteStateAsync(RunState state, string resultsRoot, CancellationToken cancellationToken)
    {
        // Read from the results the run holds: a rerun that replaces an interrupted result clears the flag, while the retries of a test stay in its result.
        state.Interrupted = state.Results.Values.Any(entry => WasInterrupted(entry.Result));
        var (status, exitCode) = StatusOf(state.Results.Values.Select(entry => entry.Result), state.Interrupted);
        var document = BuildReport.Build(new BuildReportOptions
        {
            RunId = state.RunId,
            ProjectRoot = state.ProjectRoot,
            ReportTargets = state.Targets,
            StartedAt = state.StartedAt,
            Status = status,
            ExitCode = exitCode,
            Results = [.. state.Results.Values.Select(entry => entry.Result)],
            Explore = state.Explore,
            Limits = state.Limits,
            Vcs = await VcsOf(state.Vcs).ConfigureAwait(false),
            Ci = E2EConfig.IsCi(),
            Changes = state.Changes,
            Filters = state.Filters,
        });

        // A result that gained no attempt keeps its wire form: only the test this call is about is built again.
        var nodes = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var result in document.Run.Results)
        {
            var entry = state.Results[result.Id];
            if (entry.Node is null)
            {
                var node = ReportWire.Result(result, result.Platform, resultsRoot);
                ReportWire.Redact(node, entry.Redact);
                entry.Node = node;
            }

            nodes[result.Id] = entry.Node;
        }

        var wire = ReportWire.Document(document, nodes);

        // What the report this run reran owed and this run did not run again is carried, and stays owed until a run carries it out.
        if (state.Previous is { } previous && state.Collected is { } collected && LastRun.CarryForward(previous.Report, CurrentOf(document), collected) is { } carried)
        {
            wire["run"]!["carried"] = previous.Carried(carried);
        }

        await WriteReport.WriteJsonReportAsync(PathFor(resultsRoot), wire, cancellationToken).ConfigureAwait(false);
    }

    // The part of this run's document that deciding what to carry reads, in the public form (.NET-only: upstream's builder holds the typed document).
    private static Reports.Report CurrentOf(Report1Document document) => new()
    {
        Run = new Reports.ReportRun
        {
            Targets = [.. document.Run.Targets.Select(target => new Reports.ReportTarget { Id = target.Id })],
            Results =
            [
                .. document.Run.Results.Select(result => new Reports.ReportResult
                {
                    Id = result.Id,
                    TestId = result.TestId,
                    TargetId = result.TargetId,
                    Agent = result.Agent,
                    File = result.File,
                    TitlePath = result.TitlePath,
                    Selected = result.Selected,
                    Status = Enum.Parse<Reports.ResultStatus>(result.Status.ToString()),
                }),
            ],
            Errors = [],
        },
    };

    /// <summary>
    /// What each run this process wrote a report for looks like to a reporter (upstream's <c>FinishedRun</c>): the document read back from
    /// <c>report.json</c>, so a reporter that uploads it ships the file byte for byte. A run whose file cannot be read is left out.
    /// </summary>
    public static async Task<IReadOnlyList<FinishedRun>> FinishedRunsAsync(CancellationToken cancellationToken = default)
    {
        List<(string ResultsRoot, RunState State)> runs;
        lock (Gate)
        {
            runs = [.. Runs.Select(pair => (pair.Key, pair.Value))];
        }

        List<FinishedRun> finished = [];
        foreach (var (resultsRoot, state) in runs)
        {
            // The last write of the run is done once it can take the gate.
            await state.Write.WaitAsync(cancellationToken).ConfigureAwait(false);
            state.Write.Release();
            var path = PathFor(resultsRoot);
            try
            {
                var report = ReportSerializer.Read(JsonNode.Parse(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false))!.AsObject());
                Dictionary<string, string> traces;
                lock (state.Traces)
                {
                    traces = new Dictionary<string, string>(state.Traces, StringComparer.Ordinal);
                }

                finished.Add(new FinishedRun(report, report.Run.Status, report.Run.ExitCode, state.ProjectRoot, path, resultsRoot)
                {
                    Traces = traces,
                    AiTracePath = AiTrace.WrittenPath(Path.GetDirectoryName(resultsRoot)!),
                    LastRun = state.Previous?.Report,
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.Error.WriteLine($"e2e: the report of run {state.RunId} could not be read for the reporters: {ex.Message}");
            }
        }

        return finished;
    }

    // Detection never fails a run: a task that faulted reads as no commit.
    private static async Task<VcsInfo?> VcsOf(Task<VcsInfo?> detection)
    {
        try
        {
            return await detection.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static Dictionary<string, string?> EnvironmentVariables() =>
        Environment.GetEnvironmentVariables().Cast<DictionaryEntry>().ToDictionary(pair => (string)pair.Key, pair => (string?)pair.Value, StringComparer.Ordinal);
}
