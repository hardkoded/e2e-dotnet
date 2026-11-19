// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk.Gateway;

/// <summary>The AI SDK's <c>gateway-decision-model-settings.ts</c>: the decision model ids the Gateway lists. Any other string is accepted too.</summary>
public static class GatewayDecisionModelId
{
    /// <summary>The ids upstream lists.</summary>
    public static IReadOnlyList<string> Known { get; } =
    [
        "baseten/elephant-1",
        "baseten/joey-1",
        "baseten/mammoth-1",
        "convaiinnovations/laya",
        "google/gemini-decision-1",
        "heygen/heygen-decision-1",
        "heygen/heygen-decision-1-turbo",
        "liquid/d1",
        "liquid/d1-ultra",
        "liquid/d1.2",
        "liquid/d1.2-ultra",
        "microsoft/decision-1.1",
        "microsoft/microsoft-decision-1",
        "nace-ai/drex-max-finance",
        "nace-ai/drex-max-v1.1",
        "nace-ai/drex-v2.0",
        "openai/gpt-6-luna-decisions",
        "parallel/parallel-decisions-fast",
        "perplexity/pplx-decider-flash-v1.2",
        "perplexity/pplx-decider-flash-v1.3",
        "perplexity/pplx-decider-v1.2",
        "perplexity/pplx-decider-v1.3",
        "scaledown/scaledown-classify-1",
        "typesafe-ai/jev",
        "typesafe-ai/jev-1.13.0",
        "typesafe-ai/jev-1.14.0",
    ];
}
