// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.Tests.AgentTraceCache;

/// <summary>Upstream: packages/e2e/tests/integration/agent-trace-cache.test.ts, describe('trace cache: modes').</summary>
public sealed class TraceCacheModesTests
{
    [Fact]
    public async Task No_cache_overrides_the_config_nothing_is_recorded_and_an_entry_recorded_without_it_is_left_untouched()
    {
        var project = RunProject.CreateProject();
        var cacheDirectory = Path.Combine(project.Dir, ".e2e", "cache");
        RunProjectOptions Options(TwoTapCounter.ExecutorRecord record, bool noCache) => TwoTapCounter.RunOptions(record) with { NoCache = noCache };
        try
        {
            var uncached = new TwoTapCounter.ExecutorRecord();
            var first = await RunProject.RunExistingAsync(project, Options(uncached, true));
            Assert.Equal(0, first.ExitCode);
            Assert.Equal(1, uncached.Calls);
            Assert.False(Directory.Exists(cacheDirectory));
            Assert.Null(TwoTapCounter.ActStep(first).Cache);

            var second = await RunProject.RunExistingAsync(project, Options(new TwoTapCounter.ExecutorRecord(), false));
            Assert.Equal(0, second.ExitCode);
            var recorded = TwoTapCounter.FileState(cacheDirectory);

            // Neither replayed nor rewritten: the entry keeps its bytes and its mtime.
            var again = new TwoTapCounter.ExecutorRecord();
            var third = await RunProject.RunExistingAsync(project, Options(again, true));
            Assert.Equal(0, third.ExitCode);
            Assert.Equal(1, again.Calls);
            Assert.Null(TwoTapCounter.ActStep(third).Cache);
            Assert.Equal(recorded, TwoTapCounter.FileState(cacheDirectory));
        }
        finally
        {
            project.Cleanup();
        }
    }

    [Fact]
    public async Task Caches_by_default_and_CI_demotes_an_unset_mode_to_read_only_it_replays_the_entry_and_never_rewrites_it()
    {
        var project = RunProject.CreateProject();
        var cacheDirectory = Path.Combine(project.Dir, ".e2e", "cache");
        RunProjectOptions Options(TwoTapCounter.ExecutorRecord record, IReadOnlyDictionary<string, string?>? env = null) =>
            TwoTapCounter.RunOptions(record) with { Config = null, Env = env };
        try
        {
            var first = await RunProject.RunExistingAsync(project, Options(new TwoTapCounter.ExecutorRecord()));
            Assert.Equal(0, first.ExitCode);
            Assert.Single(Directory.GetFiles(cacheDirectory));

            // An entry from before the occurrence fields: a read-write replay would complete and rewrite it.
            var cache = new FileStepCache(cacheDirectory);
            var key = Path.GetFileNameWithoutExtension(Directory.GetFiles(cacheDirectory).Single());
            var entry = cache.Read(key).Entry!;
            entry.ParamsDigest = null;
            entry.CallIndex = null;
            entry.Agent = null;
            cache.Write(key, entry);
            var legacy = TwoTapCounter.FileState(cacheDirectory);
            var inCi = new TwoTapCounter.ExecutorRecord();
            var second = await RunProject.RunExistingAsync(project, Options(inCi, new Dictionary<string, string?> { ["CI"] = "1" }));
            Assert.Equal(0, second.ExitCode);
            Assert.Equal(0, inCi.Calls);
            var cached = TwoTapCounter.ActStep(second).Cache!;
            Assert.Equal((E2E.Reports.CacheMode.SelfFinalized, 2, 2), (cached.Mode, cached.ReplayedActions, cached.TotalActions));
            Assert.Matches(new Regex("^[0-9a-f]{64}$"), cached.Entry);
            Assert.Equal(legacy, TwoTapCounter.FileState(cacheDirectory));
        }
        finally
        {
            project.Cleanup();
        }
    }
}
