// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Config;

/// <summary>
/// Upstream <c>describe('artifacts config')</c> of config.test.ts. The port has no <c>artifacts</c> key, so only the
/// test of the old trace spellings is here.
/// </summary>
public sealed class ArtifactsConfigTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "e2e-config-root");

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
}
