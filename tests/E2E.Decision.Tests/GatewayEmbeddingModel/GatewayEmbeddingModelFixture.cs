// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk.Gateway;

namespace E2E.Decision.Tests.GatewayEmbeddingModel;

/// <summary>The constants and helpers at the top of upstream's <c>gateway-embedding-model.test.ts</c>.</summary>
internal static class GatewayEmbeddingModelFixture
{
    public const string EmbeddingUrl = "https://api.test.com/embedding-model";

    public const string DummyEmbeddings = "[[0.1,0.2,0.3],[0.4,0.5,0.6]]";

    public static readonly IReadOnlyList<string> TestValues = ["sunny day at the beach", "rainy afternoon in the city"];

    public static JsonNode TestValuesJson => new JsonArray(TestValues.Select(v => (JsonNode?)v).ToArray());

    /// <summary>Upstream's <c>createTestModel</c>.</summary>
    public static E2E.Decision.Sdk.Gateway.GatewayEmbeddingModel CreateTestModel(
        TestServer server,
        IReadOnlyDictionary<string, string?>? o11yHeaders = null
    ) =>
        new(
            "openai/text-embedding-3-small",
            new GatewayEmbeddingConfig
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
        string embeddings = DummyEmbeddings,
        string usage = """{"tokens":8}""",
        string? warnings = null,
        IReadOnlyDictionary<string, string>? headers = null
    )
    {
        var body = new JsonObject { ["embeddings"] = JsonNode.Parse(embeddings), ["usage"] = JsonNode.Parse(usage) };
        if (warnings is not null)
        {
            body["warnings"] = JsonNode.Parse(warnings);
        }

        server.RespondJson(EmbeddingUrl, body.ToJsonString(), headers);
    }

    public static IReadOnlyDictionary<string, System.Text.Json.JsonElement> Options(string json) =>
        GatewayDecisionModel.GatewayDecisionModelFixture.Options(json);

    public static void AssertJsonEqual(JsonNode? expected, JsonNode? actual) =>
        GatewayDecisionModel.GatewayDecisionModelFixture.AssertJsonEqual(expected, actual);

    public static IReadOnlyList<IReadOnlyList<double>> Embeddings(string json) =>
        JsonNode
            .Parse(json)!
            .AsArray()
            .Select(e => (IReadOnlyList<double>)e!.AsArray().Select(n => n!.GetValue<double>()).ToList())
            .ToList();
}
