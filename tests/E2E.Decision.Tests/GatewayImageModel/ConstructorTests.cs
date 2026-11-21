// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayImageModel.GatewayImageModelFixture;

namespace E2E.Decision.Tests.GatewayImageModel;

/// <summary>Upstream's <c>describe('constructor')</c> of <c>gateway-image-model.test.ts</c>.</summary>
public sealed class ConstructorTests
{
    [Fact]
    public void Should_create_instance_with_correct_properties()
    {
        using var model = CreateTestModel(new TestServer());

        Assert.Equal(TestModelId, model.ModelId);
        Assert.Equal("gateway", model.Provider);
        Assert.Equal("v4", model.SpecificationVersion);
        Assert.Equal(9007199254740991, model.MaxImagesPerCall);
    }

    [Fact]
    public void Should_avoid_client_side_splitting_even_for_BFL_models()
    {
        using var model = new E2E.Decision.Sdk.Gateway.GatewayImageModel(
            "bfl/flux-pro-1.1",
            Config(null, withAuthorization: false)
        );

        Assert.Equal(9007199254740991, model.MaxImagesPerCall);
    }

    [Fact]
    public void Should_accept_custom_provider_name()
    {
        using var model = new E2E.Decision.Sdk.Gateway.GatewayImageModel(
            TestModelId,
            Config(null, provider: "custom-gateway", withAuthorization: false)
        );

        Assert.Equal("custom-gateway", model.Provider);
    }
}
