// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>
/// The AI SDK's <c>gatewayWarningSchema</c> (<c>gateway-warning-schema.ts</c>): the structured warnings the Gateway reports. A value that
/// is none of them throws an <see cref="InvalidResponseDataException"/>.
/// </summary>
internal static class GatewayWarningSchema
{
    /// <summary><c>z.optional(z.array(gatewayWarningSchema()))</c> over <paramref name="parent"/>.<paramref name="name"/>; absent gives an empty list.</summary>
    public static IReadOnlyList<SharedV4Warning> ParseOptionalArray(JsonElement parent, string name, string path)
    {
        if (!parent.TryGetProperty(name, out var value))
        {
            return [];
        }

        var arrayPath = path + "." + name;
        var warnings = new List<SharedV4Warning>();
        foreach (var warning in Zod.Array(value, arrayPath).EnumerateArray())
        {
            warnings.Add(Parse(warning, arrayPath + "[" + warnings.Count + "]"));
        }

        return warnings;
    }

    public static SharedV4Warning Parse(JsonElement warning, string path)
    {
        Zod.Object(warning, path);
        var type = Zod.String(warning, "type", path);
        return type switch
        {
            "unsupported" or "compatibility" => new SharedV4Warning
            {
                Type = type,
                Feature = Zod.String(warning, "feature", path),
                Details = Zod.OptionalString(warning, "details", path),
            },
            "deprecated" => new SharedV4Warning
            {
                Type = type,
                Setting = Zod.String(warning, "setting", path),
                Message = Zod.String(warning, "message", path),
            },
            "other" => new SharedV4Warning { Type = type, Message = Zod.String(warning, "message", path) },
            _ => throw Zod.Invalid(path + ".type", "invalid discriminator value"),
        };
    }
}
