// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk.Gateway;

/// <summary>The AI SDK's <c>gateway-decision-model-settings.ts</c>: the decision model ids the Gateway lists. Any other string is accepted too.</summary>
public static class GatewayDecisionModelId
{
    /// <summary>Upstream's <c>GatewayModelIds['decision']</c>, from <see cref="GatewayModelCatalog"/>.</summary>
    public static IReadOnlyList<string> Known { get; } = GatewayModelCatalog.Decision;
}
