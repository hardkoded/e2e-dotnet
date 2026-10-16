// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentReading;

public sealed class ReadingShapeTests
{
    /// <summary>
    /// Partial: upstream also expects a node with no name and no text to add nothing, so the shape of one is empty. The
    /// port's shape is the rendered screen and the scroll position, and is never empty.
    /// </summary>
    [Fact]
    public void Differs_when_a_row_keeps_its_name_and_its_place_but_its_text_moves_on_and_matches_for_the_same_row()
    {
        var before = Screen("Product 1");
        Assert.NotEqual(Reading.ReadingShape(before), Reading.ReadingShape(Screen("Product 2")));
        Assert.Equal(Reading.ReadingShape(before), Reading.ReadingShape(Screen("Product 1")));
        Assert.NotEqual(Reading.ReadingShape(before), Reading.ReadingShape(Screen("Product 1", "y=400")));
    }

    private static Observation Screen(string text, string? scroll = null) => new()
    {
        Route = "/",
        ScrollPosition = scroll,
        Roots = [new SemanticNode { Ref = "r", Role = "listitem", Name = "Product", Text = text, Rect = new BoundingBox(0, 100, 300, 40) }],
    };
}
