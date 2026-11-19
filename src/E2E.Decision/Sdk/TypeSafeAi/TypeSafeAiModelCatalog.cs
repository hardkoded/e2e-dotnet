// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk.TypeSafeAi;

/// <summary>The AI SDK's <c>typesafe-ai-model-catalog.ts</c>: the decision models TypeSafe lists and the questions they answer.</summary>
public static class TypeSafeAiModelCatalog
{
    private static readonly Dictionary<string, string> ModelDescriptions = new()
    {
        ["jev-1.13.0"] = "Jev 1.13 release.",
        ["jev-1.14.0"] = "Jev 1.14 release.",
        ["jev-latest"] = "Alias for the latest stable release.",
        ["jev-preview"] = "Alias for the latest release, including previews.",
    };

    /// <summary>Upstream's <c>typeSafeAiDecisionModelIds</c>. Any other string is accepted as a model id too.</summary>
    public static IReadOnlyList<string> TypeSafeAiDecisionModelIds { get; } =
    ["jev-1.13.0", "jev-1.14.0", "jev-latest", "jev-preview"];

    /// <summary>Upstream's <c>typeSafeAiSupportedQuestionTypes</c>.</summary>
    public static IReadOnlyList<string> TypeSafeAiSupportedQuestionTypes { get; } =
    ["choice", "score", "boolean", "box"];

    /// <summary>Upstream's <c>typeSafeAiProviderOptions</c>: the provider options of a decision call and of an image file part.</summary>
    public static IReadOnlyDictionary<
        string,
        IReadOnlyDictionary<string, ProviderOptionDefinition>
    > TypeSafeAiProviderOptions { get; } =
        new Dictionary<string, IReadOnlyDictionary<string, ProviderOptionDefinition>>
        {
            ["decision"] = new Dictionary<string, ProviderOptionDefinition>
            {
                ["longContext"] = new(
                    "Enable experimental long context mode with 262k tokens per request and per question; slower and less accurate than default mode."
                ),
                ["reference"] = new(
                    "Image file parts only: asset reference used to place the image in state, instructions, or criteria. Defaults to a generated unique ID."
                ),
                ["insertReference"] = new(
                    "Image file parts only: whether to insert the asset reference into state. False requires an explicit reference."
                ),
            },
        };

    /// <summary>
    /// Upstream's <c>typeSafeAiModelCatalog</c>, named <c>ModelCatalog</c> because a member cannot take the name of its class: the entry of each
    /// known decision model id.
    /// </summary>
    public static IReadOnlyDictionary<string, ModelEntry> ModelCatalog { get; } =
        TypeSafeAiDecisionModelIds.ToDictionary(id => id, GetTypeSafeAiDecisionModelEntry);

    /// <summary>Upstream's <c>getTypeSafeAiDecisionModelEntry</c>: the catalog entry of a decision model; an id the catalog does not list gets <c>unknown</c> long context.</summary>
    public static ModelEntry GetTypeSafeAiDecisionModelEntry(string modelId)
    {
        var known = TypeSafeAiDecisionModelIds.Contains(modelId);
        return new ModelEntry
        {
            Capabilities = new ModelCapabilities
            {
                Decision = new DecisionModelCapabilities
                {
                    QuestionTypes = new DecisionQuestionTypeCapabilities
                    {
                        Boolean = ModelCapability.Supported,
                        Choice = ModelCapability.Supported,
                        Score = ModelCapability.Supported,
                        Box = ModelCapability.Supported,
                    },
                    Inputs = new DecisionInputCapabilities { Image = ModelCapability.Supported },
                    Reasoning = ModelCapability.Unsupported,
                    MaxOutputTokens = ModelCapability.Unsupported,
                    Description = ModelDescriptions.GetValueOrDefault(modelId),
                },
            },
            ProviderOptions = new ModelProviderOptions(
                new Dictionary<string, IReadOnlyDictionary<string, ModelCapability>>
                {
                    ["decision"] = new Dictionary<string, ModelCapability>
                    {
                        ["longContext"] = known ? ModelCapability.Supported : ModelCapability.Unknown,
                        ["reference"] = ModelCapability.WithCondition(
                            "Image file parts only; references must be nonempty and unique within a request."
                        ),
                        ["insertReference"] = ModelCapability.WithCondition(
                            "Image file parts only; false requires an explicit reference."
                        ),
                    },
                }
            ),
        };
    }
}
