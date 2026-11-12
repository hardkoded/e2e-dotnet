// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.TraceAnchors.AnchorFixtures;

namespace E2E.Tests.TraceAnchors;

/// <summary><c>describeDelta</c>, a node only a minted test id identifies.</summary>
public sealed class ANodeOnlyAMintedTestIdIdentifiesTests
{
    private const string MintedA = "rf__node-3f9c2a1b-7d4e-4c1a-9b2e-0a1b2c3d4e5f";
    private const string MintedB = "rf__node-8e21d0c4-5b6a-4f3e-8d2c-1a0b9c8d7e6f";

    private static readonly SemanticNode Added = Node("status", "Node added");

    private static SemanticNode CanvasNode(string testId) => Node("box", testId: testId);

    [Fact]
    public void Is_skipped_while_a_stable_anchor_remains_whatever_minted_the_id()
    {
        foreach (var testId in new[] { MintedA, "rf__node-cjld2cjxh0000qzrmn831i7rn", "rf__node-652f1c3e9b1e8a0012345678", "rf__node-1697040000000" })
        {
            Assert.Equal(["status|Node added"], Shapes(Describe([Heading], [Heading, CanvasNode(testId), Added]).Appeared));
        }
    }

    [Fact]
    public void Is_kept_when_nothing_stable_appeared_for_the_replay_to_check_by_its_shape()
    {
        Assert.Equal(["box|#" + MintedA], Shapes(Describe([Heading], [Heading, CanvasNode(MintedA)]).Appeared));
    }

    [Fact]
    public void Is_no_change_when_it_re_rendered_under_a_new_id_or_joined_others_of_its_shape()
    {
        var (appeared, gone) = Describe([Heading, CanvasNode(MintedA)], [Heading, CanvasNode(MintedB), Added]);
        Assert.Equal(["status|Node added"], Shapes(appeared));
        Assert.Empty(gone);

        var (joinedAppeared, joinedGone) = Describe([Heading, CanvasNode(MintedA)], [Heading, CanvasNode(MintedA), CanvasNode(MintedB)]);
        Assert.Empty(joinedAppeared);
        Assert.Empty(joinedGone);
    }

    [Fact]
    public void Leaves_a_test_id_that_only_numbers_its_node_and_a_named_node_as_they_were()
    {
        var numbered = new[] { "todo-item-12", "rf__node-dndnode_0", "product-trail-mix-1000", "otp-input-5" }.Select(CanvasNode);
        var named = Node("button", "Delete", testId: MintedA);

        Assert.Equal(6, Describe([Heading], [Heading, .. numbered, named, Added]).Appeared.Count);
    }
}
