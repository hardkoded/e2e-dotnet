// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>
/// The AI SDK's <c>ModelCapability</c>: <c>true</c>, <c>false</c>, <c>'unknown'</c>, or <c>{ condition }</c>. A condition is documentation,
/// not an expression to execute.
/// </summary>
public sealed record ModelCapability
{
    /// <summary>Supported.</summary>
    public static ModelCapability Supported { get; } = new() { Value = true };

    /// <summary>Unsupported.</summary>
    public static ModelCapability Unsupported { get; } = new() { Value = false };

    /// <summary>Upstream's <c>'unknown'</c>: distinct from unsupported.</summary>
    public static ModelCapability Unknown { get; } = new();

    /// <summary>True or false for a plain boolean capability; null for <c>'unknown'</c> and for a condition.</summary>
    public bool? Value { get; init; }

    /// <summary>Upstream's <c>{ condition }</c>: when support is available.</summary>
    public string? Condition { get; init; }

    /// <summary>Upstream's <c>{ condition }</c>.</summary>
    public static ModelCapability WithCondition(string condition) => new() { Condition = condition };

    /// <summary>A boolean capability.</summary>
    public static ModelCapability From(bool value) => value ? Supported : Unsupported;
}

/// <summary>A provider option, declared once at the provider level. Upstream's <c>ProviderOptionDefinition</c>.</summary>
public sealed record ProviderOptionDefinition(string Description);

/// <summary>The decision question types of <c>ModelCapabilities.decision.questionTypes</c>.</summary>
public sealed record DecisionQuestionTypeCapabilities(
    ModelCapability Boolean,
    ModelCapability Choice,
    ModelCapability Score,
    ModelCapability Box
);

/// <summary>The decision inputs of <c>ModelCapabilities.decision.inputs</c>.</summary>
public sealed record DecisionInputCapabilities(ModelCapability Image);

/// <summary>Upstream's <c>ModelCapabilities.decision</c>.</summary>
public sealed record DecisionCapabilities(
    DecisionQuestionTypeCapabilities QuestionTypes,
    DecisionInputCapabilities Inputs,
    ModelCapability Reasoning,
    ModelCapability MaxOutputTokens
)
{
    /// <summary>A model description, which TypeSafe's catalog adds to the decision section.</summary>
    public string? Description { get; init; }
}

/// <summary>Upstream's <c>ModelCapabilities</c>. Only the decision section exists in the port.</summary>
public sealed record ModelCapabilities
{
    /// <summary>Omitted means undeclared, not explicitly unsupported.</summary>
    public DecisionCapabilities? Decision { get; init; }
}

/// <summary>Upstream's <c>ModelProviderOptions</c>: per-model support for a provider's options, keyed by section and option name.</summary>
public sealed record ModelProviderOptions
{
    /// <summary>The decision section.</summary>
    public IReadOnlyDictionary<string, ModelCapability>? Decision { get; init; }
}

/// <summary>Upstream's <c>ProviderOptionDefinitions</c>: the provider options a provider accepts, grouped by operation.</summary>
public sealed record ProviderOptionDefinitions
{
    /// <summary>The decision section.</summary>
    public IReadOnlyDictionary<string, ProviderOptionDefinition>? Decision { get; init; }
}

/// <summary>Upstream's <c>ModelEntry</c>: one provider-owned model record.</summary>
public sealed record ModelEntry(ModelCapabilities Capabilities)
{
    /// <summary>Omitted sections or options are undeclared, not explicitly unsupported.</summary>
    public ModelProviderOptions? ProviderOptions { get; init; }
}
