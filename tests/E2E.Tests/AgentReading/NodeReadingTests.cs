// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.AgentReading;

/// <summary>
/// Upstream's <c>describe('nodeReading')</c>. The port reads a plain, case-insensitive substring: no word boundary and no
/// length slack. So upstream's "reads a whole label, or the text bounded by nothing of a word, never a longer number
/// sharing its prefix" is ported in part, and "skips a sentence that mentions the text and a hidden node" in part.
/// </summary>
public sealed class NodeReadingTests
{
    /// <summary>Partial: the port has no word boundary, so "Row 120" reads "Row 12". Only a whole label, spacing and case are checked.</summary>
    [Fact]
    public void Reads_a_label_whatever_its_spacing_and_case()
    {
        var needle = TextRules.Normalize("Row 12");
        Assert.Equal("b", Read(needle, Node("a", text: "Row 1"), Node("b", text: "Row 12"))?.Ref);
        Assert.Equal("a", Read(needle, Node("a", text: "row  12"))?.Ref);
    }

    /// <summary>Partial: the port has no length slack, so a sentence that mentions the text reads it. Only the hidden node is checked.</summary>
    [Fact]
    public void Skips_a_hidden_node()
    {
        Assert.Null(Read(TextRules.Normalize("Row 0512"), Node("gone", text: "Row 0512", hidden: true)));
    }

    [Fact]
    public void Reads_only_inside_the_list_it_is_given_and_the_whole_screen_without_one()
    {
        var jump = Node("jump", role: "button", name: "Jump to Row 333");
        var list = Node("list", role: "list", name: "Ledger", children: [Node("r1", role: "listitem", text: "Row 1"), Node("r2", role: "listitem", text: "Row 2")]);
        var needle = TextRules.Normalize("Row 333");
        Assert.Null(ReadIn(needle, list, jump, list));
        Assert.Equal("jump", ReadIn(needle, null, jump, list)?.Ref);
        var rendered = Node("list", role: "list", name: "Ledger", children: [.. list.Children, Node("r333", role: "listitem", text: "Row 333")]);
        Assert.Equal("r333", ReadIn(needle, rendered, jump, rendered)?.Ref);
    }

    private static SemanticNode Node(string id, string? role = null, string? name = null, string? text = null, bool hidden = false, IReadOnlyList<SemanticNode>? children = null) =>
        new() { Ref = id, Role = role, Name = name, Text = text, States = new NodeStates { Hidden = hidden }, Children = children ?? [] };

    private static SemanticNode? Read(string needle, params SemanticNode[] roots) => ReadIn(needle, null, roots);

    private static SemanticNode? ReadIn(string needle, SemanticNode? scope, params SemanticNode[] roots) =>
        Reading.NodeReading(new Observation { Route = "/", Roots = roots }, needle, Redactor.None, scope);
}
