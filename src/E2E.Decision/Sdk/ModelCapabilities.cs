// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>
/// The AI SDK's <c>ModelCapability</c> (<c>@ai-sdk/provider</c>'s <c>model-capabilities.ts</c>): support declared by a provider's model catalog. It is
/// supported, unsupported, <see cref="Unknown"/> (distinct from unsupported), or available under a <see cref="Condition"/>, which is documentation.
/// </summary>
public sealed record ModelCapability
{
    private ModelCapability() { }

    /// <summary>Supported.</summary>
    public static ModelCapability Supported { get; } = new() { IsSupported = true };

    /// <summary>Unsupported.</summary>
    public static ModelCapability Unsupported { get; } = new() { IsSupported = false };

    /// <summary>Upstream's <c>'unknown'</c>.</summary>
    public static ModelCapability Unknown { get; } = new() { IsUnknown = true };

    /// <summary>True for a supported capability.</summary>
    public bool IsSupported { get; private init; }

    /// <summary>True for an <c>unknown</c> capability.</summary>
    public bool IsUnknown { get; private init; }

    /// <summary>Upstream's <c>{ condition }</c>: when the support is available.</summary>
    public string? Condition { get; private init; }

    /// <summary>A capability available under <paramref name="condition"/>.</summary>
    public static ModelCapability WithCondition(string condition) => new() { Condition = condition };
}

/// <summary>The <c>decision</c> section of the AI SDK's <c>ModelCapabilities</c>.</summary>
public sealed record DecisionModelCapabilities
{
    /// <summary>The question types the model answers.</summary>
    public required DecisionQuestionTypeCapabilities QuestionTypes { get; init; }

    /// <summary>The inputs the model takes.</summary>
    public required DecisionInputCapabilities Inputs { get; init; }

    /// <summary>Whether the model takes a reasoning option.</summary>
    public required ModelCapability Reasoning { get; init; }

    /// <summary>Whether the model takes a max output tokens option.</summary>
    public required ModelCapability MaxOutputTokens { get; init; }

    /// <summary>A description of the model.</summary>
    public string? Description { get; init; }
}

/// <summary>The <c>questionTypes</c> of <see cref="DecisionModelCapabilities"/>.</summary>
public sealed record DecisionQuestionTypeCapabilities
{
    public required ModelCapability Boolean { get; init; }

    public required ModelCapability Choice { get; init; }

    public required ModelCapability Score { get; init; }

    public required ModelCapability Box { get; init; }
}

/// <summary>The <c>inputs</c> of <see cref="DecisionModelCapabilities"/>.</summary>
public sealed record DecisionInputCapabilities
{
    public required ModelCapability Image { get; init; }
}

/// <summary>The AI SDK's <c>ModelCapabilities</c>. Only the <c>decision</c> section is modeled; omitted sections are undeclared.</summary>
public sealed record ModelCapabilities
{
    /// <summary>What the model decides.</summary>
    public DecisionModelCapabilities? Decision { get; init; }
}

/// <summary>The AI SDK's <c>ProviderOptionDefinition</c>: a provider option, declared once at the provider level.</summary>
public sealed record ProviderOptionDefinition(string Description);

/// <summary>
/// The AI SDK's <c>ModelProviderOptions</c>: per-model support for the options of the provider's option definitions, keyed by section
/// (such as <c>decision</c>) then option name.
/// </summary>
public sealed record ModelProviderOptions(IReadOnlyDictionary<string, IReadOnlyDictionary<string, ModelCapability>> Sections);

/// <summary>The AI SDK's <c>ModelEntry</c>: one provider-owned model record.</summary>
public sealed record ModelEntry
{
    /// <summary>What the model can do.</summary>
    public required ModelCapabilities Capabilities { get; init; }

    /// <summary>Support for the provider's options. Omitted sections or options are undeclared, not unsupported.</summary>
    public ModelProviderOptions? ProviderOptions { get; init; }
}
