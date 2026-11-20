// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.AsGatewayError;

/// <summary>Upstream's <c>describe('caller cancellation')</c> in <c>describe('asGatewayError')</c> of <c>as-gateway-error.test.ts</c>. Upstream's undici timeout codes are <see cref="TimeoutException"/> here, and a cancellation not caused by a timeout is a caller cancellation.</summary>
public sealed class CallerCancellationTests
{
    private static void AssertRethrown(Exception error) =>
        Assert.Same(error, Assert.Throws(error.GetType(), () => GatewayErrors.AsGatewayError(error)));

    [Fact]
    public void Partial_Should_rethrow_a_cancellation_unchanged() =>
        AssertRethrown(new OperationCanceledException("Request cancelled"));

    [Fact]
    public void Partial_Should_rethrow_a_task_cancellation_unchanged() =>
        AssertRethrown(new TaskCanceledException("Request cancelled"));

    [Fact]
    public void Partial_Should_rethrow_a_cancellation_with_a_caller_token_unchanged() =>
        AssertRethrown(new OperationCanceledException("Request cancelled", new CancellationToken(true)));

    [Fact]
    public void Partial_Should_rethrow_a_cancellation_with_an_inner_error_unchanged() =>
        AssertRethrown(new OperationCanceledException("Request cancelled", new InvalidOperationException()));
}
