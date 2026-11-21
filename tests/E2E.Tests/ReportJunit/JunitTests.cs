// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Internal;
using E2E.Tests.Store;
using E2E.Tests.Traces;
using static E2E.Tests.ReportJunit.JunitKit;
using FinishedRun = E2E.Reports.FinishedRun;
using JunitReporter = E2E.Reports.JunitReporter;
using Report = E2E.Reports.Report;

namespace E2E.Tests.ReportJunit;

/// <summary>
/// Upstream's <c>describe('junit')</c> (<c>tests/unit/report-junit.test.ts</c>). Upstream asks the reporter for its <c>reportFiles</c> and the runner writes
/// them; the port has no such hook, so the reporter writes <c>junit.xml</c> in <c>OnRunFinishedAsync</c> and each test reads the file back. The tests that set
/// <c>CI_PROJECT_DIR</c> run one at a time, in the process environment collection.
/// </summary>
[Collection(ProcessEnvironment.Name)]
public sealed class JunitTests : IDisposable
{
    private readonly List<string> _directories = [];

    public void Dispose()
    {
        foreach (var directory in _directories)
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>A fresh directory removed after each test, resolved through any symlink in the temporary path.</summary>
    private string MakeTmpDir()
    {
        var directory = Directory.CreateTempSubdirectory("junit-");
        var real = directory.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? directory.FullName;
        _directories.Add(directory.FullName);
        return Paths.OnDiskPath(real);
    }

    /// <summary>Runs the reporter on a failed run and returns the attachment line it wrote, if any.</summary>
    private static async Task<string?> AttachmentFor(string projectRoot, string artifactsRoot)
    {
        Directory.CreateDirectory(artifactsRoot);
        var reportPath = Path.Combine(artifactsRoot, "report.json");
        var run = new FinishedRun(
            ReportBuilders.Public(FailedWithShot()),
            ReportRunStatus.Failed,
            1,
            projectRoot,
            reportPath,
            artifactsRoot
        );
        var rows = await new JunitReporter().OnRunFinishedAsync(run, CancellationToken.None);
        var path = Path.Combine(artifactsRoot, "junit.xml");
        Assert.Equal("JUnit", Assert.Single(rows!).Label);
        Assert.True(File.Exists(path));
        var xml = await File.ReadAllTextAsync(path);
        AssertWellFormed(xml);
        var match = Regex.Match(xml, "\\[\\[ATTACHMENT\\|([^\\]]*)\\]\\]");
        return match.Success ? match.Groups[1].Value : null;
    }

    [Fact]
    public async Task Resolves_the_attachment_when_CI_PROJECT_DIR_is_unset()
    {
        var project = Path.Combine(MakeTmpDir(), "apps", "testbed");
        using var _ = ProcessEnvironment.Set("CI_PROJECT_DIR", null);
        Assert.Equal(
            ".e2e/results/checkout/001-failure.png",
            await AttachmentFor(project, Path.Combine(project, ".e2e", "results"))
        );
    }

    [Fact]
    public async Task Resolves_the_attachment_when_CI_PROJECT_DIR_is_blank()
    {
        var project = Path.Combine(MakeTmpDir(), "apps", "testbed");
        using var _ = ProcessEnvironment.Set("CI_PROJECT_DIR", "  ");
        Assert.Equal(
            ".e2e/results/checkout/001-failure.png",
            await AttachmentFor(project, Path.Combine(project, ".e2e", "results"))
        );
    }

    [Fact]
    public async Task Resolves_the_attachment_when_CI_PROJECT_DIR_is_a_parent_of_the_project()
    {
        var project = Path.Combine(MakeTmpDir(), "apps", "testbed");
        using var _ = ProcessEnvironment.Set("CI_PROJECT_DIR", Path.GetDirectoryName(Path.GetDirectoryName(project)));
        Assert.Equal(
            "apps/testbed/.e2e/results/checkout/001-failure.png",
            await AttachmentFor(project, Path.Combine(project, ".e2e", "results"))
        );
    }

    [Fact]
    public async Task Resolves_the_attachment_when_CI_PROJECT_DIR_is_the_artifacts_root_itself()
    {
        var project = Path.Combine(MakeTmpDir(), "apps", "testbed");
        using var _ = ProcessEnvironment.Set("CI_PROJECT_DIR", Path.Combine(project, ".e2e", "results"));
        Assert.Equal(
            "checkout/001-failure.png",
            await AttachmentFor(project, Path.Combine(project, ".e2e", "results"))
        );
    }

    [Fact]
    public async Task Resolves_through_a_CI_PROJECT_DIR_symlinked_to_the_repository_root()
    {
        var root = MakeTmpDir();
        var project = Path.Combine(root, "apps", "testbed");
        var artifacts = Path.Combine(project, ".e2e", "results");
        Directory.CreateDirectory(artifacts);
        var symlink = Path.Combine(MakeTmpDir(), "ci");
        Directory.CreateSymbolicLink(symlink, root);
        using var _ = ProcessEnvironment.Set("CI_PROJECT_DIR", symlink);
        Assert.Equal("apps/testbed/.e2e/results/checkout/001-failure.png", await AttachmentFor(project, artifacts));
    }

    [Fact]
    public async Task Resolves_through_a_CI_PROJECT_DIR_symlinked_to_the_artifacts_root()
    {
        var root = MakeTmpDir();
        var project = Path.Combine(root, "apps", "testbed");
        var artifacts = Path.Combine(project, ".e2e", "results");
        Directory.CreateDirectory(artifacts);
        var symlink = Path.Combine(MakeTmpDir(), "ci");
        Directory.CreateSymbolicLink(symlink, artifacts);
        using var _ = ProcessEnvironment.Set("CI_PROJECT_DIR", symlink);
        Assert.Equal("checkout/001-failure.png", await AttachmentFor(project, artifacts));
    }

    [Fact]
    public async Task Resolves_an_artifacts_root_that_is_a_symlink_to_CI_PROJECT_DIR()
    {
        var ci = MakeTmpDir();
        var artifacts = Path.Combine(MakeTmpDir(), "results");
        Directory.CreateSymbolicLink(artifacts, ci);
        using var _ = ProcessEnvironment.Set("CI_PROJECT_DIR", ci);
        Assert.Equal("checkout/001-failure.png", await AttachmentFor(MakeTmpDir(), artifacts));
    }

    [Fact]
    public async Task Writes_the_artifacts_root_equal_to_the_project_root_as_a_bare_path()
    {
        using var _ = ProcessEnvironment.Set("CI_PROJECT_DIR", null);
        var project = MakeTmpDir();
        Assert.Equal("checkout/001-failure.png", await AttachmentFor(project, project));
    }

    [Fact]
    public async Task Writes_no_attachment_when_the_artifacts_are_outside_CI_PROJECT_DIR()
    {
        var project = MakeTmpDir();
        using var _ = ProcessEnvironment.Set("CI_PROJECT_DIR", MakeTmpDir());
        Assert.Null(await AttachmentFor(project, Path.Combine(MakeTmpDir(), "results")));
    }

    [Fact]
    public async Task Writes_no_attachment_when_the_artifacts_are_outside_the_project_root()
    {
        var project = MakeTmpDir();
        using var _ = ProcessEnvironment.Set("CI_PROJECT_DIR", null);
        Assert.Null(await AttachmentFor(project, Path.Combine(MakeTmpDir(), "results")));
    }
}
