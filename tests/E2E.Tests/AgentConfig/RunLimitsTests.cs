// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.AgentConfig;

/// <summary>Upstream's <c>describe('run limits')</c> (<c>tests/unit/agent-config.test.ts</c>).</summary>
public sealed class RunLimitsTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "e2e-agent-config-project");

    /// <summary>Partial: upstream resolves the config twice, once with <c>cli: { agents: ['ux'] }</c>, to show the limits do not depend on the agent that runs. The port has no agent selection on a run, so it resolves once.</summary>
    [Fact]
    public void Partial_fills_the_per_agent_limits_with_the_largest_any_configured_agent_may_use_whichever_runs()
    {
        var config = Parse(
            """
            { "agents": { "default": { "maxObservationBytes": 4096, "maxInputTokens": 200000 }, "ux": { "maxObservationBytes": 65536, "maxInputTokens": 8000 } } }
            """);

        var limits = Limits(config);

        Assert.Equal(65_536, limits.MaxObservationBytes);
        Assert.Equal(200_000, limits.MaxModelTokensPerCall);
        Assert.Equal(8_192, limits.MaxLedgerBytes);
        // The implicit default agent counts too.
        Assert.Equal(64_000, Limits(Parse("""{ "agents": { "ux": { "maxInputTokens": 8000 } } }""")).MaxModelTokensPerCall);
    }

    [Fact]
    public void Rejects_the_removed_limits_key_naming_each_replacement()
    {
        Assert.StartsWith("limits was removed: set maxInputTokens on each agent", Invalid("""{ "limits": { "maxModelTokensPerCall": 1000 } }"""), StringComparison.Ordinal);
        Assert.StartsWith("limits was removed: ", Invalid("""{ "limits": {} }"""), StringComparison.Ordinal);
        Assert.StartsWith("agents.default.limits was removed: set maxInputTokens", Invalid("""{ "agents": { "default": { "limits": {} } } }"""), StringComparison.Ordinal);
    }

    private static ReportLimits Limits(E2EConfig config) =>
        ResolvedAgent.RunLimits(config.Agents.Select(pair => ResolvedAgent.From(pair.Key, pair.Value.CreateOptions())));

    private static E2EConfig Parse(string json) => E2EConfig.Parse(json, Root, _ => null);

    private static string Invalid(string json)
    {
        var error = Assert.Throws<ConfigurationException>(() => Parse(json));
        Assert.Equal("INVALID_CONFIG", error.Code);
        return error.Message;
    }
}
