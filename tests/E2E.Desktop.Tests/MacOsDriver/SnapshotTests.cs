// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Desktop.MacOs;

namespace E2E.Desktop.Tests.MacOsDriver;

/// <summary>Ported from the <c>tests</c> module of <c>crates/e2e-desktop-napi/src/snapshot.rs</c>.</summary>
public sealed class SnapshotTests
{
    private static readonly Rect Window = new(0.0, 0.0, 800.0, 600.0);

    [Fact]
    public void Names_a_row_from_the_text_inside_it()
    {
        Assert.Equal("Item 1", Snapshot.ContentName(["Item 1"]));
        Assert.Equal("Invoice 42 Paid", Snapshot.ContentName(["Invoice 42", " ", "Paid "]));
        Assert.Null(Snapshot.ContentName([]));
        Assert.Null(Snapshot.ContentName(["  "]));
    }

    [Fact]
    public void Reads_a_heading_level_only_from_a_whole_number_from_one()
    {
        Assert.Equal(2, Snapshot.LevelOf(new AxValue.NumberValue(2.0)));
        AxValue?[] none =
        [
            new AxValue.NumberValue(0.0),
            new AxValue.NumberValue(1.5),
            new AxValue.NumberValue(-1.0),
            new AxValue.BoolValue(true),
            null,
        ];
        foreach (var value in none)
        {
            Assert.Null(Snapshot.LevelOf(value));
        }
    }

    [Fact]
    public void Reads_a_node_past_the_clip_as_outside()
    {
        Assert.True(Snapshot.Outside(new Rect(0.0, 700.0, 100.0, 20.0), Window));
        Assert.False(Snapshot.Outside(new Rect(10.0, 10.0, 100.0, 20.0), Window));
    }

    [Fact]
    public void Counts_only_a_stall_as_a_hole_in_the_read()
    {
        Assert.True(Snapshot.Missed(AxError.CannotComplete));
        foreach (
            var error in new[]
            {
                AxError.InvalidElement,
                AxError.NoValue,
                AxError.AttributeUnsupported,
                AxError.Failure,
                AxError.NotImplemented,
            }
        )
        {
            Assert.False(Snapshot.Missed(error), error.ToString());
        }
    }

    [Fact]
    public void Reads_a_disclosure_triangle_by_its_value_and_no_menu_item_as_selected()
    {
        Assert.True(Snapshot.Expanded("disclosure", null, 1));
        Assert.False(Snapshot.Expanded("disclosure", null, 0));
        Assert.False(Snapshot.Expanded("checkbox", null, 1));
        Assert.True(Snapshot.Expanded("treeitem", true, null));
        Assert.False(Snapshot.Selected("menuitem", true, null));
        Assert.True(Snapshot.Selected("row", true, null));

        // An AppKit tab is the active one by its value, and no other role is selected by it.
        Assert.True(Snapshot.Selected("tab", null, 1));
        Assert.False(Snapshot.Selected("tab", null, 0));
        Assert.False(Snapshot.Selected("radiobutton", null, 1));
    }

    [Fact]
    public void Leaves_a_popover_or_menu_past_the_window_unclipped()
    {
        // A SwiftUI popover hangs off its window: its text sits above the window's top edge.
        var above = new Rect(10.0, -37.0, 100.0, 20.0);
        Assert.True(Snapshot.Outside(above, Snapshot.InnerClip("group", null, Window)!.Value));
        Assert.Null(Snapshot.InnerClip("popover", null, Window));
        Assert.Null(Snapshot.InnerClip("menu", null, Window));
    }

    [Fact]
    public void Reads_a_box_state_from_a_number_a_boolean_or_a_word()
    {
        Assert.Equal((byte)1, Snapshot.ParseToggle(new AxValue.TextValue("on")));
        Assert.Equal((byte)0, Snapshot.ParseToggle(new AxValue.TextValue("off")));
        Assert.Equal((byte)2, Snapshot.ParseToggle(new AxValue.TextValue("mixed")));
        Assert.Equal((byte)1, Snapshot.ParseToggle(new AxValue.NumberValue(1.0)));
        Assert.Equal((byte)0, Snapshot.ParseToggle(new AxValue.BoolValue(false)));
        Assert.Null(Snapshot.ParseToggle(new AxValue.TextValue("maybe")));
    }

    [Fact]
    public void Never_reads_a_zero_size_node_as_outside()
    {
        // An open context menu on macOS 27: its `AXMenu` is 0x0 at the screen's corner, its items are in the window.
        Assert.False(Snapshot.Outside(new Rect(0.0, 1080.0, 0.0, 0.0), Window));
    }
}
