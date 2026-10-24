// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.WinApp;

/// <summary>One projected element with what the session needs to act on it.</summary>
public sealed class ProjectedElement
{
    public required string Id { get; init; }

    public required RawElement Raw { get; init; }

    /// <summary>The top-level window the element sits in: where keys and the foreground go.</summary>
    public required long WindowHandle { get; init; }

    public string? Role { get; init; }

    public ProjectedElement? Parent { get; init; }

    /// <summary>The node as the contract reports it. Set once its children are projected.</summary>
    public SemanticNode Node { get; internal set; } = null!;
}

/// <summary>One window as the projection reports it.</summary>
public sealed class ProjectedWindow
{
    public required SemanticNode Node { get; init; }

    /// <summary>Every element in document order, parents before children.</summary>
    public required IReadOnlyList<ProjectedElement> Index { get; init; }

    /// <summary>True when the node budget or the driver's walk depth cut the tree short.</summary>
    public bool Truncated { get; init; }
}

/// <summary>The node budget one observation spends across its windows: upstream's node limit, see <see cref="ObservationLimits.Nodes"/>.</summary>
public sealed class ProjectionBudget(int limit)
{
    private int _taken;

    /// <summary>True once a node was refused.</summary>
    public bool Exhausted { get; private set; }

    /// <summary>Takes one node; false, and marks the budget exhausted, when none is left.</summary>
    public bool Take()
    {
        if (_taken >= limit)
        {
            Exhausted = true;
            return false;
        }

        _taken++;
        return true;
    }
}

/// <summary>
/// Projection of a UI Automation element tree onto the contract's <see cref="SemanticNode"/> tree: maps control types onto the
/// closed role vocabulary <c>screen.GetByRole</c> and trace relocation speak, converts physical screen boxes to the window's
/// device-independent pixels, and mints the ids the surface owns. The mobile engine's <c>SnapshotProjection</c>, for Windows.
/// </summary>
public static class UiaProjection
{
    /// <summary>The id of the root node every observation is reported under. The same across observations, as the contract requires.</summary>
    public const string RootId = "root";

    /// <summary>
    /// UI Automation control types onto the role vocabulary. Anything not listed keeps its lower-cased type as the role, so no
    /// information is lost; the map only makes the common controls answer the same <c>GetByRole</c> a browser does.
    /// </summary>
    private static readonly Dictionary<string, string> RoleMap = new(StringComparer.Ordinal)
    {
        ["Button"] = "button",
        ["SplitButton"] = "button",
        ["Hyperlink"] = "link",
        ["Edit"] = "textbox",
        ["Document"] = "document",
        ["CheckBox"] = "checkbox",
        ["RadioButton"] = "radio",
        ["ComboBox"] = "combobox",
        ["List"] = "list",
        ["ListItem"] = "listitem",
        ["DataGrid"] = "grid",
        ["DataItem"] = "row",
        ["Table"] = "table",
        ["Header"] = "rowgroup",
        ["HeaderItem"] = "columnheader",
        ["Tab"] = "tablist",
        ["TabItem"] = "tab",
        ["Slider"] = "slider",
        ["Spinner"] = "spinbutton",
        ["ProgressBar"] = "progressbar",
        ["Image"] = "image",
        ["Text"] = "text",
        ["Pane"] = "group",
        ["Group"] = "group",
        ["Custom"] = "group",
        ["Menu"] = "menu",
        ["MenuBar"] = "menubar",
        ["MenuItem"] = "menuitem",
        ["ToolBar"] = "toolbar",
        ["AppBar"] = "toolbar",
        ["StatusBar"] = "status",
        ["Tree"] = "tree",
        ["TreeItem"] = "treeitem",
        ["Separator"] = "separator",
        ["ScrollBar"] = "scrollbar",
        ["ToolTip"] = "tooltip",
        ["Calendar"] = "grid",
    };

    /// <summary>Framework classes that are a switch although UI Automation reports a button with a toggle.</summary>
    private static readonly string[] SwitchClasses = ["ToggleSwitch"];

    /// <summary>Framework classes of a layer over the app: a dialog, a flyout, a popup window.</summary>
    private static readonly string[] DialogClasses = ["ContentDialog", "Popup", "Flyout", "Dialog"];

    private static readonly string[] SecureClasses = ["PasswordBox"];

    /// <summary>Roles whose node text is their name: the model reads the control by it.</summary>
    private static readonly HashSet<string> TextRoles = ["text", "link", "button", "listitem", "tab", "menuitem", "checkbox", "radio", "switch", "heading", "treeitem", "tooltip", "status", "columnheader", "row"];

    /// <summary>
    /// Projects one window. <paramref name="mintId"/> is called once per node in document order, so the surface's id space stays unique
    /// across observations. Boxes are reported relative to <paramref name="origin"/>, the main window's top-left corner on the screen, over
    /// <paramref name="scale"/>, so every window of the app is in one coordinate space. <paramref name="focused"/> is the handle of the
    /// element with keyboard focus. The budget is shared by every window of the observation.
    /// </summary>
    public static ProjectedWindow ProjectWindow(RawElement root, long windowHandle, Point origin, double scale, string? focused, Func<string> mintId, ProjectionBudget budget)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(mintId);
        ArgumentNullException.ThrowIfNull(budget);
        var index = new List<ProjectedElement>();
        var truncated = false;

        SemanticNode? Build(RawElement source, ProjectedElement? parent, bool isRoot)
        {
            if (!budget.Take())
            {
                return null;
            }

            var role = RoleOf(source, isRoot);
            var secure = IsSecure(source);
            var entry = new ProjectedElement
            {
                Id = mintId(),
                Raw = source,
                WindowHandle = windowHandle,
                Role = role,
                Parent = parent,
            };
            index.Add(entry);
            var children = new List<SemanticNode>();
            foreach (var child in source.Children)
            {
                if (Build(child, entry, isRoot: false) is { } projected)
                {
                    children.Add(projected);
                }
                else
                {
                    break;
                }
            }

            if (source.HasMoreChildren == true)
            {
                truncated = true;
            }

            var name = Cut(NonEmpty(source.Name), ObservationLimits.Name);
            var text = role is not null && TextRoles.Contains(role) ? Cut(NonEmpty(source.Name), ObservationLimits.Text) : null;
            var node = new SemanticNode
            {
                Ref = entry.Id,
                Role = role,
                Name = name,
                Text = text,

                // A secure field's value is never observed; the tree carries that it is secure, not what it holds.
                Value = secure ? null : Cut(NonEmpty(source.Value), ObservationLimits.Text),
                InputPurpose = secure ? "password" : null,
                States = new NodeStates
                {
                    Disabled = !source.IsEnabled,
                    Hidden = source.IsOffscreen,
                    Checked = source.ToggleState is "On",
                    Expanded = source.ExpandState is "Expanded" or "PartiallyExpanded",
                    Focused = focused is not null && source.Handle == focused,
                    Secure = secure,
                },

                // The automation id is the node's test id: what Reactor's `.AutomationId()` and XAML's `AutomationProperties.AutomationId` set.
                TestId = NonEmpty(source.AutomationId),
                Rect = source.Bounds is { } bounds ? WinAppSupport.Logical(bounds, origin, scale) : null,
                Children = children,
            };
            entry.Node = node;
            return node;
        }

        var rootNode = Build(root, null, isRoot: true)
            ?? throw WinAppSupport.Failure("the observation's node budget is spent before the window's root");
        return new ProjectedWindow { Node = rootNode, Index = index, Truncated = truncated || budget.Exhausted };
    }

    /// <summary>
    /// The one root the contract wants over the app's windows. Its box is the main window's client area; it carries no name of
    /// its own, the app and the window title being reported as the observation's location.
    /// </summary>
    public static SemanticNode ScreenRoot(IReadOnlyList<SemanticNode> roots, ViewportSize viewport) => new()
    {
        Ref = RootId,
        Role = "screen",
        Rect = new BoundingBox(0, 0, viewport.Width, viewport.Height),
        Children = roots,
    };

    /// <summary>True when <paramref name="entry"/> is a strict descendant of <paramref name="ancestor"/>.</summary>
    public static bool IsWithin(ProjectedElement entry, ProjectedElement ancestor)
    {
        ArgumentNullException.ThrowIfNull(entry);
        for (var current = entry.Parent; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Contract role for one element. A WinUI <c>ToggleSwitch</c> reports a button with a toggle pattern, so its class says switch.
    /// A window whose class is a dialog, flyout, or popup is a <c>dialog</c>: a layer over the app, as a sheet is on a phone. The
    /// top-level window of the app is a <c>window</c>.
    /// </summary>
    internal static string? RoleOf(RawElement source, bool isRoot)
    {
        var type = source.Type ?? "";
        var className = source.ClassName ?? "";
        if (SwitchClasses.Any(known => className.Contains(known, StringComparison.Ordinal)))
        {
            return "switch";
        }

        if (DialogClasses.Any(known => className.Contains(known, StringComparison.Ordinal)))
        {
            return "dialog";
        }

        if (type == "Window")
        {
            return isRoot ? "window" : "dialog";
        }

        if (type.Length == 0)
        {
            return null;
        }

        return RoleMap.GetValueOrDefault(type) ?? type.ToLowerInvariant();
    }

    /// <summary>A password field, by the UI Automation flag when the driver read it, else by the framework class.</summary>
    internal static bool IsSecure(RawElement source) =>
        source.IsPassword == true || SecureClasses.Any(known => (source.ClassName ?? "").Contains(known, StringComparison.Ordinal));

    /// <summary>
    /// Whether an element scrolls along <paramref name="direction"/>, by the axes the library reports (<c>vertical</c>,
    /// <c>horizontal</c>, <c>both</c>, or the directions themselves). An empty report is no scroll.
    /// </summary>
    internal static bool Scrolls(string? scrollDir, ScrollDirection direction)
    {
        if (string.IsNullOrWhiteSpace(scrollDir))
        {
            return false;
        }

        var axes = scrollDir.ToLowerInvariant();
        var both = axes.Contains("both", StringComparison.Ordinal) || axes.Contains("all", StringComparison.Ordinal);
        return direction switch
        {
            ScrollDirection.Up or ScrollDirection.Down => both
                || axes.Contains("vert", StringComparison.Ordinal) || axes.Contains("up", StringComparison.Ordinal) || axes.Contains("down", StringComparison.Ordinal) || axes is "v" or "vh" or "hv",
            _ => both
                || axes.Contains("horiz", StringComparison.Ordinal) || axes.Contains("left", StringComparison.Ordinal) || axes.Contains("right", StringComparison.Ordinal) || axes is "h" or "vh" or "hv",
        };
    }

    private static string? NonEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static string? Cut(string? value, int limit) => value is null || value.Length <= limit ? value : value[..limit];
}
