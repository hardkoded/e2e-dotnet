// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.OpenSession;

/// <summary>.NET-only: upstream has no test of <c>selectTarget</c> (<c>run/select-target.ts</c>) of its own; its behavior is checked through <c>openHost</c>.</summary>
public sealed class SelectTargetTests
{
    private static TargetConfig Target(string name) => new() { Name = name, Platform = "web", App = new AppConfig { Url = "http://127.0.0.1:4173/" } };

    [Fact]
    public void Needs_a_name_when_the_config_declares_several_targets()
    {
        var error = Assert.Throws<ConfigurationException>(() => SelectTarget.Select([Target("one"), Target("two")], null, "pass `target` to openHost"));
        Assert.Equal("TARGET_REQUIRED", error.Code);
        Assert.Contains("\"one\", \"two\"", error.Message, StringComparison.Ordinal);
        Assert.Contains("pass `target` to openHost", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Picks_the_only_target_or_the_one_named()
    {
        Assert.Equal("one", SelectTarget.Select([Target("one")], null, "hint").Name);
        Assert.Equal("two", SelectTarget.Select([Target("one"), Target("two")], "two", "hint").Name);
    }

    [Fact]
    public void Rejects_an_unknown_name_and_a_config_with_no_targets()
    {
        Assert.Equal("UNKNOWN_TARGET", Assert.Throws<ConfigurationException>(() => SelectTarget.Select([Target("one")], "three", "hint")).Code);
        Assert.Equal("INVALID_CONFIG", Assert.Throws<ConfigurationException>(() => SelectTarget.Select([], null, "hint")).Code);
    }
}
