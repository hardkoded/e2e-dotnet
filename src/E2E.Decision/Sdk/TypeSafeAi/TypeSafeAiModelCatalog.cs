// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk.TypeSafeAi;

/// <summary>The AI SDK's <c>typesafe-ai-model-catalog.ts</c>: the decision models TypeSafe lists and the questions they answer.</summary>
public static class TypeSafeAiModelCatalog
{
    /// <summary>Upstream's <c>typeSafeAiDecisionModelIds</c>. Any other string is accepted as a model id too.</summary>
    public static IReadOnlyList<string> TypeSafeAiDecisionModelIds { get; } =
    ["jev-1.13.0", "jev-1.14.0", "jev-latest", "jev-preview"];

    /// <summary>Upstream's <c>typeSafeAiSupportedQuestionTypes</c>.</summary>
    public static IReadOnlyList<string> TypeSafeAiSupportedQuestionTypes { get; } =
    ["choice", "score", "boolean", "box"];
}
