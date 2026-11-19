// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's <c>TypeSafeAiDecisionModelOptions</c>: the request-level provider options under <c>typesafe</c>.</summary>
public sealed class TypeSafeAiDecisionModelOptions
{
    /// <summary>
    /// Enable TypeSafe's experimental long context mode, raising both the per-request and per-question context limits to 262k tokens.
    /// Long context is slower and less accurate than the default limits. Defaults to false.
    /// </summary>
    public bool? LongContext { get; init; }

    /// <summary>The value for <see cref="DecisionModelCall.ProviderOptions"/>.</summary>
    public IReadOnlyDictionary<string, JsonElement> ToProviderOptions()
    {
        var options = new JsonObject();
        if (LongContext is { } longContext)
        {
            options["longContext"] = longContext;
        }

        return new Dictionary<string, JsonElement> { ["typesafe"] = JsonSerializer.SerializeToElement(options) };
    }
}

/// <summary>The AI SDK's <c>TypeSafeAiFilePartProviderOptions</c>: the options of an image file part under <c>typesafe</c>.</summary>
public sealed class TypeSafeAiFilePartProviderOptions
{
    /// <summary>
    /// Asset reference for this image. Inserted into state at the file's position unless the state already contains it. Every occurrence in
    /// state, instructions, or criteria sends another copy of the image. Defaults to a generated ID. References must be nonempty and unique
    /// within the request, and should not occur accidentally in other request text.
    /// </summary>
    public string? Reference { get; init; }

    /// <summary>
    /// Whether to insert the reference into state at the file's position. Set to <c>false</c> to send the image only where you reference it in
    /// instructions or criteria. Set to <c>true</c> to insert it even when the state already contains it. <c>false</c> requires an explicit
    /// <see cref="Reference"/>. Defaults to inserting the reference unless the state already contains it.
    /// </summary>
    public bool? InsertReference { get; init; }

    /// <summary>The value for <see cref="DecisionFilePart.ProviderOptions"/>.</summary>
    public IReadOnlyDictionary<string, JsonElement> ToProviderOptions()
    {
        var options = new JsonObject();
        if (Reference is not null)
        {
            options["reference"] = Reference;
        }

        if (InsertReference is { } insertReference)
        {
            options["insertReference"] = insertReference;
        }

        return new Dictionary<string, JsonElement> { ["typesafe"] = JsonSerializer.SerializeToElement(options) };
    }
}
