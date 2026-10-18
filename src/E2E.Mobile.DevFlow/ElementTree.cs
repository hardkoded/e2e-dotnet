// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Mobile.DevFlow;

/// <summary>One element of the newest tree, as far as an action needs it.</summary>
internal sealed record ScreenElement(string Id, string Kind, string? Role, bool Focused, bool Editable, Rect? Rect);

/// <summary>
/// The newest visual tree, read into the snapshot <c>capture.snapshot</c> answers, and the element ids behind its refs.
/// A ref names an element of one read and nothing else, so an action by ref finds the element id here, and a ref of an
/// older read is stale: the screen it named may be gone. The agent checks the same with the capture epoch every action
/// carries, and answers 409 when the tree changed under it.
/// </summary>
internal sealed class DevFlowScreen
{
    private readonly Dictionary<string, ScreenElement> _byId;

    internal DevFlowScreen(JsonObject snapshot, long? captureEpoch, (double Width, double Height)? viewport, Dictionary<string, ScreenElement> byId)
    {
        Snapshot = snapshot;
        CaptureEpoch = captureEpoch;
        Viewport = viewport;
        _byId = byId;
    }

    public static DevFlowScreen Empty { get; } = new(new JsonObject { ["nodes"] = new JsonArray() }, null, null, []);

    /// <summary>The snapshot the engine reads: a flat node list with tree coordinates, the viewport, and the app's identity.</summary>
    public JsonObject Snapshot { get; }

    /// <summary>The epoch the agent stamped on this capture. Sent back with every action, so the agent refuses a stale target.</summary>
    public long? CaptureEpoch { get; }

    /// <summary>The window's logical size, when the tree had a window with bounds.</summary>
    public (double Width, double Height)? Viewport { get; }

    public IReadOnlyCollection<ScreenElement> Elements => _byId.Values;

    /// <summary>The element a ref names. A ref the newest tree does not have is stale.</summary>
    public ScreenElement ElementOf(string reference)
    {
        var key = reference.TrimStart('@');
        return _byId.TryGetValue(key, out var element) ? element : throw DevFlowErrors.StaleReference(reference);
    }

    /// <summary>The editable element that has the focus, where typed text lands. Null when none has it.</summary>
    public ScreenElement? FocusedInput() => _byId.Values.FirstOrDefault(element => element.Focused && element.Editable);
}

/// <summary>
/// Turns the agent's visual tree (<c>GET /api/v1/ui/tree</c>, an array of <c>ElementInfo</c> or an envelope with one)
/// into the agent-device snapshot shape the engine projects: a flat list with <c>index</c>, <c>parentIndex</c>, and
/// <c>depth</c>, platform element types in agent-device's vocabulary so <c>GetByRole</c> answers the same as on a
/// browser, and the identifier <c>GetByTestId</c> reads from the MAUI <c>AutomationId</c>.
/// </summary>
internal static class ElementTree
{
    /// <summary>What the agent writes in place of a password field's text, so the plaintext never leaves the app.</summary>
    public const string Redacted = "[REDACTED]";

    /// <summary>MAUI control types onto the agent-device kinds <c>SnapshotProjection</c> maps to roles. Anything else keeps its type in kebab case.</summary>
    private static readonly Dictionary<string, string> Kinds = new(StringComparer.Ordinal)
    {
        ["Window"] = "window",
        ["Button"] = "button",
        ["ImageButton"] = "button",
        ["Label"] = "static-text",
        ["Span"] = "static-text",
        ["Entry"] = "text-field",
        ["Editor"] = "text-view",
        ["SearchBar"] = "search-field",
        ["Switch"] = "switch",
        ["CheckBox"] = "checkbox",
        ["RadioButton"] = "radio-button",
        ["Slider"] = "slider",
        ["Stepper"] = "stepper",
        ["ProgressBar"] = "progress-indicator",
        ["ActivityIndicator"] = "activity-indicator",
        ["Image"] = "image",
        ["ScrollView"] = "scroll-view",
        ["CollectionView"] = "collection-view",
        ["CarouselView"] = "collection-view",
        ["ListView"] = "table",
        ["ViewCell"] = "cell",
        ["TextCell"] = "cell",
        ["ImageCell"] = "cell",
        ["Picker"] = "picker",
        ["DatePicker"] = "date-picker",
        ["TimePicker"] = "time-picker",
        ["WebView"] = "web-view",
        ["TabBar"] = "tab-bar",
        ["Tab"] = "tab",
        ["MenuItem"] = "menu-item",
        ["ToolbarItem"] = "button",
    };

    /// <summary>The agent's roles that name a kind more precisely than the type does: a tappable label is a link.</summary>
    private static readonly Dictionary<string, string> RoleKinds = new(StringComparer.Ordinal)
    {
        ["link"] = "link",
        ["heading"] = "heading",
        ["tablist"] = "tab-bar",
        ["tab"] = "tab",
        ["listitem"] = "cell",
        ["alert"] = "alert",
        ["dialog"] = "dialog",
        ["menu"] = "menu",
        ["menuitem"] = "menu-item",
        ["toolbar"] = "toolbar",
    };

    private static readonly HashSet<string> TextInputTypes = new(["Entry", "Editor", "SearchBar"], StringComparer.Ordinal);

    private static readonly HashSet<string> ToggleTypes = new(["Switch", "CheckBox", "RadioButton"], StringComparer.Ordinal);

    /// <summary>
    /// Reads one tree answer. <paramref name="interactiveOnly"/> keeps the actionable, labelled, and identified elements
    /// and re-parents the rest's children to the nearest kept ancestor, as agent-device's interactive snapshot does.
    /// </summary>
    public static DevFlowScreen Read(JsonNode? response, bool interactiveOnly, string? appName, string? appBundleId)
    {
        var roots = response switch
        {
            JsonArray array => array,
            JsonObject envelope when envelope["elements"] is JsonArray elements => elements,
            _ => [],
        };

        var nodes = new JsonArray();
        var byId = new Dictionary<string, ScreenElement>(StringComparer.Ordinal);
        long? epoch = null;
        (double, double)? viewport = null;
        foreach (var root in roots.OfType<JsonObject>())
        {
            viewport ??= RectOf(root) is { } bounds && bounds.Width > 0 && bounds.Height > 0 ? (bounds.Width, bounds.Height) : null;
            Flatten(root, parentIndex: null, depth: 0, interactiveOnly, nodes, byId, ref epoch);
        }

        var snapshot = new JsonObject { ["nodes"] = nodes, ["truncated"] = false };
        if (viewport is { } size)
        {
            snapshot["viewport"] = new JsonObject { ["width"] = size.Item1, ["height"] = size.Item2 };
        }

        if (appName is not null)
        {
            snapshot["appName"] = appName;
        }

        if (appBundleId is not null)
        {
            snapshot["appBundleId"] = appBundleId;
        }

        return new DevFlowScreen(snapshot, epoch, viewport, byId);
    }

    private static void Flatten(JsonObject element, int? parentIndex, int depth, bool interactiveOnly, JsonArray nodes, Dictionary<string, ScreenElement> byId, ref long? epoch)
    {
        var id = Text(element["id"]);
        var kept = id is not null && (!interactiveOnly || IsInteresting(element));
        var index = parentIndex;
        var childDepth = depth;
        if (kept)
        {
            epoch ??= Number(element["captureEpoch"]) is { } stamped ? (long)stamped : null;
            var (node, screenElement) = Convert(element, id!, nodes.Count, parentIndex, depth);
            nodes.Add(node);
            byId[id!] = screenElement;
            index = nodes.Count - 1;
            childDepth = depth + 1;
        }

        if (element["children"] is JsonArray children)
        {
            foreach (var child in children.OfType<JsonObject>())
            {
                Flatten(child, index, childDepth, interactiveOnly, nodes, byId, ref epoch);
            }
        }
    }

    /// <summary>An element the interactive snapshot keeps: it takes input, says something, or a test can find it by id.</summary>
    private static bool IsInteresting(JsonObject element) =>
        Text(element["type"]) == "Window"
        || Text(element["automationId"]) is { Length: > 0 }
        || Text(element["text"]) is { Length: > 0 }
        || Text(element["value"]) is { Length: > 0 }
        || (element["traits"] is JsonArray traits && traits.Any(trait => Text(trait) == "interactive"));

    private static (JsonObject Node, ScreenElement Element) Convert(JsonObject element, string id, int index, int? parentIndex, int depth)
    {
        var type = Text(element["type"]) ?? "";
        var role = Text(element["role"]);
        var kind = KindOf(type, role);
        var state = element["state"] as JsonObject;
        var enabled = Flag(state?["enabled"]) ?? Flag(element["isEnabled"]);
        var focused = Flag(state?["focused"]) ?? Flag(element["isFocused"]);
        var selected = Flag(state?["selected"]) ?? Flag(element["isSelected"]);
        var displayed = Flag(state?["displayed"]) ?? Flag(element["isVisible"]);
        var opacity = Number(state?["opacity"]) ?? Number(element["opacity"]);
        var visible = displayed is null ? null : (bool?)(displayed.Value && (opacity is null || opacity > 0));
        var text = Text(element["text"]);
        var value = Text(element["value"]);
        var rect = RectOf(element);
        var isTextInput = TextInputTypes.Contains(type) || role == "textbox";
        var readOnly = Text((element["frameworkProperties"] as JsonObject)?["IsReadOnly"]) is { } ro && string.Equals(ro, "True", StringComparison.OrdinalIgnoreCase);

        var node = new JsonObject
        {
            ["ref"] = id,
            ["index"] = index,
            ["depth"] = depth,
            ["type"] = kind,
        };
        if (parentIndex is { } parent)
        {
            node["parentIndex"] = parent;
        }

        if (role is not null)
        {
            node["kind"] = role;
        }

        if (Text(element["automationId"]) is { Length: > 0 } identifier)
        {
            node["identifier"] = identifier;
        }

        if (isTextInput)
        {
            // The agent reports a field's content as its text, and a password field's as the redaction marker: the
            // plaintext never leaves the app, and the marker is not a value a test should read.
            if (text == Redacted)
            {
                node["password"] = true;
            }
            else if (text is not null)
            {
                node["value"] = text;
            }

            node["editable"] = !readOnly;
            if (Placeholder(element) is { } placeholder)
            {
                node["placeholder"] = placeholder;
            }
        }
        else
        {
            if (text is not null)
            {
                node["label"] = text;
            }

            if (ToggleTypes.Contains(type) && bool.TryParse(value, out var toggled))
            {
                node["checked"] = toggled;
            }
            else if (value is not null)
            {
                node["value"] = value;
            }
        }

        if (role == "heading")
        {
            node["heading"] = true;
        }

        if (rect is { } bounds)
        {
            node["rect"] = new JsonObject { ["x"] = bounds.X, ["y"] = bounds.Y, ["width"] = bounds.Width, ["height"] = bounds.Height };
        }

        Set(node, "enabled", enabled);
        Set(node, "focused", focused);
        Set(node, "selected", selected);
        Set(node, "visibleToUser", visible);
        Set(node, "hittable", visible is null || enabled is null ? null : visible.Value && enabled.Value);

        return (node, new ScreenElement(id, kind, role, focused == true, isTextInput && !readOnly, rect));
    }

    /// <summary>The agent-device kind of a MAUI type: the role when it says more, the type map, else the type in kebab case.</summary>
    private static string KindOf(string type, string? role)
    {
        if (role is not null && RoleKinds.TryGetValue(role, out var fromRole))
        {
            return fromRole;
        }

        if (Kinds.TryGetValue(type, out var kind))
        {
            return kind;
        }

        return type.Length == 0 ? "other" : Kebab(type);
    }

    private static string? Placeholder(JsonObject element)
    {
        var native = (element["nativeView"] as JsonObject)?["properties"] as JsonObject;
        var framework = element["frameworkProperties"] as JsonObject;
        return Text(framework?["Placeholder"]) ?? Text(framework?["placeholder"]) ?? Text(native?["placeholderString"]) ?? Text(native?["placeholder"]) ?? Text(native?["hint"]);
    }

    private static string Kebab(string type)
    {
        var text = new StringBuilder(type.Length + 4);
        for (var i = 0; i < type.Length; i++)
        {
            var character = type[i];
            if (char.IsUpper(character) && i > 0 && (char.IsLower(type[i - 1]) || (i + 1 < type.Length && char.IsLower(type[i + 1]))))
            {
                text.Append('-');
            }

            text.Append(char.ToLowerInvariant(character));
        }

        return text.ToString();
    }

    private static void Set(JsonObject node, string name, bool? flag)
    {
        if (flag is { } value)
        {
            node[name] = value;
        }
    }

    private static Rect? RectOf(JsonObject element) =>
        element["bounds"] is JsonObject bounds && Number(bounds["x"]) is { } x && Number(bounds["y"]) is { } y && Number(bounds["width"]) is { } width && Number(bounds["height"]) is { } height
            ? new Rect(x, y, width, height)
            : null;

    private static bool? Flag(JsonNode? node) => node is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : null;

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>A JSON number, whatever type backs it: one parsed from text and one built in code differ.</summary>
    private static double? Number(JsonNode? node) =>
        node is JsonValue && JsonSerializer.SerializeToElement(node) is { ValueKind: JsonValueKind.Number } element ? element.GetDouble() : null;
}
