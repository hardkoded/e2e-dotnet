// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
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

    /// <summary>How a run stands, from its results alone: a failure outweighs an interruption, and neither is a pass.</summary>
    internal static (ReportRunStatus Status, int ExitCode) StatusOf(IEnumerable<ReportResult> results)
    {
        var all = results.ToList();
        if (all.Any(result => result.Status is ResultStatus.Failed or ResultStatus.TimedOut))
        {
            return (ReportRunStatus.Failed, 1);
        }

        return all.Any(result => result.Status == ResultStatus.Interrupted) ? (ReportRunStatus.Interrupted, 130) : (ReportRunStatus.Passed, 0);
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
                    Vcs.DetectAsync(projectRoot, EnvironmentVariables()));
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
                Vcs = await VcsOf(state).ConfigureAwait(false),
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
    private static async Task<VcsInfo?> VcsOf(RunState state)
    {
        try
        {
            return await state.Vcs.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static Dictionary<string, string?> EnvironmentVariables() =>
        Environment.GetEnvironmentVariables().Cast<DictionaryEntry>().ToDictionary(pair => (string)pair.Key, pair => (string?)pair.Value, StringComparer.Ordinal);
}
