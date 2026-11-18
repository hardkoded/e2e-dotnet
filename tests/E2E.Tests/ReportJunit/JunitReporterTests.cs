// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Reports;
using E2E.Tests.Traces;
using static E2E.Tests.ReportJunit.JunitKit;

namespace E2E.Tests.ReportJunit;

/// <summary>
/// Upstream's <c>describe('junitReporter')</c> (<c>tests/unit/report-junit.test.ts</c>). It sets <c>CI_PROJECT_DIR</c>, so its tests do not run in
/// parallel with each other.
/// </summary>
[Collection("CI_PROJECT_DIR")]
public sealed partial class JunitReporterTests : IDisposable
{
    private readonly List<string> _dirs = [];
    private readonly string? _ci = Environment.GetEnvironmentVariable("CI_PROJECT_DIR");

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("CI_PROJECT_DIR", _ci);
        foreach (var dir in _dirs)
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [GeneratedRegex(@"\[\[ATTACHMENT\|([^\]]*)\]\]")]
    private static partial Regex Attachment();

    private string NewDir()
    {
        var dir = Directory.CreateTempSubdirectory("junit-");
        _dirs.Add(dir.FullName);
        return (dir.ResolveLinkTarget(true) ?? dir).FullName;
    }

    /// <summary>Runs the reporter on a failed run and returns the attachment line it wrote, if any.</summary>
    private static async Task<string?> AttachmentFor(string projectRoot, string artifactsRoot)
    {
        Directory.CreateDirectory(artifactsRoot);
        var run = new FinishedRun(
            ReportBuilders.Public(FailedWithShot()),
            ReportRunStatus.Failed,
            1,
            projectRoot,
            Path.Combine(artifactsRoot, "report.json"),
            artifactsRoot
        );

        await new JunitReporter().OnRunFinishedAsync(run, CancellationToken.None);

        var xml = await File.ReadAllTextAsync(Path.Combine(artifactsRoot, "junit.xml"));
        AssertWellFormed(xml);
        var match = Attachment().Match(xml);
        return match.Success ? match.Groups[1].Value : null;
    }

    [Theory]
    [InlineData("unset", ".e2e/results/checkout/001-failure.png")]
    [InlineData("blank", ".e2e/results/checkout/001-failure.png")]
    [InlineData("a parent of the project", "apps/testbed/.e2e/results/checkout/001-failure.png")]
    [InlineData("the artifacts root itself", "checkout/001-failure.png")]
    public async Task Resolves_the_attachment_when_CI_PROJECT_DIR_is(string name, string expected)
    {
        var project = Path.Combine(NewDir(), "apps", "testbed");
        Environment.SetEnvironmentVariable(
            "CI_PROJECT_DIR",
            name switch
            {
                "blank" => "  ",
                "a parent of the project" => Path.GetDirectoryName(Path.GetDirectoryName(project)),
                "the artifacts root itself" => Path.Combine(project, ".e2e", "results"),
                _ => null,
            }
        );

        Assert.Equal(expected, await AttachmentFor(project, Path.Combine(project, ".e2e", "results")));
    }

    [Theory]
    [InlineData("the repository root", "apps/testbed/.e2e/results/checkout/001-failure.png")]
    [InlineData("the artifacts root", "checkout/001-failure.png")]
    public async Task Resolves_through_a_CI_PROJECT_DIR_symlinked_to(string name, string expected)
    {
        var root = NewDir();
        var project = Path.Combine(root, "apps", "testbed");
        var artifacts = Path.Combine(project, ".e2e", "results");
        Directory.CreateDirectory(artifacts);
        var symlink = Path.Combine(NewDir(), "ci");
        Directory.CreateSymbolicLink(symlink, name == "the repository root" ? root : artifacts);
        Environment.SetEnvironmentVariable("CI_PROJECT_DIR", symlink);

        Assert.Equal(expected, await AttachmentFor(project, artifacts));
    }

    [Fact]
    public async Task Resolves_an_artifacts_root_that_is_a_symlink_to_CI_PROJECT_DIR()
    {
        var ci = NewDir();
        var artifacts = Path.Combine(NewDir(), "results");
        Directory.CreateSymbolicLink(artifacts, ci);
        Environment.SetEnvironmentVariable("CI_PROJECT_DIR", ci);

        Assert.Equal("checkout/001-failure.png", await AttachmentFor(NewDir(), artifacts));
    }

    [Fact]
    public async Task Writes_the_artifacts_root_equal_to_the_project_root_as_a_bare_path()
    {
        Environment.SetEnvironmentVariable("CI_PROJECT_DIR", null);
        var project = NewDir();

        Assert.Equal("checkout/001-failure.png", await AttachmentFor(project, project));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Writes_no_attachment_when_the_artifacts_are_outside_CI_PROJECT_DIR_or_the_project_root(bool ci)
    {
        var project = NewDir();
        Environment.SetEnvironmentVariable("CI_PROJECT_DIR", ci ? NewDir() : null);

        Assert.Null(await AttachmentFor(project, Path.Combine(NewDir(), "results")));
    }
}
