// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Tests.Store;
using static E2E.Tests.ReportJunit.JunitKit;

namespace E2E.Tests.ReportJunit;

#pragma warning disable IDE0065 // Inside the namespace on purpose: the public records of a reporter win over the internal ones of the same names in E2E.
using E2E.Reports;
#pragma warning restore IDE0065

/// <summary>
/// Upstream's <c>describe('junit')</c> (<c>tests/unit/report-junit.test.ts</c>). Upstream's reporter hands its file to the runner (<c>reportFiles</c>); the
/// port's reporter writes it when the run finishes, so each test reads the file it wrote.
/// </summary>
[Collection(ProcessEnvironment.Name)]
public sealed partial class JunitTests : IDisposable
{
    private readonly List<string> _tmpDirs = [];

    [GeneratedRegex("\\[\\[ATTACHMENT\\|([^\\]]*)\\]\\]")]
    private static partial Regex Attachment();

    public void Dispose()
    {
        foreach (var dir in _tmpDirs)
        {
            Directory.Delete(dir, recursive: true);
        }
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
            "unset" => null,
            "blank" => "  ",
            "a parent of the project" => Path.GetDirectoryName(Path.GetDirectoryName(project)),
            _ => Path.Combine(project, ".e2e", "results"),
        };
        using var _ = ProcessEnvironment.Set("CI_PROJECT_DIR", ci);
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
        using var _ = ProcessEnvironment.Set("CI_PROJECT_DIR", symlink);
        Assert.Equal(expected, await AttachmentFor(project, artifacts));
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

    [Theory]
    [InlineData("CI_PROJECT_DIR", true)]
    [InlineData("the project root", false)]
    public async Task Writes_no_attachment_when_the_artifacts_are_outside(string name, bool ci)
    {
        Assert.NotEmpty(name);
        var project = MakeTmpDir();
        using var _ = ProcessEnvironment.Set("CI_PROJECT_DIR", ci ? MakeTmpDir() : null);
        Assert.Null(await AttachmentFor(project, Path.Combine(MakeTmpDir(), "results")));
    }

    // .NET-only: the reporter's options and its file placement have no upstream `it`.
    [Fact]
    public async Task Writes_to_outputFile_relative_to_the_project_root_and_refuses_the_run_report()
    {
        using var _ = ProcessEnvironment.Set("CI_PROJECT_DIR", null);
        var project = MakeTmpDir();
        var artifacts = Path.Combine(project, ".e2e", "results");
        Directory.CreateDirectory(artifacts);
        var reportPath = Path.Combine(artifacts, "report.json");
        await File.WriteAllTextAsync(reportPath, "{}");
        var run = new FinishedRun(FailedWithShot(), ReportRunStatus.Failed, 1, project, reportPath, artifacts);

        var rows = await new JunitReporter("reports/junit.xml").OnRunFinishedAsync(run, CancellationToken.None);
        Assert.Equal("reports/junit.xml", Assert.Single(rows!).Text);
        AssertWellFormed(await File.ReadAllTextAsync(Path.Combine(project, "reports", "junit.xml")));

        Assert.Null(await new JunitReporter(reportPath).OnRunFinishedAsync(run, CancellationToken.None));
        Assert.Equal("{}", await File.ReadAllTextAsync(reportPath));
    }

    [Fact]
    public void Configure_reads_outputFile_and_rejects_anything_else()
    {
        var reporter = new JunitReporter();
        reporter.Configure(System.Text.Json.JsonDocument.Parse("{\"outputFile\":\"a.xml\"}").RootElement);
        foreach (var bad in new[] { "{\"other\":1}", "{\"outputFile\":1}", "{\"outputFile\":\" \"}", "[]" })
        {
            Assert.Throws<ArgumentException>(() =>
                reporter.Configure(System.Text.Json.JsonDocument.Parse(bad).RootElement)
            );
        }
    }

    /// <summary>A fresh directory removed after each test.</summary>
    private string MakeTmpDir()
    {
        var dir = Directory.CreateTempSubdirectory("junit-").FullName;
        _tmpDirs.Add(dir);
        return Path.GetFullPath(new DirectoryInfo(dir).ResolveLinkTarget(true)?.FullName ?? dir);
    }

    /// <summary>Runs the reporter on a failed run and returns the attachment line it wrote, if any.</summary>
    private async Task<string?> AttachmentFor(string projectRoot, string artifactsRoot)
    {
        Directory.CreateDirectory(artifactsRoot);
        var run = new FinishedRun(
            FailedWithShot(),
            ReportRunStatus.Failed,
            1,
            projectRoot,
            Path.Combine(artifactsRoot, "report.json"),
            artifactsRoot
        );
        var rows = await new JunitReporter().OnRunFinishedAsync(run, CancellationToken.None);
        var row = Assert.Single(rows!);
        Assert.Equal("JUnit", row.Label);
        var file = Path.Combine(artifactsRoot, "junit.xml");
        Assert.True(File.Exists(file));
        var xml = await File.ReadAllTextAsync(file);
        AssertWellFormed(xml);
        var match = Attachment().Match(xml);
        return match.Success ? match.Groups[1].Value : null;
    }
}
