// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentScrollTarget;

/// <summary>
/// <c>describe('samePlace')</c> of <c>agent-scroll-target.test.ts</c>: re-finding a scrolled list by its place once the tree has
/// renumbered and renamed it.
/// </summary>
public sealed class SamePlaceTests
{
    private static readonly BoundingBox List = new(0, 96, 402, 778);
    private static readonly BoundingBox Carousel = new(0, 300, 402, 160);

    // A renumbered screen: the main list named after its new first row, a carousel of the same role inside it.
    private static readonly Observation Screen = new()
    {
        Route = "main",
        Viewport = new TraceViewport(402, 874),
        Roots =
        [
            new SemanticNode
            {
                Ref = "root",
                Role = "application",
                Rect = new BoundingBox(0, 0, 402, 874),
                Children =
                [
                    new SemanticNode
                    {
                        Ref = "n40",
                        Role = "scroll-view",
                        Name = "Rows 13 to 24",
                        Rect = List,
                        Children = [new SemanticNode { Ref = "n41", Role = "scroll-view", Name = "Recommended", Rect = Carousel }],
                    },
                ],
            },
        ],
    };

    [Fact]
    public void Re_finds_the_list_in_its_old_frame_not_the_carousel_of_the_same_role_inside_it()
    {
        Assert.Equal("n40", ScrollTarget.SamePlace(Screen, "scroll-view", List)?.Ref);
    }

    [Fact]
    public void Re_finds_a_carousel_in_its_old_frame_and_never_the_list_around_one_that_is_gone()
    {
        Assert.Equal("n41", ScrollTarget.SamePlace(Screen, "scroll-view", Carousel)?.Ref);
        Assert.Null(ScrollTarget.SamePlace(Screen, "scroll-view", new BoundingBox(0, 600, 402, 160)));
    }

    [Fact]
    public void Holds_out_for_the_role_and_for_a_box_that_is_mostly_the_same()
    {
        Assert.Null(ScrollTarget.SamePlace(Screen, "list", List));
        Assert.Null(ScrollTarget.SamePlace(Screen, "scroll-view", new BoundingBox(0, 96, 402, 300)));
    }
}
