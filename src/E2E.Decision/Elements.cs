// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.RegularExpressions;
using E2E.Engine;

namespace E2E.Decision;

/// <summary>Operations the executor offers on elements (upstream's <c>Operation</c>).</summary>
public enum Operation
{
    Tap,
    Type,
    TypeSecret,
    Submit,
    Select,
    Check,
    Hover,
    SecondaryTap,
    DoubleTap,
    LongPress,
    Drag,
    ScrollTo,
    Upload,
    TapAt,
    TypeAt,
}

/// <summary>
/// Viewport, history, and keyboard controls, offered when the engine declares the verb; <c>dismiss_keyboard</c> only while an on-screen
/// keyboard is showing (upstream's <c>Control</c>).
/// </summary>
public enum Control
{
    ScrollUp,
    ScrollDown,
    Back,
    DismissKeyboard,
}

/// <summary>Terminal choices: the step claims completion, reports a product failure, or gives up (upstream's <c>Terminal</c>).</summary>
public enum Terminal
{
    Done,
    Failed,
    Blocked,
}

/// <summary>The names an operation, a control, and a terminal carry in a question and an answer. .NET-only: upstream's are string unions.</summary>
public static class WireNames
{
    private static readonly (Operation Operation, string Name)[] OperationNames =
    [
        (Operation.Tap, "tap"),
        (Operation.Type, "type"),
        (Operation.TypeSecret, "typeSecret"),
        (Operation.Submit, "submit"),
        (Operation.Select, "select"),
        (Operation.Check, "check"),
        (Operation.Hover, "hover"),
        (Operation.SecondaryTap, "secondary_tap"),
        (Operation.DoubleTap, "double_tap"),
        (Operation.LongPress, "long_press"),
        (Operation.Drag, "drag"),
        (Operation.ScrollTo, "scroll_to"),
        (Operation.Upload, "upload"),
        (Operation.TapAt, "tap_at"),
        (Operation.TypeAt, "type_at"),
    ];

    private static readonly (Control Control, string Name)[] ControlNames =
    [
        (Control.ScrollUp, "scroll_up"),
        (Control.ScrollDown, "scroll_down"),
        (Control.Back, "back"),
        (Control.DismissKeyboard, "dismiss_keyboard"),
    ];

    public static string Of(Operation operation) => OperationNames.First(item => item.Operation == operation).Name;

    public static string Of(Control control) => ControlNames.First(item => item.Control == control).Name;

    public static string Of(Terminal terminal) =>
        terminal switch
        {
            Terminal.Done => "done",
            Terminal.Failed => "failed",
            _ => "blocked",
        };

    public static Operation? OperationNamed(string name) =>
        OperationNames.Where(item => item.Name == name).Select(item => (Operation?)item.Operation).FirstOrDefault();

    public static Control? ControlNamed(string name) =>
        ControlNames.Where(item => item.Name == name).Select(item => (Control?)item.Control).FirstOrDefault();
}

/// <summary>One row of the element table the decision model reads (upstream's <c>Element</c>).</summary>
public sealed record Element(
    string Index,
    string Role,
    string Label,
    string? Value,
    bool? Checked,
    bool? Expanded,
    bool? Selected,
    IReadOnlyList<Operation> Operations
);

/// <summary>One target of an operation: the node it acts on, as data. The model only ever names the key (upstream's <c>Target</c>).</summary>
/// <param name="Id">The node's id in the observation.</param>
/// <param name="Description">How the action reads in history, e.g. <c>tap Save [n4]</c>.</param>
/// <param name="OptionLabel">Native-select option label, set only for <c>select</c> options; names the choice criterion.</param>
/// <param name="Checked">The checked state a <c>check</c> flips.</param>
public sealed record Target(string Id, string Description, string? OptionLabel = null, bool? Checked = null);

/// <summary>One drop destination for <c>drag</c>, keyed like an element (upstream's <c>Destination</c>).</summary>
public sealed record Destination(string Id, string Label, string Role);

/// <summary>What the action space reads of the screen (upstream's <c>SpaceObservation</c>).</summary>
public sealed class SpaceObservation
{
    public string? Path { get; init; }

    public required TraceViewport Viewport { get; init; }

    public required IReadOnlyList<ExecutorNode> Tree { get; init; }

    /// <summary>Granted pixels; with the <c>tapAt</c> verb and a step that locates they open <c>tap_at</c>.</summary>
    public ExecutorPixels? Pixels { get; init; }

    /// <summary>Whether the step can locate a drawn control on the pixels: the decision model scores and a text model names it.</summary>
    public bool Locates { get; init; }

    /// <summary>The engine's measured keyboard state; null leaves it to the tree.</summary>
    public bool? KeyboardVisible { get; init; }
}

/// <summary>The element table and the targets bound to it (upstream's <c>ActionSpace</c>).</summary>
public sealed class ActionSpace
{
    public required IReadOnlyList<Element> Elements { get; init; }

    /// <summary>Per operation: target key to target. Only operations with at least one target appear.</summary>
    public required IReadOnlyDictionary<Operation, IReadOnlyDictionary<string, Target>> Targets { get; init; }

    public required IReadOnlyList<Control> Controls { get; init; }

    /// <summary>Where a <c>drag</c> can drop, by element key; empty when nothing can receive a drop.</summary>
    public required IReadOnlyDictionary<string, Destination> Destinations { get; init; }

    /// <summary>Whether <c>tap_at</c> is offered: a screenshot, the <c>tapAt</c> verb, and a step that locates.</summary>
    public bool TapAt { get; init; }

    /// <summary>Whether <c>type_at</c> is offered: <c>tap_at</c> is, and the engine types into whatever holds focus.</summary>
    public bool TypeAt { get; init; }

    /// <summary>Elements left out to stay under the per-question cap; scrolling can bring them into view.</summary>
    public int Omitted { get; init; }

    /// <summary>Non-interactive page text from the tree, without node ids, clipped to 6000 chars.</summary>
    public string PageText { get; init; } = "";

    /// <summary>Live regions (status, alert, log) as <c>name: text</c>, the lines a completion check reads first.</summary>
    public IReadOnlyList<string> Statuses { get; init; } = [];

    /// <summary>Stable hash of path, tree content with node ids removed, and which nodes are in view.</summary>
    public string Fingerprint { get; init; } = "";

    private IReadOnlyDictionary<string, Element> ByIndex { get; init; } = new Dictionary<string, Element>();

    internal static ActionSpace Create(
        IReadOnlyList<Element> elements,
        IReadOnlyDictionary<Operation, IReadOnlyDictionary<string, Target>> targets,
        IReadOnlyList<Control> controls,
        IReadOnlyDictionary<string, Destination> destinations,
        bool tapAt,
        bool typeAt,
        int omitted,
        string pageText,
        IReadOnlyList<string> statuses,
        string fingerprint
    ) =>
        new()
        {
            Elements = elements,
            Targets = targets,
            Controls = controls,
            Destinations = destinations,
            TapAt = tapAt,
            TypeAt = typeAt,
            Omitted = omitted,
            PageText = pageText,
            Statuses = statuses,
            Fingerprint = fingerprint,
            ByIndex = elements.ToDictionary(element => element.Index, StringComparer.Ordinal),
        };

    /// <summary>Upstream's <c>element(key)</c>: the element behind a target key, for labels and field descriptions.</summary>
    public Element? Element(string key) =>
        ByIndex.TryGetValue(E2E.Decision.Elements.TargetKeyIndex(key), out var element) ? element : null;

    /// <summary>
    /// Upstream's <c>operation(choice)</c> (named apart here: a member cannot share the name of the <see cref="E2E.Decision.Operation"/> type it
    /// returns). The operation an answer names, typed; null for anything not offered.
    /// </summary>
    public Operation? OperationNamed(string choice)
    {
        if (WireNames.OperationNamed(choice) is not { } operation)
        {
            return null;
        }

        return
            Targets.ContainsKey(operation)
            || (TapAt && operation == Operation.TapAt)
            || (TypeAt && operation == Operation.TypeAt)
            ? operation
            : null;
    }

    /// <summary>Upstream's <c>control(choice)</c> (named apart for the same reason as <see cref="OperationNamed"/>): the control an answer names, typed; null for anything not offered.</summary>
    public Control? ControlNamed(string choice) =>
        WireNames.ControlNamed(choice) is { } control && Controls.Contains(control) ? control : null;
}

/// <summary>Upstream's <c>elements.ts</c>. Its free functions live here as members (the class carries the module's name, as <c>Model</c> and <c>Budget</c> do).</summary>
public static partial class Elements
{
    /// <summary>TypeSafe's per-question choice limit; the AI SDK itself has none (upstream's <c>MAX_CHOICES</c>).</summary>
    private const int MaxChoices = 255;

    /// <summary>Targets one question can offer: every target question also carries the <c>none</c> fallback (upstream's <c>MAX_TARGETS</c>).</summary>
    private const int MaxTargets = MaxChoices - 1;

    private const int MaxStatuses = 20;
    private const int MaxStatusLength = 300;

    private static readonly HashSet<string> Tappable =
    [
        "button",
        "link",
        "menuitem",
        "menuitemcheckbox",
        "menuitemradio",
        "tab",
        "option",
        "treeitem",
    ];

    private static readonly HashSet<string> Typable = ["textbox", "searchbox", "spinbutton", "combobox"];
    private static readonly HashSet<string> SecretTypable = ["textbox", "searchbox", "combobox"];
    private static readonly HashSet<string> Checkable = ["checkbox", "radio", "switch"];

    /// <summary>Roles a device tree gives the soft keyboard and its keys; an iOS number pad lists only its keys.</summary>
    private static readonly HashSet<string> KeyboardRoles = ["keyboard", "key"];

    /// <summary>Live regions whose text reports what the app just did; a log can run long, so each line and their count are bounded.</summary>
    private static readonly HashSet<string> LiveRegions = ["status", "alert", "log"];

    /// <summary>
    /// Roles of nodes that are no control yet take a pointer: a card that opens a menu on hover, a file row that takes a right-click, a
    /// paragraph to scroll to, a column to drop on. The empty role is a plain text node.
    /// </summary>
    private static readonly HashSet<string> PassiveRoles =
    [
        "",
        "listitem",
        "article",
        "region",
        "group",
        "cell",
        "gridcell",
        "row",
        "img",
        "figure",
        "heading",
        "paragraph",
        "list",
        "section",
        "tabpanel",
    ];

    /// <summary>Roles whose selected state a tree reports even when it is false: a tab that is not the active one still carries it.</summary>
    private static readonly HashSet<string> SelectableRoles = ["tab", "option", "treeitem"];

    /// <summary>How each operation reads in a target description and in history.</summary>
    private static readonly Dictionary<Operation, string> Verbs = new()
    {
        [Operation.Select] = "select",
        [Operation.TapAt] = "tap at",
        [Operation.TypeAt] = "type at",
        [Operation.Tap] = "tap",
        [Operation.Type] = "type into",
        [Operation.TypeSecret] = "typeSecret into",
        [Operation.Submit] = "submit",
        [Operation.Check] = "check",
        [Operation.Hover] = "hover",
        [Operation.SecondaryTap] = "right-click",
        [Operation.DoubleTap] = "double-tap",
        [Operation.LongPress] = "long-press",
        [Operation.ScrollTo] = "scroll to",
        [Operation.Upload] = "upload to",
        [Operation.Drag] = "drag",
    };

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Whitespace();

    /// <summary>Upstream's <c>actionSpace</c>. Builds the element table and bound targets from the newest observation.</summary>
    public static ActionSpace ActionSpace(StepExecutorContext ctx, SpaceObservation observation, bool canType)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(observation);
        var verbs = ctx.Target.Verbs;
        var rows = new List<Row>();
        var pageText = new List<string>();
        var statuses = new List<string>();

        // Walks the tree, collecting interactive rows and page text.
        void Visit(ExecutorNode node, bool underNativeSelect)
        {
            if (!node.States.Disabled && !node.States.Hidden)
            {
                var operations = new List<Operation>();
                var role = node.Role ?? "";
                var password = node.InputPurpose == "password" || node.States.Secure;
                var nativeSelect = role == "combobox" && node.Children.Any(child => child.Role == "option");
                var childSelect = underNativeSelect || nativeSelect;
                var checkedRadio = role == "radio" && CheckedOf(node) == true;
                var fileInput = node.Attributes is { } attributes && attributes.GetValueOrDefault("type") == "file";
                var inViewport = Intersects(node, observation.Viewport);
                var pressable =
                    Tappable.Contains(role)
                    && !Checkable.Contains(role)
                    && !(role == "option" && childSelect)
                    && !fileInput;
                var field = Typable.Contains(role) && !nativeSelect;
                // A tap on a field focuses it and opens what the app attaches there: a date picker, a suggestion list.
                if (verbs.Contains(ExecutorVerb.Tap) && (pressable || field))
                {
                    operations.Add(Operation.Tap);
                }

                if (field)
                {
                    if (!password && verbs.Contains(ExecutorVerb.Type) && canType)
                    {
                        operations.Add(Operation.Type);
                    }

                    if (
                        verbs.Contains(ExecutorVerb.TypeSecret)
                        && SecretTypable.Contains(role)
                        && ctx.Step.Secrets.Count > 0
                    )
                    {
                        operations.Add(Operation.TypeSecret);
                    }

                    if (!password && verbs.Contains(ExecutorVerb.Press))
                    {
                        operations.Add(Operation.Submit);
                    }
                }

                if (verbs.Contains(ExecutorVerb.Select) && nativeSelect)
                {
                    operations.Add(Operation.Select);
                }

                // `check` toggles, so a checked radio would be unchecked: offer radios only when unchecked.
                if (verbs.Contains(ExecutorVerb.Check) && Checkable.Contains(role) && !checkedRadio)
                {
                    operations.Add(Operation.Check);
                }

                // Paths come from the text model, as typed values do.
                if (verbs.Contains(ExecutorVerb.Upload) && fileInput && canType)
                {
                    operations.Add(Operation.Upload);
                }

                var interactive = operations.Count > 0;
                if (pressable)
                {
                    if (verbs.Contains(ExecutorVerb.DoubleTap))
                    {
                        operations.Add(Operation.DoubleTap);
                    }

                    if (verbs.Contains(ExecutorVerb.LongPress))
                    {
                        operations.Add(Operation.LongPress);
                    }
                }

                // Pointer and scroll operations reach any labeled node, not only controls: a card menu that opens on hover, a file row
                // that takes a right-click, a footnote to bring into view. A port tree lists the page's top-level nodes with no root
                // of its own, so no node is left out as upstream leaves out its root.
                var pointable = (interactive || (PassiveRoles.Contains(role) && !childSelect)) && NodeLabel(node) != "";
                if (pointable)
                {
                    if (verbs.Contains(ExecutorVerb.Hover))
                    {
                        operations.Add(Operation.Hover);
                    }

                    if (verbs.Contains(ExecutorVerb.SecondaryTap))
                    {
                        operations.Add(Operation.SecondaryTap);
                    }

                    if (verbs.Contains(ExecutorVerb.Drag))
                    {
                        operations.Add(Operation.Drag);
                    }

                    if (verbs.Contains(ExecutorVerb.ScrollTo) && node.Rect is not null && !inViewport)
                    {
                        operations.Add(Operation.ScrollTo);
                    }
                }

                if (operations.Count > 0)
                {
                    rows.Add(new Row(node, operations, inViewport, !interactive));
                }

                if (!interactive)
                {
                    var line = PageLine(node);
                    // A checked radio leaves the table, so its state rides on the page text.
                    if (line != "")
                    {
                        pageText.Add(checkedRadio ? line + " (checked)" : line);
                    }

                    if (LiveRegions.Contains(role) && statuses.Count < MaxStatuses)
                    {
                        var text = Whitespace().Replace(node.Text ?? "", " ").Trim();
                        if (text != "")
                        {
                            var name = (node.Name ?? "").Trim();
                            statuses.Add(Clip((name == "" ? role : name) + ": " + text, MaxStatusLength));
                        }
                    }
                }
            }

            foreach (var child in node.Children)
            {
                Visit(child, underNativeSelect || node.Role == "combobox");
            }
        }

        foreach (var node in observation.Tree)
        {
            Visit(node, false);
        }

        // Controls before passive rows, viewport nodes first within each, then tree order; drop the rest past the cap so the model can
        // scroll to them, and never block on a large screen. A control below the fold still outranks any amount of visible prose.
        var ordered = rows.Select((row, order) => (Row: row, Order: order))
            .OrderBy(item => item.Row.Passive ? 1 : 0)
            .ThenBy(item => item.Row.InViewport ? 0 : 1)
            .ThenBy(item => item.Order)
            .Select(item => item.Row)
            .ToList();
        var omitted = Math.Max(0, ordered.Count - MaxTargets);
        var kept = ordered.Take(MaxTargets).ToList();

        // Drop targets come from the kept rows a pointer reaches, whatever their role: a board whose columns are bare text takes a drop
        // on the column's title. A drag needs a second row to drop on, or it is no operation.
        var destinations = new Dictionary<string, Destination>(StringComparer.Ordinal);
        if (verbs.Contains(ExecutorVerb.Drag))
        {
            foreach (var (position, row) in kept.Index())
            {
                var role = row.Node.Role ?? "";
                var label = Clip(NodeLabel(row.Node), 120);
                if (row.Operations.Contains(Operation.Drag))
                {
                    destinations[(position + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)] =
                        new Destination(row.Node.Id, label, role);
                }
            }

            if (destinations.Count < 2)
            {
                destinations.Clear();
            }
        }

        var targets = new OrderedDictionary<Operation, IReadOnlyDictionary<string, Target>>();
        var groups = new Dictionary<Operation, OrderedDictionary<string, Target>>();
        var selectCount = 0;
        var elements = new List<Element>();
        foreach (var (position, row) in kept.Index())
        {
            var index = (position + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var label = Clip(NodeLabel(row.Node), 120);
            var role = row.Node.Role ?? "";
            var operations =
                destinations.Count == 0 ? row.Operations.Where(op => op != Operation.Drag).ToList() : row.Operations;
            foreach (var operation in operations)
            {
                if (operation == Operation.Select)
                {
                    var options = new OrderedDictionary<string, Target>();
                    foreach (var (optionIndex, child) in row.Node.Children.Index())
                    {
                        if (child.Role != "option")
                        {
                            continue;
                        }

                        // An option the page hides or disables is no choice: the native select cannot perform it, so the model must
                        // never see it.
                        if (child.States.Hidden || child.States.Disabled)
                        {
                            continue;
                        }

                        // Every select shares one target group and one question, so options stop at the per-question cap. They stay out
                        // of `omitted`: scrolling never reveals an option the cap dropped.
                        if (selectCount >= MaxTargets)
                        {
                            continue;
                        }

                        var key = index + ":" + optionIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        var optionLabel = child.Name ?? child.Text ?? "";
                        options[key] = new Target(
                            row.Node.Id,
                            "select option " + JsonText.Quote(optionLabel) + " in " + Named(role, label),
                            optionLabel
                        );
                        selectCount++;
                    }

                    if (options.Count > 0)
                    {
                        if (!groups.TryGetValue(Operation.Select, out var group))
                        {
                            group = new OrderedDictionary<string, Target>();
                            groups[Operation.Select] = group;
                            targets[Operation.Select] = group;
                        }

                        foreach (var (key, target) in options)
                        {
                            group[key] = target;
                        }
                    }
                }
                else
                {
                    if (!groups.TryGetValue(operation, out var group))
                    {
                        group = new OrderedDictionary<string, Target>();
                        groups[operation] = group;
                        targets[operation] = group;
                    }

                    group[index] = new Target(
                        row.Node.Id,
                        Verbs[operation] + " " + Named(role, label),
                        null,
                        CheckedOf(row.Node)
                    );
                }
            }

            elements.Add(
                new Element(
                    index,
                    role,
                    label,
                    row.Node.Value,
                    CheckedOf(row.Node),
                    ExpandedOf(row.Node, ctx),
                    SelectedOf(row.Node),
                    operations
                )
            );
        }

        var tapAt = observation.Pixels is not null && observation.Locates && verbs.Contains(ExecutorVerb.TapAt);
        var typeAt = tapAt && verbs.Contains(ExecutorVerb.TypeText);
        var controls = new List<Control>();
        if (verbs.Contains(ExecutorVerb.Scroll))
        {
            controls.Add(Control.ScrollUp);
            controls.Add(Control.ScrollDown);
        }

        if (verbs.Contains(ExecutorVerb.Back))
        {
            controls.Add(Control.Back);
        }

        if (
            verbs.Contains(ExecutorVerb.DismissKeyboard)
            && (observation.KeyboardVisible ?? observation.Tree.Any(KeyboardShowing))
        )
        {
            controls.Add(Control.DismissKeyboard);
        }

        return E2E.Decision.ActionSpace.Create(
            elements,
            targets,
            controls,
            destinations,
            tapAt,
            typeAt,
            omitted,
            Clip(string.Join('\n', pageText), 6000),
            statuses,
            Fingerprint(observation)
        );
    }

    /// <summary>Upstream's <c>targetKeyIndex</c>. Element index behind a target key (<c>7</c> for both <c>7</c> and <c>7:2</c>).</summary>
    public static string TargetKeyIndex(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var at = key.IndexOf(':', StringComparison.Ordinal);
        return at == -1 ? key : key[..at];
    }

    /// <summary>
    /// A node as an action line names it: <c>checkbox "Agree to terms"</c>. Measured on gpt-6-luna over captured requests, the role and the
    /// quoted label lift a completion verdict the bare label with a node id left inconclusive.
    /// </summary>
    private static string Named(string role, string label) =>
        role == "" ? JsonText.Quote(label) : role + " " + JsonText.Quote(label);

    /// <summary>
    /// The checked state a tree reports; null for a node that reports none (the port's <see cref="NodeStates"/> carries a flag where upstream's
    /// state is optional).
    /// </summary>
    private static bool? CheckedOf(ExecutorNode node) => node.States.CheckedReported ? node.States.Checked : null;

    /// <summary>
    /// The expanded state: true when the node is, and false on the web when the page set <c>aria-expanded</c> to false. Another engine's
    /// tree has no absent marker, so it reports only a node that is expanded (.NET-only: upstream's state is optional).
    /// </summary>
    private static bool? ExpandedOf(ExecutorNode node, StepExecutorContext ctx)
    {
        if (node.States.Expanded)
        {
            return true;
        }

        return !node.States.ExpandedAbsent && ctx.Target.Platform == "web" ? false : null;
    }

    /// <summary>
    /// The selected state: true when the node is selected, false for a role that carries one (a tab, an option, a tree item). The port's state
    /// is a flag, with no absent marker (.NET-only: upstream's state is optional).
    /// </summary>
    private static bool? SelectedOf(ExecutorNode node)
    {
        if (node.States.Selected)
        {
            return true;
        }

        return SelectableRoles.Contains(node.Role ?? "") ? false : null;
    }

    /// <summary>
    /// Whether the tree lists an on-screen keyboard, for an engine that does not measure it. Offering the control only then keeps a choice
    /// that does nothing out of the operation question.
    /// </summary>
    private static bool KeyboardShowing(ExecutorNode node)
    {
        if (node.States.Hidden)
        {
            return false;
        }

        return KeyboardRoles.Contains(node.Role ?? "") || node.Children.Any(KeyboardShowing);
    }

    /// <summary>Label the model reads: name, placeholder, or text.</summary>
    private static string NodeLabel(ExecutorNode node) =>
        node.Name ?? node.Attributes?.GetValueOrDefault("placeholder") ?? node.Text ?? "";

    /// <summary>
    /// One page-text line for a non-interactive node. A named node keeps its text when the two differ, as the runner's <c>formatNode</c> does: a
    /// status named "Greeting" whose text says "Welcome back, admin!" is the evidence a completion check needs, and the name alone hides it.
    /// </summary>
    private static string PageLine(ExecutorNode node)
    {
        var name = (node.Name ?? "").Trim();
        var text = Whitespace().Replace(node.Text ?? "", " ").Trim();
        if (name == "")
        {
            return text == "" ? (node.Attributes?.GetValueOrDefault("placeholder") ?? "").Trim() : text;
        }

        return text == "" || text == name ? name : name + " text=" + JsonText.Quote(text);
    }

    /// <summary>Whether the node's box meets the viewport. Nodes without a box stay in tree order.</summary>
    private static bool Intersects(ExecutorNode node, TraceViewport viewport)
    {
        if (node.Rect is not { } rect)
        {
            return false;
        }

        return rect.X < viewport.Width
            && rect.Y < viewport.Height
            && rect.X + rect.Width > 0
            && rect.Y + rect.Height > 0;
    }

    private static string Clip(string text, int limit) => text.Length > limit ? text[..limit] : text;

    /// <summary>
    /// Stable hash of the path, the measured keyboard state, the tree content with node ids removed, whether each node meets the viewport,
    /// and the pixels when the step sees them. Ids change on every capture, so they stay out. In-view membership lets a scroll that brings
    /// other nodes into view count as progress, while one that moves nothing (the page bottom) still reads as unchanged; raw coordinates stay
    /// out so small layout shifts do not count. Pixels are in because a drawn control changes nothing in the tree: a keypad digit entered on
    /// a canvas is progress only the screenshot shows. The keyboard state is in because a dismissal can leave the tree as it was, and an
    /// unmeasured state hashes apart from a measured <c>false</c>.
    /// </summary>
    private static string Fingerprint(SpaceObservation observation)
    {
        var parts = new List<object>
        {
            observation.Path ?? "",
            observation.KeyboardVisible is { } visible ? (visible ? "true" : "false") : "unmeasured",
        };
        if (observation.Pixels is { } pixels)
        {
            parts.Add(pixels.Data);
        }

        void Visit(ExecutorNode node)
        {
            parts.Add(node.Role ?? "");
            parts.Add(node.Name ?? "");
            parts.Add(node.Text ?? "");
            parts.Add(node.Value ?? "");
            parts.Add(JsonText.Stringify(node.States));
            parts.Add(JsonText.Stringify(node.Attributes));
            parts.Add(Intersects(node, observation.Viewport) ? "v" : "-");
            foreach (var child in node.Children)
            {
                Visit(child);
            }
        }

        foreach (var node in observation.Tree)
        {
            Visit(node);
        }

        return Hash.Fnv1a(parts);
    }

    private sealed record Row(ExecutorNode Node, List<Operation> Operations, bool InViewport, bool Passive);
}
