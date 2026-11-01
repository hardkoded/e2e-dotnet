// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

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
/// Wires the test filters into a framework adapter's run (upstream's host applies <c>filterSelection</c> after <c>select</c>; .NET-only
/// name). The adapter hands over the tests it found by reflection once, before the first test. The seam collects them, selects, runs
/// <see cref="TestFilters.FilterSelection"/>, and returns the tests the filters left out, each with its reason, for the test to skip itself.
/// </summary>
internal static class HostTestFilters
{
    /// <summary>The environment variable that names the base ref when the host gives none (<c>--changed-since</c>).</summary>
    public const string ChangedSinceVariable = "E2E_CHANGED_SINCE";

    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase) { "bin", "obj", "node_modules", ".git", ".vs", ".e2e" };

    /// <summary>The base ref to measure the change from: <paramref name="configured"/>, else <c>E2E_CHANGED_SINCE</c>; null when neither is set.</summary>
    public static string? ResolveChangedSince(string? configured, Func<string, string?>? environment = null)
    {
        var value = string.IsNullOrWhiteSpace(configured) ? (environment ?? Environment.GetEnvironmentVariable)(ChangedSinceVariable) : configured;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// The tests the filters left out, by <see cref="DiscoveredTest.Key"/>, with the reason each filter gave. Empty when the filters left
    /// none out or none ran. The selection is made once for the whole run.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, string>> ExcludeAsync(
        E2EConfig config,
        IReadOnlyList<ITestFilter> filters,
        string changedSince,
        IReadOnlyList<DiscoveredTest> discovered,
        Action<string> notice,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(discovered);
        ArgumentNullException.ThrowIfNull(notice);
        var resolved = Resolve(config, filters);
        var files = SourceFiles(config.ProjectRoot);
        var collection = Collect(discovered, files, out var keys);
        var selectionFilters = new SelectionFilters();
        var first = Selector.Select(collection, resolved, selectionFilters, passWithNoTests: true);
        var filtered = await TestFilters.FilterSelection(first, new FilterSelectionOptions
        {
            Config = resolved,
            ChangedSince = changedSince,
            CancellationToken = cancellationToken,
            Notice = notice,
            Collection = collection,
            SelectionFilters = selectionFilters,
        }).ConfigureAwait(false);

        var excluded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in filtered.Selection.Pairs.Where(pair => pair.Skip is { Cause: "filtered", Filter: not null }))
        {
            excluded.TryAdd(keys[pair.Test.Id], pair.Skip!.Reason);
        }

        return excluded;
    }

    /// <summary>A one-target <see cref="ResolvedConfig"/> from the loaded config (.NET-only: the port has no resolver).</summary>
    internal static ResolvedConfig Resolve(E2EConfig config, IReadOnlyList<ITestFilter> filters) => new()
    {
        ProjectRoot = config.ProjectRoot,
        ConfigPath = config.ConfigPath,
        Ci = config.Ci,
        Targets = [new ResolvedTarget(config.Target.Name, config.Target.Platform, config.Target.App)],
        Filters = filters,
        AllSecrets = [.. config.Secrets.Names.Select(name => (name, config.Secrets.Get(name).Value))],
    };

    internal static Collection Collect(IReadOnlyList<DiscoveredTest> discovered, IReadOnlyList<string> sourceFiles, out Dictionary<string, string> keys)
    {
        keys = new Dictionary<string, string>(StringComparer.Ordinal);
        var tests = new List<CollectedTest>();
        foreach (var test in discovered)
        {
            var file = FileOf(test, sourceFiles);
            var id = AttemptReport.TestId(file, test.TypeName + " > " + test.Title);
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

            foreach (var child in Directory.EnumerateDirectories(directory).Where(child => !IgnoredDirectories.Contains(Path.GetFileName(child))))
            {
                pending.Push(child);
            }
        }

        found.Sort(StringComparer.Ordinal);
        return found;
    }
}
