// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayBatch.GatewayBatchFixture;

namespace E2E.Decision.Tests.GatewayBatch;

/// <summary>Upstream's <c>describe('doCancelBatch')</c> of <c>gateway-batch.test.ts</c>.</summary>
public sealed class DoCancelBatchTests
{
    [Theory]
    [InlineData("pending", "cancel_requested")]
    [InlineData("completed", "completed")]
    [InlineData("failed", "cancelled")]
    public async Task Should_acknowledge_cancellation_without_treating_status_as_a_cancellation_result(
        string status,
        string jobStatus
    )
    {
        var providerMetadata = $$$$"""{"gateway":{"asyncJob":{"jobId":"job_123","status":"{{{{jobStatus}}}}"}}}""";
        var server = CreateServer();
        server.RespondJson(
            Cancel,
            $$$$"""{"batchId":"job_123","status":"{{{{status}}}}","providerMetadata":{{{{providerMetadata}}}}}"""
        );
        using var controller = new CancellationTokenSource();

        var result = await CreateTestBatch(
                server.Client,
                new Dictionary<string, string?> { ["ai-o11y-deployment-id"] = "dpl_123" }
            )
            .DoCancelBatchAsync(
                new BatchV4OperationOptions
                {
                    BatchId = "job_123",
                    Headers = new Dictionary<string, string?> { ["custom-header"] = "cancel-value" },
                },
                controller.Token
            );

        Assert.True(JsonEqual(providerMetadata, result.ProviderMetadata));
        var call = Assert.Single(server.Calls);
        Assert.True(JsonEqual("""{"batchId":"job_123"}""", call.RequestBodyJson));
        Assert.Equal("Bearer test-token", call.RequestHeaders["authorization"]);
        Assert.Equal("api-key", call.RequestHeaders["ai-gateway-auth-method"]);
        Assert.Equal("dpl_123", call.RequestHeaders["ai-o11y-deployment-id"]);
        Assert.Equal("cancel-value", call.RequestHeaders["custom-header"]);
        Assert.False(call.RequestHeaders.ContainsKey("ai-model-id"));
        Assert.True(call.RequestToken.CanBeCanceled);
    }

    [Fact]
    public async Task Should_allow_an_acknowledgement_without_provider_metadata()
    {
        var server = CreateServer();
        server.RespondJson(Cancel, """{"status":"pending","providerMetadata":null}""");

        var result = await CreateTestBatch(server.Client)
            .DoCancelBatchAsync(new BatchV4OperationOptions { BatchId = "job_123" });

        Assert.Equal(new BatchV4CancelResult(), result);
    }

    [Fact]
    public async Task Should_map_missing_jobs_to_GatewayNotFoundError_without_retrying()
    {
        var server = CreateServer();
        server.RespondError(Cancel, 404, NotFoundBody);

        await Assert.ThrowsAsync<GatewayNotFoundException>(() =>
            CreateTestBatch(server.Client).DoCancelBatchAsync(new BatchV4OperationOptions { BatchId = "missing-job" })
        );
        Assert.Single(server.Calls);
    }

    [Fact]
    public async Task Should_preserve_AbortError_without_wrapping_or_retrying_it()
    {
        var error = new OperationCanceledException("Request interrupted.");
        using var controller = new CancellationTokenSource();
        var handler = new ThrowingHandler(error, controller);

        var thrown = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateTestBatch(new HttpClient(handler))
                .DoCancelBatchAsync(new BatchV4OperationOptions { BatchId = "job_123" }, controller.Token)
        );

        Assert.Equal(error.Message, thrown.Message);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Should_preserve_TimeoutError_without_wrapping_or_retrying_it()
    {
        var error = new TimeoutException("Request interrupted.");
        var handler = new ThrowingHandler(error);

        var thrown = await Assert.ThrowsAsync<TimeoutException>(() =>
            CreateTestBatch(new HttpClient(handler))
                .DoCancelBatchAsync(new BatchV4OperationOptions { BatchId = "job_123" })
        );

        Assert.Same(error, thrown);
        Assert.Equal(1, handler.Calls);
    }
}
