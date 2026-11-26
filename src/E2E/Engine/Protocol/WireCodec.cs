// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace E2E.Engine.Protocol;

/// <summary>
/// The JSON an engine member's arguments and results take on the wire. Upstream's types are JSON already (<c>LocatorAction</c>,
/// <c>PointerAction</c>, <c>LocatorExpression</c>, <c>TextPattern</c>), so it has no counterpart: this is the .NET-only mapping between the
/// port's types and upstream's wire shapes, which a server written in another language reads. The server decodes with it and the client encodes.
/// </summary>
internal static class WireCodec
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Options for the records that cross as they are: camelCase, absent for null.</summary>
    internal static JsonSerializerOptions Json { get; } =
        new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };

    internal static T Read<T>(JsonNode? node) =>
        node.Deserialize<T>(Json) ?? throw new JsonException($"expected a {typeof(T).Name}, got null");

    internal static JsonNode? Write<T>(T value) => JsonSerializer.SerializeToNode(value, Json);

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    private static T ParseEnum<T>(JsonNode? node, string what)
        where T : struct, Enum =>
        Enum.TryParse<T>(Text(node), ignoreCase: true, out var value)
            ? value
            : throw new JsonException($"unknown {what} \"{Text(node)}\"");

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static string Required(JsonNode? node, string name) =>
        Text(node?[name]) ?? throw new JsonException($"\"{name}\" must be a string");

    private static bool Flag(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

    private static TimeSpan? Duration(JsonNode? ms) => ms is null ? null : TimeSpan.FromMilliseconds((double)ms);

    /// <summary>The wire form of a duration: milliseconds.</summary>
    private static JsonNode? Milliseconds(TimeSpan? duration) =>
        duration is { } value ? JsonValue.Create(value.TotalMilliseconds) : null;

    // --- text patterns ---------------------------------------------------------------------------------------------

    /// <summary>A text match as upstream's <c>TextPattern</c>: <c>exact</c> rides on a string, the flags on a regular expression.</summary>
    internal static JsonObject EncodeTextPattern(TextMatch match, bool exact)
    {
        if (match.Pattern is not { } pattern)
        {
            return new JsonObject
            {
                ["kind"] = "string",
                ["value"] = match.Text,
                ["exact"] = exact,
            };
        }

        var flags = "";
        if ((pattern.Options & RegexOptions.IgnoreCase) != 0)
        {
            flags += "i";
        }

        if ((pattern.Options & RegexOptions.Multiline) != 0)
        {
            flags += "m";
        }

        if ((pattern.Options & RegexOptions.Singleline) != 0)
        {
            flags += "s";
        }

        return new JsonObject
        {
            ["kind"] = "regexp",
            ["source"] = pattern.ToString(),
            ["flags"] = flags,
        };
    }

    internal static (TextMatch Match, bool Exact) DecodeTextPattern(JsonNode? node)
    {
        switch (Required(node, "kind"))
        {
            case "string":
                return (TextMatch.FromString(Required(node, "value")), node?["exact"] is null || Flag(node["exact"]));
            case "regexp":
                var options = RegexOptions.None;
                var source = Required(node, "source");
                foreach (var flag in Required(node, "flags"))
                {
                    // A .NET regular expression reads UTF-16 code units, so `u` and `v` (code point semantics) cannot be honored: refuse
                    // them rather than match something other than what the client asked for.
                    if (flag is 'u' or 'v')
                    {
                        throw new JsonException($"the regular expression flag \"{flag}\" has no .NET equivalent");
                    }

                    // JavaScript's sticky flag matches only at the start index: \G anchors a .NET match there.
                    if (flag == 'y')
                    {
                        source = $@"\G(?:{source})";
                    }

                    options |= flag switch
                    {
                        'i' => RegexOptions.IgnoreCase,
                        'm' => RegexOptions.Multiline,
                        's' => RegexOptions.Singleline,
                        _ => RegexOptions.None,
                    };
                }

                return (TextMatch.FromRegex(new Regex(source, options, RegexTimeout)), true);
            default:
                throw new JsonException($"unknown text pattern \"{Required(node, "kind")}\"");
        }
    }

    // --- locator expressions ---------------------------------------------------------------------------------------

    /// <summary>The port's flat expression as upstream's nested one: <c>query</c> (with <c>scope</c>), <c>filter</c>, <c>index</c>, <c>selector</c>, <c>frame</c>, <c>union</c>.</summary>
    internal static JsonObject EncodeExpression(LocatorExpression expression)
    {
        switch (expression.Kind)
        {
            case "filter":
                var filter = new JsonObject { ["kind"] = "filter", ["source"] = EncodeExpression(expression.Source!) };
                if (expression.HasText is { } hasText)
                {
                    filter["hasText"] = EncodeTextPattern(hasText, expression.HasTextExact);
                }

                if (expression.HasNotText is { } hasNotText)
                {
                    filter["hasNotText"] = EncodeTextPattern(hasNotText, expression.HasNotTextExact);
                }

                if (expression.Has is { } has)
                {
                    filter["has"] = EncodeExpression(has);
                }

                if (expression.HasNot is { } hasNot)
                {
                    filter["hasNot"] = EncodeExpression(hasNot);
                }

                return filter;
            case "index":
                return new JsonObject
                {
                    ["kind"] = "index",
                    ["source"] = EncodeExpression(expression.Source!),
                    ["index"] = expression.Index == LocatorExpression.LastIndex ? "last" : expression.Index,
                };
            case "selector":
                return new JsonObject { ["kind"] = "selector", ["selector"] = expression.Selector };
            case "frame":
                return new JsonObject
                {
                    ["kind"] = "frame",
                    ["selector"] = expression.Selector,
                    ["source"] = EncodeExpression(expression.Source!),
                };
            case "union":
                return new JsonObject
                {
                    ["kind"] = "union",
                    ["left"] = EncodeExpression(expression.Left!),
                    ["right"] = EncodeExpression(expression.Right!),
                };
        }

        var query = new JsonObject();
        if (expression.Kind == "role")
        {
            query["kind"] = "role";
            query["value"] = EncodeTextPattern(TextMatch.FromString(expression.Role!), exact: true);
            if (expression.Value is { } name)
            {
                query["name"] = EncodeTextPattern(name, expression.Exact);
            }

            var states = new JsonObject();
            foreach (
                var (key, state) in new (string, bool?)[]
                {
                    ("checked", expression.Checked),
                    ("disabled", expression.Disabled),
                    ("selected", expression.Selected),
                    ("expanded", expression.Expanded),
                    ("pressed", expression.Pressed),
                }
            )
            {
                if (state is { } value)
                {
                    states[key] = value;
                }
            }

            if (states.Count > 0)
            {
                query["states"] = states;
            }

            if (expression.Level is { } level)
            {
                query["level"] = level;
            }
        }
        else
        {
            query["kind"] = expression.Kind == "testid" ? "testId" : expression.Kind;
            query["value"] = EncodeTextPattern(expression.Value!, expression.Kind == "testid" || expression.Exact);
        }

        if (expression.Visible)
        {
            query["visible"] = true;
        }

        var wire = new JsonObject { ["kind"] = "query", ["query"] = query };
        if (expression.Parent is { } parent)
        {
            wire["scope"] = EncodeExpression(parent);
        }

        return wire;
    }

    internal static LocatorExpression DecodeExpression(JsonNode? node)
    {
        switch (Required(node, "kind"))
        {
            case "filter":
                var filter = new LocatorExpression { Kind = "filter", Source = DecodeExpression(node!["source"]) };
                if (node["hasText"] is { } hasText)
                {
                    var (match, exact) = DecodeTextPattern(hasText);
                    filter = filter with { HasText = match, HasTextExact = exact };
                }

                if (node["hasNotText"] is { } hasNotText)
                {
                    var (match, exact) = DecodeTextPattern(hasNotText);
                    filter = filter with { HasNotText = match, HasNotTextExact = exact };
                }

                return filter with
                {
                    Has = node["has"] is { } has ? DecodeExpression(has) : null,
                    HasNot = node["hasNot"] is { } hasNot ? DecodeExpression(hasNot) : null,
                };
            case "index":
                return new LocatorExpression
                {
                    Kind = "index",
                    Source = DecodeExpression(node!["source"]),
                    Index = Text(node["index"]) switch
                    {
                        "first" => 0,
                        "last" => LocatorExpression.LastIndex,
                        _ => (int)node["index"]!,
                    },
                };
            case "selector":
                return new LocatorExpression { Kind = "selector", Selector = Required(node, "selector") };
            case "frame":
                return new LocatorExpression
                {
                    Kind = "frame",
                    Selector = Required(node, "selector"),
                    Source = DecodeExpression(node!["source"]),
                };
            case "union":
                return new LocatorExpression
                {
                    Kind = "union",
                    Left = DecodeExpression(node!["left"]),
                    Right = DecodeExpression(node["right"]),
                };
            case "query":
                break;
            default:
                throw new JsonException($"unknown locator expression \"{Required(node, "kind")}\"");
        }

        var query = node!["query"] ?? throw new JsonException("a query expression needs a query");
        var parent = node["scope"] is { } scope ? DecodeExpression(scope) : null;
        var visible = Flag(query["visible"]);
        var kind = Required(query, "kind");
        if (kind == "role")
        {
            var states = query["states"];
            TextMatch? name = null;
            var exact = true;
            if (query["name"] is { } wireName)
            {
                (name, exact) = DecodeTextPattern(wireName);
            }

            return new LocatorExpression
            {
                Kind = "role",
                Parent = parent,
                Role = DecodeTextPattern(query["value"]).Match.Text,
                Value = name,
                Exact = exact,
                Visible = visible,
                Checked = states?["checked"] is { } c ? (bool)c : null,
                Disabled = states?["disabled"] is { } d ? (bool)d : null,
                Selected = states?["selected"] is { } s ? (bool)s : null,
                Expanded = states?["expanded"] is { } e ? (bool)e : null,
                Pressed = states?["pressed"] is { } p ? (bool)p : null,
                Level = query["level"] is { } level ? (int)level : null,
            };
        }

        var (value, valueExact) = DecodeTextPattern(query["value"]);
        return new LocatorExpression
        {
            Kind = kind == "testId" ? "testid" : kind,
            Parent = parent,
            Value = value,
            Exact = valueExact,
            Visible = visible,
        };
    }

    // --- actions ---------------------------------------------------------------------------------------------------

    private static JsonNode? EncodeModifiers(IReadOnlyList<KeyModifier>? modifiers) =>
        modifiers is null
            ? null
            : new JsonArray([.. modifiers.Select(modifier => JsonValue.Create(modifier.ToString()))]);

    private static IReadOnlyList<KeyModifier>? DecodeModifiers(JsonNode? node) =>
        node is JsonArray array ? [.. array.Select(item => ParseEnum<KeyModifier>(item, "key modifier"))] : null;

    /// <summary>A locator action as upstream's <c>LocatorAction</c>. File payload bytes cross as base64, through <see cref="Protocol.EncodeLocatorAction"/>.</summary>
    internal static JsonObject EncodeLocatorAction(LocatorAction action)
    {
        var wire = new JsonObject { ["kind"] = EngineActionKinds.Of(action) };
        switch (action)
        {
            case LocatorAction.SetInputFilePayloads:
                wire["files"] = Write(((WireSetInputFilePayloads)Protocol.EncodeLocatorAction(action)).Files);
                break;
            case LocatorAction.Tap(var modifiers):
                wire["modifiers"] = EncodeModifiers(modifiers);
                break;
            case LocatorAction.DoubleTap(var modifiers):
                wire["modifiers"] = EncodeModifiers(modifiers);
                break;
            case LocatorAction.MultiTap(var count, var modifiers):
                wire["count"] = count;
                wire["modifiers"] = EncodeModifiers(modifiers);
                break;
            case LocatorAction.SecondaryTap(var modifiers):
                wire["modifiers"] = EncodeModifiers(modifiers);
                break;
            case LocatorAction.LongPress(var duration):
                wire["durationMs"] = Milliseconds(duration);
                break;
            case LocatorAction.Fill(var value, var sensitive):
                wire["value"] = value;
                wire["sensitive"] = sensitive;
                break;
            case LocatorAction.Press(var key):
                wire["key"] = key;
                break;
            case LocatorAction.Select(var text, var option):
                wire["value"] =
                    text is not null ? text
                    : option is null ? null
                    : option.Label is not null ? new JsonObject { ["label"] = option.Label }
                    : option.Value is not null ? new JsonObject { ["value"] = option.Value }
                    : new JsonObject { ["index"] = option.Index };
                break;
            case LocatorAction.SetInputFiles(var paths):
                wire["paths"] = new JsonArray([.. paths.Select(path => JsonValue.Create(path))]);
                break;
            case LocatorAction.DragTo(var target):
                // The revision belongs to the observation, which a node of it does not carry: the engine's own adapter holds it.
                wire["target"] = Write(new NodeRef(target.Ref, ""));
                break;
            case LocatorAction.Swipe(var direction, var momentum):
                wire["direction"] = Camel(direction.ToString());
                wire["momentum"] = momentum is { } fling ? Camel(fling.ToString()) : null;
                break;
        }

        return wire;
    }

    internal static LocatorAction DecodeLocatorAction(JsonNode? node)
    {
        var kind = Required(node, "kind");
        switch (kind)
        {
            case "setInputFilePayloads":
                return Protocol.DecodeLocatorAction(Read<WireSetInputFilePayloads>(node));
            case "tap":
                return new LocatorAction.Tap(DecodeModifiers(node!["modifiers"]));
            case "doubleTap":
                return new LocatorAction.DoubleTap(DecodeModifiers(node!["modifiers"]));
            case "multiTap":
                return new LocatorAction.MultiTap(
                    node?["count"] is JsonValue count && count.TryGetValue<long>(out var times)
                        ? times
                        : throw new JsonException("\"count\" must be an integer"),
                    DecodeModifiers(node!["modifiers"])
                );
            case "secondaryTap":
                return new LocatorAction.SecondaryTap(DecodeModifiers(node!["modifiers"]));
            case "longPress":
                return new LocatorAction.LongPress(Duration(node!["durationMs"]));
            case "fill":
                return new LocatorAction.Fill(Required(node, "value"), Flag(node!["sensitive"]));
            case "press":
                return new LocatorAction.Press(Required(node, "key"));
            case "selectOption":
                return node!["value"] switch
                {
                    JsonValue text when text.TryGetValue<string>(out var label) => new LocatorAction.Select(label),
                    { } option => new LocatorAction.Select(Read<SelectOption>(option)),
                    _ => throw new JsonException("selectOption needs a value"),
                };
            case "setInputFiles":
                return new LocatorAction.SetInputFiles(Read<string[]>(node!["paths"]));
            case "check":
                return new LocatorAction.Check();
            case "uncheck":
                return new LocatorAction.Uncheck();
            case "clear":
                return new LocatorAction.Clear();
            case "focus":
                return new LocatorAction.Focus();
            case "blur":
                return new LocatorAction.Blur();
            case "hover":
                return new LocatorAction.Hover();
            case "scrollIntoView":
                return new LocatorAction.ScrollIntoView();
            case "dragTo":
                return new LocatorAction.DragTo(new SemanticNode { Ref = Read<NodeRef>(node!["target"]).Id });
            case "swipe":
                return new LocatorAction.Swipe(
                    ParseEnum<ScrollDirection>(node!["direction"], "scroll direction"),
                    node["momentum"] is { } momentum ? ParseEnum<Momentum>(momentum, "momentum") : null
                );
            default:
                throw new JsonException($"unknown locator action \"{kind}\"");
        }
    }

    /// <summary>A pointer action as upstream's <c>PointerAction</c>.</summary>
    internal static JsonObject EncodePointerAction(PointerAction action)
    {
        var wire = new JsonObject { ["kind"] = EngineActionKinds.Of(action) };
        switch (action)
        {
            case PointerAction.LongPress(var duration):
                wire["durationMs"] = Milliseconds(duration);
                break;
            case PointerAction.DragTo(var target):
                wire["target"] = Write(target);
                break;
            case PointerAction.Swipe(var direction, var momentum):
                wire["direction"] = Camel(direction.ToString());
                wire["momentum"] = momentum is { } fling ? Camel(fling.ToString()) : null;
                break;
            case PointerAction.SwipeTo(var target, var duration):
                wire["target"] = Write(target);
                wire["durationMs"] = Milliseconds(duration);
                break;
        }

        return wire;
    }

    internal static PointerAction DecodePointerAction(JsonNode? node)
    {
        var kind = Required(node, "kind");
        return kind switch
        {
            "tap" => new PointerAction.Tap(),
            "doubleTap" => new PointerAction.DoubleTap(),
            "secondaryTap" => new PointerAction.SecondaryTap(),
            "hover" => new PointerAction.Hover(),
            "longPress" => new PointerAction.LongPress(Duration(node!["durationMs"])),
            "dragTo" => new PointerAction.DragTo(Read<Point>(node!["target"])),
            "swipe" => new PointerAction.Swipe(
                ParseEnum<ScrollDirection>(node!["direction"], "scroll direction"),
                node["momentum"] is { } momentum ? ParseEnum<Momentum>(momentum, "momentum") : null
            ),
            "swipeTo" => new PointerAction.SwipeTo(Read<Point>(node!["target"]), Duration(node["durationMs"])),
            _ => throw new JsonException($"unknown pointer action \"{kind}\""),
        };
    }

    /// <summary>A captured state keeps its <c>data</c> member even when it is null: the state contract requires the field.</summary>
    internal static JsonObject EncodeState(EngineState state)
    {
        var wire = new JsonObject
        {
            ["format"] = state.Format,
            ["version"] = state.Version,
            ["data"] = state.Data?.DeepClone(),
        };
        if (state.ExpiresAt is not null)
        {
            wire["expiresAt"] = state.ExpiresAt;
        }

        return wire;
    }

    // --- video -----------------------------------------------------------------------------------------------------

    internal static JsonNode EncodeVideoSegments(IReadOnlyList<VideoSegment> segments) =>
        new JsonArray([
            .. segments.Select(segment =>
                segment switch
                {
                    VideoFile file => (JsonNode)new JsonObject { ["path"] = file.Path, ["startedAt"] = file.StartedAt },
                    VideoLink link => new JsonObject
                    {
                        ["url"] = link.Url,
                        ["mediaType"] = link.MediaType,
                        ["startedAt"] = link.StartedAt,
                    },
                    _ => throw new JsonException($"unknown video segment {segment.GetType().Name}"),
                }
            ),
        ]);

    internal static IReadOnlyList<VideoSegment> DecodeVideoSegments(JsonNode? node) =>
        [
            .. (node as JsonArray ?? throw new JsonException("video segments must be an array")).Select(segment =>
                segment?["url"] is not null
                    ? (VideoSegment)
                        new VideoLink
                        {
                            Url = Required(segment, "url"),
                            MediaType = Required(segment, "mediaType"),
                            StartedAt = Required(segment, "startedAt"),
                        }
                    : new VideoFile { Path = Required(segment, "path"), StartedAt = Required(segment, "startedAt") }
            ),
        ];
}
