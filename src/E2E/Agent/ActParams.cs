// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace E2E.Internal;

/// <summary>
/// Checks the params of <c>agent.act</c>, <c>agent.assert</c>, and <c>agent.waitFor</c> as upstream does: JSON-safe, at most 32 levels deep, no cycle,
/// and at most 64 KiB once projected to JSON. A <see cref="Secret"/> projects to its name and
/// purpose, and a <see cref="UniqueValue"/> to its value.
/// </summary>
internal static class ActParams
{
    public const int MaxBytes = 65_536;

    public const int MaxDepth = 32;

    private static readonly JsonSerializerOptions Json = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    public static void Validate(IReadOnlyDictionary<string, object?>? parameters, string api)
    {
        if (parameters is null)
        {
            return;
        }

        var projected = Project(parameters, 0, new HashSet<object>(ReferenceEqualityComparer.Instance), null, api);
        var bytes = Encoding.UTF8.GetByteCount(projected?.ToJsonString(Json) ?? "null");
        if (bytes > MaxBytes)
        {
            throw Invalid(api + " params are " + bytes.ToString(CultureInfo.InvariantCulture) + " canonical bytes; the maximum is " + MaxBytes.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>The params as the JSON an executor reads them: a <see cref="Secret"/> as its name and purpose, a <see cref="UniqueValue"/> as its value.</summary>
    public static JsonObject? Projected(IReadOnlyDictionary<string, object?> parameters, string api = "agent.act") =>
        Project(parameters, 0, new HashSet<object>(ReferenceEqualityComparer.Instance), null, api) as JsonObject;

    /// <summary>
    /// Upstream's <c>validateParams</c> result: the params as the model reads them, and the JSON Pointer (RFC 6901) and value of every
    /// <see cref="UniqueValue"/> at any depth, in the order the walk meets them.
    /// </summary>
    public static (JsonObject? Projected, List<ParamTemplate> Templates) ValidateParams(IReadOnlyDictionary<string, object?>? parameters, string api)
    {
        List<ParamTemplate> templates = [];
        var projected = parameters is null ? null : Project(parameters, 0, new HashSet<object>(ReferenceEqualityComparer.Instance), null, api, "", templates) as JsonObject;
        return (projected, templates);
    }

    /// <summary>
    /// Upstream's <c>redactParams</c>: the projected params with <paramref name="redact"/> applied to every string in them, object keys
    /// included, and their <c>unique()</c> templates moved with their leaves: a template's pointer follows the redacted keys and its
    /// value is redacted as the leaf is, so the trace cache slots what the params hold. Two keys of one object that redact alike
    /// are <c>INVALID_ARGUMENT</c>. <c>api</c> names the call in error messages.
    /// </summary>
    public static (JsonObject Params, List<ParamTemplate> Templates) RedactParams(JsonObject parameters, IReadOnlyList<ParamTemplate> templates, Func<string, string> redact, string api)
    {
        Dictionary<string, string> pointers = new(StringComparer.Ordinal);
        var redacted = (JsonObject)RedactJson(parameters, redact, "", "", pointers, api)!;
        return (redacted, [.. templates.Select(template => new ParamTemplate(pointers.GetValueOrDefault(template.Pointer, template.Pointer), redact(template.Value)))]);
    }

    // The node with `redact` applied to every string in it. Records in `pointers` where each node's pointer (`from`) lands once its keys are redacted (`to`).
    private static JsonNode? RedactJson(JsonNode? node, Func<string, string> redact, string from, string to, Dictionary<string, string> pointers, string api)
    {
        pointers[from] = to;
        switch (node)
        {
            case JsonValue value when value.TryGetValue<string>(out var text):
                return JsonValue.Create(redact(text));
            case JsonObject map:
                var cleaned = new JsonObject();
                foreach (var (key, entry) in map)
                {
                    var clean = redact(key);
                    if (cleaned.ContainsKey(clean))
                    {
                        throw Invalid(api + " params has two keys that read \"" + clean + "\" once secret values are redacted; a key cannot be told apart by a secret");
                    }

                    cleaned[clean] = RedactJson(entry, redact, CacheKeys.ParamPointer(from, key), CacheKeys.ParamPointer(to, clean), pointers, api);
                }

                return cleaned;
            case JsonArray list:
                var items = new JsonArray();
                for (var index = 0; index < list.Count; index++)
                {
                    items.Add(RedactJson(list[index], redact, CacheKeys.ParamPointer(from, index), CacheKeys.ParamPointer(to, index), pointers, api));
                }

                return items;
            default:
                return node?.DeepClone();
        }
    }

    /// <summary>The secrets the params declare at any depth, one per name, as upstream's walk of the params finds them.</summary>
    public static List<Secret> SecretsIn(IReadOnlyDictionary<string, object?>? parameters)
    {
        var found = new Dictionary<(string Name, string Value), Secret>();
        if (parameters is not null)
        {
            Project(parameters, 0, new HashSet<object>(ReferenceEqualityComparer.Instance), found, "agent.act");
        }

        return [.. found.Values];
    }

    private static JsonNode? Project(object? value, int depth, HashSet<object> ancestors, Dictionary<(string Name, string Value), Secret>? found, string api, string pointer = "", List<ParamTemplate>? templates = null)
    {
        if (depth > MaxDepth)
        {
            throw Invalid(api + " params exceeds " + MaxDepth.ToString(CultureInfo.InvariantCulture) + " levels of nesting");
        }

        switch (value)
        {
            case null:
                return null;
            case Secret secret:
                if (found is not null)
                {
                    found[(secret.Name, secret.Value)] = secret;
                }

                var projected = new JsonObject { ["kind"] = "secret", ["name"] = secret.Name };
                if (secret.Purpose is not null)
                {
                    projected["purpose"] = secret.Purpose;
                }

                return projected;
            case UniqueValue unique:
                templates?.Add(new ParamTemplate(pointer, unique.Value));
                return JsonValue.Create(unique.Value);
            case string text:
                return JsonValue.Create(text);
            case double number when !double.IsFinite(number):
            case float single when !float.IsFinite(single):
                throw Invalid(api + " params contains a non-finite number");
            case JsonElement element:
                return Element(element, depth, api);
            case JsonNode node:
                return Element(JsonSerializer.SerializeToElement(node, Json), depth, api);
        }

        var info = Json.GetTypeInfo(value.GetType());
        if (info.Kind == JsonTypeInfoKind.None)
        {
            try
            {
                return JsonSerializer.SerializeToNode(value, value.GetType(), Json);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException)
            {
                throw Invalid(api + " params must be JSON-safe: " + ex.Message);
            }
        }

        if (!value.GetType().IsValueType && !ancestors.Add(value))
        {
            throw Invalid(api + " params contains a cycle");
        }

        try
        {
            switch (info.Kind)
            {
                case JsonTypeInfoKind.Dictionary:
                    var map = new JsonObject();
                    foreach (var (key, entry) in Entries((IEnumerable)value))
                    {
                        map[key] = Project(entry, depth + 1, ancestors, found, api, CacheKeys.ParamPointer(pointer, key), templates);
                    }

                    return map;
                case JsonTypeInfoKind.Enumerable:
                    var list = new JsonArray();
                    foreach (var entry in (IEnumerable)value)
                    {
                        list.Add(Project(entry, depth + 1, ancestors, found, api, CacheKeys.ParamPointer(pointer, list.Count), templates));
                    }

                    return list;
                default:
                    var shape = new JsonObject();
                    foreach (var property in info.Properties)
                    {
                        if (property.Get is { } get)
                        {
                            shape[property.Name] = Project(get(value), depth + 1, ancestors, found, api, CacheKeys.ParamPointer(pointer, property.Name), templates);
                        }
                    }

                    return shape;
            }
        }
        finally
        {
            ancestors.Remove(value);
        }
    }

    private static JsonNode? Element(JsonElement element, int depth, string api)
    {
        if (depth > MaxDepth)
        {
            throw Invalid(api + " params exceeds " + MaxDepth.ToString(CultureInfo.InvariantCulture) + " levels of nesting");
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var map = new JsonObject();
                foreach (var property in element.EnumerateObject())
                {
                    map[property.Name] = Element(property.Value, depth + 1, api);
                }

                return map;
            case JsonValueKind.Array:
                var list = new JsonArray();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(Element(item, depth + 1, api));
                }

                return list;
            case JsonValueKind.Undefined or JsonValueKind.Null:
                return null;
            default:
                return JsonValue.Create(element);
        }
    }

    private static IEnumerable<(string Key, object? Value)> Entries(IEnumerable dictionary)
    {
        if (dictionary is IDictionary plain)
        {
            foreach (DictionaryEntry entry in plain)
            {
                yield return (Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? "", entry.Value);
            }

            yield break;
        }

        foreach (var item in dictionary)
        {
            var type = item!.GetType();
            var key = type.GetProperty("Key")?.GetValue(item);
            yield return (Convert.ToString(key, CultureInfo.InvariantCulture) ?? "", type.GetProperty("Value")?.GetValue(item));
        }
    }

    private static TestException Invalid(string message) => new("INVALID_ARGUMENT", message);
}
