// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using E2E.Engine;
using E2E.Internal;
using static E2E.Agent;

namespace E2E;

/// <summary>
/// Upstream's <c>PlacedPoint</c> (<c>agent/actions.ts</c>): a bare viewport point and the listed node that contained it, if any, with the
/// container key (<paramref name="Within"/>) and twin <paramref name="Position"/> of that node. The recorder places the point inside the
/// node's box.
/// </summary>
internal sealed record PlacedPoint(Point Point, SemanticNode? Under = null, string? Within = null, TracePosition? Position = null);

/// <summary>
/// Upstream's <c>Placement</c>: where a node sat when it was acted on, for the trace. The container it sat in, and its place among the
/// controls its own description matched.
/// </summary>
internal readonly record struct Placement(string? Within, TracePosition? Position);

/// <summary>
/// Upstream's <c>agent/actions.ts</c>, the part that names the container a node sits in: the key a trace records with an action
/// (<c>within</c>) and a replay requires of the node it re-finds.
/// </summary>
internal static class AgentActions
{
    /// <summary>The name an event reports for an action string (upstream's <c>ActionName</c>); a project tool reads <see cref="ActionName.Tool"/>.</summary>
    public static ActionName NameOf(string action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (action.StartsWith(ActionNames.ProjectToolEventPrefix, StringComparison.Ordinal))
        {
            return ActionName.Tool;
        }

        return action switch
        {
            "tap" => ActionName.Tap,
            "doubleTap" => ActionName.DoubleTap,
            "longPress" => ActionName.LongPress,
            "secondaryTap" => ActionName.SecondaryTap,
            "hover" => ActionName.Hover,
            "scrollTo" => ActionName.ScrollTo,
            "fill" or "clear" => ActionName.Type,
            "press" => ActionName.Press,
            "select" => ActionName.Select,
            "check" or "uncheck" => ActionName.Check,
            "upload" => ActionName.Upload,
            "drag" => ActionName.Drag,
            "scroll" => ActionName.Scroll,
            "scrollUntil" => ActionName.ScrollUntil,
            "navigate" => ActionName.Navigate,
            "back" => ActionName.Back,
            "resize" => ActionName.Resize,
            "switchTab" => ActionName.SwitchTab,
            "closeTab" => ActionName.CloseTab,
            "tapAt" => ActionName.TapAt,
            "hoverAt" => ActionName.HoverAt,
            "dragAt" => ActionName.DragAt,
            "typeText" => ActionName.TypeText,
            "pressKey" => ActionName.PressKey,
            "dismissKeyboard" => ActionName.DismissKeyboard,
            _ => throw new ArgumentException("\"" + action + "\" is not an action an event reports.", nameof(action)),
        };
    }

    /// <summary>The project tool an action string names, or null.</summary>
    public static string? ToolNameOf(string action) =>
        action.StartsWith(ActionNames.ProjectToolEventPrefix, StringComparison.Ordinal) ? action[ActionNames.ProjectToolEventPrefix.Length..] : null;

    /// <summary>
    /// Upstream's <c>actionEvent</c>: the <c>onAction</c> event of one committed action. A point action carries its points; a secret fill
    /// reads <see cref="ActionName.TypeSecret"/>. Points are values, so a hook cannot move what the trace cache records.
    /// </summary>
    public static ActionEvent EventOf(RecordedAction action, int index, string summary, ActionSource source)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action.Kind switch
        {
            "tapAt" when action.Point is { } point => new TapAtActionEvent(index, action.Kind, summary, source, point),
            "hoverAt" when action.Point is { } point => new HoverAtActionEvent(index, action.Kind, summary, source, point),
            "dragAt" when action.From is { Point: { } from } && action.To is { Point: { } to } => new DragAtActionEvent(index, action.Kind, summary, source, from, to),
            "fill" when action.Value is { } value && value.StartsWith("<secret:", StringComparison.Ordinal) => new ActionEvent(index, action.Kind, summary, source) { ActionName = ActionName.TypeSecret },
            _ => new ActionEvent(index, action.Kind, summary, source),
        };
    }

    /// <summary>Roles whose first text names the thing a control belongs to: a row's key, a list item's title.</summary>
    private static readonly HashSet<string> ContainerRoles = new(["row", "listitem", "article", "group", "region", "dialog", "tabpanel"], StringComparer.Ordinal);

    /// <summary>Longest container key kept (upstream's <c>MAX_WITHIN_CHARS</c>).</summary>
    public const int MaxWithinChars = 80;

    /// <summary>
    /// Upstream's <c>containerKey</c>: the key of the nearest named container a node sits in, the first text of its row or list item
    /// or else the container's own label, when that key says more than the node's own name. Ten rows each with a "Delete" button are
    /// ten identical descriptors; "Delete in the row that starts with Budget draft" is one. Only the nearest container counts: the
    /// list around a row with no text names every row alike, so its key would tell no twin apart. A container whose first text
    /// belongs to a twin of the row the node came up through is such a list, and keys nothing by that text: a windowed list renders
    /// whichever rows are near the viewport, so its first row is a scroll position, not a place. Its own label still keys it. The
    /// text is read redacted, as the model reads it and the trace records it.
    /// </summary>
    public static string? ContainerKey(SemanticNode node, IReadOnlyDictionary<SemanticNode, SemanticNode> parents, Redactor redactor)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(parents);
        ArgumentNullException.ThrowIfNull(redactor);
        var own = Squash(Read(node.Name ?? node.Text, redactor));
        var came = node;
        while (parents.TryGetValue(came, out var container))
        {
            if (ContainerRoles.Contains(container.Role ?? ""))
            {
                var key = (NamedAfterTwinOf(container, came, redactor) ? null : FirstLeafText(container, redactor)) ?? OwnLabel(container, redactor);
                var clean = key is null ? "" : TextRules.Bound(TextRules.Collapse(key), MaxWithinChars);
                return clean.Length == 0 || Squash(clean) == own ? null : clean;
            }

            came = container;
        }

        return null;
    }

    /// <summary>Upstream's <c>parentsOf</c>: the parent of every non-root node of the tree.</summary>
    public static Dictionary<SemanticNode, SemanticNode> ParentsOf(IEnumerable<SemanticNode> roots)
    {
        var parents = new Dictionary<SemanticNode, SemanticNode>(ReferenceEqualityComparer.Instance);
        foreach (var node in LocatorResolver.Walk(roots))
        {
            foreach (var child in node.Children)
            {
                parents[child] = node;
            }
        }

        return parents;
    }

    // Whether the container's first text sits in a child that is a container of the same role as the branch, the child the keyed node
    // came up through, and not the branch itself: the container is then a list of such rows, named after whichever one comes first.
    private static bool NamedAfterTwinOf(SemanticNode container, SemanticNode branch, Redactor redactor)
    {
        if (!ContainerRoles.Contains(branch.Role ?? ""))
        {
            return false;
        }

        foreach (var child in container.Children)
        {
            var own = child.Children.Count == 0 ? (Read(child.Text ?? child.Name, redactor) ?? "").Trim() : "";
            if (own.Length == 0 && FirstLeafText(child, redactor) is null)
            {
                continue;
            }

            return !ReferenceEquals(child, branch) && child.Role == branch.Role;
        }

        return false;
    }

    // The first leaf's own text under a container, depth-first.
    private static string? FirstLeafText(SemanticNode node, Redactor redactor)
    {
        foreach (var child in node.Children)
        {
            var own = child.Children.Count == 0 ? (Read(child.Text ?? child.Name, redactor) ?? "").Trim() : "";
            if (own.Length > 0)
            {
                return own;
            }

            if (FirstLeafText(child, redactor) is { } deeper)
            {
                return deeper;
            }
        }

        return null;
    }

    // A container's own name or text, when it has one.
    private static string? OwnLabel(SemanticNode node, Redactor redactor)
    {
        var label = (Read(node.Name ?? node.OwnText ?? node.Text, redactor) ?? "").Trim();
        return label.Length == 0 ? null : label;
    }

    // Upstream redacts each node field once, when it builds the observation; the port keeps raw nodes, so the text is redacted here.
    private static string? Read(string? text, Redactor redactor) => RedactField(text, ObservationLimits.Text, redactor);

    private static string Squash(string? text) => TextRules.Normalize(text ?? "").ToLowerInvariant();

    /// <summary>Upstream's <c>NODE_ACTION_PROSE</c>: how each node action reads around the node it acted on.</summary>
    private static readonly Dictionary<string, string> NodeActionProse = new(StringComparer.Ordinal)
    {
        ["tap"] = "tap {0}",
        ["doubleTap"] = "double-tap {0}",
        ["longPress"] = "long-press {0}",
        ["secondaryTap"] = "secondary-tap {0}",
        ["hover"] = "hover over {0}",
        ["scrollTo"] = "scroll {0} into view",
    };

    /// <summary>
    /// Upstream's <c>describeAction</c> (the <c>summary</c> of its <c>DescribedAction</c>): one committed action as redacted, bounded prose,
    /// <c>tap button "Approve"</c> or <c>fill secret "password" into textbox "Password"</c>. A secret's value never appears, its name stands in.
    /// A typed or selected value, a key, a location, and an uploaded path pass through <paramref name="redactor"/>. The target is the descriptor
    /// the action holds flat; <paramref name="target"/> and <paramref name="destination"/> stand in for the ones a point action sat in, which the
    /// recorder reads off the listed node before it drops an anonymous one.
    /// </summary>
    public static string DescribeAction(RecordedAction action, Redactor redactor, RecordedTarget? target = null, RecordedTarget? destination = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(redactor);
        switch (action.Kind)
        {
            case "tapAt" or "hoverAt":
                target ??= action.Under?.Target;
                break;
            case "dragAt":
                target ??= action.From?.Under?.Target;
                destination ??= action.To?.Under?.Target;
                break;
            default:
                // The Text of a scrollUntil and a tool gap is the text paged toward and the tool's name, not the target's text.
                var descriptor = E2E.Cache.TraceRecorder.TargetOf(action);
                if (action.Kind is "scrollUntil" or "tool")
                {
                    descriptor.Text = null;
                }

                target ??= descriptor;
                destination ??= action.Destination;
                break;
        }

        target = Described(target);
        destination = Described(destination);
        var where = DescribeForSummary(target);
        string Safe(string value) => QuoteText(redactor.Redact(TextRules.SanitizeText(value)));
        var prose = action.Kind switch
        {
            "tap" or "doubleTap" or "longPress" or "secondaryTap" or "hover" or "scrollTo" =>
                string.Format(CultureInfo.InvariantCulture, NodeActionProse[action.Kind], where) + (action.Modifiers is { Count: > 0 } held ? " with " + string.Join('+', held) + " held" : ""),
            "fill" when Trace.IsSecretMarker(action) => "fill secret " + QuoteText(action.Value![8..^1]) + " into " + where,
            "fill" => "type " + Safe(action.Value ?? "") + " into " + where,
            "clear" => "type " + Safe("") + " into " + where,
            "press" => "press " + Safe(action.Key ?? "") + " on " + where,
            "select" => "select " + Safe(action.Value ?? "") + " in " + where,
            "check" => "check " + where,
            "uncheck" => "uncheck " + where,
            "upload" => "upload " + string.Join(", ", (action.Paths ?? []).Select(Safe)) + " to " + where,
            "drag" => "drag " + where + " to " + DescribeForSummary(destination),
            "scroll" => target is null ? "scroll " + action.Direction : "scroll " + action.Direction + " on " + where,
            "scrollUntil" => "scroll " + action.Direction + (target is null ? "" : " on " + where) + " until " + Safe(action.Text ?? "") + " shows" + PagesTaken(action.Screens),
            "navigate" => "navigate to " + Safe(action.Url ?? ""),
            "back" => "navigate back",
            "resize" => "resize the viewport to " + Number(action.Size?.Width ?? 0) + "x" + Number(action.Size?.Height ?? 0),
            "switchTab" => "switch to tab " + Number((action.Tab ?? 0) + 1),
            "closeTab" => "close tab " + Number((action.Tab ?? 0) + 1),
            "typeText" => "type " + Safe(action.Value ?? "") + " into the focused field" + (action.Replace == true ? ", replacing its value" : ""),
            "pressKey" => "press " + Safe(action.Key ?? "") + " on the focused field",
            "dismissKeyboard" => "dismiss the keyboard",
            "tapAt" or "hoverAt" => (action.Kind == "tapAt" ? "tap" : "hover over") + " the point " + PointText(action.Point) + (target is null ? "" : " on " + where),
            "dragAt" => "drag from the point " + PointText(action.From?.Point) + (target is null ? "" : " on " + where) + " to the point " + PointText(action.To?.Point) + (destination is null ? "" : " on " + DescribeForSummary(destination)),
            "tool" => "tool " + redactor.Redact(action.Text ?? ""),
            _ => action.Kind,
        };
        return TextRules.Bound(prose, Trace.MaxTraceSummaryChars);
    }

    // A descriptor that names nothing is no target (upstream's describeTarget returns undefined for it).
    private static RecordedTarget? Described(RecordedTarget? target) =>
        target is { } named && (named.Role is not null || named.Name is not null || named.Text is not null || named.TestId is not null || named.Placeholder is not null || named.InputPurpose is not null)
            ? named
            : null;

    // Upstream's describeForSummary: the target's label, the container it sat in, then its place among its twins, (2 of 3).
    private static string DescribeForSummary(RecordedTarget? target)
    {
        if (target is null)
        {
            return "the screen";
        }

        var label = RecordedTarget.TargetLabel(target);
        var placed = target.Within is null ? label : label + " in " + QuoteText(target.Within);
        return target.Position is null ? placed : placed + " (" + Number(target.Position.Index + 1) + " of " + Number(target.Position.Of) + ")";
    }

    // Upstream's quote: a value bounded to 40 characters, as JSON text.
    private static string QuoteText(string value) => UploadPaths.Quote(TextRules.Bound(value, 40));

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string PointText(Point? point) => point is { } at ? "(" + Number(at.X) + ", " + Number(at.Y) + ")" : "(0, 0)";

    private static string PagesTaken(int? screens) =>
        screens is not { } pages ? "" : " (" + (pages == 0 ? "already there" : Number(pages) + (pages == 1 ? " screen" : " screens")) + ")";
}
