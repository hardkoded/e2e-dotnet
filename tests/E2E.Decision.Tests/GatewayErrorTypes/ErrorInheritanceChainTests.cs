// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.GatewayErrorTypes;

/// <summary>Upstream's <c>describe('Error inheritance chain')</c> of <c>gateway-error-types.test.ts</c>.</summary>
public sealed class ErrorInheritanceChainTests
{
    [Fact]
    public void Should_maintain_proper_inheritance()
    {
        var error = new GatewayAuthenticationException();

        Assert.IsAssignableFrom<Exception>(error);
        Assert.IsAssignableFrom<GatewayException>(error);
        Assert.IsType<GatewayAuthenticationException>(error);
    }

    [Fact]
    public void Should_have_proper_stack_traces()
    {
        GatewayAuthenticationException? error = null;
        try
        {
            throw new GatewayAuthenticationException("Test error");
        }
        catch (GatewayAuthenticationException caught)
        {
            error = caught;
        }

        Assert.NotNull(error.StackTrace);
        Assert.Contains("Should_have_proper_stack_traces", error.StackTrace);
        Assert.Contains("Test error", error.Message);
    }
}
