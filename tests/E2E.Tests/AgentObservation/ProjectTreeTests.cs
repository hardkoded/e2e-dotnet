// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.AgentObservation;

public sealed class ProjectTreeTests
{
    [Fact]
    public void Renders_a_redacted_tree_names_text_values_and_attributes_redacted_no_secure_value_no_selection_a_secret_may_show_no_selector()
    {
        var redactor = Redactor.For([Secret.Create("password", "hunter2")]);
        var rect = new BoundingBox(1, 2, 3, 4);
        var tree = new SemanticNode
        {
            Ref = "root",
            Role = "document",
            Children =
            [
                new SemanticNode { Ref = "n1", Role = "textbox", Name = "Password", Value = "hunter2", Selection = "hunter2", Selector = "input[name=password]", States = new NodeStates { Secure = true, Focused = true } },
                new SemanticNode { Ref = "n3", Role = "textbox", Value = "hunter2 stays", Selection = "hunter2" },
                new SemanticNode
                {
                    Ref = "n2",
                    Role = "link",
                    Name = "hunter2 profile",
                    Text = "see hunter2",
                    Attributes = new Dictionary<string, string> { ["href"] = "/u/hunter2" },
                    Rect = rect,
                },
            ],
        };

        var projected = global::E2E.AgentObservation.ProjectTree([tree], redactor, null).Single();

        Assert.Equal("root", projected.Id);
        Assert.Equal("document", projected.Role);
        var secure = projected.Children[0];
        Assert.Equal("Password", secure.Name);
        Assert.Null(secure.Value);
        Assert.Null(secure.Selection);
        Assert.True(secure.States.Secure);
        Assert.True(secure.States.Focused);
        var plain = projected.Children[1];
        Assert.Equal("<secret:password> stays", plain.Value);
        Assert.Null(plain.Selection);
        // Upstream compares the whole projection: n1 carries no value, text, attributes, or rect, and the root no name.
        Assert.Null(projected.Name);
        Assert.Null(projected.Value);
        Assert.Null(secure.Text);
        Assert.Null(secure.Attributes);
        Assert.Null(secure.Rect);
        Assert.Null(plain.Name);
        Assert.Null(plain.Text);
        Assert.Null(plain.Attributes);
        Assert.Null(plain.Rect);
        var link = projected.Children[2];
        Assert.Equal("<secret:password> profile", link.Name);
        Assert.Equal("see <secret:password>", link.Text);
        Assert.Equal("/u/<secret:password>", link.Attributes!["href"]);
        Assert.Equal(rect, link.Rect);
        Assert.DoesNotContain("hunter2", System.Text.Json.JsonSerializer.Serialize(projected), StringComparison.Ordinal);
        Assert.DoesNotContain("selector", System.Text.Json.JsonSerializer.Serialize(projected), StringComparison.Ordinal);
    }
}
