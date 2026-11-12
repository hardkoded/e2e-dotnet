// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Mobile.Tests.RawNodes;

namespace E2E.Mobile.Tests.Nodes;

public sealed class NodesUnderTheSoftKeyboardTests
{
    private static readonly Rect Keyboard = new(0, 566, 402, 308);

    /// <summary>
    /// The shape XCTest reports for a form with the keyboard up: the keyboard's window first, then the app. The Brand field and
    /// the Continue row sit under the keyboard; the Product name field, the form, and the suggestion bar agent-device marks
    /// keyboard-owned do not.
    /// </summary>
    private static readonly RawNode[] Form =
    [
        N("@e0", index: 0, depth: 0, type: "Application", label: "Shop", rect: new Rect(0, 0, 402, 874)),
        N("@e1", index: 1, parentIndex: 0, depth: 1, type: "Window", rect: new Rect(0, 0, 402, 874)),
        N("@e2", index: 2, parentIndex: 1, depth: 2, type: "Other", rect: new Rect(0, 566, 402, 308)),
        N("@e3", index: 3, parentIndex: 2, depth: 3, type: "Keyboard", rect: new Rect(0, 583, 402, 233)),
        N("@e4", index: 4, parentIndex: 3, depth: 4, type: "Key", label: "q", rect: new Rect(4, 590, 39, 54)),
        N("@e5", index: 5, parentIndex: 3, depth: 4, type: "Button", label: "return", rect: new Rect(300, 752, 99, 54)),
        N("@e6", index: 6, parentIndex: 2, depth: 3, type: "Button", label: "Dictate", keyboardOwned: true, rect: new Rect(325, 805, 68, 68)),

        // The keyboard's window holds its dock; agent-device does not always mark the dock keyboard-owned.
        N("@e14", index: 14, parentIndex: 2, depth: 3, type: "Button", label: "Next keyboard", rect: new Rect(8, 805, 68, 68)),
        N("@e7", index: 7, parentIndex: 0, depth: 1, type: "Other", label: "Form", rect: new Rect(0, 116, 402, 758)),
        N("@e8", index: 8, parentIndex: 7, depth: 2, type: "TextField", label: "Product name", rect: new Rect(24, 200, 354, 44)),
        N("@e9", index: 9, parentIndex: 7, depth: 2, type: "TextField", label: "Brand", identifier: "brand", rect: new Rect(24, 600, 354, 44)),
        N("@e10", index: 10, parentIndex: 7, depth: 2, type: "Other", label: "Continue", rect: new Rect(24, 806, 354, 44)),
        N("@e11", index: 11, parentIndex: 10, depth: 3, type: "StaticText", label: "Continue", rect: new Rect(160, 818, 80, 20)),

        // A row whose top shows above the keyboard while its centre is under it: the label child shows, so the row does.
        N("@e12", index: 12, parentIndex: 7, depth: 2, type: "Cell", label: "Notes", rect: new Rect(0, 520, 402, 120)),
        N("@e13", index: 13, parentIndex: 12, depth: 3, type: "StaticText", label: "Notes", rect: new Rect(24, 528, 80, 20)),
    ];

    private static List<string?> HiddenNamesOf(RawNode[] nodes, Rect? keyboard)
    {
        var counter = 0;
        return [.. SnapshotProjection.ProjectSnapshot(nodes, () => $"n{++counter}", keyboard: keyboard).Index.Where(entry => entry.Node.States.Hidden).Select(entry => entry.Node.Name)];
    }

    [Fact]
    public void Hides_a_node_whose_centre_the_keyboard_covers_and_none_of_whose_children_shows()
    {
        Assert.Equal(["Brand", "Continue", "Continue"], HiddenNamesOf(Form, Keyboard));
    }

    [Fact]
    public void Keeps_the_keyboards_own_nodes_a_straddling_row_with_a_label_above_the_keyboard_and_everything_above_it()
    {
        var counter = 0;
        var index = SnapshotProjection.ProjectSnapshot(Form, () => $"n{++counter}", keyboard: Keyboard).Index;
        foreach (var name in new[] { "q", "return", "Dictate", "Next keyboard", "Product name", "Form", "Notes", "Shop" })
        {
            Assert.False(index.First(entry => entry.Node.Name == name).Node.States.Hidden, name);
        }
    }

    [Fact]
    public void Keeps_the_dock_that_follows_the_keyboard_element_under_the_same_parent_and_the_focused_field()
    {
        // The shape agent-device's iOS 27 simulator capture reports: no window, the app first, then the keyboard and its dock
        // as siblings. The focused search field reports a frame from before the keyboard came up, under the keyboard, while the
        // screen draws it above.
        RawNode[] nodes =
        [
            N("@e0", index: 0, depth: 0, type: "Application", label: "Settings", rect: new Rect(0, 0, 402, 874)),
            N("@e1", index: 1, parentIndex: 0, depth: 1, type: "Button", label: "Passwords", rect: new Rect(16, 718, 370, 52)),
            N("@e2", index: 2, parentIndex: 0, depth: 1, type: "SearchField", label: "Search", focused: true, rect: new Rect(26, 789, 344, 28)),
            N("@e3", index: 3, parentIndex: 0, depth: 1, type: "Keyboard", rect: new Rect(0, 590, 402, 226)),
            N("@e4", index: 4, parentIndex: 0, depth: 1, type: "Button", label: "Next keyboard", rect: new Rect(8, 805, 68, 68)),
        ];
        Assert.Equal(["Passwords"], HiddenNamesOf(nodes, new Rect(0, 590, 402, 284)));
    }

    [Fact]
    public void Hides_app_content_listed_after_a_keyboard_that_has_no_window_above_the_dock()
    {
        // Keyboard first, then the app: a later sibling that starts above the band is app content.
        RawNode[] nodes =
        [
            N("@e0", index: 0, depth: 0, type: "Application", label: "Shop", rect: new Rect(0, 0, 402, 874)),
            N("@e1", index: 1, parentIndex: 0, depth: 1, type: "Keyboard", rect: new Rect(0, 500, 402, 300)),
            N("@e2", index: 2, parentIndex: 0, depth: 1, type: "Button", label: "Next keyboard", rect: new Rect(8, 805, 68, 68)),
            N("@e3", index: 3, parentIndex: 0, depth: 1, type: "Other", label: "Form", rect: new Rect(0, 116, 402, 758)),
            N("@e4", index: 4, parentIndex: 3, depth: 2, type: "Button", label: "Continue", rect: new Rect(24, 650, 354, 40)),
        ];
        Assert.Equal(["Continue"], HiddenNamesOf(nodes, new Rect(0, 500, 402, 374)));
    }

    [Fact]
    public void Reads_the_band_off_keys_a_capture_lists_with_no_keyboard_element_around_them()
    {
        RawNode[] keys =
        [
            N("@e0", index: 0, depth: 0, type: "Application", label: "Shop", rect: new Rect(0, 0, 402, 874)),
            N("@e1", index: 1, parentIndex: 0, depth: 1, type: "Key", label: "q", rect: new Rect(4, 590, 39, 54)),
            N("@e2", index: 2, parentIndex: 0, depth: 1, type: "Key", label: "p", rect: new Rect(360, 590, 39, 54)),
        ];
        Assert.Equal(new Rect(4, 590, 395, 284), SnapshotProjection.KeyboardBand(keys, null, null, null, null));
    }

    [Fact]
    public void Hides_nothing_when_the_producer_measured_no_keyboard_frame()
    {
        Assert.Empty(HiddenNamesOf(Form, null));
    }

    [Fact]
    public void Keeps_a_node_beside_a_keyboard_that_does_not_span_the_screen()
    {
        Assert.Empty(HiddenNamesOf(Form, new Rect(0, 566, 150, 308)));
    }
}
