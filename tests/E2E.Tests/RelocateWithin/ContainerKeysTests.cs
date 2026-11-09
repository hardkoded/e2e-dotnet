// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;
using Recorder = E2E.Cache.TraceRecorder;

namespace E2E.Tests.RelocateWithin;

/// <summary>
/// Ports <c>relocate-within.test.ts</c>'s "container keys". The prose is <see cref="AgentActions.DescribeAction"/>, upstream's <c>describeAction</c>.
/// The relocation case of "relocates a same-named control by its row instead of diverging as ambiguous" and the windowed list run through <see cref="E2E.Agent.Find"/>, the
/// descriptor match replay uses today.
/// </summary>
public sealed class ContainerKeysTests
{
    private static int _next;

    private static SemanticNode Node(string role, string? name = null, string? text = null, params SemanticNode[] children) =>
        new() { Ref = "n" + Interlocked.Increment(ref _next), Role = role, Name = name, Text = text, Children = children };

    private static PreparedObservation Screen(params SemanticNode[] roots) => global::E2E.Tests.TraceReplay.TestHost.Prepare(new Observation { Route = "/", Roots = roots }, Redactor.None);

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

    [Fact]
    public void Records_the_key_with_the_action_and_reads_it_back_in_the_prose()
    {
        var (_, _, delete1, _) = Table();

        var recorded = new Recorder(() => Redactor.None).Record("tap", delete1, within: "Budget draft");

        Assert.Equal("Budget draft", recorded.Within);
        Assert.Equal("tap button \"Delete\" in \"Budget draft\"", AgentActions.DescribeAction(recorded, Redactor.None));
    }

    [Fact]
    public void Relocates_a_same_named_control_by_its_row_instead_of_diverging_as_ambiguous()
    {
        var (table, _, _, delete2) = Table();
        var screen = Screen(table);

        // The match is the prepared (redacted) node of the screen, so it is the same node by its ref, not the same object.
        Assert.Equal(delete2.Ref, Assert.Single(E2E.Agent.Find(screen, "button", "Delete", null, null, Redactor.None, "Vendor list")).Ref);
        Assert.Equal(2, E2E.Agent.Find(screen, "button", "Delete", null, null, Redactor.None).Count);
        // The row is gone: not found, never the other row's button.
        Assert.Empty(E2E.Agent.Find(screen, "button", "Delete", null, null, Redactor.None, "Offsite plan"));
    }

    [Fact]
    public void Keys_an_icon_button_by_its_rows_own_label_when_the_row_has_no_other_text_and_finds_it_after_the_rows_reorder()
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
        var position = Relocate.DescribePosition(recordedAlpha, within, Screen(recorded), Redactor.None);
        Assert.Equal((0, 1), (position?.Index, position?.Of));

        var (live, liveAlpha) = List("Beta", "Alpha");

        var found = Relocate.RelocateDescriptor(new RecordedTarget { Role = "button", Within = within, Position = position }, Screen(live), Redactor.None);
        Assert.Equal(new RelocationResult.Found(liveAlpha.Ref), found);
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
        Assert.Equal(liveRows[512].Row.Ref, Assert.Single(E2E.Agent.Find(Screen(live), "group", "Row 0512", "row-512", null, Redactor.None)).Ref);
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
