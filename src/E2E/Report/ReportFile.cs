// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using E2E.Internal;

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

    private sealed class RunState(string runId, DateTimeOffset startedAt, Task<VcsInfo?> vcs)
    {
        public SemaphoreSlim Write { get; } = new(1, 1);

        public string RunId { get; } = runId;

        /// <summary>The earliest start of an attempt added so far: tests that run in parallel finish in any order.</summary>
        public DateTimeOffset StartedAt { get; set; } = startedAt;

        public Task<VcsInfo?> Vcs { get; } = vcs;

        public Dictionary<string, Entry> Results { get; } = new(StringComparer.Ordinal);

        public List<ReportTarget> Targets { get; } = [];

        public ReportExplore? Explore { get; set; }
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
    /// </summary>
    internal static (ReportRunStatus Status, int ExitCode) StatusOf(IEnumerable<ReportResult> results)
    {
        var exitCode = ErrorSerializer.CombineExitCodes(VerdictExitCodes(results));
        return (exitCode switch { 0 => ReportRunStatus.Passed, 1 => ReportRunStatus.Failed, 130 => ReportRunStatus.Interrupted, _ => ReportRunStatus.Error }, exitCode);
    }

    /// <summary>The exit codes the verdicts imply: a failed result exits 1, or its attempts' error category when that is worse (upstream's <c>verdictExitCodes</c>).</summary>
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
        var (status, exitCode) = StatusOf([result]);
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
        RunState state;
        lock (Gate)
        {
            if (!Runs.TryGetValue(resultsRoot, out state!))
            {
                state = Runs[resultsRoot] = new RunState(
                    report.Run.Id,
                    added.Attempts.FirstOrDefault()?.StartedAt ?? DateTimeOffset.UtcNow,
                    DetectVcs(projectRoot));
            }
        }

        await state.Write.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            state.Results[added.Id] = new Entry(added) { Redact = redact };
            if (added.Attempts.Select(attempt => attempt.StartedAt).Min() is { } earliest && earliest < state.StartedAt)
            {
                state.StartedAt = earliest;
            }

            foreach (var target in report.Run.Targets.Where(target => state.Targets.TrueForAll(known => known.Id != target.Id)))
            {
                state.Targets.Add(target);
            }

            state.Explore = report.Run.Explore ?? state.Explore;
            var (status, exitCode) = StatusOf(state.Results.Values.Select(entry => entry.Result));
            var document = BuildReport.Build(new BuildReportOptions
            {
                RunId = state.RunId,
                ProjectRoot = projectRoot,
                ReportTargets = state.Targets,
                StartedAt = state.StartedAt,
                Status = status,
                ExitCode = exitCode,
                Results = [.. state.Results.Values.Select(entry => entry.Result)],
                Explore = state.Explore,
                Vcs = await VcsOf(state.Vcs).ConfigureAwait(false),
                Ci = E2EConfig.IsCi(),
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

            await WriteReport.WriteJsonReportAsync(PathFor(resultsRoot), ReportWire.Document(document, nodes), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            state.Write.Release();
        }
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
