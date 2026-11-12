// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;
using static E2E.Tests.RelocatePosition.DescribePositionTests;

namespace E2E.Tests.RelocateMintedTestId;

/// <summary>
/// Ports the <c>describePosition for a target only a minted test id identifies</c> describe of upstream's
/// <c>relocate-minted-test-id.test.ts</c>: a target counted among the nodes of its test id shape, even alone.
/// </summary>
public sealed class DescribePositionForATargetOnlyAMintedTestIdIdentifiesTests
{
    internal const string A = "rf__node-3f9c2a1b-7d4e-4c1a-9b2e-0a1b2c3d4e5f";

    internal const string B = "rf__node-8e21d0c4-5b6a-4f3e-8d2c-1a0b9c8d7e6f";

    internal const string C = "rf__node-0b1c2d3e-4f5a-4b6c-8d7e-9f0a1b2c3d4e";

    internal static SemanticNode CanvasNode(string id, string testId, string role = "box", string? name = null) =>
        new() { Ref = id, Role = role, TestId = testId, Name = name };

    [Fact]
    public void Counts_the_target_among_the_nodes_of_its_shape_even_alone()
    {
        var a = CanvasNode("a", A);
        var one = Screen(a);
        Assert.Equal((0, 1), Position(Relocate.DescribePosition(a, null, one, Redactor.None)));
        var b = CanvasNode("b", B);
        var two = Screen(CanvasNode("a", A), b);
        Assert.Equal((1, 2), Position(Relocate.DescribePosition(b, null, two, Redactor.None)));
    }

    [Fact]
    public void Records_nothing_for_a_test_id_that_only_numbers_its_node_or_a_node_its_name_identifies()
    {
        var a = CanvasNode("a", "rf__node-dndnode_0");
        var o = CanvasNode("o", "order-12345678");
        var numbered = Screen(a, o);
        Assert.Null(Relocate.DescribePosition(a, null, numbered, Redactor.None));
        Assert.Null(Relocate.DescribePosition(o, null, numbered, Redactor.None));
        var d = CanvasNode("d", A, "button", "Delete");
        Assert.Null(Relocate.DescribePosition(d, null, Screen(d), Redactor.None));
    }

    private static (int Index, int Of) Position(TracePosition? position) => (position!.Index, position.Of);
}
