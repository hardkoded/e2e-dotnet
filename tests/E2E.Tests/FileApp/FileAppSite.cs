// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.FileApp;

/// <summary>
/// The site and helpers of upstream's <c>file-app.test.ts</c> (<c>packages/e2e/tests/integration</c>): a project directory with a
/// <c>site/</c> directory, a page beside it, and the file the app is opened from.
/// </summary>
internal static class FileAppSite
{
    /// <summary>Upstream's <c>SITE</c>.</summary>
    public static readonly IReadOnlyDictionary<string, string> Site = new Dictionary<string, string>
    {
        ["site/index.html"] =
            "<!doctype html><title>Home</title><h1>Home</h1><a href=\"pages/about.html\">About</a><a href=\"../secret.html\">Notes</a>",
        ["site/pages/about.html"] =
            "<!doctype html><title>About</title><h1>About</h1><a href=\"../index.html\">Home</a>",
        ["secret.html"] = "<!doctype html><h1>TOP-SECRET-NOTES</h1>",
    };

    /// <summary>Upstream's <c>createProject({ ...SITE })</c>.</summary>
    public static FixtureProject CreateProject()
    {
        var project = RunProject.CreateProject();
        foreach (var (file, content) in Site)
        {
            var path = Path.Combine(project.Dir, file);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        return project;
    }

    /// <summary>Upstream's <c>fileUrl</c>: the app file's URL.</summary>
    public static string FileUrl(FixtureProject project) =>
        new Uri(Path.Combine(project.Dir, "site", "index.html")).AbsoluteUri;

    public static RunProjectOptions Options(
        FixtureProject project,
        IReadOnlyList<InMemoryTest> tests,
        IReadOnlyDictionary<string, AgentOptions>? agents = null
    ) =>
        new()
        {
            Tests = tests,
            Engine = new WebEngine(headless: true),
            BaseUrl = FileUrl(project),
            Agents = agents,
        };

    /// <summary>The screenshots a run kept, as upstream filters <c>artifacts</c> by <c>kind === 'screenshot'</c>.</summary>
    public static IReadOnlyList<E2E.Reports.ArtifactRecord> Screenshots(E2E.Reports.ReportAttempt attempt) =>
        [.. attempt.Artifacts.Where(artifact => artifact.Kind == E2E.Reports.ArtifactKind.Screenshot)];

    public static IReadOnlyList<E2E.Reports.ArtifactRecord> Screenshots(RunOutcome outcome) =>
        [.. outcome.Report.Run.Results.SelectMany(result => result.Attempts).SelectMany(Screenshots)];
}
