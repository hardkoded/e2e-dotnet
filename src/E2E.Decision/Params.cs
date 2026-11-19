// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Decision;

/// <summary>Upstream's <c>params.ts</c>.</summary>
internal static class Params
{
    /// <summary>Upstream's <c>nonSecretParams</c>: the step params minus secret projections, which travel only as handles.</summary>
    public static JsonObject NonSecretParams(IReadOnlyDictionary<string, JsonElement>? parameters)
    {
        var kept = new JsonObject();
        foreach (var (key, value) in parameters ?? new Dictionary<string, JsonElement>())
        {
            if (WithoutSecret(value) is { Removed: false } cleaned)
            {
                kept[key] = cleaned.Node;
            }
        }

        return kept;
    }

    /// <summary>Removes secret-marker leaves at any depth. <c>Removed</c> means the value itself was one.</summary>
    private static (bool Removed, JsonNode? Node) WithoutSecret(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Array:
                var items = new JsonArray();
                foreach (var item in value.EnumerateArray())
                {
                    var cleaned = WithoutSecret(item);
                    items.Add(cleaned.Removed ? null : cleaned.Node);
                }

                return (false, items);
            case JsonValueKind.Object:
                if (
                    value.TryGetProperty("kind", out var kind)
                    && kind.ValueKind == JsonValueKind.String
                    && kind.GetString() == "secret"
                )
                {
                    return (true, null);
                }

                var record = new JsonObject();
                foreach (var property in value.EnumerateObject())
                {
                    var cleaned = WithoutSecret(property.Value);
                    if (!cleaned.Removed)
                    {
                        record[property.Name] = cleaned.Node;
                    }
                }

                return (false, record);
            default:
                return (false, JsonNode.Parse(value.GetRawText()));
        }
    }
}
