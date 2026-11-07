// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.CliCache;

/// <summary><c>describe('e2e cache clear')</c> of upstream <c>tests/unit/cli-cache.test.ts</c>.</summary>
public sealed class E2eCacheClearTests : IDisposable
{
    private readonly CacheCli _cli = new();

    public void Dispose() => _cli.Dispose();

    [Fact]
    public void Deletes_the_entries_and_the_directory()
    {
        _cli.Seed(
            (new string('a', 64), CacheCli.Trace(null, null, null, 1)),
            (new string('b', 64), CacheCli.Trace(null, null, null, 1)));
        _cli.Invoke("clear");
        Assert.Contains("cleared 2 file(s)", _cli.Stdout, StringComparison.Ordinal);
        Assert.Equal(["e2e.config.json"], Directory.GetFileSystemEntries(_cli.Root).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.Equal(0, _cli.ExitCode);
    }

    [Fact]
    public void Removes_an_unreadable_entry_and_a_crashed_writers_temporary_file()
    {
        Directory.CreateDirectory(_cli.Store);
        File.WriteAllText(Path.Combine(_cli.Store, new string('c', 64) + ".json"), "torn");
        File.WriteAllText(Path.Combine(_cli.Store, new string('d', 64) + ".json.0123456789abcdef.tmp"), "{}");
        _cli.Invoke("clear");
        Assert.Contains("cleared 2 file(s)", _cli.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Leaves_files_the_runner_never_wrote_and_says_so()
    {
        Directory.CreateDirectory(_cli.Store);
        File.WriteAllText(Path.Combine(_cli.Store, "notes.md"), "keep me");
        _cli.Invoke("clear");
        Assert.Contains("notes.md", _cli.Stderr, StringComparison.Ordinal);
        Assert.Equal(["notes.md"], Directory.GetFileSystemEntries(_cli.Store).Select(Path.GetFileName));
    }

    [Fact]
    public void Has_nothing_to_clear_when_the_store_was_never_written()
    {
        _cli.Invoke("clear");
        Assert.Contains("no replay cache entries to clear", _cli.Stdout, StringComparison.Ordinal);
        Assert.Equal(0, _cli.ExitCode);
    }
}
