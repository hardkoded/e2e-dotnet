// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;
using static E2E.Agent;

namespace E2E;

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
            "dragAt" when action.From is { } from && action.To is { } to => new DragAtActionEvent(index, action.Kind, summary, source, from.Point, to.Point),
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
}
