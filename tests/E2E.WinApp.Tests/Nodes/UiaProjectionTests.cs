// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.WinApp;
using E2E.WinApp.Tests.Helpers;

namespace E2E.WinApp.Tests.Nodes;

/// <summary>.NET-only: upstream has no Windows engine, so the projection of a UI Automation tree has no upstream <c>it</c>.</summary>
public sealed class UiaProjectionTests
{
    private static readonly Point Origin = new(100, 50);

    private static ProjectedWindow Project(RawElement root, string? focused = null, double scale = 1, int budget = ObservationLimits.Nodes)
    {
        var counter = 0;
        return UiaProjection.ProjectWindow(root, FakeWindowDriver.MainHandle, Origin, scale, focused, () => $"n{++counter}", new ProjectionBudget(budget));
    }

    private static SemanticNode Find(ProjectedWindow window, string handle) => window.Index.First(entry => entry.Raw.Handle == handle).Node;

    [Theory]
    [InlineData("Button", null, "button")]
    [InlineData("Button", "ToggleSwitch", "switch")]
    [InlineData("Hyperlink", null, "link")]
    [InlineData("Edit", "TextBox", "textbox")]
    [InlineData("CheckBox", null, "checkbox")]
    [InlineData("RadioButton", null, "radio")]
    [InlineData("ComboBox", null, "combobox")]
    [InlineData("List", null, "list")]
    [InlineData("ListItem", null, "listitem")]
    [InlineData("Tab", null, "tablist")]
    [InlineData("TabItem", null, "tab")]
    [InlineData("Slider", null, "slider")]
    [InlineData("ProgressBar", null, "progressbar")]
    [InlineData("Image", null, "image")]
    [InlineData("Text", null, "text")]
    [InlineData("Pane", null, "group")]
    [InlineData("Custom", null, "group")]
    [InlineData("MenuItem", null, "menuitem")]
    [InlineData("AppBar", null, "toolbar")]
    [InlineData("Window", "ContentDialog", "dialog")]
    [InlineData("Window", "Xaml_WindowedPopupClass", "dialog")]
    [InlineData("Thumb", null, "thumb")]
    public void Maps_each_control_type_onto_the_role_vocabulary(string type, string? className, string role)
    {
        var child = RawElements.Element("x", type, "X") with { ClassName = className };
        var window = Project(RawElements.Window("Main", child));
        Assert.Equal(role, Find(window, "x").Role);
    }

    [Fact]
    public void The_top_window_is_a_window_and_a_nested_window_a_dialog()
    {
        var window = Project(RawElements.Window("Main", RawElements.Element("inner", "Window", "Confirm")));
        Assert.Equal("window", window.Node.Role);
        Assert.Equal("dialog", Find(window, "inner").Role);
    }

    [Fact]
    public void A_password_box_is_secure_and_never_reports_its_value()
    {
        var window = Project(RawElements.Settings());
        var password = Find(window, "edit-pass");
        Assert.True(password.States.Secure);
        Assert.Equal("password", password.InputPurpose);
        Assert.Null(password.Value);
        Assert.Equal("textbox", password.Role);
        Assert.Equal("password-input", password.TestId);

        var byClass = Project(RawElements.Window("W", RawElements.TextBox("p", "Pin") with { ClassName = "PasswordBox", IsPassword = null, Value = "1234" }));
        Assert.True(Find(byClass, "p").States.Secure);
        Assert.Null(Find(byClass, "p").Value);
    }

    [Fact]
    public void Reports_states_off_the_pattern_values()
    {
        var window = Project(RawElements.Settings(), focused: "edit-name");
        Assert.True(Find(window, "chk-terms").States.Checked);
        Assert.False(Find(window, "tog-dark").States.Checked);
        Assert.True(Find(window, "grp-footer").States.Disabled);
        Assert.True(Find(window, "btn-off").States.Hidden);
        Assert.True(Find(window, "edit-name").States.Focused);
        Assert.False(Find(window, "btn-save").States.Focused);
        Assert.False(Find(window, "combo-size").States.Expanded);
        Assert.True(Find(Project(RawElements.Window("W", RawElements.ComboBox("c", "Size", expanded: true))), "c").States.Expanded);
    }

    [Fact]
    public void Reports_the_automation_id_as_the_test_id_and_the_value_of_a_field()
    {
        var window = Project(RawElements.Settings());
        var name = Find(window, "edit-name");
        Assert.Equal("name-input", name.TestId);
        Assert.Equal("Ada", name.Value);
        Assert.Equal("Name", name.Name);
        Assert.Null(Find(window, "tog-dark").TestId);
    }

    [Fact]
    public void Text_is_the_name_of_a_text_or_control_node_and_nothing_for_a_field()
    {
        var window = Project(RawElements.Settings());
        Assert.Equal("Settings are saved on exit", Find(window, "txt-note").Text);
        Assert.Equal("Save", Find(window, "btn-save").Text);
        Assert.Null(Find(window, "edit-name").Text);
    }

    [Fact]
    public void Boxes_are_relative_to_the_window_origin_in_logical_pixels()
    {
        var window = Project(RawElements.Window("W", RawElements.Button("b", "Go", null, new Rect(300, 250, 200, 100))), scale: 2);
        var rect = Assert.IsType<BoundingBox>(Find(window, "b").Rect);
        Assert.Equal(new BoundingBox(100, 100, 100, 50), rect);
        Assert.Null(Find(Project(RawElements.Settings()), "btn-off").Rect);
    }

    [Fact]
    public void Cuts_a_name_and_a_text_at_the_contract_limits()
    {
        var longName = new string('a', ObservationLimits.Text + 10);
        var window = Project(RawElements.Window("W", RawElements.Element("t", "Text", longName)));
        Assert.Equal(ObservationLimits.Name, Find(window, "t").Name!.Length);
        Assert.Equal(ObservationLimits.Text, Find(window, "t").Text!.Length);
    }

    [Fact]
    public void Mints_ids_in_document_order_and_binds_each_to_its_element()
    {
        var window = Project(RawElements.Window("W", RawElements.Button("a", "A"), RawElements.Element("g", "Group", null, null, null, RawElements.Button("b", "B"))));
        Assert.Equal(["n1", "n2", "n3", "n4"], window.Index.Select(entry => entry.Id));
        Assert.Equal(["root", "a", "g", "b"], window.Index.Select(entry => entry.Raw.Handle));
        Assert.Same(window.Index[0], window.Index[3].Parent!.Parent);
        Assert.True(UiaProjection.IsWithin(window.Index[3], window.Index[0]));
        Assert.False(UiaProjection.IsWithin(window.Index[1], window.Index[2]));
    }

    [Fact]
    public void Stops_at_the_node_budget_and_says_so()
    {
        var items = Enumerable.Range(1, 10).Select(index => RawElements.ListItem($"i{index}", $"Item {index}")).ToArray();
        var window = Project(RawElements.Window("W", RawElements.List("l", "Items", items)), budget: 5);
        Assert.True(window.Truncated);
        Assert.Equal(5, window.Index.Count);
        Assert.Equal(3, Find(window, "l").Children.Count);
    }

    [Fact]
    public void A_walk_the_driver_cut_short_is_truncated_too()
    {
        var window = Project(RawElements.Window("W", RawElements.List("l", "Items") with { HasMoreChildren = true }));
        Assert.True(window.Truncated);
        Assert.False(Project(RawElements.Settings()).Truncated);
    }

    [Fact]
    public void The_screen_root_has_the_stable_id_and_the_viewport_box()
    {
        var root = UiaProjection.ScreenRoot([Project(RawElements.Settings()).Node], new ViewportSize(800, 600));
        Assert.Equal(UiaProjection.RootId, root.Ref);
        Assert.Equal("screen", root.Role);
        Assert.Equal(new BoundingBox(0, 0, 800, 600), root.Rect);
        Assert.Single(root.Children);
    }

    [Theory]
    [InlineData("vertical", ScrollDirection.Down, true)]
    [InlineData("vertical", ScrollDirection.Right, false)]
    [InlineData("horizontal", ScrollDirection.Left, true)]
    [InlineData("horizontal", ScrollDirection.Up, false)]
    [InlineData("both", ScrollDirection.Up, true)]
    [InlineData("both", ScrollDirection.Right, true)]
    [InlineData("", ScrollDirection.Down, false)]
    [InlineData(null, ScrollDirection.Down, false)]
    public void Knows_which_way_an_element_scrolls(string? scrollDir, ScrollDirection direction, bool scrolls)
    {
        Assert.Equal(scrolls, UiaProjection.Scrolls(scrollDir, direction));
    }
}
