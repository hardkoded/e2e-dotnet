// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.StepScreens;

/// <summary>
/// Upstream's <c>describe('the session screen route and environment')</c> (<c>step-screens.test.ts</c>). The tests build the port's
/// <see cref="SessionScreens"/> and <see cref="EnvironmentFacts"/> directly: the port has no <c>createEngineSession</c> adapter.
/// </summary>
public sealed class SessionScreenRouteAndEnvironmentTests
{
    private static Observation Snapshot(string name) => new()
    {
        Route = "com.example.app",
        Roots = [new SemanticNode { Ref = "root", Role = "document", Children = [new SemanticNode { Ref = "n1", Role = "button", Name = name }] }],
    };

    // Partial: the engine hands the port a typed observation, so upstream's malformed snapshots and `treeUnavailable` ones have no counterpart to drop.
    [Fact]
    public void Hands_every_engine_screen_to_the_routed_sink_until_it_is_unrouted()
    {
        var screens = new SessionScreens(traced: true);
        var heard = new List<Observation>();

        screens.Push(Snapshot("before any sink"));
        screens.Route(heard.Add);
        screens.Push(Snapshot("Add"));
        Assert.Single(heard);
        Assert.Equal("com.example.app", heard[0].Route);

        screens.Route(null);
        screens.Push(Snapshot("after"));
        Assert.Single(heard);
    }

    [Fact]
    public void Takes_no_screen_at_all_for_an_attempt_that_keeps_no_trace()
    {
        var screens = new SessionScreens(traced: false);
        var heard = new List<Observation>();
        screens.Route(heard.Add);

        screens.Push(Snapshot("Add"));

        Assert.False(screens.Traced);
        Assert.Empty(heard);
    }

    [Fact]
    public void Merges_environment_facts_overwriting_a_name_dropping_blanks_and_keeping_a_handful_redacted_before_they_are_clipped()
    {
        var environment = new EnvironmentFacts();
        Assert.Null(environment.Read(text => text));

        environment.Push(new Dictionary<string, string> { ["browser"] = "chromium 140" });
        // A secret that straddles the value's clip: redacted whole first, so no head of it is left.
        environment.Push(new Dictionary<string, string> { ["browser"] = "chromium 141", ["blank"] = "  ", ["user agent"] = "Mozilla " + new string('x', 190) + "hunter2-secret" });
        for (var index = 0; index < 10; index++)
        {
            environment.Push(new Dictionary<string, string> { [$"fact {index}"] = "yes" });
        }

        var facts = environment.Read(text => text.Replace("hunter2-secret", "<secret:ua>", StringComparison.Ordinal))!;

        Assert.Equal("chromium 141", facts["browser"]);
        Assert.DoesNotContain("blank", facts.Keys);
        Assert.Equal(200, facts["user agent"].Length);
        Assert.DoesNotContain("hu", facts["user agent"], StringComparison.Ordinal);
        Assert.Equal(8, facts.Count);
    }

    [Fact]
    public void Redacts_a_secret_longer_than_anything_held_before_whole()
    {
        var environment = new EnvironmentFacts();
        var secret = "sk-" + new string('z', 6000);
        environment.Push(new Dictionary<string, string> { ["token"] = secret });

        var facts = environment.Read(text => text.Replace(secret, "<secret:token>", StringComparison.Ordinal))!;

        Assert.Equal("<secret:token>", facts["token"]);
    }
}
