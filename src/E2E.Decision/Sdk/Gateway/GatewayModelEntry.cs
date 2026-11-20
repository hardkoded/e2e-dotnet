// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk.Gateway;

/// <summary>The AI SDK's <c>GatewayLanguageModelEntry</c> and <c>KNOWN_MODEL_TYPES</c> (<c>gateway-model-entry.ts</c>).</summary>
public static class GatewayModelEntryTypes
{
    /// <summary>Upstream's <c>KNOWN_MODEL_TYPES</c>. <c>evaluation</c> stays until the Gateway migrates its model catalog to <c>decision</c>.</summary>
    public static readonly IReadOnlyList<string> KnownModelTypes =
    [
        "embedding",
        "decision",
        "evaluation",
        "extraction",
        "image",
        "language",
        "realtime",
        "reranking",
        "speech",
        "transcription",
        "video",
    ];
}

/// <summary>Upstream's <c>GatewayLanguageModelEntry.pricing</c>: the cost of a model in USD.</summary>
public sealed record GatewayModelPricing
{
    /// <summary>Cost per input token in USD.</summary>
    public required string Input { get; init; }

    /// <summary>Cost per output token in USD.</summary>
    public required string Output { get; init; }

    /// <summary>Cost per cached input token in USD. Only present for models that support prompt caching.</summary>
    public string? CachedInputTokens { get; init; }

    /// <summary>Cost per input token to create cache entries in USD. Only present for models that support prompt caching.</summary>
    public string? CacheCreationInputTokens { get; init; }
}

/// <summary>Upstream's <c>GatewayLanguageModelSpecification</c>: <c>Pick&lt;LanguageModelV4, 'specificationVersion' | 'provider' | 'modelId'&gt;</c>.</summary>
public sealed record GatewayLanguageModelSpecification(string SpecificationVersion, string Provider, string ModelId);

/// <summary>The AI SDK's <c>GatewayLanguageModelEntry</c>: a model the Gateway offers.</summary>
public sealed record GatewayLanguageModelEntry
{
    /// <summary>The model id used in model settings and to name the intended model.</summary>
    public required string Id { get; init; }

    /// <summary>The display name of the model.</summary>
    public required string Name { get; init; }

    /// <summary>Optional description of the model.</summary>
    public string? Description { get; init; }

    /// <summary>Optional pricing information for the model.</summary>
    public GatewayModelPricing? Pricing { get; init; }

    /// <summary>The AI SDK language model specification of the model.</summary>
    public required GatewayLanguageModelSpecification Specification { get; init; }

    /// <summary>One of <see cref="GatewayModelEntryTypes.KnownModelTypes"/>, to tell the model types apart.</summary>
    public string? ModelType { get; init; }
}
