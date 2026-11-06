// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.TraceCacheConfig;

/// <summary>
/// <c>REPLAY_STALE advice</c> (<c>trace-cache-config.test.ts</c>). Each <c>it</c> is partial: upstream reads
/// <c>createAgentCacheContext(...).strict.advice</c>, which the port does not have, so these call <c>AttemptScope.StaleAdvice</c> on the store
/// <c>cache.dir</c> and <c>E2E_CACHE_DIR</c> resolve to; the port has no <c>--strict-cache</c> or <c>--cache-dir</c> flag.
/// </summary>
public sealed class ReplayStaleAdviceTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "e2e-cache-config-tests"));

    [Fact]
    public void Partial_Names_the_knob_that_turned_strict_on_and_the_cache_directory_relative_to_the_project()
    {
        Assert.Equal(
            "re-record it with a read-write run with cache.strict set to false and commit the changed entry under .e2e/cache",
            AdviceFor("""{ "cache": { "strict": true } }"""));
        Assert.Equal(
            "re-record it with a read-write run with cache.strict set to false and commit the changed entry under recordings/replays",
            AdviceFor("""{ "cache": { "strict": true, "dir": "recordings/replays" } }"""));
        Assert.Equal(
            "re-record it with a read-write run with cache.strict set to false and commit the changed entry under shared/e2e",
            AdviceFor("""{ "cache": { "strict": true } }""", Env(("E2E_CACHE_DIR", "shared/e2e"))));
    }

    [Fact]
    public void Partial_Names_a_cache_directory_outside_the_project_without_asking_for_a_commit()
    {
        var outside = Path.GetFullPath("/shared/e2e");
        Assert.Equal(
            "re-record it with a read-write run with cache.strict set to false; the run writes the new entry under " + outside.Replace(Path.DirectorySeparatorChar, '/'),
            AdviceFor("""{ "cache": { "strict": true } }""", Env(("E2E_CACHE_DIR", outside))));
    }

    [Fact]
    public void Partial_Points_at_the_configured_store_rather_than_a_directory_when_a_custom_store_holds_the_entries()
    {
        var config = E2EConfig.Parse("""{ "cache": { "strict": true } }""", Root, Env()).WithCacheStore(new EmptyStore());
        Assert.Equal(
            "re-record it with a read-write run with cache.strict set to false; the run writes the new entry to the configured cache.store",
            AttemptScope.StaleAdvice(config.Cache.Store!, config.ProjectRoot));
    }

    private static string AdviceFor(string json, Func<string, string?>? environment = null)
    {
        var config = E2EConfig.Parse(json, Root, environment ?? Env());
        Assert.True(config.Cache.Strict);
        return AttemptScope.StaleAdvice(config.Cache.Store ?? new FileStepCache(config.Cache.Directory), config.ProjectRoot);
    }

    private static Func<string, string?> Env(params (string Name, string Value)[] values)
    {
        var map = values.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal);
        return name => map.GetValueOrDefault(name);
    }

    /// <summary>A store that holds nothing: a stand-in for a remote cache.</summary>
    private sealed class EmptyStore : IStepCache
    {
        public CacheLookup Read(string key) => new() { Reason = "no-entry" };

        public void Write(string key, CacheEntry entry)
        {
        }

        public void Delete(string key)
        {
        }
    }
}
