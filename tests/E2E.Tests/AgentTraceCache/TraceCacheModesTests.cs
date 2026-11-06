// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.Tests.AgentTraceCache;

/// <summary>
/// Upstream runs the suite through the runner with <c>runOptions: { noCache }</c> or <c>env: { CI: '1' }</c> and reads the test from
/// its report. The port runs the same step in sessions, with the mode the config resolves.
/// </summary>
public sealed class TraceCacheModesTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "e2e-trace-cache-modes"));

    // Partial (follow-up #1286): the port has no --no-cache flag, so the run's mode is CacheMode.Off on the session rather than a run option over a read-write config; no report to read the cache field from, so the step result's Cache is read.
    [Fact]
    public async Task Partial_no_cache_overrides_the_config_nothing_is_recorded_and_an_entry_recorded_without_it_is_left_untouched()
    {
        var directory = TwoTapCounter.NewDirectory();

        var uncached = TwoTapCounter.Model();
        ActResult first;
        await using (var session = await TwoTapCounter.StartAsync(directory, uncached, mode: CacheMode.Off))
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
        await using (var session = await TwoTapCounter.StartAsync(directory, again, mode: CacheMode.Off))
        {
            third = await TwoTapCounter.RunAsync(session);
            session.Complete();
        }

        Assert.Equal(3, again.CallCount);
        Assert.Null(third.Cache);
        Assert.Equal(recorded, TwoTapCounter.FileState(directory));
    }

    // Partial (follow-up #1287): the legacy-entry setup is not ported (the port's entry has no recordedFor occurrence fields, and a read-write replay keeps an entry whole), so the unchanged file proves a replay under the demoted mode, not that a read-write replay would have rewritten it.
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

        Assert.Equal(CacheMode.ReadOnly, ResolvedMode(ci: true));
        var recorded = TwoTapCounter.FileState(directory);
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
        Assert.Equal(recorded, TwoTapCounter.FileState(directory));
    }

    /// <summary>The mode a config with no cache setting resolves to, with or without <c>CI</c> in the environment.</summary>
    private static CacheMode ResolvedMode(bool ci)
    {
        return E2EConfig.Parse("{}", ProjectRoot, name => name == "CI" && ci ? "1" : null).Cache.Mode;
    }
}
