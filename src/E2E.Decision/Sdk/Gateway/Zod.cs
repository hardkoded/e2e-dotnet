// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>
/// .NET-only: the checks the <c>z.object(...)</c> schemas of the Gateway response files make (<c>zod.ts</c> upstream). A value of the wrong
/// type throws an <see cref="InvalidResponseDataException"/>, which <see cref="ProviderUtils.ProcessSuccessfulResponse{T}"/> reports as
/// "Failed to process successful response".
/// </summary>
internal static class Zod
{
    public static JsonElement Object(JsonElement value, string path) =>
        value.ValueKind == JsonValueKind.Object ? value : throw Invalid(path, "expected object");

    public static JsonElement Array(JsonElement value, string path) =>
        value.ValueKind == JsonValueKind.Array ? value : throw Invalid(path, "expected array");

    public static string String(JsonElement parent, string name, string path) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw Invalid(path + "." + name, "expected string");

    /// <summary><c>z.nullish(z.string())</c>.</summary>
    public static string? NullishString(JsonElement parent, string name, string path) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : throw Invalid(path + "." + name, "expected string")
            : null;

    /// <summary><c>z.optional(z.string())</c>: absent is fine, null is not.</summary>
    public static string? OptionalString(JsonElement parent, string name, string path) =>
        parent.TryGetProperty(name, out var value)
            ? value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : throw Invalid(path + "." + name, "expected string")
            : null;

    /// <summary><c>z.optional(z.number())</c>: absent is fine, null is not.</summary>
    public static double? OptionalNumber(JsonElement parent, string name, string path) =>
        parent.TryGetProperty(name, out var value)
            ? value.ValueKind == JsonValueKind.Number
                ? value.GetDouble()
                : throw Invalid(path + "." + name, "expected number")
            : null;

    public static double Number(JsonElement parent, string name, string path) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : throw Invalid(path + "." + name, "expected number");

    /// <summary><c>z.optional(z.number())</c> and <c>z.nullish(z.number())</c>: a null counts as absent.</summary>
    public static double? NullishNumber(JsonElement parent, string name, string path) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.ValueKind == JsonValueKind.Number
                ? value.GetDouble()
                : throw Invalid(path + "." + name, "expected number")
            : null;

    public static bool Boolean(JsonElement parent, string name, string path) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : throw Invalid(path + "." + name, "expected boolean");

    public static JsonElement Property(JsonElement parent, string name, string path) =>
        parent.TryGetProperty(name, out var value) ? value : throw Invalid(path + "." + name, "required");

    /// <summary>
    /// <c>z.optional(z.record(z.string(), z.record(z.string(), z.unknown())))</c> over <paramref name="parent"/>.providerMetadata; absent gives
    /// null. <paramref name="entrySchema"/> checks each entry (for the image model's catchall object).
    /// </summary>
    public static IReadOnlyDictionary<string, JsonElement>? OptionalProviderMetadata(
        JsonElement parent,
        string path,
        Action<JsonElement, string>? entrySchema = null
    )
    {
        if (!parent.TryGetProperty("providerMetadata", out var value))
        {
            return null;
        }

        var metadata = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var entry in Object(value, path + ".providerMetadata").EnumerateObject())
        {
            var entryPath = path + ".providerMetadata." + entry.Name;
            Object(entry.Value, entryPath);
            entrySchema?.Invoke(entry.Value, entryPath);
            metadata[entry.Name] = entry.Value.Clone();
        }

        return metadata;
    }

    public static InvalidResponseDataException Invalid(string path, string reason) =>
        new("Type validation failed: " + path + ": " + reason);
}
