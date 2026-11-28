// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.RegularExpressions;
using E2E.Engine;

namespace E2E.Desktop;

/// <summary>What an action needs to know about a node, kept beside the node the harness sees. Upstream's <c>NodeBinding</c>.</summary>
public sealed record NodeBinding
{
    public required string Id { get; init; }

    /// <summary>The driver's snapshot-qualified ref (<c>@s8f3k2p9:e3</c>); null for a node it gives none, such as static text.</summary>
    public string? Ref { get; init; }

    public string? Role { get; init; }

    public string? Name { get; init; }

    public required NodeStates States { get; init; }

    /// <summary>The actions the driver says the element takes (<c>Click</c>, <c>SetValue</c>, <c>Expand</c>, ...).</summary>
    public required IReadOnlyList<string> Actions { get; init; }

    /// <summary>The window the snapshot read the node in.</summary>
    public string? WindowId { get; init; }

    /// <summary>The node's box in the window, which finds it again where its id cannot (a look-alike's).</summary>
    public Rect? Rect { get; init; }

    /// <summary>
    /// Whether the id comes from the node's identity and every ancestor's, and so names it in the next snapshot too; a look-alike's, or one inside
    /// a look-alike, is new in every snapshot.
    /// </summary>
    public bool Stable { get; init; }

    /// <summary>
    /// How many look-alikes the node and each of its ancestors have among their siblings, root first (<c>/1/2/1</c>): a look-alike found again by
    /// its role and place is the same one only if no twin around it came or went.
    /// </summary>
    public required string Likeness { get; init; }

    /// <summary>The developer id (<c>AXIdentifier</c>), which a look-alike found again by its place must still carry.</summary>
    public string? TestId { get; init; }

    /// <summary>Read with <c>-i</c>, whose ids and look-alike counts a check of the same control must read the same way to compare.</summary>
    public bool Interactive { get; init; }

    /// <summary>
    /// Hidden only because a scroll area holds it out of view, which a scroll into view can undo; one in a minimized window, under a hidden
    /// ancestor, or with no size stays hidden whatever scrolls.
    /// </summary>
    public bool ScrolledOut { get; init; }
}

/// <summary>The first scrollable node along each axis, which the observation root's swipe in that direction scrolls.</summary>
public sealed record ScrollTargets(string? Vertical, string? Horizontal);

/// <summary>A snapshot as the engine keeps it: the tree the harness sees, and a binding for every node in it. Upstream's <c>ProjectedScreen</c>.</summary>
public sealed record ProjectedScreen
{
    public required SemanticNode Root { get; init; }

    public required IReadOnlyDictionary<string, NodeBinding> Bindings { get; init; }

    /// <summary>The ids of <see cref="Bindings"/> in the order the nodes were projected, which a dictionary does not keep (.NET-only: a JavaScript Map does).</summary>
    public required IReadOnlyList<string> BindingOrder { get; init; }

    /// <summary>The window, in screen points: the viewport every node's rect is measured from.</summary>
    public required Rect Window { get; init; }

    public required double ViewportWidth { get; init; }

    public required double ViewportHeight { get; init; }

    /// <summary>Rects of the secure fields, in the viewport's points, for masking. Null when a secure field has no bounds to mask.</summary>
    public IReadOnlyList<Rect>? SecureRects { get; init; }

    public bool Truncated { get; init; }

    /// <summary>
    /// The read was cut short by its deadline, a stall, or the node budget (<c>complete: false</c>, <c>truncated</c>), which the next read may not be; a
    /// tree cut only at its depth limit is cut the same way every time.
    /// </summary>
    public bool CutShort { get; init; }

    /// <summary>The deadline stopped the read: the caller's time ran out before the window was read whole.</summary>
    public bool TimedOut { get; init; }

    /// <summary>The app did not answer for the window itself (<c>root_unread</c>).</summary>
    public bool RootUnread { get; init; }

    public string? WindowId { get; init; }

    /// <summary>Set by the surface: the sheet on the window that held its focus when the window was read (see <c>SheetKey</c>).</summary>
    public string? Sheet { get; init; }

    public string? Title { get; init; }

    public ScrollTargets? ScrollTargets { get; init; }
}

/// <summary>What <see cref="DesktopNodes.ProjectSnapshot"/> takes beside the snapshot. Upstream's options bag of <c>projectSnapshot</c>.</summary>
public sealed record ProjectionOptions
{
    /// <summary>Every node of a minimized window is hidden: accessibility still reads its controls, but nothing of it is on screen.</summary>
    public bool Minimized { get; init; }

    /// <summary>Marks the bindings of a read the driver pruned, whose tree, and so whose ids and look-alike counts, differ from a whole read's.</summary>
    public bool Interactive { get; init; }

    /// <summary>Hears each identity a look-alike sibling made shared, for a caller that must retire it.</summary>
    public Action<string>? OnLookAlike { get; init; }

    /// <summary>Looks up the id an identity held before, without handing one out.</summary>
    public Func<string, string?>? PriorIdOf { get; init; }
}

/// <summary>
/// The native driver's snapshot tree projected onto the contract's semantic nodes. The driver speaks one closed role and state vocabulary (the one
/// agent-desktop defined, which the engine was first built on); this maps it onto the ARIA names <c>screen.getByRole</c> and the agent use on every
/// other engine, so <c>getByRole('textbox')</c> means the same field in a browser, on a phone, and in a desktop app. .NET-only holder name
/// (C# has no free functions): upstream's <c>projectSnapshot</c> and its tables in <c>nodes.ts</c>.
/// </summary>
public static partial class DesktopNodes
{
    /// <summary>The observation root's id: one window, the same id across observations, as the contract requires. Upstream's <c>ROOT_ID</c>.</summary>
    public const string RootId = "root";

    /// <summary>The longest accessible name a node carries as its <c>label</c> attribute, far past any control's.</summary>
    private const int MaxLabelChars = 1_000;

    /// <summary>
    /// ARIA's name for each driver role that has one. A role missing here keeps the driver's own token: it is still one closed vocabulary, just not
    /// one a browser shares. Upstream's <c>ROLE_MAP</c>.
    /// </summary>
    private static readonly Dictionary<string, string> RoleMap = new(StringComparer.Ordinal)
    {
        ["alert"] = "alert",
        ["alertdialog"] = "alertdialog",
        ["application"] = "application",
        ["article"] = "article",
        ["banner"] = "banner",
        ["button"] = "button",
        ["cell"] = "cell",
        ["checkbox"] = "checkbox",
        ["column"] = "columnheader",
        ["combobox"] = "combobox",
        ["complementary"] = "complementary",
        ["contentinfo"] = "contentinfo",
        ["definition"] = "definition",
        ["dialog"] = "dialog",
        ["disclosure"] = "button",
        ["document"] = "document",
        ["form"] = "form",
        ["grid"] = "grid",
        ["group"] = "group",
        ["heading"] = "heading",
        ["image"] = "image",
        ["incrementor"] = "spinbutton",
        ["link"] = "link",
        ["list"] = "list",
        ["listbox"] = "listbox",
        ["log"] = "log",
        ["main"] = "main",
        ["marquee"] = "marquee",
        ["math"] = "math",
        ["menu"] = "menu",
        ["menubutton"] = "button",
        ["menuitem"] = "menuitem",
        ["navigation"] = "navigation",
        ["note"] = "note",
        ["option"] = "option",
        ["outline"] = "tree",
        ["paragraph"] = "paragraph",
        ["popover"] = "dialog",
        ["progressbar"] = "progressbar",
        ["radiobutton"] = "radio",
        ["radiogroup"] = "radiogroup",
        ["region"] = "region",
        ["row"] = "row",
        ["scrollbar"] = "scrollbar",
        ["search"] = "search",
        ["separator"] = "separator",
        ["sheet"] = "dialog",
        ["slider"] = "slider",
        ["statictext"] = "text",
        ["status"] = "status",
        ["switch"] = "switch",
        ["tab"] = "tab",
        ["tablist"] = "tablist",
        ["tabpanel"] = "tabpanel",
        ["table"] = "table",
        ["term"] = "term",
        ["textfield"] = "textbox",
        ["timer"] = "timer",
        ["toolbar"] = "toolbar",
        ["tooltip"] = "tooltip",
        ["treeitem"] = "treeitem",
        ["webarea"] = "document",
        ["window"] = "window",
    };

    /// <summary>Roles whose checked state is a fact whenever it is not stated: an unchecked box reports no <c>checked</c>. Upstream's <c>CHECKABLE_ROLES</c>.</summary>
    private static readonly HashSet<string> CheckableRoles = ["checkbox", "switch", "radiobutton"];

    /// <summary>Roles whose name labels what they hold, so it is never their text. Upstream's <c>INPUT_ROLES</c>.</summary>
    private static readonly HashSet<string> InputRoles =
    [
        "textfield",
        "combobox",
        "slider",
        "incrementor",
        "datefield",
        "timefield",
        "colorwell",
    ];

    /// <summary>Roles the driver reads text into <c>value</c>, which the contract calls <c>text</c>. Upstream's <c>TEXT_ROLES</c>.</summary>
    private static readonly HashSet<string> TextRoles = ["statictext", "heading"];

    private enum Hiding
    {
        None,
        Scrolled,
        Other,
    }

    /// <summary>
    /// A label: a whitespace-only one is no label. Nothing is cut here: <c>locate</c> answers single-node reads, which carry the whole value, and
    /// the harness cuts what it shows the agent.
    /// </summary>
    private static string? Label(string? value) => value is null || value.Trim().Length == 0 ? null : value;

    /// <summary>A node the driver reports but nothing reads: a wrapper with no name, value, id, ref, or text of its own.</summary>
    private static bool Transparent(RawNode raw)
    {
        // A secure node is never dropped: its bounds are what a screenshot masks.
        if (raw.RefId is not null || raw.NativeId is not null || HasState(raw, "secure"))
        {
            return false;
        }

        if (Label(raw.Name) is not null || Label(raw.Value) is not null)
        {
            return false;
        }

        return raw.Role is "group" or "unknown" or "layoutitem" or "matte" or "splitter";
    }

    private static bool HasState(RawNode raw, string state) => raw.States?.Contains(state) == true;

    /// <summary>
    /// What makes a node the same node in the next snapshot, among its parent's children: its role and its developer id, else its role and name
    /// (or, for static text, its text). A field's value is not part of it, so a field keeps its id while it is typed into, and a status with a
    /// developer id keeps its id while its text changes. <paramref name="named"/> adds the name to the developer id, for siblings a toolkit gives
    /// one id (AppKit menu items share <c>menuItemSelected:</c>). Upstream's <c>identityOf</c>.
    /// </summary>
    private static string IdentityOf(RawNode node, bool named = false)
    {
        var name = node.Name ?? (TextRoles.Contains(node.Role) ? node.Value : null) ?? "";
        var id = node.NativeId?.Value;
        // A tagged tuple, so no id or name, whatever characters it holds, can spell another kind's key.
        return id is null
            ? JsonSerializer.Serialize(new[] { "name", node.Role, name })
            : JsonSerializer.Serialize(named ? new[] { "id+name", node.Role, id, name } : ["id", node.Role, id]);
    }

    /// <summary>
    /// Projects one snapshot. <paramref name="idFor"/> turns a node's identity (its parent's id, and its own role and developer id or name) into the id
    /// the harness sees: the caller hands out the same id for the same identity across observations, so a control keeps its id for as long as it
    /// exists, as the contract asks. A node with a look-alike sibling has no identity (null) and gets a new id, which no later snapshot gives out
    /// again. The window is always <c>root</c>. Wrappers nothing reads (anonymous groups) are dropped and their children lifted, so the model reads
    /// the controls, not the scaffolding (the driver leaves the window's title-bar buttons out). Every node of a minimized window is hidden:
    /// accessibility still reads its controls, but nothing of it is on screen. Upstream's <c>projectSnapshot</c>.
    /// </summary>
    public static ProjectedScreen ProjectSnapshot(
        RawSnapshot raw,
        Func<string?, string> idFor,
        ProjectionOptions? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(idFor);
        options ??= new ProjectionOptions();
        var window = raw.Tree.Bounds ?? new Rect(0, 0, 0, 0);
        var bindings = new Dictionary<string, NodeBinding>(StringComparer.Ordinal);
        var bindingOrder = new List<string>();
        var secureRects = new List<Rect>();
        var unmaskable = false;
        string? scrollVertical = null;
        string? scrollHorizontal = null;

        // `hiddenAbove`: an ancestor is hidden, a wrapper left out included, so nothing under it is on screen; the resolver reads each node's own
        // state only. `Scrolled` when a scroll area is all that hides it, which a scroll into view of the node can undo, else `Other`.
        // `prior`: the id the node held before, which a cut gave it a fresh one in place of; look-alikes under it are reported against that id,
        // which their identities are kept under.
        SemanticNode Project(RawNode node, string id, bool stable, string likeness, Hiding hiddenAbove, string? prior)
        {
            var offscreen = HasState(node, "offscreen");
            var role =
                RoleMap.TryGetValue(node.Role, out var mapped) ? mapped
                : node.Role == "unknown" ? null
                : node.Role;
            var secure = HasState(node, "secure");
            var hidden = HasState(node, "hidden") || offscreen || options.Minimized || hiddenAbove != Hiding.None;
            // A control collapsed to no width or height draws nothing, and the driver will not act on it. Only a leaf: a container with no box can still
            // hold drawn children, listed or cut off at the depth limit.
            var collapsed =
                id != RootId
                && node.Bounds is { } box
                && (box.Width <= 0 || box.Height <= 0)
                && (node.Children ?? []).Count == 0
                && (node.ChildrenCount ?? 0) == 0;
            if (collapsed)
            {
                hidden = true;
            }

            var scrolledOut =
                (offscreen || hiddenAbove == Hiding.Scrolled)
                && !options.Minimized
                && hiddenAbove != Hiding.Other
                && !collapsed;
            // A mixed checkbox is neither checked nor unchecked: no state, so check() and uncheck() always reach the control.
            bool? isChecked = null;
            if (CheckableRoles.Contains(node.Role) && !HasState(node, "indeterminate"))
            {
                isChecked = HasState(node, "checked") || node.Value == "1";
            }
            else if (HasState(node, "checked"))
            {
                isChecked = true;
            }

            var expanded = false;
            if (
                (node.Role is "combobox" or "disclosure" or "menubutton")
                && (
                    node.AvailableActions?.Contains("Expand") == true
                    || node.AvailableActions?.Contains("Collapse") == true
                )
            )
            {
                expanded = HasState(node, "expanded");
            }
            else if (HasState(node, "expanded"))
            {
                expanded = true;
            }

            // The driver marks a text field whose value cannot be set `readonly`, and offers SetValue on one whose value can. A combo box takes text
            // only when its value can be set; a pop-up menu takes none, which is not read-only, so it gets no state, as on a device.
            var settable = node.AvailableActions?.Contains("SetValue") == true;
            bool? readOnly = null;
            if (node.Role == "textfield" && (settable || HasState(node, "readonly")))
            {
                readOnly = !settable;
            }
            else if (node.Role == "combobox" && settable)
            {
                readOnly = false;
            }

            var isText = TextRoles.Contains(node.Role);
            var name = Label(node.Name ?? (isText ? node.Value : null));
            // A control shows its name (a button's title) as its text, as on a device; an input's name is a label, not what it holds.
            var text =
                isText ? Label(node.Value ?? node.Name)
                : InputRoles.Contains(node.Role) ? null
                : name;
            // A checkbox's value is its state ("0", "1"), already in `checked`; a secure field's is never reported. A snapshot may leave out the
            // value of an empty text field; it is the empty string, as inputValue() reads it, so getByDisplayValue('') finds the field.
            var value =
                secure || isText || CheckableRoles.Contains(node.Role) ? null
                : node.Role == "textfield" ? node.Value ?? ""
                : node.Value;
            BoundingBox? rect = node.Bounds is { } bounds
                ? new BoundingBox(bounds.X - window.X, bounds.Y - window.Y, bounds.Width, bounds.Height)
                : null;
            if (secure)
            {
                // A box with no area masks nothing, so it proves nothing about a field that may still show.
                if (rect is null || rect.Width <= 0 || rect.Height <= 0)
                {
                    unmaskable = true;
                }
                else
                {
                    secureRects.Add(new Rect(rect.X, rect.Y, rect.Width, rect.Height));
                }
            }

            var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
            // Under the names a device node uses: the accessible name, and the developer's identifier, which is also the test id. A text's name is
            // its text, which textContent reads, and attributes are not cut as name and text are: a paragraph would ride along whole in every
            // observation, so text, and a name longer than any control's, get no label.
            if (name is not null && !secure && !isText && name.Length <= MaxLabelChars)
            {
                attributes["label"] = name;
            }

            if (node.NativeId is not null)
            {
                attributes["identifier"] = node.NativeId.Value;
            }

            var placeholder = Label(node.Placeholder);
            if (placeholder is not null)
            {
                attributes["placeholder"] = placeholder;
            }

            if (Label(node.Hint) is { } hint)
            {
                attributes["hint"] = hint;
            }

            if (Label(node.Description) is { } description)
            {
                attributes["description"] = description;
            }

            if (node.NativeId is not null)
            {
                attributes["native-id-kind"] = node.NativeId.Kind;
            }

            // A swipe on the window scrolls the first node the driver offers Scroll along the swipe's axis, which it scrolls only then and only while
            // visible and enabled: a list with no scroll mechanism, a pane off screen, or a carousel for a swipe down would refuse it.
            var disabled = HasState(node, "disabled");
            if (node.RefId is not null && node.AvailableActions?.Contains("Scroll") == true && !hidden && !disabled)
            {
                IReadOnlyList<string> axes = node.ScrollAxes ?? ["vertical", "horizontal"];
                if (scrollVertical is null && axes.Contains("vertical"))
                {
                    scrollVertical = id;
                }

                if (scrollHorizontal is null && axes.Contains("horizontal"))
                {
                    scrollHorizontal = id;
                }
            }

            var states = new NodeStates
            {
                Focused = HasState(node, "focused"),
                Disabled = disabled,
                Secure = secure,
                Expanded = expanded,
                Selected = HasState(node, "selected"),
                Hidden = hidden,
                Pressed = HasState(node, "pressed"),
                Readonly = readOnly,
            };
            if (isChecked is { } on)
            {
                states = new NodeStates
                {
                    Focused = states.Focused,
                    Disabled = states.Disabled,
                    Secure = states.Secure,
                    Expanded = states.Expanded,
                    Selected = states.Selected,
                    Hidden = states.Hidden,
                    Pressed = states.Pressed,
                    Readonly = states.Readonly,
                    Checked = on,
                };
            }

            var testId = node.NativeId is null || node.NativeId.Value == "" ? null : node.NativeId.Value;
            var binding = new NodeBinding
            {
                Id = id,
                Ref = node.RefId,
                Role = role,
                Name = name,
                States = states,
                Actions = node.AvailableActions ?? [],
                WindowId = raw.Window?.Id,
                Rect = node.Bounds is null ? null : new Rect(rect!.X, rect.Y, rect.Width, rect.Height),
                Stable = stable,
                Likeness = likeness,
                Interactive = options.Interactive,
                ScrolledOut = scrolledOut,
                TestId = testId,
            };
            bindings[id] = binding;
            bindingOrder.Add(id);

            var kept = new List<RawNode>();
            var underHidden = new Dictionary<RawNode, Hiding>(ReferenceEqualityComparer.Instance);
            // Whether a wrapper left out had children the read cut off: its unread children are this node's, and one could be a twin of a listed one.
            var wrapperCut = false;
            void Collect(RawNode child, Hiding throughHidden)
            {
                if (Transparent(child))
                {
                    if (ChildrenCut(child))
                    {
                        wrapperCut = true;
                    }

                    var hiddenWrapper =
                        throughHidden == Hiding.Other || HasState(child, "hidden") ? Hiding.Other
                        : throughHidden == Hiding.Scrolled || HasState(child, "offscreen") ? Hiding.Scrolled
                        : Hiding.None;
                    foreach (var grandchild in child.Children ?? [])
                    {
                        Collect(grandchild, hiddenWrapper);
                    }
                }
                else
                {
                    kept.Add(child);
                    if (throughHidden != Hiding.None)
                    {
                        underHidden[child] = throughHidden;
                    }
                }
            }

            foreach (var child in node.Children ?? [])
            {
                Collect(child, Hiding.None);
            }

            Dictionary<string, int> Tally(bool named)
            {
                var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var child in kept)
                {
                    var key = IdentityOf(child, named);
                    counts[key] = counts.GetValueOrDefault(key) + 1;
                }

                return counts;
            }

            var plain = Tally(false);
            var namedTally = Tally(true);
            // Siblings sharing a developer id tell themselves apart by name. A look-alike that still matches has no identity: if the first of two
            // "Delete" buttons goes, the second must not take over its id, so each look-alike gets a new id every snapshot. A node whose children
            // the read cut off lists only some of them: one that looks unique may have a twin the read did not reach, so none takes an id.
            var cut = ChildrenCut(node) || wrapperCut;
            var children = new List<SemanticNode>();
            foreach (var child in kept)
            {
                var plainKey = IdentityOf(child);
                var namedKey = IdentityOf(child, true);
                var seen =
                    plain.GetValueOrDefault(plainKey) == 1 ? plainKey
                    : namedTally.GetValueOrDefault(namedKey) == 1 ? namedKey
                    : null;
                var identity = cut ? null : seen;
                // An identity two siblings share may pass on to neither, even where their names tell them apart now.
                if (prior is not null && plain.GetValueOrDefault(plainKey) > 1)
                {
                    options.OnLookAlike?.Invoke($"{prior}/{plainKey}");
                }

                if (prior is not null && namedTally.GetValueOrDefault(namedKey) > 1)
                {
                    options.OnLookAlike?.Invoke($"{prior}/{namedKey}");
                }

                // A child's id is built on its parent's: under a look-alike, whose id is new every snapshot, so is the child's, and no identity is
                // kept for it.
                var childStable = stable && identity is not null;
                var childId = idFor(childStable ? $"{id}/{identity}" : null);
                var childPrior =
                    childStable ? childId
                    : prior is not null && seen is not null ? options.PriorIdOf?.Invoke($"{prior}/{seen}")
                    : null;
                children.Add(
                    Project(
                        child,
                        childId,
                        childStable,
                        $"{likeness}/{(plain.TryGetValue(plainKey, out var count) ? count : 1)}",
                        hidden ? (scrolledOut ? Hiding.Scrolled : Hiding.Other) : underHidden.GetValueOrDefault(child),
                        childPrior
                    )
                );
            }

            return new SemanticNode
            {
                Ref = id,
                Role = role,
                Name = name,
                Text = text,
                Value = value,
                TestId = testId,
                InputPurpose = secure ? "password" : null,
                States = states,
                Level = node.Role == "heading" ? HeadingLevel(node) : null,
                Attributes = attributes,
                Placeholder = placeholder,
                Rect = rect,
                Children = children,
            };
        }

        var root = Project(raw.Tree, RootId, true, "", Hiding.None, RootId);
        var title = Label(raw.Tree.Name) ?? Label(raw.Window?.Title);
        return new ProjectedScreen
        {
            Root = root,
            Bindings = bindings,
            BindingOrder = bindingOrder,
            Window = window,
            ViewportWidth = window.Width,
            ViewportHeight = window.Height,
            SecureRects = unmaskable ? null : secureRects,
            Truncated = raw.Complete == false || raw.Truncated == true || HasTruncatedSubtree(raw.Tree),
            CutShort = raw.Complete == false || raw.Truncated == true,
            TimedOut = raw.TimedOut == true,
            RootUnread = raw.RootUnread == true,
            WindowId = raw.Window?.Id,
            // The window's accessibility root carries its title; the window server's is the app's name without Screen Recording permission.
            Title = title,
            ScrollTargets =
                scrollVertical is null && scrollHorizontal is null
                    ? null
                    : new ScrollTargets(scrollVertical, scrollHorizontal),
        };
    }

    /// <summary>A heading's level: the one the app reports, else one its description names (<c>heading level 2</c>).</summary>
    private static int? HeadingLevel(RawNode node)
    {
        if (node.Level is { } level)
        {
            return level;
        }

        var match = HeadingLevelPattern().Match(node.Description ?? "");
        return match.Success
            ? int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)
            : null;
    }

    /// <summary>Whether the read listed only some of a node's children: the driver flags the node, or counts more children than it listed at the depth limit.</summary>
    private static bool ChildrenCut(RawNode node) =>
        node.SubtreeTruncated == true || (node.ChildrenCount is { } count && count > (node.Children?.Count ?? 0));

    private static bool HasTruncatedSubtree(RawNode node) =>
        ChildrenCut(node) || (node.Children ?? []).Any(HasTruncatedSubtree);

    [GeneratedRegex(@"\blevel (\d)\b", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HeadingLevelPattern();
}
