// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Config;

/// <summary>.NET-only: <c>E2E_TARGET</c> is the port's <c>e2e run --target</c> (one name), which <c>e2e init --ci</c> sets in each job that runs one target. Upstream's <c>--target</c> has no <c>it</c> that loads a config.</summary>
public sealed class TargetSelectionTests
{
    private const string Json =
        """{ "targets": [{ "name": "web", "app": { "url": "http://127.0.0.1:4173/" } }, { "name": "api", "app": { "url": "http://127.0.0.1:4174/" } }] }""";

    private static readonly string Root = Path.Combine(Path.GetTempPath(), "e2e-target-selection-project");

    [Fact]
    public void Needs_E2E_TARGET_when_the_config_declares_several_targets()
    {
        var error = Assert.Throws<ConfigurationException>(() => E2EConfig.Parse(Json, Root, _ => null).Target);
        Assert.Equal("TARGET_REQUIRED", error.Code);
        Assert.Contains("set E2E_TARGET", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Runs_on_the_target_E2E_TARGET_names()
    {
        var config = E2EConfig.Parse(Json, Root, name => name == "E2E_TARGET" ? " api " : null);
        Assert.Equal("api", config.Target.Name);
    }

    [Fact]
    public void Rejects_a_name_the_config_does_not_declare_and_ignores_a_blank_one()
    {
        var unknown = E2EConfig.Parse(Json, Root, name => name == "E2E_TARGET" ? "nope" : null);
        Assert.Equal("UNKNOWN_TARGET", Assert.Throws<ConfigurationException>(() => unknown.Target).Code);
        var one = E2EConfig.Parse(
            """{ "targets": [{ "name": "web", "app": { "url": "http://127.0.0.1:4173/" } }] }""",
            Root,
            name => name == "E2E_TARGET" ? "  " : null
        );
        Assert.Equal("web", one.Target.Name);
    }
}
