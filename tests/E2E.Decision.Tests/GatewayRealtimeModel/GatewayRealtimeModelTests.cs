// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using static E2E.Decision.Tests.GatewayRealtimeModel.GatewayRealtimeModelFixture;

namespace E2E.Decision.Tests.GatewayRealtimeModel;

/// <summary>Upstream's <c>describe('GatewayRealtimeModel')</c> of <c>gateway-realtime-model.test.ts</c>.</summary>
public sealed class GatewayRealtimeModelTests
{
    [Fact]
    public void Rejects_unsupported_intents_during_serialization_as_well_as_connection_setup()
    {
        var target = CreateTestModel();
        const string intent = RealtimeModelV4SessionIntent.Transcription;

        var serialize = Assert.Throws<InvalidOperationException>(() =>
            target.SerializeClientEvent(new RealtimeModelV4ResponseCreate(), intent)
        );
        Assert.Contains("does not support", serialize.Message, StringComparison.Ordinal);
        var build = Assert.Throws<InvalidOperationException>(() =>
            target.BuildSessionConfig(new RealtimeModelV4SessionConfig(), intent)
        );
        Assert.Contains("does not support", build.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Exposes_the_v4_specification_provider_and_model_id()
    {
        var model = CreateTestModel();
        Assert.Equal("v4", model.SpecificationVersion);
        Assert.Equal("gateway.realtime", model.Provider);
        Assert.Equal("openai/gpt-realtime", model.ModelId);
    }
}
