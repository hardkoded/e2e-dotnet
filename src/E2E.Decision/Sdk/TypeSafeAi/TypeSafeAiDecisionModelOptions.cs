// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Decision.Sdk.TypeSafeAi;

/// <summary>
/// The AI SDK's <c>typesafeAiDecisionModelOptions</c>: the <c>typesafe</c> provider options of a decision call.
/// </summary>
public sealed class TypeSafeAiDecisionModelOptions
{
    /// <summary>
    /// Enables TypeSafe's experimental long context mode, raising both the per-request and per-question context limits to 262k tokens. Long
    /// context is slower and less accurate than the default limits. Defaults to false.
    /// </summary>
    public bool? LongContext { get; init; }

    /// <summary>Upstream's <c>parseProviderOptions</c> over the schema: null options when there are none, and a bad shape is rejected.</summary>
    internal static TypeSafeAiDecisionModelOptions? Parse(IReadOnlyDictionary<string, JsonElement>? providerOptions)
    {
        if (providerOptions is null || !providerOptions.TryGetValue("typesafe", out var typesafe))
        {
            return null;
        }

        if (typesafe.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (typesafe.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidArgumentException(
                "providerOptions",
                "invalid typesafe provider options: expected an object"
            );
        }

        if (!typesafe.TryGetProperty("longContext", out var longContext))
        {
            return new TypeSafeAiDecisionModelOptions();
        }

        return longContext.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? new TypeSafeAiDecisionModelOptions { LongContext = longContext.GetBoolean() }
            : throw new InvalidArgumentException(
                "providerOptions",
                "invalid typesafe provider options: longContext must be a boolean"
            );
    }
}

/// <summary>
/// The AI SDK's <c>typesafeAiFilePartProviderOptions</c>: the <c>typesafe</c> options of an image file part.
/// </summary>
public sealed class TypeSafeAiFilePartProviderOptions
{
    /// <summary>
    /// Asset reference for this image. Inserted into state at the file's position unless the state already contains it. Every occurrence in
    /// state, instructions, or criteria sends another copy of the image. Defaults to a generated ID. References must be nonempty and unique within
    /// the request, and should not occur accidentally in other request text.
    /// </summary>
    public string? Reference { get; init; }

    /// <summary>
    /// Whether to insert the reference into state at the file's position. <c>false</c> sends the image only where you reference it in
    /// instructions or criteria. <c>true</c> inserts it even when the state already contains it. <c>false</c> requires an explicit
    /// <see cref="Reference"/>. Defaults to inserting the reference unless the state already contains it.
    /// </summary>
    public bool? InsertReference { get; init; }

    internal static TypeSafeAiFilePartProviderOptions? Parse(IReadOnlyDictionary<string, JsonElement>? providerOptions)
    {
        if (
            providerOptions is null
            || !providerOptions.TryGetValue("typesafe", out var typesafe)
            || typesafe.ValueKind == JsonValueKind.Null
        )
        {
            return null;
        }

        static InvalidArgumentException Invalid(string reason) =>
            new("providerOptions", "invalid typesafe provider options: " + reason);

        if (typesafe.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("expected an object");
        }

        string? reference = null;
        if (typesafe.TryGetProperty("reference", out var rawReference))
        {
            reference =
                rawReference.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(rawReference.GetString())
                    ? rawReference.GetString()
                    : throw Invalid("reference must be a non-blank string");
        }

        bool? insertReference = null;
        if (typesafe.TryGetProperty("insertReference", out var rawInsert))
        {
            insertReference = rawInsert.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? rawInsert.GetBoolean()
                : throw Invalid("insertReference must be a boolean");
        }

        return new TypeSafeAiFilePartProviderOptions { Reference = reference, InsertReference = insertReference };
    }
}
