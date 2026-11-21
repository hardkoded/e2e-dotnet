// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk.Gateway;

/// <summary>The AI SDK's <c>gateway-realtime-model-settings.ts</c>.</summary>
public static class GatewayRealtimeModelSettings
{
    /// <summary>
    /// Realtime model IDs that use the continuous Live API by default (upstream's <c>gatewayLiveModelIds</c>). Other IDs use the realtime
    /// API unless <c>Api = "live"</c> is passed.
    /// </summary>
    public static IReadOnlyList<string> GatewayLiveModelIds { get; } = ["openai/gpt-live-1", "openai/gpt-live-1.1"];
}

/// <summary>The AI SDK's <c>GatewayRealtimeOptions</c>.</summary>
public class GatewayRealtimeOptions
{
    /// <summary>Selects the Gateway API for the model ID: <c>live</c> or <c>realtime</c>, including for new Live models.</summary>
    public string? Api { get; init; }
}
