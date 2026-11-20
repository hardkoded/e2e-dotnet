// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using E2E.Decision.Tests.TelemetryRegistry;

namespace E2E.Decision.Tests.CreateTelemetryDispatcher;

/// <summary>The AI SDK's <c>create-telemetry-dispatcher.test.ts</c>, <c>describe('global vs local integration resolution')</c>.</summary>
[Collection(GlobalTelemetryCollection.Name)]
public sealed class GlobalVsLocalIntegrationResolutionTests : GlobalTelemetryTestBase
{
    private static readonly DecideStartEvent s_dummyEvent = new()
    {
        RuntimeContext = new Dictionary<string, object?>(),
        CallId = "call",
        Provider = "provider",
        ModelId = "model",
        State = [],
        Questions = new Dictionary<string, DecisionQuestion>(),
        MaxRetries = 0,
        ProviderOptions = new Dictionary<string, System.Text.Json.JsonElement>(),
    };

    [Fact]
    public async Task Uses_globally_registered_integrations_when_no_local_integrations_are_provided()
    {
        var global = new Integration();
        Sdk.TelemetryRegistry.RegisterTelemetry(global);

        await new TelemetryDispatcher(null).OnDecideStartAsync(s_dummyEvent);

        Assert.Equal(1, global.Starts);
    }

    [Fact]
    public async Task Uses_only_local_integrations_when_provided_ignoring_global()
    {
        var global = new Integration();
        var local = new Integration();
        Sdk.TelemetryRegistry.RegisterTelemetry(global);

        await new TelemetryDispatcher(new TelemetryOptions { Integrations = [local] }).OnDecideStartAsync(s_dummyEvent);

        Assert.Equal(0, global.Starts);
        Assert.Equal(1, local.Starts);
    }

    [Fact]
    public async Task Uses_only_local_integration_array_when_provided_ignoring_global()
    {
        var global = new Integration();
        var local1 = new Integration();
        var local2 = new Integration();
        Sdk.TelemetryRegistry.RegisterTelemetry(global);

        await new TelemetryDispatcher(new TelemetryOptions { Integrations = [local1, local2] }).OnDecideStartAsync(
            s_dummyEvent
        );

        Assert.Equal(0, global.Starts);
        Assert.Equal(1, local1.Starts);
        Assert.Equal(1, local2.Starts);
    }

    [Fact]
    public async Task Global_integrations_still_work_for_calls_without_local_integrations()
    {
        var global = new Integration();
        var local = new Integration();
        Sdk.TelemetryRegistry.RegisterTelemetry(global);

        var withLocal = new TelemetryDispatcher(new TelemetryOptions { Integrations = [local] });
        var withoutLocal = new TelemetryDispatcher(new TelemetryOptions());
        await withLocal.OnDecideStartAsync(s_dummyEvent);
        await withoutLocal.OnDecideStartAsync(s_dummyEvent);

        Assert.Equal(1, local.Starts);
        Assert.Equal(1, global.Starts);
    }

    [Fact]
    public async Task Partial_ignores_globally_registered_integrations_when_is_enabled_is_false()
    {
        var global = new Integration();
        Sdk.TelemetryRegistry.RegisterTelemetry(global);

        await new TelemetryDispatcher(new TelemetryOptions { IsEnabled = false }).OnDecideStartAsync(s_dummyEvent);

        Assert.Equal(0, global.Starts);
    }
}
