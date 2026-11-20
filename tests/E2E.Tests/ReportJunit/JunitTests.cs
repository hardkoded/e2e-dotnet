// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Reports;
using E2E.Tests.Store;
using static E2E.Tests.ReportJunit.JunitKit;
using R = E2E.Reports;

namespace E2E.Tests.ReportJunit;

/// <summary>Upstream's <c>describe('junit')</c> (<c>tests/unit/report-junit.test.ts</c>): the reporter, and where it points the attachment.</summary>
[Collection(ProcessEnvironment.Name)]
public sealed class JunitTests : IDisposable
{
    private readonly List<string> _tmpDirs = [];

    /// <summary>A fresh directory removed after each test.</summary>
    private string MakeTmpDir()
    {
        var dir = Directory.CreateTempSubdirectory("junit-");
        var resolved = dir.ResolveLinkTarget(true)?.FullName ?? dir.FullName;
        _tmpDirs.Add(resolved);
        return resolved;
    }

    public void Dispose()
    {
        foreach (var dir in _tmpDirs)
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Runs the reporter on a failed run and returns the attachment line it wrote, if any.</summary>
    private static async Task<string?> AttachmentFor(string projectRoot, string artifactsRoot)
    {
        var run = new R.FinishedRun(
            FailedWithShot(),
            ReportRunStatus.Failed,
            1,
            projectRoot,
            Path.Combine(artifactsRoot, "report.json"),
            artifactsRoot
        );
        var rows = await new JunitReporter().OnRunFinishedAsync(run, CancellationToken.None);
        var file = Path.Combine(artifactsRoot, "junit.xml");
        Assert.Equal("JUnit", Assert.Single(rows!).Label);
        var xml = await File.ReadAllTextAsync(file);
        AssertWellFormed(xml);
        var match = Regex.Match(xml, @"\[\[ATTACHMENT\|([^\]]*)\]\]");
        return match.Success ? match.Groups[1].Value : null;
    }

    [Theory]
    [InlineData("unset", ".e2e/results/checkout/001-failure.png")]
    [InlineData("blank", ".e2e/results/checkout/001-failure.png")]
    [InlineData("a parent of the project", "apps/testbed/.e2e/results/checkout/001-failure.png")]
    [InlineData("the artifacts root itself", "checkout/001-failure.png")]
    public async Task Resolves_the_attachment_when_CI_PROJECT_DIR_is(string name, string expected)
    {
        var project = Path.Combine(MakeTmpDir(), "apps", "testbed");
        var ci = name switch
        {
            "blank" => "  ",
            "a parent of the project" => Path.GetDirectoryName(Path.GetDirectoryName(project)),
            "the artifacts root itself" => Path.Combine(project, ".e2e", "results"),
            _ => null,
        };
        using var env = ProcessEnvironment.Set("CI_PROJECT_DIR", ci);
        Assert.Equal(expected, await AttachmentFor(project, Path.Combine(project, ".e2e", "results")));
    }

    [Theory]
    [InlineData("the repository root", "apps/testbed/.e2e/results/checkout/001-failure.png")]
    [InlineData("the artifacts root", "checkout/001-failure.png")]
    public async Task Resolves_through_a_CI_PROJECT_DIR_symlinked_to(string name, string expected)
    {
        var root = MakeTmpDir();
        var project = Path.Combine(root, "apps", "testbed");
        var artifacts = Path.Combine(project, ".e2e", "results");
        Directory.CreateDirectory(artifacts);
        var symlink = Path.Combine(MakeTmpDir(), "ci");
        Directory.CreateSymbolicLink(symlink, name == "the repository root" ? root : artifacts);
        using var env = ProcessEnvironment.Set("CI_PROJECT_DIR", symlink);
        Assert.Equal(expected, await AttachmentFor(project, artifacts));
    }

    [Fact]
    public async Task Resolves_an_artifacts_root_that_is_a_symlink_to_CI_PROJECT_DIR()
    {
        var ci = MakeTmpDir();
        var artifacts = Path.Combine(MakeTmpDir(), "results");
        Directory.CreateSymbolicLink(artifacts, ci);
        using var env = ProcessEnvironment.Set("CI_PROJECT_DIR", ci);
        Assert.Equal("checkout/001-failure.png", await AttachmentFor(MakeTmpDir(), artifacts));
    }

    [Fact]
    public async Task Writes_the_artifacts_root_equal_to_the_project_root_as_a_bare_path()
    {
        using var env = ProcessEnvironment.Set("CI_PROJECT_DIR", null);
        var project = MakeTmpDir();
        Assert.Equal("checkout/001-failure.png", await AttachmentFor(project, project));
    }

    [Theory]
    [InlineData("CI_PROJECT_DIR", true)]
    [InlineData("the project root", false)]
    public async Task Writes_no_attachment_when_the_artifacts_are_outside(string name, bool ci)
    {
        var project = MakeTmpDir();
        using var env = ProcessEnvironment.Set("CI_PROJECT_DIR", ci ? MakeTmpDir() : null);
        Assert.Null(await AttachmentFor(project, Path.Combine(MakeTmpDir(), "results")));
        Assert.NotEmpty(name);
    }
}
