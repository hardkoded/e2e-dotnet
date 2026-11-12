// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;
using static E2E.Tests.RelocateMintedTestId.DescribePositionForATargetOnlyAMintedTestIdIdentifiesTests;
using static E2E.Tests.RelocatePosition.DescribePositionTests;
using static E2E.Tests.RelocatePosition.RelocationWithARecordedPositionTests;

namespace E2E.Tests.RelocateMintedTestId;

/// <summary>
/// Ports the <c>relocation by test id shape</c> describe of upstream's <c>relocate-minted-test-id.test.ts</c>: a target only a minted test id
/// identifies is re-found by that shape once its own id is gone. The port's <see cref="Relocate.RelocateDescriptor"/> takes the observation
/// and the redactor in place of upstream's map of redacted nodes.
/// </summary>
public sealed class RelocationByTestIdShapeTests
{
    private static RecordedTarget Alone(string? testId = null, string role = "box") =>
        new() { Role = role, TestId = testId ?? A, Position = new TracePosition { Index = 0, Of = 1 } };

    private static void AssertNotFound(RelocationResult result) => Assert.Equal(new RelocationResult.Failed(RelocationFailure.TargetNotFound), result);

    private static RelocationResult Relocated(RecordedTarget descriptor, params SemanticNode[] nodes) =>
        Relocate.RelocateDescriptor(descriptor, Screen(nodes), Redactor.None);

    /// <summary>A named row holding one node: the container key (<c>within</c>) is its label.</summary>
    private static SemanticNode Row(string id, string label, SemanticNode node) =>
        new() { Ref = id, Role = "row", Children = [new SemanticNode { Ref = id + "-label", Text = label }, node] };

    [Fact]
    public void Still_finds_the_recorded_id_while_it_is_on_screen()
    {
        var descriptor = Alone();
        descriptor.Position = new TracePosition { Index = 0, Of = 2 };

        Assert.Equal(new RelocationResult.Found("a"), Relocated(descriptor, CanvasNode("b", B), CanvasNode("a", A)));
    }

    [Fact]
    public void Re_finds_the_node_by_its_shape_once_the_app_minted_it_a_new_id()
    {
        (string Recorded, string Live)[] minted =
        [
            (A, B),
            ("rf__node-abcdefab-1234-4c1a-9b2e-0a1b2c3d4e5f", B),
            ("rf__node-cjld2cjxh0000qzrmn831i7rn", "rf__node-cjld2cyuq0000t3rmniod1foy"),
            ("rf__node-652f1c3e9b1e8a0012345678", "rf__node-652f1d0a77c4b2001234abcd"),
            ("rf__node-1697040000000", "rf__node-1697040123456"),
        ];
        foreach (var (recorded, live) in minted)
        {
            Assert.Equal(new RelocationResult.Found("b"), Relocated(Alone(recorded), CanvasNode("b", live)));
        }
    }

    [Fact]
    public void Keeps_the_rest_of_the_test_id_so_another_control_of_the_same_record_is_not_the_target()
    {
        var remove = Alone("row-" + A[9..] + "-delete");

        AssertNotFound(Relocated(remove, CanvasNode("e", "row-" + B[9..] + "-edit")));
        Assert.Equal(new RelocationResult.Found("d"), Relocated(remove, CanvasNode("d", "row-" + B[9..] + "-delete")));
    }

    [Fact]
    public void Acts_only_where_a_place_means_something_alone_at_the_recorded_count_or_in_a_named_container()
    {
        AssertAmbiguous(Relocated(Alone(), CanvasNode("x", B), CanvasNode("y", C)), "x", "y");
        // Two of a shape with no container are an order, which a replay may render the other way.
        var ordered = new RecordedTarget { Role = "box", TestId = B, Position = new TracePosition { Index = 1, Of = 2 } };
        AssertNotFound(Relocated(ordered, CanvasNode("x", A), CanvasNode("y", C)));
        var recorded = Screen(Row("r1", "Start", CanvasNode("a", A)), Row("r2", "End", CanvasNode("b", B)));
        var b = recorded.Tree[1].Children[1];
        var position = Relocate.DescribePosition(b, "End", recorded, Redactor.None);
        Assert.Equal((0, 1), (position!.Index, position.Of));
        var end = new RecordedTarget { Role = "box", TestId = B, Within = "End", Position = new TracePosition { Index = 0, Of = 1 } };
        Assert.Equal(new RelocationResult.Found("d"), Relocated(end, Row("r1", "End", CanvasNode("d", C)), Row("r2", "Start", CanvasNode("c", A))));
    }

    [Fact]
    public void Is_not_tried_without_a_recorded_count_an_entry_recorded_before_it_or_a_live_re_find()
    {
        AssertNotFound(Relocated(new RecordedTarget { Role = "box", TestId = A }, CanvasNode("b", B)));
    }

    [Fact]
    public void Is_not_tried_for_a_test_id_that_only_numbers_its_node_nor_onto_a_node_of_another_role_or_with_a_name()
    {
        AssertNotFound(Relocated(Alone("todo-item-12"), CanvasNode("b", "todo-item-13")));
        AssertNotFound(Relocated(Alone("order-12345678"), CanvasNode("b", "order-87654321")));
        AssertNotFound(Relocated(Alone("row-1234567890"), CanvasNode("b", "row-9876543210")));
        AssertNotFound(Relocated(Alone(), CanvasNode("b", B, "button")));
        AssertNotFound(Relocated(Alone(), CanvasNode("b", B, name: "Start")));
    }
}
