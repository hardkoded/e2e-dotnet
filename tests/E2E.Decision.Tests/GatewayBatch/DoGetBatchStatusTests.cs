// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayBatch.GatewayBatchFixture;

namespace E2E.Decision.Tests.GatewayBatch;

/// <summary>Upstream's <c>describe('doGetBatchStatus')</c> of <c>gateway-batch.test.ts</c>.</summary>
public sealed class DoGetBatchStatusTests
{
    private static BatchV4OperationOptions Job(string batchId = "job_123") => new() { BatchId = batchId };

    [Fact]
    public async Task Should_post_the_batchId_and_map_the_status_response()
    {
        var server = CreateServer();
        PrepareBatchStatusResponse(
            server,
            """
            {
              "batchId": "job_123",
              "status": "completed",
              "rawStatus": "ended",
              "requestCounts": { "total": 2, "pending": 0, "completed": 2, "failed": 0 },
              "createdAt": "2026-08-18T00:00:00.000Z"
            }
            """
        );

        var status = await CreateTestBatch(server.Client).DoGetBatchStatusAsync(Job());

        Assert.True(JsonEqual("""{"batchId":"job_123"}""", server.Calls[0].RequestBodyJson));
        Assert.Equal("Bearer test-token", server.Calls[0].RequestHeaders["authorization"]);
        Assert.False(server.Calls[0].RequestHeaders.ContainsKey("ai-model-id"));
        Assert.Equal(
            new BatchV4Status
            {
                Status = "completed",
                RawStatus = "ended",
                RequestCounts = new BatchV4RequestCounts(2, 0, 2, 0),
                CreatedAt = "2026-08-18T00:00:00.000Z",
            },
            status
        );
    }

    [Fact]
    public async Task Should_map_a_failed_status_with_error_details()
    {
        var server = CreateServer();
        PrepareBatchStatusResponse(
            server,
            """{"batchId":"job_123","status":"failed","error":{"message":"Batch failed.","type":"batch_failed","statusCode":502}}"""
        );

        var status = await CreateTestBatch(server.Client).DoGetBatchStatusAsync(Job());

        Assert.Equal(
            new BatchV4Status
            {
                Status = "failed",
                Error = new BatchV4Error
                {
                    Message = "Batch failed.",
                    Type = "batch_failed",
                    StatusCode = 502,
                },
            },
            status
        );
    }

    [Fact]
    public async Task Should_omit_partial_request_counts_instead_of_fabricating_zeros()
    {
        var server = CreateServer();
        PrepareBatchStatusResponse(server, """{"batchId":"job_123","status":"pending","requestCounts":{"total":2}}""");

        var status = await CreateTestBatch(server.Client).DoGetBatchStatusAsync(Job());

        Assert.Equal(new BatchV4Status { Status = "pending" }, status);
    }

    [Fact]
    public async Task Should_map_a_404_not_found_response_to_GatewayNotFoundError()
    {
        var server = CreateServer();
        server.RespondError(Status, 404, NotFoundBody);

        var error = await Assert.ThrowsAsync<GatewayNotFoundException>(() =>
            CreateTestBatch(server.Client).DoGetBatchStatusAsync(Job("missing-job"))
        );

        Assert.Equal("Async job not found.", error.Message);
        Assert.Equal(404, error.StatusCode);
    }

    [Fact]
    public async Task Should_preserve_an_AbortError_instead_of_converting_it_to_a_retryable_gateway_error()
    {
        var abortError = new OperationCanceledException("The operation was aborted.");
        using var controller = new CancellationTokenSource();

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateTestBatch(new HttpClient(new ThrowingHandler(abortError, controller)))
                .DoGetBatchStatusAsync(Job(), controller.Token)
        );

        // HttpClient rethrows the handler's cancellation as its own TaskCanceledException, so the instance differs; it is still a cancellation.
        Assert.Equal(abortError.Message, error.Message);
    }

    [Fact]
    public async Task Should_pass_abortSignal_to_fetch_when_provided()
    {
        var server = CreateServer();
        PrepareBatchStatusResponse(server, """{"batchId":"job_123","status":"pending"}""");
        using var controller = new CancellationTokenSource();

        await CreateTestBatch(server.Client).DoGetBatchStatusAsync(Job(), controller.Token);

        Assert.True(server.Calls[0].RequestToken.CanBeCanceled);
    }
}
