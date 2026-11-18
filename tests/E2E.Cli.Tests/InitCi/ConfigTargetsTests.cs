// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Cli.Tests.InitCi;

/// <summary>Upstream's <c>describe('configTargets')</c>. The port reads the JSON config's <c>targets</c> without resolving engines, so a target an engine would refuse is not left out.</summary>
public sealed class ConfigTargetsTests
{
    [Fact]
    // Partial: the platform is the target's own, defaulting to web, and the name defaults to the platform.
    public void Partial_Names_and_places_each_target_the_way_the_config_declares_it()
    {
        var config = JsonNode
            .Parse(
                """{ "targets": [ { "platform": "web" }, { "name": "iphone", "platform": "ios" }, { "name": "api", "platform": "custom" }, { "name": "unknown" } ] }"""
            )!
            .AsObject();
        Assert.Equal(
            [new CiTarget("web", "web"), new("iphone", "ios"), new("api", "custom"), new("unknown", "web")],
            Ci.ConfigTargets(config)
        );
        Assert.Equal([new CiTarget("web", "web")], Ci.ConfigTargets([]));
    }
}
