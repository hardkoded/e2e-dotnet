// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;

namespace E2E.Decision.Tests.TelemetryRegistry;

/// <summary>The AI SDK's <c>telemetry-registry.test.ts</c>, <c>describe('registerTelemetry')</c>.</summary>
[Collection(GlobalTelemetryCollection.Name)]
public sealed class RegisterTelemetryTests : GlobalTelemetryTestBase
{
    [Fact]
    public void Adds_an_integration_to_the_global_registry()
    {
        var integration = new Integration();

        Sdk.TelemetryRegistry.RegisterTelemetry(integration);

        Assert.Equal([integration], Sdk.TelemetryRegistry.GetGlobalTelemetryIntegrations());
    }

    [Fact]
    public void Adds_multiple_integrations_in_registration_order()
    {
        var integration1 = new Integration();
        var integration2 = new Integration();

        Sdk.TelemetryRegistry.RegisterTelemetry(integration1);
        Sdk.TelemetryRegistry.RegisterTelemetry(integration2);

        Assert.Equal([integration1, integration2], Sdk.TelemetryRegistry.GetGlobalTelemetryIntegrations());
    }

    [Fact]
    public void Adds_multiple_integrations_passed_in_a_single_call()
    {
        var integration1 = new Integration();
        var integration2 = new Integration();
        var integration3 = new Integration();

        Sdk.TelemetryRegistry.RegisterTelemetry(integration1, integration2, integration3);

        Assert.Equal(
            [integration1, integration2, integration3],
            Sdk.TelemetryRegistry.GetGlobalTelemetryIntegrations()
        );
    }

    [Fact]
    public void Is_a_no_op_when_called_with_no_integrations()
    {
        Sdk.TelemetryRegistry.RegisterTelemetry();

        Assert.Empty(Sdk.TelemetryRegistry.GetGlobalTelemetryIntegrations());
    }
}
