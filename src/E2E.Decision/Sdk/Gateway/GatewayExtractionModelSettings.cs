// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk.Gateway;

/// <summary>The AI SDK's <c>gateway-extraction-model-settings.ts</c>: the extraction model ids the Gateway lists. Any other string is accepted too.</summary>
public static class GatewayExtractionModelId
{
    /// <summary>The ids upstream lists.</summary>
    public static IReadOnlyList<string> Known { get; } = ["scaledown/extract-1"];
}
