// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using E2E.Reports;

namespace E2E.GitHub;

/// <summary>Options of <see cref="GitHubReporter"/> (upstream's <c>GitHubOptions</c>, <c>reporter.ts</c>).</summary>
public sealed record GitHubOptions
{
    /// <summary>
    /// Tells one job's comment apart from another's when the workflow, job, and project are the same, as in a matrix. Without it, matrix replicas of
    /// one job share a comment and the last one to finish wins. Never the run id: a rerun must find the old comment. The key is also the comment's
    /// name in its headline: <c>e2e chromium: 77 passed</c>. A <see cref="GitHubReporter"/> with no options reads it from the
    /// <c>E2E_GITHUB_KEY</c> environment variable (.NET-only: the attribute that registers a reporter takes a type, not a value).
    /// </summary>
    public string? Key { get; init; }
}

/// <summary>What the reporter touches outside itself, so tests run it against fakes (upstream's <c>ReportDeps</c>, <c>reporter.ts</c>).</summary>
/// <param name="Env">Reads an environment variable.</param>
/// <param name="ReadFile">Reads a text file.</param>
/// <param name="Http">Sends the GitHub requests (upstream's <c>fetch</c>).</param>
/// <param name="AppendFile">Appends text to a file.</param>
/// <param name="Exists">Whether a path exists; the checkout root is the directory that holds <c>.git</c>.</param>
internal sealed record ReportDeps(
    Func<string, string?> Env,
    Func<string, CancellationToken, Task<string>> ReadFile,
    HttpClient Http,
    Func<string, string, CancellationToken, Task> AppendFile,
    Func<string, bool> Exists) : ActionsDeps(Env, ReadFile);

/// <summary>
/// The GitHub reporter (upstream's <c>github()</c>, <c>reporter.ts</c>): register it with <c>[assembly: E2EReporter(typeof(GitHubReporter))]</c>.
/// On GitHub Actions it writes the run to the job summary and, for a pull request, posts or updates one comment. Everything it reads (the token,
/// the event, the repository) is read when the run finishes, never when it is created, so constructing it has no side effects. Derive from it and
/// call <see cref="GitHubReporter(GitHubOptions)"/> to set the options in code. Not ported: the fold of a <c>--last-failed</c> rerun into the run
/// it selected from (upstream's <c>merge.ts</c>), since <see cref="FinishedRun"/> has no <c>LastRun</c> yet.
/// </summary>
public class GitHubReporter : Reporter
{
    private const string SummaryLabel = "GitHub";

    /// <summary>The environment variable that names the key when no options are given (.NET-only).</summary>
    internal const string KeyVariable = "E2E_GITHUB_KEY";

    /// <summary>
    /// Each encoded marker field is at most this many characters, so four fields and their names stay under a kilobyte whatever the input, well
    /// inside the room the page leaves under GitHub's size limit.
    /// </summary>
    private const int MaxMarkerFieldChars = 200;

    /// <summary>Hex characters of the SHA-256 digest that stands in for a long field's cut tail.</summary>
    private const int MarkerDigestChars = 16;

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(2);

    private static readonly Regex CutEscape = new(@"%[0-9A-F]?\z", RegexOptions.CultureInvariant, MatchTimeout);

    private readonly GitHubOptions? _options;

    /// <summary>A reporter whose key comes from the <c>E2E_GITHUB_KEY</c> environment variable. The public constructor <see cref="E2EReporterAttribute"/> needs.</summary>
    public GitHubReporter()
    {
    }

    /// <summary>A reporter with these options. For a class that derives from this one and is registered with <see cref="E2EReporterAttribute"/>.</summary>
    protected GitHubReporter(GitHubOptions options) => _options = options;

    /// <inheritdoc />
    public override string Name => "github";

    /// <inheritdoc />
    public override async Task<IReadOnlyList<ReporterSummaryRow>?> OnRunFinishedAsync(FinishedRun run, CancellationToken cancellationToken)
    {
        using var http = new HttpClient();
        var deps = new ReportDeps(
            Environment.GetEnvironmentVariable,
            (file, token) => File.ReadAllTextAsync(file, token),
            http,
            (file, text, token) => File.AppendAllTextAsync(file, text, token),
            path => File.Exists(path) || Directory.Exists(path));
        return await ReportRunAsync(run, deps, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The run's report with the options this reporter has, or the key <see cref="KeyVariable"/> names.</summary>
    internal Task<IReadOnlyList<ReporterSummaryRow>> ReportRunAsync(FinishedRun run, ReportDeps deps, CancellationToken cancellationToken) =>
        ReportRunAsync(run, _options ?? new GitHubOptions { Key = deps.Env(KeyVariable) }, deps, cancellationToken);

    /// <summary>
    /// One marker field, URI-encoded. A field that fits is the encoded value itself, as it always was, so comments already on open pull requests are
    /// still found. A longer one keeps a readable prefix, never cut inside a percent escape, then <c>#</c> and a digest of the whole value: encoding
    /// escapes every <c>#</c>, so no value that fits can read the same, and two long values that share a prefix still differ.
    /// </summary>
    internal static string MarkerField(string value)
    {
        var encoded = EncodeUriComponent(value);
        if (encoded.Length <= MaxMarkerFieldChars)
        {
            return encoded;
        }

        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..MarkerDigestChars];
        var prefix = CutEscape.Replace(encoded[..(MaxMarkerFieldChars - MarkerDigestChars - 1)], "");
        return $"{prefix}#{digest}";
    }

    /// <summary>JavaScript's <c>encodeURIComponent</c>, which also leaves <c>! ' ( ) *</c> as they are, so a marker reads the same as upstream's.</summary>
    private static string EncodeUriComponent(string value) =>
        Uri.EscapeDataString(value).Replace("%21", "!", StringComparison.Ordinal).Replace("%27", "'", StringComparison.Ordinal)
            .Replace("%28", "(", StringComparison.Ordinal).Replace("%29", ")", StringComparison.Ordinal).Replace("%2A", "*", StringComparison.Ordinal);

    /// <summary>
    /// The marker one job's comment carries, so two workflows or jobs each keep their own. Every field is cut the same way on every run, so a rerun
    /// derives the same marker and finds its comment. An <c>e2e explore</c> run is its own kind: in a job that runs the tests and then explores, each
    /// keeps its comment. A test run adds no kind, so its comments posted before explore had one are still found.
    /// </summary>
    internal static string CommentMarker(FinishedRun run, ActionsContext context, string? key)
    {
        (string Name, string? Value)[] fields =
        [
            ("project", run.Report.Run.Project.Id),
            ("workflow", context.Workflow),
            ("job", context.Job),
            ("kind", run.Report.Run.Explore is null ? null : "explore"),
            ("key", key),
        ];
        var parts = fields.Where(field => !string.IsNullOrEmpty(field.Value)).Select(field => $"{field.Name}={MarkerField(field.Value!)}");
        return $"<!-- e2e-github {string.Join(' ', parts)} -->";
    }

    /// <summary>
    /// The directory the repository is checked out in: the nearest ancestor of the project root, itself included, that holds <c>.git</c>.
    /// <c>actions/checkout</c> puts it at <c>GITHUB_WORKSPACE</c> unless its <c>path</c> input names a subdirectory, so the workspace bounds the walk
    /// and stands in when no <c>.git</c> is found.
    /// </summary>
    private static string CheckoutRoot(string workspace, string projectRoot, Func<string, bool> exists)
    {
        var dir = projectRoot;
        while (dir.StartsWith(workspace, StringComparison.Ordinal))
        {
            if (exists(Path.Combine(dir, ".git")))
            {
                return dir;
            }

            var parent = Path.GetDirectoryName(dir);
            if (parent is null || parent == dir)
            {
                break;
            }

            dir = parent;
        }

        return workspace;
    }

    /// <summary>
    /// The project's path inside the checkout, as segments. The report's files are relative to the project root, and a suite that lives in
    /// <c>packages/e2e-tests</c> links to <c>blob/&lt;sha&gt;/packages/e2e-tests/tests/...</c>. A project at the checkout root, or one outside it, adds nothing.
    /// </summary>
    private static string[] ProjectSegments(string? workspace, string projectRoot, Func<string, bool> exists)
    {
        if (workspace is null)
        {
            return [];
        }

        var relative = Path.GetRelativePath(CheckoutRoot(workspace, projectRoot, exists), projectRoot);
        var parent = $"..{Path.DirectorySeparatorChar}";
        if (relative is "." or ".." || relative.StartsWith(parent, StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            return [];
        }

        return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static Func<string, int, string>? SourceUrl(ActionsContext context, string projectRoot, ReportDeps deps)
    {
        if (context.Sha is not { } sha)
        {
            return null;
        }

        var prefix = ProjectSegments(context.Workspace, projectRoot, deps.Exists);
        return (file, line) =>
            $"{context.ServerUrl}/{context.Repository}/blob/{sha}/{string.Join('/', prefix.Concat(file.Split('/')).Select(EncodeUriComponent))}#L{line}";
    }

    private static async Task<string?> WriteSummaryAsync(ActionsContext context, string text, ReportDeps deps, CancellationToken cancellationToken)
    {
        if (context.StepSummaryPath is null)
        {
            return null;
        }

        try
        {
            await deps.AppendFile(context.StepSummaryPath, text + "\n", cancellationToken).ConfigureAwait(false);
            return "";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return string.IsNullOrEmpty(ex.Message) ? ex.GetType().Name : ex.Message;
        }
    }

    /// <summary>
    /// Reports one finished run and returns the summary rows describing what happened (upstream's <c>reportRun</c>). A GitHub failure surfaces as the
    /// one error the runner prints, after the summary is written.
    /// </summary>
    internal static async Task<IReadOnlyList<ReporterSummaryRow>> ReportRunAsync(FinishedRun run, GitHubOptions options, ReportDeps deps, CancellationToken cancellationToken)
    {
        var context = await Actions.DetectActionsAsync(deps, cancellationToken).ConfigureAwait(false);
        if (context is null)
        {
            return [new ReporterSummaryRow(SummaryLabel, "not posted: not running on GitHub Actions")];
        }

        // One page for both places; the comment carries the marker a rerun finds it by. Evidence links land on the run page's Artifacts section,
        // where the job's upload-artifact step put the files; the page names each file's path inside it.
        var page = MarkdownReport.RenderMarkdownReport(
            run.Report,
            new MarkdownReportOptions
            {
                ArtifactsUrl = $"{context.RunUrl}#artifacts",
                SourceUrl = SourceUrl(context, run.ProjectRoot, deps),
                Title = options.Key,
            });
        var summary = await WriteSummaryAsync(context, page, deps, cancellationToken).ConfigureAwait(false);
        var marker = CommentMarker(run, context, options.Key);

        string lead;
        if (context.PullRequest is not { } pullRequest)
        {
            lead = $"not posted: {context.EventName} is not a pull request{(summary == "" ? "; written to the job summary" : "")}";
        }
        else if (context.Token is not { } token)
        {
            lead = $"not posted: set GITHUB_TOKEN in the step's env (GITHUB_TOKEN: ${{{{ github.token }}}}){(summary == "" ? "; written to the job summary" : "")}";
        }
        else
        {
            lead = await Post.UpsertCommentAsync(
                new PostParams(deps.Http, context.ApiUrl, token, context.Repository, pullRequest, marker, $"{marker}\n{page}"),
                cancellationToken).ConfigureAwait(false);
        }

        List<ReporterSummaryRow> rows = [new(SummaryLabel, lead)];
        if (!string.IsNullOrEmpty(summary))
        {
            rows.Add(new(SummaryLabel, $"job summary not written: {summary}"));
        }

        return rows;
    }
}
