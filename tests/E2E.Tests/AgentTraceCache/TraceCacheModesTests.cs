// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.Tests.AgentTraceCache;

/// <summary>
/// Upstream runs the suite through the runner with <c>runOptions: { noCache }</c> or <c>env: { CI: '1' }</c> and reads the test from
/// its report. The port runs the same step in sessions, with the mode the config resolves (<c>E2E_NO_CACHE</c> for <c>noCache</c>).
/// </summary>
public sealed class TraceCacheModesTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "e2e-trace-cache-modes"));

    // Partial (follow-up #1336): the run's mode is what a read-write config resolves to with E2E_NO_CACHE set (upstream's runOptions: { noCache }); the port has no runExisting, so there is no exit code and no report to read the cache field from (the step result's Cache is read), and the scripted model's call count is 3 where upstream's executor counts 1.
    [Fact]
    public async Task Partial_no_cache_overrides_the_config_nothing_is_recorded_and_an_entry_recorded_without_it_is_left_untouched()
    {
        var directory = TwoTapCounter.NewDirectory();
        var readWrite = """{ "cache": { "mode": "read-write" } }""";
        var noCache = RunMode(readWrite, noCache: true);
        Assert.Equal(CacheMode.Off, noCache);
        Assert.Equal(CacheMode.ReadWrite, RunMode(readWrite, noCache: false));

        var uncached = TwoTapCounter.Model();
        ActResult first;
        await using (var session = await TwoTapCounter.StartAsync(directory, uncached, mode: noCache))
        {
            first = await TwoTapCounter.RunAsync(session);
            session.Complete();
        }

        Assert.Equal(3, uncached.CallCount);
        Assert.False(Directory.Exists(directory));
        Assert.Null(first.Cache);

        await using (var session = await TwoTapCounter.StartAsync(directory, TwoTapCounter.Model()))
        {
            await TwoTapCounter.RunAsync(session);
            session.Complete();
        }

        var recorded = TwoTapCounter.FileState(directory);

        // Neither replayed nor rewritten: the entry keeps its bytes and its write time.
        var again = TwoTapCounter.Model();
        ActResult third;
        await using (var session = await TwoTapCounter.StartAsync(directory, again, mode: noCache))
        {
            third = await TwoTapCounter.RunAsync(session);
            session.Complete();
        }

        Assert.Equal(3, again.CallCount);
        Assert.Null(third.Cache);
        Assert.Equal(recorded, TwoTapCounter.FileState(directory));
    }

    // Partial (follow-up #1336): the port has no runExisting, so there is no exit code to assert and no report to read the cache field from (the step result's Cache is read). The legacy-entry setup and its unchanged file are ported (#1287).
    [Fact]
    public async Task Partial_caches_by_default_and_CI_demotes_an_unset_mode_to_read_only_it_replays_the_entry_and_never_rewrites_it()
    {
        var directory = TwoTapCounter.NewDirectory();

        await using (var session = await TwoTapCounter.StartAsync(directory, TwoTapCounter.Model(), mode: ResolvedMode(ci: false)))
        {
            await TwoTapCounter.RunAsync(session);
            session.Complete();
        }

        Assert.Single(Directory.GetFiles(directory));

        // An entry from before the occurrence fields: a read-write replay would complete and rewrite it.
        var cache = new FileStepCache(directory);
        var key = Path.GetFileNameWithoutExtension(Directory.GetFiles(directory).Single());
        var entry = cache.Read(key).Entry!;
        entry.ParamsDigest = null;
        entry.CallIndex = null;
        entry.Agent = null;
        cache.Write(key, entry);

        Assert.Equal(CacheMode.ReadOnly, ResolvedMode(ci: true));
        var legacy = TwoTapCounter.FileState(directory);
        var inCi = TwoTapCounter.Model();
        ActResult second;
        await using (var session = await TwoTapCounter.StartAsync(directory, inCi, mode: ResolvedMode(ci: true)))
        {
            second = await TwoTapCounter.RunAsync(session);
            session.Complete();
        }

        Assert.Equal(0, inCi.CallCount);
        Assert.Equal(("self-finalized", 2, 2), (second.Cache?.Mode, second.Cache?.ReplayedActions, second.Cache?.TotalActions));
        Assert.Matches(new Regex("^[0-9a-f]{64}$"), second.Cache?.Entry);
        Assert.Equal(legacy, TwoTapCounter.FileState(directory));
    }

    /// <summary>The mode a config resolves to for one run: <c>E2E_NO_CACHE</c> is the run option <c>noCache</c>.</summary>
    private static CacheMode RunMode(string json, bool noCache)
    {
        return E2EConfig.Parse(json, ProjectRoot, name => name == "E2E_NO_CACHE" && noCache ? "1" : null).Cache.Mode;
    }

    /// <summary>The mode a config with no cache setting resolves to, with or without <c>CI</c> in the environment.</summary>
    private static CacheMode ResolvedMode(bool ci)
    {
        return E2EConfig.Parse("{}", ProjectRoot, name => name == "CI" && ci ? "1" : null).Cache.Mode;
    }
}
