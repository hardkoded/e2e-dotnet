// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;
using Recorder = E2E.Cache.TraceRecorder;

namespace E2E.Tests.RelocateWithin;

/// <summary>
/// Ports <c>relocate-within.test.ts</c>'s "container keys". The port records no position and no action summary, so "records the
/// key with the action and reads it back in the prose" and "keys an icon button by its row's own label" are partial, and the
/// relocation cases run through <see cref="E2E.Agent.Find"/>, the port's descriptor match.
/// </summary>
public sealed class ContainerKeysTests
{
    private static int _next;

    private static SemanticNode Node(string role, string? name = null, string? text = null, params SemanticNode[] children) =>
        new() { Ref = "n" + Interlocked.Increment(ref _next), Role = role, Name = name, Text = text, Children = children };

    private static Observation Screen(params SemanticNode[] roots) => new() { Route = "/", Roots = roots };

    private static string? KeyOf(SemanticNode root, SemanticNode node) =>
        AgentActions.ContainerKey(node, AgentActions.ParentsOf([root]), Redactor.None);

    /// <summary>A two-row table: identical "Delete" buttons told apart only by their row.</summary>
    private static (SemanticNode Table, SemanticNode Cell1, SemanticNode Delete1, SemanticNode Delete2) Table()
    {
        var cell1 = Node("cell", text: "Budget draft");
        var delete1 = Node("button", "Delete");
        var delete2 = Node("button", "Delete");
        var row1 = Node("row", null, null, cell1, delete1);
        var row2 = Node("row", null, null, Node("cell", text: "Vendor list"), delete2);
        return (Node("table", "Documents", null, row1, row2), cell1, delete1, delete2);
    }

    [Fact]
    public void Names_the_row_a_control_sits_in_by_the_rows_first_text()
    {
        var (table, cell1, delete1, delete2) = Table();

        Assert.Equal("Budget draft", KeyOf(table, delete1));
        Assert.Equal("Vendor list", KeyOf(table, delete2));
        // The cell itself is the row's key: nothing to add.
        Assert.Null(KeyOf(table, cell1));
    }

    /// <summary>Partial port of "records the key with the action and reads it back in the prose": the port has no summary to read it back in, so only the recorded key is checked.</summary>
    [Fact]
    public void Partial_records_the_key_with_the_action()
    {
        var (_, _, delete1, _) = Table();

        var described = Recorder.DescribeTarget(delete1, Redactor.None, "Budget draft");

        Assert.Equal("Budget draft", described.Within);
    }

    [Fact]
    public void Relocates_a_same_named_control_by_its_row_instead_of_diverging_as_ambiguous()
    {
        var (table, _, _, delete2) = Table();
        var screen = Screen(table);

        Assert.Same(delete2, Assert.Single(E2E.Agent.Find(screen, "button", "Delete", null, null, Redactor.None, "Vendor list")));
        Assert.Equal(2, E2E.Agent.Find(screen, "button", "Delete", null, null, Redactor.None).Count);
        // The row is gone: not found, never the other row's button.
        Assert.Empty(E2E.Agent.Find(screen, "button", "Delete", null, null, Redactor.None, "Offsite plan"));
    }

    /// <summary>
    /// Partial port of "keys an icon button by its row's own label when the row has no other text, and finds it after the rows
    /// reorder": the port records no position, so the button is found by its role and row.
    /// </summary>
    [Fact]
    public void Partial_keys_an_icon_button_by_its_rows_own_label_and_finds_it_after_the_rows_reorder()
    {
        // <li>Alpha <button><svg/></button></li>: the list item is named from its content, the button by nothing.
        static (SemanticNode List, SemanticNode Alpha) List(params string[] order)
        {
            SemanticNode? alpha = null;
            var items = order.Select(label =>
            {
                var button = Node("button");
                if (label == "Alpha")
                {
                    alpha = button;
                }

                return Node("listitem", label, null, button);
            }).ToArray();
            return (Node("list", null, null, items), alpha!);
        }

        var (recorded, recordedAlpha) = List("Alpha", "Beta");
        var within = KeyOf(recorded, recordedAlpha);
        Assert.Equal("Alpha", within);

        var (live, liveAlpha) = List("Beta", "Alpha");

        Assert.Same(liveAlpha, Assert.Single(E2E.Agent.Find(Screen(live), "button", null, null, null, Redactor.None, within)));
    }

    [Fact]
    public void Does_not_key_a_row_of_a_windowed_list_by_whichever_twin_row_is_rendered_first()
    {
        // An Android FlatList: an unnamed group of group rows, the rendered window moving with the scroll.
        static (SemanticNode List, Dictionary<int, (SemanticNode Row, SemanticNode Text)> Rows) List(int first)
        {
            var rows = new Dictionary<int, (SemanticNode, SemanticNode)>();
            for (var n = first; n < first + 14; n++)
            {
                var label = "Row " + n.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
                var text = Node("text", text: label);
                rows[n] = (new SemanticNode { Ref = "row-" + n, Role = "group", Name = label, TestId = "row-" + n, Children = [text] }, text);
            }

            return (Node("group", null, null, rows.Values.Select(row => row.Item1).ToArray()), rows);
        }

        var (recorded, recordedRows) = List(500);
        Assert.Null(KeyOf(recorded, recordedRows[512].Row));
        Assert.Null(KeyOf(recorded, recordedRows[512].Text));
        // A control inside a row is still keyed by its row.
        var (table, _, delete1, _) = Table();
        Assert.Equal("Budget draft", KeyOf(table, delete1));

        var (live, liveRows) = List(501);
        Assert.Same(liveRows[512].Row, Assert.Single(E2E.Agent.Find(Screen(live), "group", "Row 0512", "row-512", null, Redactor.None)));
    }

    [Fact]
    public void Keys_a_control_by_its_nearest_container_only_so_rows_with_no_text_of_their_own_share_no_outer_label()
    {
        var button = Node("button");
        var group = Node("group", null, null, button);
        var region = Node("region", null, null, Node("generic", text: "Shipping address"), group);
        Assert.Null(KeyOf(region, button));

        // A row whose first text is the link itself names it already: the list around it says nothing more.
        var link = Node("link", "Beta");
        var item = Node("listitem", "Beta", null, link);
        var outer = Node("region", null, null, item);
        Assert.Null(KeyOf(outer, link));
    }
}
