// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Formats.Tar;

namespace E2E.Cli.Tests.InitExamples;

/// <summary>Upstream's <c>describe('extractFolder')</c> of <c>tests/unit/init-examples.test.ts</c>.</summary>
public sealed class ExtractFolderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("e2e-init-example-").FullName;

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Writes_the_folder_without_its_prefix_keeps_executables_executable_and_stops_past_the_folder()
    {
        var longName = "e2e-main/samples/X/" + string.Concat(Enumerable.Repeat("deep/", 30)) + "file.txt";
        var archive = ExampleArchives.Tar(
        [
            new ArchiveEntry("e2e-main/samples/X/gradlew", "#!/bin/sh\n", (UnixFileMode)0b111_101_101),
            new ArchiveEntry(longName, "from pax"),
            new ArchiveEntry("e2e-main/samples/X/" + new string('g', 120) + ".txt", "from gnu", Gnu: true),
            new ArchiveEntry("e2e-main/samples/X/link", Type: TarEntryType.SymbolicLink),
            new ArchiveEntry("e2e-main/samples/Y/after.txt", "never read"),
        ]);
        var top = new List<string>();
        using var source = new ExampleArchives.ChunkedStream(archive);
        Assert.Equal(3, await Examples.ExtractFolderAsync(source, "samples/X/", _dir, top.Add));
        Assert.Equal("#!/bin/sh\n", await File.ReadAllTextAsync(Path.Combine(_dir, "gradlew")));
        if (!OperatingSystem.IsWindows())
        {
            Assert.NotEqual(UnixFileMode.None, File.GetUnixFileMode(Path.Combine(_dir, "gradlew")) & UnixFileMode.UserExecute);
        }

        Assert.Equal("from pax", await File.ReadAllTextAsync(Path.Combine(_dir, Path.Combine(Enumerable.Repeat("deep", 30).ToArray()), "file.txt")));
        Assert.Equal("from gnu", await File.ReadAllTextAsync(Path.Combine(_dir, new string('g', 120) + ".txt")));
        Assert.False(File.Exists(Path.Combine(_dir, "link")));
        Assert.False(File.Exists(Path.Combine(_dir, "after.txt")));
        Assert.Equal(new HashSet<string>(["gradlew", "deep", new string('g', 120) + ".txt", "link"]), new HashSet<string>(top));
    }

    [Fact]
    public async Task Refuses_a_path_that_leaves_the_destination()
    {
        var archive = ExampleArchives.Tar([new ArchiveEntry("e2e-main/samples/X/../../escape.txt", "no")]);
        using var source = new ExampleArchives.ChunkedStream(archive);
        var failure = await Assert.ThrowsAsync<ExampleDownloadError>(() => Examples.ExtractFolderAsync(source, "samples/X/", _dir, _ => { }));
        Assert.Contains("unsafe path", failure.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_dir, "..", "escape.txt")));
    }
}
