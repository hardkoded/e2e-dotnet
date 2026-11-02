// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Config;

/// <summary>
/// Upstream <c>describe('artifacts config')</c> of config.test.ts. Not ported: <c>defaults, and a target inherits the config mode as a
/// run-wide one</c>, <c>lets a target override the config, and the flag override both</c>, <c>keeps the mode, at the top and on a target,
/// out of the digest</c>, and <c>refuses a mode it does not know, wherever it is set</c>, which <c>TraceTests</c> and <c>VideoTests</c> hold.
/// </summary>
public sealed class ArtifactsConfigTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "e2e-config-root");

    /// <summary>Partial: the port has no digest, and a store is set with <see cref="E2EConfig.WithArtifactStore"/>, not by the config file.</summary>
    [Fact]
    public void Partial_Holds_only_the_store_and_keeps_it_out_of_the_digest()
    {
        Assert.Null(E2EConfig.Parse("{}", Root).ArtifactStore);
        Assert.Null(E2EConfig.Parse("{ \"artifacts\": {} }", Root).ArtifactStore);
        var store = new Store();
        var config = E2EConfig.Parse("{}", Root);
        var withStore = config.WithArtifactStore(store);
        Assert.Same(store, withStore.ArtifactStore);
        Assert.Null(config.ArtifactStore);
    }

    /// <summary>Partial: a store is code, so the key itself is refused; a store of another type does not compile, and the message for a non-object names an object.</summary>
    [Fact]
    public void Partial_Rejects_unknown_keys_and_a_non_store_store()
    {
        Assert.Contains("unknown artifacts config key \"ttl\"", Failure("""{ "artifacts": { "ttl": 1 } }""").Message, StringComparison.Ordinal);
        Assert.Contains(
            "artifacts config key \"store\" is not supported by the .NET port: a store is code, so call E2EConfig.WithArtifactStore",
            Failure("""{ "artifacts": { "store": { "upload": true } } }""").Message,
            StringComparison.Ordinal);
        Assert.Contains("artifacts must be an object", Failure("""{ "artifacts": "on" }""").Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_the_removed_kinds_list_in_either_form_and_the_removed_trace_block_naming_the_trace_or_video_mode_it_meant()
    {
        var both = Failure("""{ "artifacts": ["screenshot", "trace"] }""");
        Assert.Equal("INVALID_CONFIG", both.Code);
        Assert.Contains("artifacts no longer lists kinds: write trace: 'on' at the config root instead", both.Message, StringComparison.Ordinal);
        var screenshotOnly = Failure("""{ "artifacts": { "kinds": ["screenshot"] } }""").Message;
        Assert.StartsWith("artifacts.kinds was removed: write trace: 'off'", screenshotOnly, StringComparison.Ordinal);
        Assert.DoesNotContain("failure screenshots", screenshotOnly, StringComparison.Ordinal);

        // A list without screenshot used to turn the failure screenshot off, which is no longer possible.
        Assert.Contains("failure screenshots are always captured now", Failure("""{ "artifacts": [] }""").Message, StringComparison.Ordinal);
        Assert.Contains("failure screenshots are always captured now", Failure("""{ "artifacts": { "kinds": ["trace"] } }""").Message, StringComparison.Ordinal);
        var trace = Failure("""{ "artifacts": { "trace": { "record": "retries" } } }""");
        Assert.Equal("INVALID_CONFIG", trace.Code);
        Assert.Contains("artifacts.trace was removed: write trace: 'on-all-retries' at the config root", trace.Message, StringComparison.Ordinal);
        Assert.Contains("video is its own option: video: 'on'", Failure("""{ "artifacts": { "kinds": ["video"] } }""").Message, StringComparison.Ordinal);
        Assert.Contains(
            "artifacts.kinds and artifacts.trace were removed: write trace: 'on-all-retries'",
            Failure("""{ "artifacts": { "kinds": ["screenshot", "trace"], "trace": { "record": "retries" } } }""").Message,
            StringComparison.Ordinal);
        Assert.Contains("write trace: 'off'", Failure("""{ "artifacts": { "kinds": ["screenshot"], "trace": { "record": "retries" } } }""").Message, StringComparison.Ordinal);
        Assert.Contains("artifacts.video was removed: video is its own option", Failure("""{ "artifacts": { "video": { "retain": "on-failure" } } }""").Message, StringComparison.Ordinal);
    }

    /// <summary>Partial: upstream refuses <c>--trace all</c>. The port has no flag, so the same check runs on the E2E_TRACE variable and its message.</summary>
    [Fact]
    public void Maps_the_old_trace_spellings_lifted_to_where_a_mode_goes_to_the_mode_they_meant()
    {
        (string Json, string Message)[] cases =
        [
            ("""{ "trace": { "record": "retries" } }""", "trace { record: 'retries' } is the old spelling of trace: 'on-all-retries'"),
            ("""{ "trace": { "record": "all" } }""", "trace { record: 'all' } is the old spelling of trace: 'on'"),
            ("""{ "trace": "retries" }""", "trace 'retries' is the old spelling of trace: 'on-all-retries'"),
            ("""{ "trace": "all" }""", "trace 'all' is the old spelling of trace: 'on'"),
        ];
        foreach (var (json, message) in cases)
        {
            var error = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(json, Root));
            Assert.Equal("INVALID_CONFIG", error.Code);
            Assert.Contains(message, error.Message, StringComparison.Ordinal);
        }

        // A block without `record` is no old spelling: the message quotes nothing the config did not say.
        var unspelled = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("""{ "trace": {} }""", Root)).Message;
        Assert.StartsWith("trace must be one of off, on, ", unspelled, StringComparison.Ordinal);
        Assert.DoesNotContain("record", unspelled, StringComparison.Ordinal);
        Assert.Contains(
            "targets[0].trace 'retries' is the old spelling of trace: 'on-all-retries'",
            Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("""{ "targets": [{ "trace": "retries" }] }""", Root)).Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "E2E_TRACE 'all' is the old spelling of E2E_TRACE=on",
            Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("{}", Root, name => name == "E2E_TRACE" ? "all" : null)).Message,
            StringComparison.Ordinal);
        Assert.Equal(
            "video must be one of off, on, retain-on-failure, on-first-retry, on-all-retries, got \"all\"",
            Assert.Throws<ConfigurationException>(() => E2EConfig.Parse("""{ "video": "all" }""", Root)).Message);
    }

    private static ConfigurationException Failure(string json) => Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(json, Root));

    private sealed class Store : IArtifactStore
    {
        public Task<string> PutAsync(StoredArtifact artifact, CancellationToken cancellationToken = default) => Task.FromResult("ref");
    }
}
