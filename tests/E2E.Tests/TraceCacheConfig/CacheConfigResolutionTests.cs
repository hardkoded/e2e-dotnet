// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.TraceCacheConfig;

/// <summary><c>cache config resolution</c>: how <c>cache</c> and <c>E2E_CACHE_DIR</c> become the cache mode and directory.</summary>
public sealed class CacheConfigResolutionTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "e2e-cache-config-tests"));
    private static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // Partial: the port's cache is an object, never a mode string.
    [Fact]
    public void Is_opt_out_unset_defaults_to_read_write_under_dot_e2e_cache_or_the_configured_dir()
    {
        var resolved = Parse("{}").Cache;
        Assert.Equal(CacheMode.ReadWrite, resolved.Mode);
        Assert.Null(resolved.Store);
        Assert.Equal(Path.Combine(Root, ".e2e", "cache"), resolved.Directory);
        Assert.Equal(Path.Combine(Root, "shared"), Parse("""{ "cache": { "dir": "shared" } }""").Cache.Directory);
        Assert.Equal(CacheMode.ReadOnly, Parse("""{ "cache": { "mode": "read-only" } }""").Cache.Mode);
        Assert.Equal(CacheMode.Off, Parse("""{ "cache": { "mode": "off" } }""").Cache.Mode);
    }

    // Partial: without --cache-dir, which the port has no command line for.
    [Fact]
    public void Takes_the_directory_from_E2E_CACHE_DIR_then_cache_dir_from_the_project_root()
    {
        const string configured = """{ "cache": { "mode": "read-only", "dir": "configured" } }""";
        var env = Env(("E2E_CACHE_DIR", "../shared/e2e"));
        Assert.Equal(Path.GetFullPath(Path.Combine(Root, "../shared/e2e")), Parse(configured, env).Cache.Directory);
        Assert.Equal(Path.Combine(Root, "configured"), Parse(configured, Env(("E2E_CACHE_DIR", " "))).Cache.Directory);
        Assert.Equal(Path.GetFullPath(Path.Combine(Root, "../shared/e2e")), Parse("{}", env).Cache.Directory);
    }

    // Partial: without --no-cache, which the port has no command line for.
    [Fact]
    public void Leaves_the_mode_to_the_config_when_the_directory_is_overridden()
    {
        var env = Env(("E2E_CACHE_DIR", "/abs/shared"));
        var resolved = Parse("""{ "cache": { "mode": "read-only" } }""", env).Cache;
        Assert.Equal(CacheMode.ReadOnly, resolved.Mode);
        Assert.Equal(Path.GetFullPath("/abs/shared"), resolved.Directory);
        Assert.Equal(CacheMode.ReadOnly, Parse("{}", Env(("E2E_CACHE_DIR", "/abs/shared"), ("CI", "1"))).Cache.Mode);
    }

    // Partial: without --cache-dir, which the port has no command line for, and without the output the variable's directory would overlap.
    [Fact]
    public void Partial_Refuses_cache_dir_beside_a_cache_store_and_ignores_E2E_CACHE_DIR_there_even_one_the_output_would_overlap()
    {
        var store = new EmptyStore();
        var resolved = Parse("{}", Env(("E2E_CACHE_DIR", ".e2e"))).WithCacheStore(store).Cache;
        Assert.Same(store, resolved.Store);
        Assert.Equal(Path.Combine(Root, ".e2e", "cache"), resolved.Directory);
        Assert.Equal(Path.Combine(Root, "configured"), Parse("""{ "cache": { "dir": "configured" } }""", Env(("E2E_CACHE_DIR", ".e2e"))).WithCacheStore(store).Cache.Directory);
    }

    // Partial: without --cache-dir, which the port has no command line for.
    [Fact]
    public void Reads_a_leading_tilde_in_E2E_CACHE_DIR_as_the_home_directory()
    {
        Assert.Equal(Path.Combine(Home, "x"), Parse("{}", Env(("E2E_CACHE_DIR", "~/x"))).Cache.Directory);
        Assert.Equal(Home, Parse("{}", Env(("E2E_CACHE_DIR", "~"))).Cache.Directory);
        Assert.Equal(Path.Combine(Root, "~x"), Parse("{}", Env(("E2E_CACHE_DIR", "~x"))).Cache.Directory);
    }

    // Partial: the port has no string cache mode.
    [Fact]
    public void Demotes_only_an_unset_mode_in_CI_an_explicit_read_write_or_off_is_honored()
    {
        var ci = Env(("CI", "1"));
        Assert.Equal(CacheMode.ReadOnly, Parse("{}", ci).Cache.Mode);
        Assert.Equal(CacheMode.Off, Parse("""{ "cache": { "mode": "off" } }""", ci).Cache.Mode);
        Assert.Equal(CacheMode.ReadOnly, Parse("""{ "cache": { "dir": "shared" } }""", ci).Cache.Mode);
        Assert.Equal(CacheMode.ReadWrite, Parse("""{ "cache": { "mode": "read-write" } }""", ci).Cache.Mode);
        Assert.Equal(CacheMode.ReadOnly, Parse("""{ "cache": { "mode": "read-only" } }""", ci).Cache.Mode);
    }

    [Fact]
    public void Exempts_a_host_supplied_store_from_the_CI_clamp()
    {
        var store = new EmptyStore();
        var resolved = Parse("""{ "cache": { "mode": "read-write" } }""", Env(("CI", "1"))).WithCacheStore(store).Cache;
        Assert.Equal(CacheMode.ReadWrite, resolved.Mode);
        Assert.Same(store, resolved.Store);

        // The store states its own trust, so an unset mode is not demoted either.
        Assert.Equal(CacheMode.ReadWrite, Parse("{}", Env(("CI", "1"))).WithCacheStore(store).Cache.Mode);
        Assert.Equal(CacheMode.ReadWrite, Parse("""{ "cache": { "dir": "shared" } }""", Env(("CI", "1"))).WithCacheStore(store).Cache.Mode);
        Assert.Equal(CacheMode.ReadOnly, Parse("""{ "cache": { "mode": "read-only" } }""", Env(("CI", "1"))).WithCacheStore(store).Cache.Mode);
    }

    // Partial: without --strict-cache, which the port has no command line for.
    [Fact]
    public void Is_lenient_unless_the_config_asks_and_rejects_a_strict_that_is_not_a_boolean()
    {
        Assert.False(Parse("{}").Cache.Strict);
        var strict = Parse("""{ "cache": { "strict": true } }""").Cache;
        Assert.Equal(CacheMode.ReadWrite, strict.Mode);
        Assert.True(strict.Strict);
        Assert.Contains("cache.strict must be a boolean", Assert.Throws<ConfigurationException>(() => Parse("""{ "cache": { "strict": "yes" } }""")).Message, StringComparison.Ordinal);
    }

    // Partial: a store is code, so a JSON file cannot hold a non-store value; the port refuses the key itself, and a store of another type does not compile.
    [Fact]
    public void Partial_Rejects_unknown_modes_unknown_keys_and_non_store_store_values()
    {
        Assert.Contains(
            "cache config key \"store\" is not supported by the .NET port: a store is code, so call E2EConfig.WithCacheStore",
            Assert.Throws<ConfigurationException>(() => Parse("""{ "cache": { "store": { "read": true } } }""")).Message,
            StringComparison.Ordinal);
        Assert.Contains("cache.mode", Assert.Throws<ConfigurationException>(() => Parse("""{ "cache": { "mode": "aggressive" } }""")).Message, StringComparison.Ordinal);
        Assert.Contains("ttl", Assert.Throws<ConfigurationException>(() => Parse("""{ "cache": { "mode": "off", "ttl": 5 } }""")).Message, StringComparison.Ordinal);
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

    private static E2EConfig Parse(string json, Func<string, string?>? environment = null) => E2EConfig.Parse(json, Root, environment ?? Env());

    private static Func<string, string?> Env(params (string Name, string Value)[] values)
    {
        var map = values.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal);
        return name => map.GetValueOrDefault(name);
    }
}
