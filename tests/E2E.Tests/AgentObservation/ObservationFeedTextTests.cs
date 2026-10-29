// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentObservation;

/// <summary>
/// .NET-only: the port's feed renders a look's text on demand and keeps the newest rendering, where upstream redacts each
/// capture once when it is taken (<c>prepareObservation</c>) and keeps the redacted capture. No upstream <c>it</c> covers the
/// kept rendering.
/// </summary>
public sealed class ObservationFeedTextTests
{
    private const string Value = "feed-secret-Qx7-2718";

    private static AttemptScope Scope() => new()
    {
        Session = null!,
        Agents = new Dictionary<string, ResolvedAgent>(),
        Cache = null,
        CacheEnabled = false,
        TestTitle = "feed",
        EnginePlatform = "web",
    };

    [Fact]
    public void Redacts_a_secret_registered_after_the_look_was_first_rendered()
    {
        var scope = Scope();
        var feed = new ObservationFeed(null!, TimeProvider.System) { Scope = scope };
        var look = new Observation
        {
            Route = "/",
            Roots = [new SemanticNode { Ref = "n1", Role = "button", Name = "Pay with " + Value }],
        };

        Assert.Contains(Value, feed.Text(look), StringComparison.Ordinal);

        scope.Register("CARD", Value);

        var text = feed.Text(look);
        Assert.DoesNotContain(Value, text, StringComparison.Ordinal);
        Assert.Contains("<secret:CARD>", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Reuses_the_rendering_of_a_look_while_no_secret_is_registered()
    {
        var feed = new ObservationFeed(null!, TimeProvider.System) { Scope = Scope() };
        var look = new Observation { Route = "/", Roots = [new SemanticNode { Ref = "n1", Role = "button", Name = "Pay" }] };

        Assert.Same(feed.Text(look), feed.Text(look));
    }
}
