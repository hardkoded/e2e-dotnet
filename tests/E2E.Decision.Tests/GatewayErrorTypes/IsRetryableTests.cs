// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.GatewayErrorTypes;

/// <summary>Upstream's <c>describe('isRetryable')</c> of <c>gateway-error-types.test.ts</c>.</summary>
public sealed class IsRetryableTests
{
    [Fact]
    public void Should_be_true_for_GatewayInternalServerError_500() =>
        Assert.True(new GatewayInternalServerException().IsRetryable);

    [Fact]
    public void Should_be_true_for_GatewayRateLimitError_429() =>
        Assert.True(new GatewayRateLimitException().IsRetryable);

    [Fact]
    public void Should_be_true_for_GatewayTimeoutError_408() => Assert.True(new GatewayTimeoutException().IsRetryable);

    [Fact]
    public void Should_be_true_for_status_codes_gte_500() =>
        Assert.True(new GatewayInternalServerException(statusCode: 503).IsRetryable);

    [Fact]
    public void Should_be_false_for_GatewayAuthenticationError_401() =>
        Assert.False(new GatewayAuthenticationException().IsRetryable);

    [Fact]
    public void Should_be_false_for_GatewayInvalidRequestError_400() =>
        Assert.False(new GatewayInvalidRequestException().IsRetryable);

    [Fact]
    public void Should_be_false_for_GatewayModelNotFoundError_404() =>
        Assert.False(new GatewayModelNotFoundException().IsRetryable);

    [Fact]
    public void Should_be_true_for_GatewayResponseError_502() =>
        Assert.True(new GatewayResponseException().IsRetryable);
}
