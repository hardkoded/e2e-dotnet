// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.TraceCacheConfig;

/// <summary>
/// .NET-only (no upstream <c>it</c>): <c>E2E_CACHE_DIR_OVERRIDE</c> is the port's <c>--cache-dir</c> for the test host, which has no command
/// line. It takes the place of the flag in <c>resolveCacheDir</c>, so the ported <c>it</c> of <c>CacheConfigResolutionTests</c> cover its rules.
/// </summary>
public sealed class CacheDirOverrideVariableTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "e2e-cache-override-tests"));

    [Fact]
    public void Wins_over_E2E_CACHE_DIR_and_cache_dir_and_names_the_flag_as_the_source()
    {
        var config = Parse("""{ "cache": { "dir": "configured" } }""", ("E2E_CACHE_DIR", "variable"), ("E2E_CACHE_DIR_OVERRIDE", "flag"));
        Assert.Equal(Path.Combine(Root, "flag"), config.Cache.Directory);
        Assert.Equal("--cache-dir", config.Cache.DirectorySource);
    }

    [Fact]
    public void Is_ignored_when_empty_and_refused_when_blank_or_beside_a_cache_store()
    {
        Assert.Equal(Path.Combine(Root, "variable"), Parse("{}", ("E2E_CACHE_DIR", "variable"), ("E2E_CACHE_DIR_OVERRIDE", "")).Cache.Directory);
        Assert.Contains("--cache-dir must be a non-empty path", Assert.Throws<ConfigurationException>(() => Parse("{}", ("E2E_CACHE_DIR_OVERRIDE", " "))).Message, StringComparison.Ordinal);
        Assert.Contains(
            "--cache-dir names a file store directory",
            Assert.Throws<ConfigurationException>(() => Parse("{}", ("E2E_CACHE_DIR_OVERRIDE", "flag")).WithCacheStore(new EmptyStore())).Message,
            StringComparison.Ordinal);
    }

    private static E2EConfig Parse(string json, params (string Name, string Value)[] values)
    {
        var map = values.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal);
        return E2EConfig.Parse(json, Root, name => map.GetValueOrDefault(name));
    }

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
