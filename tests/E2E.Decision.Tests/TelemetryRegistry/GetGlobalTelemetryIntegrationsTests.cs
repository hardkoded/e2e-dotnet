// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Tests.TelemetryRegistry;

/// <summary>The AI SDK's <c>telemetry-registry.test.ts</c>, <c>describe('getGlobalTelemetryIntegrations')</c>.</summary>
[Collection(GlobalTelemetryCollection.Name)]
public sealed class GetGlobalTelemetryIntegrationsTests : GlobalTelemetryTestBase
{
    [Fact]
    public void Returns_an_empty_array_when_no_integrations_are_registered()
    {
        Assert.Empty(Sdk.TelemetryRegistry.GetGlobalTelemetryIntegrations());
    }
}
