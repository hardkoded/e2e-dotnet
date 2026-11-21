// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayBatch.GatewayBatchFixture;

namespace E2E.Decision.Tests.GatewayBatch;

/// <summary>Upstream's <c>describe('doGetBatchResults')</c> of <c>gateway-batch.test.ts</c>.</summary>
public sealed class DoGetBatchResultsTests
{
    private const string SucceededItem = """
        {"type":"text","id":"req-1","status":"succeeded","result":{"content":[{"type":"text","text":"pong 1"}],"finishReason":{"unified":"stop"},"response":{"modelId":"openai/gpt-5.6-luna","timestamp":"2026-08-18T00:00:00.000Z"},"usage":{"inputTokens":{"total":4,"noCache":4},"outputTokens":{"total":2,"text":2}},"warnings":[]}}
        """;

    private const string FailedItem = """{"type":"text","id":"req-2","status":"failed","error":{"message":"boom"}}""";

    private const string CancelledItem = """{"type":"text","id":"req-3","status":"cancelled"}""";

    private static BatchV4OperationOptions Job(string batchId = "job_123") => new() { BatchId = batchId };

    // What the parsed stream yields: the wire's ISO timestamp revived to a date.
    private static JsonNode ExpectedSucceededItem()
    {
        var item = JsonNode.Parse(SucceededItem)!;
        item["result"]!["response"]!["timestamp"] = JsonValue.Create(DateTimeOffset.Parse("2026-08-18T00:00:00.000Z"));
        return item;
    }

    [Theory]
    [InlineData(16)]
    [InlineData(4096)]
    public async Task Applies_the_factory_maxLineBytes_setting_of(int maxLineBytes)
    {
        var server = CreateServer();
        PrepareBatchResultsResponse(server, SucceededItem.Trim() + "\n");
        var batch = new E2E.Decision.Sdk.Gateway.GatewayProvider(
            new GatewayProviderSettings
            {
                ApiKey = "test-api-key",
                BaseUrl = "https://api.test.com",
                BatchResultDownloads = new GatewayBatchResultDownloads { MaxLineBytes = maxLineBytes },
                HttpClient = server.Client,
            }
        ).ExperimentalBatch();

        var stream = await batch.DoGetBatchResultsAsync(Job());
        if (maxLineBytes == 16)
        {
            var error = await Assert.ThrowsAsync<DownloadException>(() => ToListAsync(stream));
            Assert.Equal("https://api.test.com/batch/results", error.Url);
        }
        else
        {
            Assert.Single(await ToListAsync(stream));
        }
    }

    [Fact]
    public async Task Should_revive_response_timestamp_into_a_Date_on_succeeded_items()
    {
        var server = CreateServer();
        PrepareBatchResultsResponse(server, SucceededItem.Trim() + "\n");

        var stream = await CreateTestBatch(server.Client).DoGetBatchResultsAsync(Job());
        var item = Assert.Single(await ToListAsync(stream));

        Assert.Equal("succeeded", item.Status);
        var timestamp = item.Result!["response"]!["timestamp"]!.AsValue();
        Assert.True(timestamp.TryGetValue<DateTimeOffset>(out var date));
        Assert.Equal("2026-08-18T00:00:00.000Z", date.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"));
        // Nested V4 usage is the shape core dereferences (usage.inputTokens.total / usage.outputTokens.total).
        Assert.True(
            JsonEqual(
                """{"inputTokens":{"total":4,"noCache":4},"outputTokens":{"total":2,"text":2}}""",
                item.Result["usage"]
            )
        );
    }

    [Fact]
    public async Task Should_map_a_404_not_found_response_to_GatewayNotFoundError()
    {
        var server = CreateServer();
        server.RespondError(Results, 404, NotFoundBody);

        var error = await Assert.ThrowsAsync<GatewayNotFoundException>(() =>
            CreateTestBatch(server.Client).DoGetBatchResultsAsync(Job("missing-job"))
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
                .DoGetBatchResultsAsync(Job(), controller.Token)
        );

        // HttpClient rethrows the handler's cancellation as its own TaskCanceledException, so the instance differs; it is still a cancellation.
        Assert.Equal(abortError.Message, error.Message);
    }

    [Fact]
    public async Task Should_post_the_batchId_with_the_correct_headers()
    {
        var server = CreateServer();
        PrepareBatchResultsResponse(server, SucceededItem.Trim() + "\n");

        var stream = await CreateTestBatch(server.Client).DoGetBatchResultsAsync(Job());
        await ToListAsync(stream);

        Assert.True(JsonEqual("""{"batchId":"job_123"}""", server.Calls[0].RequestBodyJson));
        Assert.Equal("Bearer test-token", server.Calls[0].RequestHeaders["authorization"]);
        Assert.False(server.Calls[0].RequestHeaders.ContainsKey("ai-model-id"));
    }

    [Fact]
    public async Task Should_parse_multi_line_NDJSON_results_including_lines_split_across_chunks_and_a_trailing_line_without_a_newline()
    {
        var server = CreateServer();
        var line1 = JsonNode.Parse(SucceededItem)!.ToJsonString();
        var line2 = JsonNode.Parse(FailedItem)!.ToJsonString();
        var line3 = JsonNode.Parse(CancelledItem)!.ToJsonString();
        PrepareBatchResultsResponse(
            server,
            // line 1 split across two chunks
            line1[..25],
            line1[25..] + "\n" + line2[..10],
            // line 2 completes; line 3 has no trailing newline
            line2[10..] + "\n" + line3
        );

        var stream = await CreateTestBatch(server.Client).DoGetBatchResultsAsync(Job());
        var items = await ToListAsync(stream);

        Assert.Equal(3, items.Count);
        Assert.True(JsonNode.DeepEquals(ExpectedSucceededItem(), items[0].Raw));
        Assert.True(JsonEqual(FailedItem, items[1].Raw));
        Assert.Equal("boom", items[1].Error!.Message);
        Assert.True(JsonEqual(CancelledItem, items[2].Raw));
    }

    [Fact]
    public async Task Should_skip_empty_lines()
    {
        var server = CreateServer();
        PrepareBatchResultsResponse(server, SucceededItem.Trim() + "\n\n" + FailedItem.Trim() + "\n");

        var stream = await CreateTestBatch(server.Client).DoGetBatchResultsAsync(Job());
        var items = await ToListAsync(stream);

        Assert.Equal(["req-1", "req-2"], items.Select(item => item.Id));
        Assert.True(JsonNode.DeepEquals(ExpectedSucceededItem(), items[0].Raw));
        Assert.True(JsonEqual(FailedItem, items[1].Raw));
    }

    [Fact]
    public async Task Should_convert_a_400_for_a_non_terminal_batch_via_the_gateway_error_path()
    {
        var server = CreateServer();
        server.RespondError(
            Results,
            400,
            """{"error":{"message":"The batch is not yet terminal.","type":"invalid_request_error"}}"""
        );

        var error = await Assert.ThrowsAsync<GatewayInvalidRequestException>(() =>
            CreateTestBatch(server.Client).DoGetBatchResultsAsync(Job())
        );

        Assert.Equal("The batch is not yet terminal.", error.Message);
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task Should_pass_abortSignal_to_fetch_when_provided()
    {
        var server = CreateServer();
        PrepareBatchResultsResponse(server, SucceededItem.Trim() + "\n");
        using var controller = new CancellationTokenSource();

        var stream = await CreateTestBatch(server.Client).DoGetBatchResultsAsync(Job(), controller.Token);
        await ToListAsync(stream);

        Assert.True(server.Calls[0].RequestToken.CanBeCanceled);
    }
}
