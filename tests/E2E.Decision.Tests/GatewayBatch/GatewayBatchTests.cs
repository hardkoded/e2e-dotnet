// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.GatewayBatch.GatewayBatchFixture;

namespace E2E.Decision.Tests.GatewayBatch;

/// <summary>The tests of upstream's <c>describe('GatewayBatch')</c> in <c>gateway-batch.test.ts</c> that sit outside a nested <c>describe</c>.</summary>
public sealed class GatewayBatchTests
{
    [Fact]
    public void Should_expose_the_three_batch_methods_as_functions_batch_capability_duck_type()
    {
        var model = CreateTestBatch(CreateServer().Client);

        // The C# counterpart of the duck-type: the batch is an IBatchV4 with the start, status, and results operations.
        var batch = Assert.IsAssignableFrom<E2E.Decision.Sdk.IBatchV4>(model);
        foreach (var name in new[] { "DoStartBatchAsync", "DoGetBatchStatusAsync", "DoGetBatchResultsAsync" })
        {
            Assert.NotNull(typeof(E2E.Decision.Sdk.IBatchV4).GetMethod(name));
        }
    }
}
