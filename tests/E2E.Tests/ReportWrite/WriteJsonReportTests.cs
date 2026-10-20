// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Tests.ReportWrite;

/// <summary>Upstream's <c>describe('writeJsonReport')</c> (<c>tests/unit/report-write.test.ts</c>).</summary>
public sealed class WriteJsonReportTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("e2e-write-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task Creates_missing_parent_directories()
    {
        var file = Path.Combine(_dir, "deep", "nested", "report.json");

        await WriteReport.WriteJsonReportAsync(file, Array.Empty<int>());

        Assert.True(JsonNode.DeepEquals(new JsonArray(), JsonNode.Parse(await File.ReadAllTextAsync(file))));
    }

    [Fact]
    public async Task Atomically_replaces_an_existing_report()
    {
        var file = Path.Combine(_dir, "report.json");

        await WriteReport.WriteJsonReportAsync(file, new { version = 1 });
        await WriteReport.WriteJsonReportAsync(file, new { version = 2 });

        Assert.Equal(2, (int)JsonNode.Parse(await File.ReadAllTextAsync(file))!["version"]!);
        Assert.Equal(["report.json"], Directory.GetFileSystemEntries(_dir).Select(Path.GetFileName));
    }

    // .NET-only: the document's shape on disk, which upstream's JSON.stringify(document, null, 2) fixes.
    [Fact]
    public async Task Writes_two_space_indent_and_a_trailing_newline()
    {
        var file = Path.Combine(_dir, "report.json");

        await WriteReport.WriteJsonReportAsync(file, new { version = 1 });

        Assert.Equal("{\n  \"version\": 1\n}\n", await File.ReadAllTextAsync(file));
    }

    // .NET-only: upstream's JSON.stringify leaves text as it is, and the port's serializer escapes non-ASCII and HTML characters by default.
    [Fact]
    public async Task Writes_text_as_it_is_without_unicode_escapes()
    {
        var file = Path.Combine(_dir, "report.json");

        await WriteReport.WriteJsonReportAsync(file, new { label = "tap <Sign in> \u00e9" });

        Assert.Contains("tap <Sign in> \u00e9", await File.ReadAllTextAsync(file), StringComparison.Ordinal);
    }

    // .NET-only: upstream's writeFileAtomic promises that a failed write leaves no temporary file behind.
    [Fact]
    public async Task Leaves_no_temporary_file_behind_when_the_write_fails()
    {
        var target = Path.Combine(_dir, "report.json");
        Directory.CreateDirectory(target);

        await Assert.ThrowsAnyAsync<Exception>(() => WriteReport.WriteJsonReportAsync(target, new { version = 1 }));

        Assert.Equal(["report.json"], Directory.GetFileSystemEntries(_dir).Select(Path.GetFileName));
    }

    // .NET-only: upstream's toPosixPath, which its report tests reach only through the artifact paths.
    [Fact]
    public void Keeps_POSIX_separators_in_a_report_path()
    {
        var relative = string.Join(Path.DirectorySeparatorChar, "results", "attempt-1", "video.webm");

        Assert.Equal("results/attempt-1/video.webm", WriteReport.ToPosixPath(relative));
    }
}
