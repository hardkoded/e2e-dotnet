// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.TraceCacheConfig;

/// <summary>
/// <c>REPLAY_STALE advice</c> (<c>trace-cache-config.test.ts</c>): the advice <c>CacheContexts.CreateAgentCacheContext</c> puts on <c>Strict</c>.
/// </summary>
public sealed class ReplayStaleAdviceTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "e2e-cache-config-tests"));

    [Fact]
    public void Names_the_knob_that_turned_strict_on_and_the_cache_directory_relative_to_the_project()
    {
        Assert.Null(AdviceFor("{}"));
        Assert.Equal(
            "re-record it with a read-write run without --strict-cache and commit the changed entry under .e2e/cache",
            AdviceFor("{}", new CliOverrides { CacheStrict = true }));
        Assert.Equal(
            "re-record it with a read-write run with cache.strict set to false and commit the changed entry under recordings/replays",
            AdviceFor("""{ "cache": { "strict": true, "dir": "recordings/replays" } }"""));
        Assert.Contains(
            "without --strict-cache and with cache.strict set to false",
            AdviceFor("""{ "cache": { "strict": true } }""", new CliOverrides { CacheStrict = true }));
    }

    [Fact]
    public void Names_a_cache_directory_outside_the_project_without_asking_for_a_commit()
    {
        var outside = Path.GetFullPath("/shared/e2e");
        Assert.Equal(
            "re-record it with a read-write run without --strict-cache; the run writes the new entry under " + outside.Replace(Path.DirectorySeparatorChar, '/'),
            AdviceFor("{}", new CliOverrides { CacheStrict = true, CacheDir = outside }));
    }

    [Fact]
    public void Points_at_the_configured_store_rather_than_a_directory_when_a_custom_store_holds_the_entries()
    {
        Assert.Equal(
            "re-record it with a read-write run with cache.strict set to false; the run writes the new entry to the configured cache.store",
            AdviceFor("""{ "cache": { "strict": true } }""", store: new EmptyStore()));
    }

    private static string? AdviceFor(string json, CliOverrides? cli = null, Func<string, string?>? environment = null, IStepCache? store = null)
    {
        var config = E2EConfig.Parse(json, Root, environment ?? Env(), cli ?? new CliOverrides());
        if (store is not null)
        {
            config = config.WithCacheStore(store);
        }

        return CacheContexts.CreateAgentCacheContext(config.Cache, config.ProjectRoot, 0)?.Strict?.Advice;
    }

    private static Func<string, string?> Env(params (string Name, string Value)[] values)
    {
        var map = values.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal);
        return name => map.GetValueOrDefault(name);
    }

    /// <summary>A store that holds nothing: a stand-in for a remote cache.</summary>
    private sealed class EmptyStore : IStepCache
    {
        public bool Writable => true;

        public CacheLookup Read(string key) => new() { Reason = "no-entry" };

        public int? Write(string key, CacheEntry entry) => 0;

        public void Delete(string key)
        {
        }
    }
}
