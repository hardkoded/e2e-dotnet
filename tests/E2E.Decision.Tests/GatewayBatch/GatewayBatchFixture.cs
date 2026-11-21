// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.GatewayBatch;

/// <summary>The helpers at the top of upstream's <c>gateway-batch.test.ts</c>.</summary>
internal static class GatewayBatchFixture
{
    public const string Start = "https://api.test.com/batch/start";
    public const string Status = "https://api.test.com/batch/status";
    public const string Results = "https://api.test.com/batch/results";
    public const string Cancel = "https://api.test.com/batch/cancel";

    public const string TestPromptJson = """[{"role":"user","content":[{"type":"text","text":"Hello"}]}]""";

    /// <summary>Upstream's <c>createTestBatch</c>.</summary>
    public static E2E.Decision.Sdk.Gateway.GatewayBatch CreateTestBatch(
        HttpClient client,
        IReadOnlyDictionary<string, string?>? o11yHeaders = null
    ) =>
        new(
            new GatewayBatchConfig
            {
                Provider = "test-provider",
                BaseUrl = "https://api.test.com",
                Headers = _ =>
                    Task.FromResult<IReadOnlyDictionary<string, string?>>(
                        new Dictionary<string, string?>
                        {
                            ["Authorization"] = "Bearer test-token",
                            ["ai-gateway-auth-method"] = "api-key",
                        }
                    ),
                HttpClient = client,
                O11yHeaders = _ => Task.FromResult(o11yHeaders ?? new Dictionary<string, string?>()),
            }
        );

    /// <summary>Upstream's <c>createTestServer</c> with the four batch URLs.</summary>
    public static TestServer CreateServer() => new();

    /// <summary>Upstream's <c>BATCH_PROMPT_REQUESTS</c>.</summary>
    public static IReadOnlyList<BatchV4Request> BatchPromptRequests() =>
        [
            new TextBatchV4Request
            {
                Id = "req-1",
                ModelId = "test-model-1",
                Options = new JsonObject { ["prompt"] = JsonNode.Parse(TestPromptJson) },
            },
            new TextBatchV4Request
            {
                Id = "req-2",
                ModelId = "test-model-1",
                Options = new JsonObject
                {
                    ["prompt"] = JsonNode.Parse(TestPromptJson),
                    ["maxOutputTokens"] = 32,
                    ["temperature"] = 0,
                },
            },
        ];

    /// <summary>The wire form of <see cref="BatchPromptRequests"/>.</summary>
    public static JsonNode BatchPromptRequestsJson() =>
        JsonNode.Parse(
            """
            [
              { "id": "req-1", "type": "text", "modelId": "test-model-1", "options": { "prompt": $P } },
              { "id": "req-2", "type": "text", "modelId": "test-model-1", "options": { "prompt": $P, "maxOutputTokens": 32, "temperature": 0 } }
            ]
            """.Replace("$P", TestPromptJson)
        )!;

    /// <summary>Upstream's <c>prepareBatchStartResponse</c>.</summary>
    public static void PrepareBatchStartResponse(
        TestServer server,
        string body = """{"batchId":"job_123","status":"pending","warnings":[]}"""
    ) => server.RespondJson(Start, body);

    /// <summary>Upstream's <c>prepareBatchStatusResponse</c>.</summary>
    public static void PrepareBatchStatusResponse(TestServer server, string body) => server.RespondJson(Status, body);

    /// <summary>Upstream's <c>prepareBatchResultsResponse</c>.</summary>
    public static void PrepareBatchResultsResponse(TestServer server, params string[] chunks) =>
        server.RespondChunks(Results, chunks, "application/x-ndjson");

    public static bool JsonEqual(string expected, JsonNode? actual) =>
        JsonNode.DeepEquals(JsonNode.Parse(expected), actual);

    public static async Task<List<BatchV4ItemResult>> ToListAsync(IAsyncEnumerable<BatchV4ItemResult> stream)
    {
        var items = new List<BatchV4ItemResult>();
        await foreach (var item in stream)
        {
            items.Add(item);
        }

        return items;
    }

    public static string NotFoundBody => """{"error":{"message":"Async job not found.","type":"not_found"}}""";
}
