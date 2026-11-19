// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk;

/// <summary>
/// The AI SDK's global telemetry integration registry (<c>telemetry/telemetry-registry.ts</c>). A decide call that sets no
/// <see cref="TelemetryOptions.Integrations"/> uses the integrations registered here.
/// </summary>
public static class TelemetryRegistry
{
    private static readonly object s_lock = new();

    private static ITelemetry[]? s_integrations;

    /// <summary>Registers one or more telemetry integrations globally (<c>registerTelemetry</c>).</summary>
    public static void RegisterTelemetry(params ITelemetry[] integrations)
    {
        lock (s_lock)
        {
            s_integrations = [.. s_integrations ?? [], .. integrations];
        }
    }

    /// <summary>The globally registered telemetry integrations, in registration order (<c>getGlobalTelemetryIntegrations</c>).</summary>
    public static IReadOnlyList<ITelemetry> GetGlobalTelemetryIntegrations()
    {
        lock (s_lock)
        {
            return s_integrations ?? [];
        }
    }

    /// <summary>Clears the registry, like the upstream tests' <c>globalThis.AI_SDK_TELEMETRY_INTEGRATIONS = undefined</c>.</summary>
    internal static void Reset()
    {
        lock (s_lock)
        {
            s_integrations = null;
        }
    }
}
