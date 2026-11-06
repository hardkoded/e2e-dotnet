// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Reports;

namespace E2E;

/// <summary>
/// A test a framework adapter found by reflection, as the host-side test filters read it. .NET-only: upstream collects tests by running the
/// test files (<c>collectModule</c>), and a .NET assembly has no source file to run.
/// </summary>
/// <param name="Key">The adapter's own name for the test; <see cref="HostTestFilters.ExcludeAsync"/> answers with it.</param>
/// <param name="Namespace">The namespace of the test class; empty for none.</param>
/// <param name="TypeName">The simple name of the test class.</param>
/// <param name="Title">The test's title.</param>
/// <param name="Tags">The categories or traits of the test, its class, and its enclosing classes.</param>
/// <param name="SkipReason">Why the test is skipped outright (<c>[Ignore]</c>); null when it is not.</param>
internal sealed record DiscoveredTest(string Key, string Namespace, string TypeName, string Title, IReadOnlyList<string> Tags, string? SkipReason = null);

/// <summary>
/// What the filters decided for a run (.NET-only): the tests left out with their reasons, for each test to skip itself, and the report's
/// part of it, which upstream's runner builds: the skipped <see cref="Results"/> of the tests left out, what each filter decided, and the change judged.
/// </summary>
/// <param name="Excluded">The reason each test left out gives, by <see cref="DiscoveredTest.Key"/>.</param>
/// <param name="Results">One skipped, unselected result per test left out (upstream's skipped record of a filtered pair).</param>
/// <param name="Filters">What each filter decided (<c>run.filters</c>); empty when no filter ran.</param>
/// <param name="Changes">The change judged, or why no filter ran (<c>run.changes</c>); null when there was nothing to judge by.</param>
/// <param name="Previous">The report a <c>--last-failed</c> rerun selected from; null for any other run.</param>
/// <param name="Collected">The tests the host found, which decide what a rerun still carries; set with <paramref name="Previous"/>.</param>
/// <param name="RerunDir">The directory the rerun's attempts go in, inside each test's (<c>rerun-&lt;n&gt;</c>); set with <paramref name="Previous"/>.</param>
internal sealed record HostSelection(
    IReadOnlyDictionary<string, string> Excluded,
    IReadOnlyList<ReportResult> Results,
    IReadOnlyList<ReportFilter> Filters,
    ReportChanges? Changes,
    PreviousRun? Previous = null,
    RerunCollection? Collected = null,
    string? RerunDir = null);

/// <summary>
/// Wires the test filters into a framework adapter's run (upstream's host applies <c>filterSelection</c> after <c>select</c>; .NET-only
/// name). The adapter hands over the tests it found by reflection once, before the first test. The seam collects them, selects, runs
/// <see cref="TestFilters.FilterSelection"/>, and returns the tests the filters left out, each with its reason, for the test to skip itself.
/// </summary>
internal static class HostTestFilters
{
    /// <summary>The environment variable that names the base ref when the host gives none (<c>--changed-since</c>).</summary>
    public const string ChangedSinceVariable = "E2E_CHANGED_SINCE";

    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase) { "bin", "obj", "node_modules", ".git", ".vs", ".e2e" };

    /// <summary>The environment variable that asks for a <c>--last-failed</c> rerun when the host does not (<c>1</c> or <c>true</c>).</summary>
    public const string LastFailedVariable = "E2E_LAST_FAILED";

    /// <summary>Whether the run is a <c>--last-failed</c> rerun: <paramref name="configured"/>, else <c>E2E_LAST_FAILED</c> set to <c>1</c> or <c>true</c>.</summary>
    public static bool ResolveLastFailed(bool configured, Func<string, string?>? environment = null) =>
        configured || (environment ?? Environment.GetEnvironmentVariable)(LastFailedVariable)?.Trim() is { } value && (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase));

    /// <summary>The base ref to measure the change from: <paramref name="configured"/>, else <c>E2E_CHANGED_SINCE</c>; null when neither is set.</summary>
    public static string? ResolveChangedSince(string? configured, Func<string, string?>? environment = null)
    {
        var value = string.IsNullOrWhiteSpace(configured) ? (environment ?? Environment.GetEnvironmentVariable)(ChangedSinceVariable) : configured;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// The tests the filters left out, by <see cref="DiscoveredTest.Key"/>, with the reason each filter gave, and the part of the report that
    /// says so. Nothing is left out when the filters left none out or none ran. The selection is made once for the whole run. A
    /// <paramref name="lastFailed"/> rerun reads the report the previous run left where this run will write its own, and leaves out the tests that
    /// run did not fail; the filters then run only when <paramref name="changedSince"/> names a base ref, and without asking (upstream's <c>filterSelection</c>).
    /// </summary>
    public static async Task<HostSelection> ExcludeAsync(
        E2EConfig config,
        IReadOnlyList<ITestFilter> filters,
        string? changedSince,
        IReadOnlyList<DiscoveredTest> discovered,
        Action<string> notice,
        CancellationToken cancellationToken,
        bool lastFailed = false)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(discovered);
        ArgumentNullException.ThrowIfNull(notice);
        var resolved = Resolve(config, filters);

        // Each line also goes to the run's events (upstream's runner emits the filter's notices as `notice` events with target `filter`).
        void Say(string message)
        {
            notice(message);
            RunEvents.Emit(new NoticeEvent("filter", message));
        }

        // With no filter nothing judges a file, so the tree is not walked.
        var files = filters.Count == 0 ? [] : SourceFiles(config.ProjectRoot);
        var collection = Collect(discovered, files, out var keys);

        // The previous run's report is read where this run will write its own, before anything replaces it; no report is NO_LAST_RUN.
        var previous = lastFailed ? await LastRun.ReadPreviousRunAsync(ReportFile.PathFor(OutputLayout.Of(config.Output).Results), cancellationToken).ConfigureAwait(false) : null;
        var selectionFilters = new SelectionFilters { LastFailed = previous is null ? null : LastRun.LastFailedIds(previous.Report) };
        var first = Selector.Select(collection, resolved, selectionFilters, passWithNoTests: true);
        var filtered = changedSince is null
            ? new FilteredSelection(first, [], null)
            : await TestFilters.FilterSelection(first, new FilterSelectionOptions
            {
                Config = resolved,
                ChangedSince = changedSince,
                CancellationToken = cancellationToken,
                Notice = Say,
                Collection = collection,
                SelectionFilters = selectionFilters,
                Step = FilterStepAsync,
            }).ConfigureAwait(false);

        var excluded = new Dictionary<string, string>(StringComparer.Ordinal);
        var results = new List<ReportResult>();
        foreach (var pair in filtered.Selection.Pairs.Where(pair => pair.Skip is { Cause: "filtered" }))
        {
            if (excluded.TryAdd(keys[pair.Test.Id], pair.Skip!.Reason))
            {
                results.Add(LeftOutResult(pair.Test, resolved.Targets[0], pair.Skip));
            }
        }

        // The tree the rerun starts from keeps the files the report it reruns names, and its own attempts go in a directory no earlier run wrote into.
        string? rerunDir = null;
        if (previous is not null)
        {
            var resultsRoot = OutputLayout.Of(config.Output).Results;
            await ArtifactPaths.PruneArtifactsAsync(resultsRoot, LastRun.ReportArtifactPaths(previous.Report), cancellationToken).ConfigureAwait(false);
            rerunDir = await ArtifactPaths.NextRerunDirAsync(resultsRoot).ConfigureAwait(false);
        }

        return new HostSelection(
            excluded,
            results,
            filtered.Records,
            filtered.Changes,
            previous,
            previous is null ? null : new RerunCollection(new HashSet<string>(collection.Tests.Select(test => test.Id), StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal)),
            rerunDir);
    }

    // One filter's call as the `filter` setup step: started as it begins, finished with how long it took and, for a call that did not answer, how it ended.
    private static async Task<FilterCall> FilterStepAsync(string name, Func<Task<FilterCall>> work)
    {
        var step = new FilterSetupStep(name);
        RunEvents.Emit(new SetupEvent(step, SetupState.Started));
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var call = await work().ConfigureAwait(false);
        RunEvents.Emit(new SetupEvent(
            step,
            SetupState.Finished,
            (long)System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            call.Outcome switch { FilterOutcome.Failed => SetupOutcome.Failed, FilterOutcome.TimedOut => SetupOutcome.TimedOut, _ => null }));
        return call;
    }

    // A test left out ran no attempt, so the reason sits on the result, and the summary does not count the test as selected.
    private static ReportResult LeftOutResult(CollectedTest test, ResolvedTarget target, SkipInfo skip) => new()
    {
        // The id its executed row gets (class as the file, method as the title), so a fold finds both as one test.
        Id = AttemptReport.ResultId(test.Id[..test.Id.LastIndexOf("::", StringComparison.Ordinal)], test.Title),
        TestId = test.Id,
        File = test.File,
        TitlePath = test.TitlePath,
        DeclarationIndex = test.DeclarationIndex,
        TargetId = target.Name,
        Platform = target.Platform,
        Tags = test.Tags,
        Selected = false,
        Status = ResultStatus.Skipped,
        Skip = new ReportSkip(skip.Cause, skip.Reason, skip.Filter),
    };

    /// <summary>
    /// Puts what the filters decided into the run's <c>report.json</c> (<c>run.changes</c>, <c>run.filters</c>, and a skipped result per test
    /// left out), so the reporters can say so. Nothing is written when no filter had a say. The report is a courtesy: a failure is one line on stderr.
    /// </summary>
    public static async Task WriteReportAsync(E2EConfig config, HostSelection selection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.Changes is null && selection.Results.Count == 0 && selection.Previous is null)
        {
            return;
        }

        try
        {
            var target = new ReportTarget(config.Target.Name, 0, config.Target.Platform, BuildReport.DescribeTarget(null));
            await ReportFile.WriteLeftOutAsync(selection, target, config.ProjectRoot, OutputLayout.Of(config.Output).Results, Resolve(config, []), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine("[e2e] could not write " + ReportFile.FileName + " for the tests the filters left out: " + ex.Message);
        }
    }

    /// <summary>A one-target <see cref="ResolvedConfig"/> from the loaded config (.NET-only: the port has no resolver).</summary>
    internal static ResolvedConfig Resolve(E2EConfig config, IReadOnlyList<ITestFilter> filters) => new()
    {
        ProjectRoot = config.ProjectRoot,
        ConfigPath = config.ConfigPath,
        Ci = config.Ci,
        Targets = [new ResolvedTarget(config.Target.Name, config.Target.Platform, config.Target.App)],
        Filters = ResolvedConfig.ResolveTestFilters(filters),
        ConfigDigest = ResolvedConfig.ComputeConfigDigest(config),
        AllSecrets = [.. config.AllSecrets.Select(secret => (secret.Name, secret.Value))],
    };

    internal static Collection Collect(IReadOnlyList<DiscoveredTest> discovered, IReadOnlyList<string> sourceFiles, out Dictionary<string, string> keys)
    {
        keys = new Dictionary<string, string>(StringComparer.Ordinal);
        var tests = new List<CollectedTest>();
        foreach (var test in discovered)
        {
            var file = FileOf(test, sourceFiles);

            // The id a session reports for the test: its class and method (.NET-only: the report names a test by what the framework runs).
            var id = AttemptReport.TestId(test.Namespace.Length == 0 ? test.TypeName : test.Namespace + "." + test.TypeName, test.Title);
            keys[id] = test.Key;
            tests.Add(new CollectedTest
            {
                Kind = TestKind.Test,
                Title = test.Title,
                TitlePath = [test.TypeName, test.Title],
                DeclarationIndex = tests.Count,
                Tags = [.. test.Tags.Distinct(StringComparer.Ordinal)],
                File = file,
                Id = id,
                Options = new ResolvedTestOptions(null, [], null, ["default"], test.SkipReason, false),
            });
        }

        return new Collection { Files = [.. tests.Select(test => test.File).Distinct(StringComparer.Ordinal).Select(file => new CollectedFile { File = file })], Tests = tests };
    }

    /// <summary>
    /// The project-relative path of the file that declares the test class, by layout (.NET-only: reflection names no file). The file is
    /// <c>&lt;TypeName&gt;.cs</c> in a folder that follows the namespace. Of the files with that name under the project root, the one whose
    /// folders match the most trailing namespace parts wins; with none, the namespace folders themselves are the guess. A class that lives
    /// in a file of another name, or several classes in one file, give a wrong path.
    /// </summary>
    internal static string FileOf(DiscoveredTest test, IReadOnlyList<string> sourceFiles)
    {
        // A nested class lives in the file of its outermost class.
        var outer = test.TypeName.Split('+')[0];
        var name = outer + ".cs";
        var parts = test.Namespace.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var best = (File: (string?)null, Score: -1);
        foreach (var file in sourceFiles.Where(file => string.Equals(Path.GetFileName(file), name, StringComparison.Ordinal)))
        {
            var folders = file.Split('/')[..^1];
            var score = 0;
            while (score < parts.Length && score < folders.Length && string.Equals(folders[^(score + 1)], parts[^(score + 1)], StringComparison.Ordinal))
            {
                score++;
            }

            if (score > best.Score)
            {
                best = (file, score);
            }
        }

        return best.File ?? string.Join('/', [.. parts, name]);
    }

    /// <summary>Every <c>.cs</c> file under <paramref name="root"/>, as project-relative POSIX paths, build output and VCS folders left out.</summary>
    internal static IReadOnlyList<string> SourceFiles(string root)
    {
        var found = new List<string>();
        if (!Directory.Exists(root))
        {
            return found;
        }

        var pending = new Stack<string>([root]);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var file in Directory.EnumerateFiles(directory, "*.cs"))
            {
                found.Add(Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/'));
            }

            foreach (var child in Directory.EnumerateDirectories(directory).Where(child => !IgnoredDirectories.Contains(Path.GetFileName(child)) && !new DirectoryInfo(child).Attributes.HasFlag(FileAttributes.ReparsePoint)))
            {
                pending.Push(child);
            }
        }

        found.Sort(StringComparer.Ordinal);
        return found;
    }
}
