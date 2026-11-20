// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.GatewayErrorTypes;

/// <summary>Upstream's <c>describe('GatewayAccessPolicyDeniedError')</c> of <c>gateway-error-types.test.ts</c>.</summary>
public sealed class GatewayAccessPolicyDeniedErrorTests
{
    [Fact]
    public void Should_create_error_with_default_values()
    {
        var error = new GatewayAccessPolicyDeniedException();

        Assert.Equal("GatewayAccessPolicyDeniedError", error.Name);
        Assert.Equal("access_policy_denied", error.Type);
        Assert.Equal("Access policy denied", error.Message);
        Assert.Equal(403, error.StatusCode);
        Assert.False(error.IsRetryable);
        Assert.Null(error.PolicyIds);
        Assert.Null(error.RemovedModels);
        Assert.Null(error.RemovedProviders);
    }

    [Fact]
    public void Should_be_distinguishable_from_GatewayForbiddenError()
    {
        var denied = new GatewayAccessPolicyDeniedException();
        var forbidden = new GatewayForbiddenException();

        Assert.IsType<GatewayAccessPolicyDeniedException>(denied);
        Assert.IsAssignableFrom<GatewayException>(denied);
        Assert.IsNotType<GatewayForbiddenException>(denied);
        Assert.IsNotType<GatewayAccessPolicyDeniedException>(forbidden);
    }
}
