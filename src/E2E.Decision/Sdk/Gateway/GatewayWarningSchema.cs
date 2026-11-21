// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>The AI SDK's <c>gatewayWarningSchema</c> (<c>gateway-warning-schema.ts</c>): the warnings a Gateway response carries.</summary>
internal static class GatewayWarningSchema
{
    /// <summary><c>z.optional(z.array(gatewayWarningSchema()))</c> over <paramref name="parent"/>.<paramref name="name"/>; absent gives an empty list.</summary>
    public static IReadOnlyList<SharedV4Warning> ReadOptionalArray(JsonElement parent, string name, string path)
    {
        if (!parent.TryGetProperty(name, out var value))
        {
            return [];
        }

        var warnings = new List<SharedV4Warning>();
        var index = 0;
        foreach (var warning in Zod.Array(value, path + "." + name).EnumerateArray())
        {
            warnings.Add(
                Read(Zod.Object(warning, path + "." + name + "[" + index + "]"), path + "." + name + "[" + index + "]")
            );
            index++;
        }

        return warnings;
    }

    private static SharedV4Warning Read(JsonElement warning, string path) =>
        Zod.String(warning, "type", path) switch
        {
            "unsupported" => new SharedV4UnsupportedWarning(
                Zod.String(warning, "feature", path),
                Zod.OptionalString(warning, "details", path)
            ),
            "compatibility" => new SharedV4CompatibilityWarning(
                Zod.String(warning, "feature", path),
                Zod.OptionalString(warning, "details", path)
            ),
            "deprecated" => new SharedV4DeprecatedWarning(
                Zod.String(warning, "setting", path),
                Zod.String(warning, "message", path)
            ),
            "other" => new SharedV4OtherWarning(Zod.String(warning, "message", path)),
            _ => throw Zod.Invalid(path + ".type", "invalid discriminator value"),
        };
}
