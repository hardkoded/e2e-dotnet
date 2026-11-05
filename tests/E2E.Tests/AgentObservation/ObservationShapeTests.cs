// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.AgentObservation;

/// <summary>
/// <c>observationShape</c>: what two looks are compared by. The lines use the port's screen text, where a node
/// reference reads <c>#e1</c> before the role and each state is its own bracket.
/// </summary>
public sealed class ObservationShapeTests
{
    private static string ShapeOfPixels(params byte[] data) =>
        global::E2E.AgentObservation.Shape(new Observation
        {
            Route = "/",
            Roots = [new SemanticNode { Ref = "e1", Role = "document", Name = "Map" }],
            Pixels = new EngineScreenshot(data, 1, 0),
        });

    [Fact]
    public void Is_shaped_by_the_pixels_when_a_capture_carries_them_so_a_canvas_that_redrew_counts_as_changed()
    {
        var bare = global::E2E.AgentObservation.Shape(new Observation { Route = "/", Roots = [new SemanticNode { Ref = "e1", Role = "document", Name = "Map" }] });
        Assert.Equal(ShapeOfPixels(1, 2, 3), ShapeOfPixels(1, 2, 3));
        Assert.NotEqual(ShapeOfPixels(1, 2, 3), ShapeOfPixels(1, 2, 4));
        Assert.NotEqual(ShapeOfPixels(1, 2, 3), bare);
    }

    private static string ShapeOf(string text) => global::E2E.AgentObservation.Shape(text);

    [Fact]
    public void Ignores_the_per_observation_node_ids()
    {
        Assert.Equal(
            ShapeOf("#e1 button \"Save\"\n #e2 link \"Home\""),
            ShapeOf("#e9 button \"Save\"\n #e7 link \"Home\""));
    }

    [Fact]
    public void Ignores_focus_which_moves_without_the_page_changing()
    {
        // The browser settling focus after load, a script claiming it, a widget
        // stealing it: none of it changes what a judgment would answer.
        Assert.Equal(ShapeOf("#e1 textbox \"Email\" [focused]"), ShapeOf("#e1 textbox \"Email\""));
        Assert.Equal(ShapeOf("#e1 checkbox \"Terms\" [checked] [focused]"), ShapeOf("#e1 checkbox \"Terms\" [checked]"));
    }

    [Fact]
    public void Still_notices_a_state_that_is_about_the_page()
    {
        Assert.NotEqual(ShapeOf("#e1 checkbox \"Terms\" [checked]"), ShapeOf("#e1 checkbox \"Terms\""));
        Assert.NotEqual(ShapeOf("#e1 button \"Save\" [disabled]"), ShapeOf("#e1 button \"Save\""));
    }

    [Fact]
    public void Ignores_clock_like_values_which_tick_without_the_page_changing()
    {
        // A timer would end the wait for an action's effect on its first tick and
        // keep a settle from ever seeing two looks agree.
        Assert.Equal(ShapeOf("#e1 status \"Elapsed 00:12\""), ShapeOf("#e1 status \"Elapsed 00:13\""));
        Assert.Equal(ShapeOf("#e1 text \"12:05:59\""), ShapeOf("#e1 text \"12:06:00\""));
        Assert.NotEqual(ShapeOf("#e1 status \"Items 12\""), ShapeOf("#e1 status \"Items 13\""));
    }

    [Fact]
    public void Notices_changed_text_and_changed_structure()
    {
        Assert.NotEqual(ShapeOf("#e1 status \"Loading\""), ShapeOf("#e1 status \"Ready\""));
        Assert.NotEqual(
            ShapeOf("#e1 list\n #e2 listitem \"A\""),
            ShapeOf("#e1 list\n #e2 listitem \"A\"\n #e3 listitem \"B\""));
    }
}
