// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E;

/// <summary>What <see cref="TracePages.WriteTracePagesAsync"/> needs to know about where the files are.</summary>
/// <param name="ProjectRoot">Where the paths it returns start.</param>
/// <param name="ResultsRoot"><c>&lt;output&gt;/results</c>: the artifact paths are relative to it, and each page goes in its test's directory there.</param>
/// <param name="CacheDir">The replay cache directory, where a step's entry file is; null when a custom store holds the cache.</param>
internal sealed record WriteTracePagesOptions(string ProjectRoot, string ResultsRoot, string? CacheDir = null);

/// <summary>
/// The traces a run keeps (upstream's <c>report/traces.ts</c>): one markdown page per test whose <c>trace</c> mode kept one, as
/// <c>trace.md</c> in the test's directory under <c>&lt;output&gt;/results/</c>, beside its attempts' artifacts, telling the whole
/// attempt: each step with what it did, the cache's decisions, what the app logged, the agent's turns, and the screen at failure. A
/// coding agent reads one file instead of the report.
/// </summary>
internal static class TracePages
{
    /// <summary>What a test's page is called inside its directory. Upstream's <c>TRACE_PAGE</c>.</summary>
    public const string TracePage = "trace.md";

    /// <summary>
    /// Writes the page of every result in <paramref name="traced"/> (result ids) into its test's directory,
    /// <c>&lt;results&gt;/&lt;test&gt;/trace.md</c>, and returns where each went, as a path from the project root. The text goes
    /// through <paramref name="redact"/> before it is written.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, string>> WriteTracePagesAsync(
        Report1Document report,
        IReadOnlySet<string> traced,
        WriteTracePagesOptions options,
        Func<string, string>? redact = null,
        CancellationToken cancellationToken = default)
    {
        string Relative(string absolute)
        {
            var path = Path.GetRelativePath(options.ProjectRoot, absolute).Replace('\\', '/');
            return path.Length == 0 ? "." : path;
        }

        string? ReadArtifact(string reportPath)
        {
            try
            {
                return File.ReadAllText(Path.Combine(options.ResultsRoot, reportPath));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        Dictionary<string, string> pages = new(StringComparer.Ordinal);
        foreach (var result in report.Run.Results.Where(result => traced.Contains(result.Id)))
        {
            var dir = result.Directory ?? ArtifactPaths.ResultSegment(result.Id, result.File, result.TitlePath);
            var file = Path.Combine(options.ResultsRoot, dir, TracePage);
            var page = FailureText.RenderTracePage(
                report,
                result,
                Outcome.Of(result),
                new TracePageOptions
                {
                    ArtifactsDir = Source.ProjectRelativePath(options.ProjectRoot, options.ResultsRoot) ?? options.ResultsRoot.Replace('\\', '/'),
                    PageDir = dir,
                    ReadArtifact = ReadArtifact,
                    CacheDir = options.CacheDir is null ? null : Source.ProjectRelativePath(options.ProjectRoot, options.CacheDir) ?? options.CacheDir.Replace('\\', '/'),
                });
            await WriteTextAsync(file, redact?.Invoke(page) ?? page, cancellationToken).ConfigureAwait(false);
            pages[result.Id] = Relative(file);
        }

        return pages;
    }

    // Through a unique temp name, then moved, so a reader never sees half a page.
    internal static async Task WriteTextAsync(string file, string text, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temp = file + "." + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temp, text, cancellationToken).ConfigureAwait(false);
            File.Move(temp, file, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }
    }
}
