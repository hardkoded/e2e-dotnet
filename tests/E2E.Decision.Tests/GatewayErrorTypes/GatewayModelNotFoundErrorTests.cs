// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.GatewayErrorTypes;

/// <summary>Upstream's <c>describe('GatewayModelNotFoundError')</c> of <c>gateway-error-types.test.ts</c>.</summary>
public sealed class GatewayModelNotFoundErrorTests
{
    [Fact]
    public void Should_create_error_with_default_values()
    {
        var error = new GatewayModelNotFoundException();

        Assert.Equal("GatewayModelNotFoundError", error.Name);
        Assert.Equal("model_not_found", error.Type);
        Assert.Equal("Model not found", error.Message);
        Assert.Equal(404, error.StatusCode);
        Assert.Null(error.ModelId);
    }

    [Fact]
    public void Should_create_error_with_model_ID()
    {
        var error = new GatewayModelNotFoundException("Model gpt-4 not found", modelId: "gpt-4");

        Assert.Equal("Model gpt-4 not found", error.Message);
        Assert.Equal("gpt-4", error.ModelId);
    }

    [Fact]
    public void Should_be_identifiable_via_instance_check()
    {
        var error = new GatewayModelNotFoundException();

        Assert.IsType<GatewayModelNotFoundException>(error);
        Assert.IsAssignableFrom<GatewayException>(error);
    }
}
