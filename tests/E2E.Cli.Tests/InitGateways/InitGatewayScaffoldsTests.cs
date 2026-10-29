// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Tests.InitGateways;

/// <summary>
/// Upstream's <c>describe('init gateway scaffolds')</c> of <c>tests/unit/init-gateways.test.ts</c>: every gateway the
/// scaffold offers must build a model the config accepts, or <c>e2e init</c> writes a config that fails to load.
/// </summary>
public sealed class InitGatewayScaffoldsTests
{
    public static TheoryData<string> GatewayIds() => [.. Gateways.All.Select(gateway => gateway.Id)];

    [Theory]
    [MemberData(nameof(GatewayIds))]
    // Partial: the port names a provider where upstream imports an export, so there is no import line and no package to check.
    public void Partial_Imports_a_real_export_and_builds_a_model_the_config_accepts(string id)
    {
        var config = Scaffold.CreateScaffold(["web"], new ScaffoldModel(id)).Config;
        var parsed = E2EConfig.Parse(config, Path.GetTempPath());
        var agent = parsed.Agents["default"];
        Assert.Contains(agent.Provider, AgentConfig.Providers);
        Assert.False(string.IsNullOrEmpty(agent.Model));
        Assert.Equal(Gateways.GetGatewayPreset(id).Provider, agent.Provider);
    }
}
