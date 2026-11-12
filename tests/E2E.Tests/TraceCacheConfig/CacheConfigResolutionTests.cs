// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.TraceCacheConfig;

/// <summary><c>cache config resolution</c>: how <c>cache</c>, <c>--cache-dir</c>, and <c>E2E_CACHE_DIR</c> become the cache mode and directory.</summary>
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

    [Fact]
    public void Lets_the_no_cache_override_win_over_the_config_even_in_CI()
    {
        Assert.Equal(CacheMode.Off, Parse("""{ "cache": { "mode": "read-write" } }""", Env(("E2E_NO_CACHE", "1"))).Cache.Mode);
        Assert.Equal(CacheMode.Off, Parse("{}", Env(("CI", "1"), ("E2E_NO_CACHE", "true"))).Cache.Mode);
        Assert.Equal(CacheMode.ReadWrite, Parse("""{ "cache": { "mode": "read-write" } }""", Env(("E2E_NO_CACHE", "0"))).Cache.Mode);
        Assert.Equal(CacheMode.Off, Parse("""{ "cache": { "mode": "read-write" } }""", Env(("E2E_NO_CACHE", "1"))).WithCacheStore(new EmptyStore()).Cache.Mode);
    }

    [Fact]
    public void Takes_the_directory_from_cache_dir_then_E2E_CACHE_DIR_then_cache_dir_from_the_project_root()
    {
        const string configured = """{ "cache": { "mode": "read-only", "dir": "configured" } }""";
        var env = Env(("E2E_CACHE_DIR", "../shared/e2e"));
        Assert.Equal(Path.GetFullPath("/abs/flag"), Parse(configured, env, new CliOverrides { CacheDir = "/abs/flag" }).Cache.Directory);
        Assert.Equal(Path.GetFullPath(Path.Combine(Root, "../shared/e2e")), Parse(configured, env).Cache.Directory);
        Assert.Equal(Path.Combine(Root, "configured"), Parse(configured, Env(("E2E_CACHE_DIR", " "))).Cache.Directory);
        Assert.Equal(Path.GetFullPath(Path.Combine(Root, "../shared/e2e")), Parse("{}", env).Cache.Directory);
    }

    // The port's --no-cache is E2E_NO_CACHE (upstream's `cli.cache = 'off'`).
    [Fact]
    public void Leaves_the_mode_to_the_config_and_no_cache_when_the_directory_is_overridden()
    {
        var env = Env(("E2E_CACHE_DIR", "/abs/shared"));
        var readOnly = Parse("""{ "cache": { "mode": "read-only" } }""", env).Cache;
        Assert.Equal((CacheMode.ReadOnly, Path.GetFullPath("/abs/shared")), (readOnly.Mode, readOnly.Directory));
        Assert.Equal(
            (CacheMode.Off, Path.Combine(Root, "flag")),
            CacheOf(Parse("{}", Env(("E2E_CACHE_DIR", "/abs/shared"), ("E2E_NO_CACHE", "1")), new CliOverrides { CacheDir = "flag" })));
        Assert.Equal(CacheMode.ReadOnly, Parse("{}", Env(("E2E_CACHE_DIR", "/abs/shared"), ("CI", "1"))).Cache.Mode);
    }

    [Fact]
    public void Refuses_cache_dir_beside_a_cache_store_and_ignores_E2E_CACHE_DIR_there_even_one_the_output_would_overlap()
    {
        var store = new EmptyStore();
        var refusal = Assert.Throws<ConfigurationException>(() => Parse("{}", Env(), new CliOverrides { CacheDir = "shared" }).WithCacheStore(store));
        Assert.Equal("INVALID_CONFIG", refusal.Code);
        Assert.Contains("--cache-dir names a file store directory, but cache.store replaces the file store; drop one", refusal.Message, StringComparison.Ordinal);
        var resolved = Parse("{}", Env(("E2E_CACHE_DIR", ".e2e"))).WithCacheStore(store);
        Assert.Same(store, resolved.Cache.Store);
        Assert.Equal(Path.Combine(Root, ".e2e", "cache"), resolved.Cache.Directory);
        Assert.Equal(Path.Combine(Root, ".e2e"), resolved.Output);
        Assert.Equal(Path.Combine(Root, "configured"), Parse("""{ "cache": { "dir": "configured" } }""", Env(("E2E_CACHE_DIR", ".e2e"))).WithCacheStore(store).Cache.Directory);
    }

    [Fact]
    public void Reads_a_leading_tilde_in_cache_dir_and_E2E_CACHE_DIR_as_the_home_directory()
    {
        Assert.Equal(Path.Combine(Home, "x"), Parse("{}", Env(("E2E_CACHE_DIR", "~/x"))).Cache.Directory);
        Assert.Equal(Home, Parse("{}", Env(), new CliOverrides { CacheDir = "~" }).Cache.Directory);
        Assert.Equal(Path.Combine(Root, "~x"), Parse("{}", Env(), new CliOverrides { CacheDir = "~x" }).Cache.Directory);
    }

    [Fact]
    public void Rejects_a_blank_cache_dir_and_an_output_the_overridden_cache_directory_overlaps()
    {
        Assert.Contains("--cache-dir must be a non-empty path", Assert.Throws<ConfigurationException>(() => Parse("{}", Env(), new CliOverrides { CacheDir = " " })).Message, StringComparison.Ordinal);
        Assert.Contains(
            "output \".e2e\" (the default) is the cache directory .e2e or inside it",
            Assert.Throws<ConfigurationException>(() => Parse("{}", Env(("E2E_CACHE_DIR", ".e2e"))).Output).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "output \".e2e\" (the default) would hold --cache-dir .e2e/results/cache under results/, which the run owns; move --cache-dir or the output",
            Assert.Throws<ConfigurationException>(() => Parse("{}", Env(), new CliOverrides { CacheDir = ".e2e/results/cache" }).Output).Message.Replace('\\', '/'),
            StringComparison.Ordinal);
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
        public bool Writable => true;

        public CacheLookup Read(string key) => new() { Reason = "no-entry" };

        public int? Write(string key, CacheEntry entry) => 0;

        public void Delete(string key)
        {
        }
    }

    private static (CacheMode Mode, string Directory) CacheOf(E2EConfig config) => (config.Cache.Mode, config.Cache.Directory);

    private static E2EConfig Parse(string json, Func<string, string?>? environment = null, CliOverrides? cli = null) => E2EConfig.Parse(json, Root, environment ?? Env(), cli ?? new CliOverrides());

    private static Func<string, string?> Env(params (string Name, string Value)[] values)
    {
        var map = values.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal);
        return name => map.GetValueOrDefault(name);
    }
}
