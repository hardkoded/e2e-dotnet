// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayBatch.GatewayBatchFixture;

namespace E2E.Decision.Tests.GatewayBatch;

/// <summary>Upstream's <c>describe('doStartBatch')</c> of <c>gateway-batch.test.ts</c>.</summary>
public sealed class DoStartBatchTests
{
    private static BatchV4StartOptions Options(
        IReadOnlyList<BatchV4Request>? requests = null,
        JsonObject? providerOptions = null,
        string? webhookUrl = null,
        IReadOnlyDictionary<string, string?>? headers = null
    ) =>
        new()
        {
            Requests = requests ?? BatchPromptRequests(),
            ProviderOptions = providerOptions,
            WebhookUrl = webhookUrl,
            Headers = headers,
        };

    [Fact]
    public async Task Should_reject_unsupported_request_types_before_sending_a_request()
    {
        var server = CreateServer();
        var error = await Assert.ThrowsAsync<UnsupportedFunctionalityException>(() =>
            CreateTestBatch(server.Client)
                .DoStartBatchAsync(
                    Options([
                        new ImageBatchV4Request
                        {
                            Id = "image-1",
                            ModelId = "test-model-1",
                            Options = [],
                        },
                    ])
                )
        );

        Assert.Equal("batch request type: image", error.Functionality);
        Assert.Empty(server.Calls);
    }

    [Fact]
    public async Task Should_fail_before_sending_a_request_when_models_are_mixed()
    {
        var server = CreateServer();
        var requests = BatchPromptRequests();
        var error = await Assert.ThrowsAsync<InvalidArgumentException>(() =>
            CreateTestBatch(server.Client)
                .DoStartBatchAsync(Options([requests[0], requests[1] with { ModelId = "test-model-2" }]))
        );

        Assert.Equal("requests", error.Parameter);
        Assert.Equal(
            "Invalid argument for parameter requests: The AI Gateway Batch API requires all requests in a batch to use the same model. Found \"test-model-1\" and \"test-model-2\".",
            error.Message
        );
        Assert.Empty(server.Calls);
    }

    [Fact]
    public async Task Should_send_the_shared_request_model_in_the_model_header()
    {
        var server = CreateServer();
        PrepareBatchStartResponse(server);

        await CreateTestBatch(server.Client, new Dictionary<string, string?> { ["ai-o11y-deployment-id"] = "dpl_123" })
            .DoStartBatchAsync(Options(headers: new Dictionary<string, string?> { ["Custom-Header"] = "batch-value" }));

        var headers = server.Calls[0].RequestHeaders;
        Assert.Equal("Bearer test-token", headers["authorization"]);
        Assert.Equal("api-key", headers["ai-gateway-auth-method"]);
        Assert.Equal("test-model-1", headers["ai-model-id"]);
        Assert.Equal("dpl_123", headers["ai-o11y-deployment-id"]);
        Assert.Equal("batch-value", headers["custom-header"]);
        Assert.False(headers.ContainsKey("idempotency-key"));
    }

    [Fact]
    public async Task Should_send_the_modality_per_request_model_ids_and_provider_options()
    {
        var server = CreateServer();
        PrepareBatchStartResponse(server);

        await CreateTestBatch(server.Client)
            .DoStartBatchAsync(
                Options(providerOptions: (JsonObject)JsonNode.Parse("""{"gateway":{"order":["openai"]}}""")!)
            );

        Assert.True(
            JsonNode.DeepEquals(
                new JsonObject
                {
                    ["requests"] = BatchPromptRequestsJson(),
                    ["providerOptions"] = JsonNode.Parse("""{"gateway":{"order":["openai"]}}"""),
                },
                server.Calls[0].RequestBodyJson
            )
        );
    }

    [Fact]
    public async Task Should_map_the_start_response_to_a_BatchV4StartResult()
    {
        var server = CreateServer();
        PrepareBatchStartResponse(
            server,
            """
            {
              "batchId": "job_123",
              "status": "pending",
              "rawStatus": "validating",
              "requestCounts": { "total": 2, "pending": 2, "completed": 0, "failed": 0 },
              "warnings": [{ "requestId": "req-1", "warning": { "type": "other", "message": "a warning" } }],
              "providerMetadata": { "gateway": { "asyncJob": { "jobId": "job_123", "status": "queued" } } }
            }
            """
        );

        var result = await CreateTestBatch(server.Client).DoStartBatchAsync(Options());

        Assert.Equal("job_123", result.BatchId);
        Assert.Equal("pending", result.Status);
        Assert.Equal("validating", result.RawStatus);
        Assert.Equal(new BatchV4RequestCounts(2, 2, 0, 0), result.RequestCounts);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal("req-1", warning.RequestId);
        Assert.True(JsonEqual("""{"type":"other","message":"a warning"}""", warning.Warning));
        Assert.True(
            JsonEqual("""{"gateway":{"asyncJob":{"jobId":"job_123","status":"queued"}}}""", result.ProviderMetadata)
        );
        Assert.Null(result.Error);
        Assert.Null(result.CreatedAt);
        Assert.Null(result.ExpiresAt);
    }

    [Fact]
    public async Task Should_send_the_idempotency_key_header_from_providerOptions_gateway_idempotencyKey_without_forwarding_it_in_the_body()
    {
        var server = CreateServer();
        PrepareBatchStartResponse(server);

        await CreateTestBatch(server.Client)
            .DoStartBatchAsync(
                Options(providerOptions: (JsonObject)JsonNode.Parse("""{"gateway":{"idempotencyKey":"idem-abc"}}""")!)
            );

        Assert.Equal("idem-abc", server.Calls[0].RequestHeaders["idempotency-key"]);
        // Transport metadata stays out of the payload the Gateway digests for replay identity; an empty providerOptions is omitted entirely.
        Assert.True(
            JsonNode.DeepEquals(
                new JsonObject { ["requests"] = BatchPromptRequestsJson() },
                server.Calls[0].RequestBodyJson
            )
        );
    }

    [Fact]
    public async Task Should_keep_other_gateway_provider_options_in_the_body_while_stripping_the_idempotency_key()
    {
        var server = CreateServer();
        PrepareBatchStartResponse(server);

        await CreateTestBatch(server.Client)
            .DoStartBatchAsync(
                Options(
                    providerOptions: (JsonObject)
                        JsonNode.Parse("""{"gateway":{"idempotencyKey":"idem-abc","order":["openai"]}}""")!
                )
            );

        Assert.Equal("idem-abc", server.Calls[0].RequestHeaders["idempotency-key"]);
        Assert.True(
            JsonEqual("""{"gateway":{"order":["openai"]}}""", server.Calls[0].RequestBodyJson!["providerOptions"])
        );
    }

    [Fact]
    public async Task Should_send_webhookUrl_as_the_top_level_callbackUrl_body_field()
    {
        var server = CreateServer();
        PrepareBatchStartResponse(
            server,
            """
            {
              "batchId": "batch_abc123",
              "status": "pending",
              "warnings": [],
              "providerMetadata": { "gateway": { "asyncJob": { "jobId": "batch_abc123", "status": "queued", "webhookSigningSecret": "whsec_test" } } }
            }
            """
        );

        var result = await CreateTestBatch(server.Client)
            .DoStartBatchAsync(Options(webhookUrl: "https://example.com/batch-webhook"));

        Assert.True(
            JsonNode.DeepEquals(
                new JsonObject
                {
                    ["callbackUrl"] = "https://example.com/batch-webhook",
                    ["requests"] = BatchPromptRequestsJson(),
                },
                server.Calls[0].RequestBodyJson
            )
        );
        // The webhook signing secret rides back on providerMetadata.
        Assert.True(
            JsonEqual(
                """{"gateway":{"asyncJob":{"jobId":"batch_abc123","status":"queued","webhookSigningSecret":"whsec_test"}}}""",
                result.ProviderMetadata
            )
        );
    }

    [Fact]
    public async Task Should_not_send_a_callbackUrl_body_field_when_no_webhookUrl_is_provided()
    {
        var server = CreateServer();
        PrepareBatchStartResponse(server);

        await CreateTestBatch(server.Client).DoStartBatchAsync(Options());

        Assert.False(((JsonObject)server.Calls[0].RequestBodyJson!).ContainsKey("callbackUrl"));
    }

    [Fact]
    public async Task Should_base64_encode_Uint8Array_file_data_in_batch_request_prompts()
    {
        var server = CreateServer();
        PrepareBatchStartResponse(server);
        byte[] bytes = [1, 2, 3, 4];
        var expectedBase64 = Convert.ToBase64String(bytes);
        var prompt = new JsonArray(
            new JsonObject
            {
                ["role"] = "user",
                ["content"] = new JsonArray(
                    new JsonObject
                    {
                        ["type"] = "file",
                        ["data"] = new JsonObject { ["type"] = "data", ["data"] = JsonValue.Create(bytes) },
                        ["mediaType"] = "image/png",
                    }
                ),
            }
        );

        await CreateTestBatch(server.Client)
            .DoStartBatchAsync(
                Options([
                    new TextBatchV4Request
                    {
                        Id = "req-1",
                        ModelId = "test-model-1",
                        Options = new JsonObject { ["prompt"] = prompt },
                    },
                ])
            );

        var data = server.Calls[0].RequestBodyJson!["requests"]![0]!["options"]!["prompt"]![0]!["content"]![0]!["data"];
        Assert.True(JsonEqual($$"""{"type":"data","data":"{{expectedBase64}}"}""", data));
    }

    [Fact]
    public async Task Should_pass_abortSignal_to_fetch_when_provided()
    {
        var server = CreateServer();
        PrepareBatchStartResponse(server);
        using var controller = new CancellationTokenSource();

        await CreateTestBatch(server.Client).DoStartBatchAsync(Options(), controller.Token);

        // The client links the caller's token into the request's: cancelling the caller's token cancels the request's.
        Assert.False(server.Calls[0].RequestToken.IsCancellationRequested);
        Assert.True(server.Calls[0].RequestToken.CanBeCanceled);
    }

    [Fact]
    public async Task Should_preserve_an_AbortError_instead_of_converting_it_to_a_retryable_gateway_error()
    {
        var abortError = new OperationCanceledException("The operation was aborted.");
        using var controller = new CancellationTokenSource();
        var handler = new ThrowingHandler(abortError, controller);

        // An aborted batch start may still have been accepted server-side; surfacing a retryable 500 would invite a duplicate submission.
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateTestBatch(new HttpClient(handler)).DoStartBatchAsync(Options(), controller.Token)
        );
        // HttpClient rethrows the handler's cancellation as its own TaskCanceledException, so the instance differs; it is still a cancellation.
        Assert.Equal(abortError.Message, error.Message);
    }

    [Fact]
    public async Task Should_convert_HTTP_errors_via_the_gateway_error_path()
    {
        var server = CreateServer();
        server.RespondError(
            Start,
            404,
            """{"error":{"message":"Model xyz not found","type":"model_not_found","param":{"modelId":"xyz"}}}"""
        );

        var error = await Assert.ThrowsAsync<GatewayModelNotFoundException>(() =>
            CreateTestBatch(server.Client).DoStartBatchAsync(Options())
        );

        Assert.Equal("Model xyz not found", error.Message);
        Assert.Equal(404, error.StatusCode);
    }
}
