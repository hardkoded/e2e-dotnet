// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Mobile;

namespace E2E.TestMu.Mobile;

/// <summary>Turns the page source Appium returns into the snapshot object <c>capture.snapshot</c> answers.</summary>
internal interface IPageSourceConverter
{
    JsonObject Convert(string pageSource, MobilePlatform platform);
}

/// <summary>
/// The refs of the newest snapshot. A ref names a node of one read and nothing else, so an action by ref finds the
/// node's rect here, and a ref of an older read is stale: the screen it named may be gone. Rects are in the units the
/// pointer takes (points on iOS, pixels on Android), as the snapshot gives them.
/// </summary>
internal sealed class ScreenRefs
{
    private Dictionary<string, JsonObject> _byRef = [];

    /// <summary>The nodes of the newest snapshot, as the converter wrote them.</summary>
    public IReadOnlyList<JsonObject> Nodes { get; private set; } = [];

    /// <summary>Replaces the refs with those of <paramref name="snapshot"/>.</summary>
    public void Replace(JsonObject snapshot)
    {
        var nodes = (snapshot["nodes"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
        var byRef = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (Read(node, "ref") is JsonValue value && value.TryGetValue<string>(out var reference))
            {
                byRef[reference.TrimStart('@')] = node;
            }
        }

        Nodes = nodes;
        _byRef = byRef;
    }

    /// <summary>The centre of the node a ref names. A ref the newest snapshot does not have, or one with no bounds, is stale.</summary>
    public (double X, double Y) CentreOf(string reference)
    {
        var key = reference.TrimStart('@');
        if (!_byRef.TryGetValue(key, out var node))
        {
            throw AppiumErrors.StaleReference(reference);
        }

        return RectOf(node) is { } rect ? (rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2)) : throw AppiumErrors.StaleReference(reference);
    }

    /// <summary>
    /// The first button in the first navigation bar, in document order: the leading one, which is the back button.
    /// Null when the screen has no navigation bar or the bar has no button.
    /// </summary>
    public (double X, double Y)? NavigationBarBackButton()
    {
        var byIndex = Nodes.Where(node => Number(Read(node, "index")) is not null).ToDictionary(node => (int)Number(Read(node, "index"))!.Value);
        var bar = Nodes.FirstOrDefault(node => Kind(node) == "navigationbar");
        if (bar is null || Number(Read(bar, "index")) is not { } barIndex)
        {
            return null;
        }

        foreach (var node in Nodes)
        {
            if (Kind(node) == "button" && RectOf(node) is { } rect && Within(node, (int)barIndex, byIndex))
            {
                return (rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));
            }
        }

        return null;
    }

    private static bool Within(JsonObject node, int ancestor, Dictionary<int, JsonObject> byIndex)
    {
        var steps = 0;
        var parent = Number(Read(node, "parentIndex"));
        while (parent is { } index && steps++ < byIndex.Count)
        {
            if ((int)index == ancestor)
            {
                return true;
            }

            parent = byIndex.TryGetValue((int)index, out var up) ? Number(Read(up, "parentIndex")) : null;
        }

        return false;
    }

    /// <summary>The node's kind or type, lower-case without dashes: <c>navigation-bar</c> and <c>NavigationBar</c> are one.</summary>
    private static string Kind(JsonObject node) =>
        ((Read(node, "kind") ?? Read(node, "type")) is JsonValue value && value.TryGetValue<string>(out var text) ? text : "").Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();

    private static Rect? RectOf(JsonObject node) =>
        Read(node, "rect") is JsonObject rect && Number(Read(rect, "x")) is { } x && Number(Read(rect, "y")) is { } y && Number(Read(rect, "width")) is { } width && Number(Read(rect, "height")) is { } height
            ? new Rect(x, y, width, height)
            : null;

    /// <summary>The snapshot's JSON is read case-insensitively, as the engine reads it.</summary>
    private static JsonNode? Read(JsonObject node, string name) =>
        node.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

    private static double? Number(JsonNode? node) =>
        node is JsonValue && JsonSerializer.SerializeToElement(node) is { ValueKind: JsonValueKind.Number } element ? element.GetDouble() : null;
}
