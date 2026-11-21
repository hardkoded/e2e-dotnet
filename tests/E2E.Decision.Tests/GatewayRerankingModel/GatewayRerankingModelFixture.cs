// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.GatewayRerankingModel;

/// <summary>The constants and helpers at the top of upstream's <c>gateway-reranking-model.test.ts</c>.</summary>
internal static class GatewayRerankingModelFixture
{
    public const string RerankingUrl = "https://api.test.com/reranking-model";

    public const string DummyRanking =
        """[{"index":0,"relevanceScore":0.89},{"index":2,"relevanceScore":0.15},{"index":1,"relevanceScore":0.12}]""";

    public static readonly RerankingModelV4TextDocuments TestDocuments = new([
        "Paris is the capital of France.",
        "Berlin is the capital of Germany.",
        "Madrid is the capital of Spain.",
    ]);

    public const string TestDocumentsJson =
        """{"type":"text","values":["Paris is the capital of France.","Berlin is the capital of Germany.","Madrid is the capital of Spain."]}""";

    public const string TestQuery = "What is the capital of France?";

    /// <summary>Upstream's <c>createTestModel</c>.</summary>
    public static E2E.Decision.Sdk.Gateway.GatewayRerankingModel CreateTestModel(
        TestServer server,
        IReadOnlyDictionary<string, string?>? o11yHeaders = null
    ) =>
        new(
            "cohere/rerank-v3.5",
            new GatewayRerankingConfig
            {
                Provider = "gateway",
                BaseUrl = "https://api.test.com",
                Headers = _ =>
                    Task.FromResult<IReadOnlyDictionary<string, string?>>(
                        new Dictionary<string, string?>
                        {
                            ["Authorization"] = "Bearer test-token",
                            ["ai-gateway-auth-method"] = "api-key",
                        }
                    ),
                HttpClient = server.Client,
                O11yHeaders = _ => Task.FromResult(o11yHeaders ?? new Dictionary<string, string?>()),
            }
        );

    /// <summary>Upstream's <c>prepareJsonResponse</c>.</summary>
    public static void PrepareJsonResponse(
        TestServer server,
        string ranking = DummyRanking,
        string? warnings = null,
        IReadOnlyDictionary<string, string>? headers = null
    )
    {
        var body = new JsonObject { ["ranking"] = JsonNode.Parse(ranking) };
        if (warnings is not null)
        {
            body["warnings"] = JsonNode.Parse(warnings);
        }

        server.RespondJson(RerankingUrl, body.ToJsonString(), headers);
    }

    public static RerankingModelV4CallOptions Call(
        int? topN = null,
        IReadOnlyDictionary<string, string?>? headers = null,
        IReadOnlyDictionary<string, System.Text.Json.JsonElement>? providerOptions = null
    ) =>
        new()
        {
            Documents = TestDocuments,
            Query = TestQuery,
            TopN = topN,
            Headers = headers,
            ProviderOptions = providerOptions,
        };

    public static void AssertJsonEqual(JsonNode? expected, JsonNode? actual) =>
        GatewayDecisionModel.GatewayDecisionModelFixture.AssertJsonEqual(expected, actual);
}
