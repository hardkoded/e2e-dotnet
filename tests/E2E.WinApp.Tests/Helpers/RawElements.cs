// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.WinApp;

namespace E2E.WinApp.Tests.Helpers;

/// <summary>Builders for the element trees the tests project: a settings window with the controls the role map must answer for.</summary>
public static class RawElements
{
    public static RawElement Element(string handle, string type, string? name = null, string? automationId = null, Rect? bounds = null, params RawElement[] children) => new()
    {
        Handle = handle,
        Type = type,
        Name = name,
        AutomationId = automationId,
        Bounds = bounds ?? new Rect(100, 50, 100, 20),
        Children = children,
    };

    public static RawElement Button(string handle, string name, string? automationId = null, Rect? bounds = null) =>
        Element(handle, "Button", name, automationId, bounds) with { IsInvokable = true };

    public static RawElement TextBox(string handle, string name, string? value = null, string? automationId = null) =>
        Element(handle, "Edit", name, automationId, new Rect(120, 80, 200, 32)) with { Value = value, ClassName = "TextBox" };

    public static RawElement PasswordBox(string handle, string name) =>
        Element(handle, "Edit", name, "password-input", new Rect(120, 120, 200, 32)) with { ClassName = "PasswordBox", IsPassword = true, Value = "secret" };

    public static RawElement Toggle(string handle, string name, bool on) =>
        Element(handle, "Button", name, null, new Rect(120, 160, 60, 20)) with { ClassName = "ToggleSwitch", ToggleState = on ? "On" : "Off", IsInvokable = true };

    public static RawElement CheckBox(string handle, string name, bool checked_) =>
        Element(handle, "CheckBox", name, null, new Rect(120, 190, 120, 20)) with { ToggleState = checked_ ? "On" : "Off", IsInvokable = true };

    public static RawElement Radio(string handle, string name, bool selected) =>
        Element(handle, "RadioButton", name, null, new Rect(120, 220, 120, 20)) with { IsSelected = selected, IsInvokable = true };

    public static RawElement ListItem(string handle, string name) =>
        Element(handle, "ListItem", name, null, new Rect(120, 260, 200, 24)) with { IsInvokable = true };

    public static RawElement List(string handle, string name, params RawElement[] items) =>
        Element(handle, "List", name, "benchmark-list", new Rect(100, 250, 400, 300), items) with { ScrollDir = "vertical" };

    public static RawElement ComboBox(string handle, string name, bool expanded, params RawElement[] items) =>
        Element(handle, "ComboBox", name, null, new Rect(120, 560, 200, 32), items) with { ExpandState = expanded ? "Expanded" : "Collapsed", IsInvokable = true };

    public static RawElement Dialog(string handle, string title, params RawElement[] children) =>
        Element(handle, "Window", title, null, new Rect(300, 200, 400, 200), children) with { ClassName = "ContentDialog" };

    public static RawElement Window(string title, params RawElement[] children) =>
        Element("root", "Window", title, null, new Rect(100, 50, 800, 600), children);

    /// <summary>A settings window with one of each control.</summary>
    public static RawElement Settings() => Window(
        "Settings",
        Button("btn-save", "Save", "save-button", new Rect(500, 600, 80, 30)),
        Button("btn-off", "Hidden", "hidden-button", new Rect(0, 0, 0, 0)) with { IsOffscreen = true, Bounds = null },
        TextBox("edit-name", "Name", "Ada", "name-input"),
        PasswordBox("edit-pass", "Password"),
        Toggle("tog-dark", "Dark mode", on: false),
        CheckBox("chk-terms", "Accept terms", checked_: true),
        Radio("rad-small", "Small", selected: false),
        List("list-items", "Items", ListItem("item-1", "First"), ListItem("item-2", "Second")),
        ComboBox("combo-size", "Size", expanded: false),
        Element("txt-note", "Text", "Settings are saved on exit", null, new Rect(120, 20, 300, 20)),
        Element("grp-footer", "Pane", null, null, new Rect(100, 600, 800, 50)) with { IsEnabled = false });
}
