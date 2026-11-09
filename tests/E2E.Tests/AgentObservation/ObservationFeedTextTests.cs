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
    public void Remembers_the_text_a_screen_showed_redacted_and_leaves_out_the_fields_that_echo_input()
    {
        var scope = Scope();
        scope.Register("CARD", Value);
        var feed = new ObservationFeed(null!, TimeProvider.System) { Scope = scope };

        feed.NoteShown(new Observation
        {
            Route = "/",
            Roots =
            [
                new SemanticNode { Ref = "n1", Role = "button", Name = "Pay with " + Value },
                new SemanticNode { Ref = "n2", Role = "textbox", Name = "typed-by-the-model" },
                new SemanticNode { Ref = "n3", Role = "text", Text = "Ready", Value = "echo" },
            ],
        });

        Assert.Equal(["Pay with <secret:CARD>"], feed.ShownText());
    }

    [Fact]
    public void Shows_the_text_the_tree_and_the_path_of_a_view_from_one_prepared_look_after_a_late_secret()
    {
        var scope = Scope();
        var feed = new ObservationFeed(null!, TimeProvider.System) { Scope = scope };
        var look = new Observation
        {
            Route = "/pay?card=" + Value,
            Location = "https://shop.test/pay?card=" + Value,
            Roots = [new SemanticNode { Ref = "n1", Role = "button", Name = "Pay with " + Value }],
        };
        _ = feed.View(feed.PreparedOf(look), scope, new ExecutorObserveOptions { Tree = true });

        scope.Register("CARD", Value);
        var view = feed.View(feed.PreparedOf(look), scope, new ExecutorObserveOptions { Tree = true });

        Assert.Equal("/pay?card=%3Csecret:CARD%3E", view.Path);
        Assert.Equal("Pay with <secret:CARD>", view.Tree!.Single().Name);
        Assert.DoesNotContain(Value, view.Text, StringComparison.Ordinal);
        Assert.Contains("<secret:CARD>", view.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Reads_the_path_of_a_screen_with_no_tree_from_its_location_as_a_prepared_look_does()
    {
        var scope = Scope();
        scope.Register("CARD", Value);
        var feed = new ObservationFeed(null!, TimeProvider.System) { Scope = scope };
        var look = new Observation { Route = "/pay?card=" + Value, Location = "https://shop.test/pay?card=" + Value, Roots = [], TreeUnavailable = true };

        Assert.Equal("/pay?card=%3Csecret:CARD%3E", feed.View(feed.PreparedOf(look), scope).Path);
    }

    [Fact]
    public void Reuses_the_rendering_of_a_look_while_no_secret_is_registered()
    {
        var feed = new ObservationFeed(null!, TimeProvider.System) { Scope = Scope() };
        var look = new Observation { Route = "/", Roots = [new SemanticNode { Ref = "n1", Role = "button", Name = "Pay" }] };

        Assert.Same(feed.Text(look), feed.Text(look));
    }
}
