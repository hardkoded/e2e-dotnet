// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.CliCache;

/// <summary><c>describe('e2e cache stats')</c> of upstream <c>tests/unit/cli-cache.test.ts</c>.</summary>
public sealed class E2eCacheStatsTests : IDisposable
{
    private readonly CacheCli _cli = new();

    public void Dispose() => _cli.Dispose();

    [Fact]
    public void Prints_the_directory_the_entry_count_and_the_size()
    {
        _cli.Seed(
            (new string('a', 64), CacheCli.Trace(null, null, null, 1)),
            (new string('b', 64), CacheCli.Trace(null, null, null, 1)));
        _cli.Invoke("stats");
        Assert.Contains("directory  " + _cli.Store, _cli.Stdout, StringComparison.Ordinal);
        Assert.Contains("entries    2", _cli.Stdout, StringComparison.Ordinal);
        Assert.Matches(@"size {7}\d+(\.\d)? (B|KiB)", _cli.Stdout);
    }

    [Fact]
    public void Reads_the_directory_cache_dir_or_E2E_CACHE_DIR_names_over_cache_dir()
    {
        _cli.Invoke("stats", "--cache-dir", "from-flag");
        Assert.Contains("directory  " + Path.Combine(_cli.Root, "from-flag"), _cli.Stdout, StringComparison.Ordinal);
        _cli.Invoke(["stats"], new Dictionary<string, string?> { ["E2E_CACHE_DIR"] = "from-env" });
        Assert.Contains("directory  " + Path.Combine(_cli.Root, "from-env"), _cli.Stdout, StringComparison.Ordinal);
    }
}
