// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using static E2E.Decision.Tests.GatewayRerankingModel.GatewayRerankingModelFixture;

namespace E2E.Decision.Tests.GatewayRerankingModel;

/// <summary>Upstream's <c>describe('doRerank')</c> of <c>gateway-reranking-model.test.ts</c>.</summary>
[Collection("GatewayProviderEnvironment")]
public sealed class DoRerankTests
{
    [Fact]
    public async Task Should_pass_headers_correctly()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoRerankAsync(Call(headers: new Dictionary<string, string?> { ["Custom-Header"] = "test-value" }));

        var headers = server.Calls[0].RequestHeaders;
        Assert.Equal("Bearer test-token", headers["authorization"]);
        Assert.Equal("test-value", headers["custom-header"]);
        Assert.Equal("4", headers["ai-reranking-model-specification-version"]);
        Assert.Equal("cohere/rerank-v3.5", headers["ai-model-id"]);
    }

    [Fact]
    public async Task Should_include_o11y_headers()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(
            server,
            new Dictionary<string, string?>
            {
                ["ai-o11y-deployment-id"] = "deployment-1",
                ["ai-o11y-environment"] = "production",
                ["ai-o11y-region"] = "iad1",
            }
        );

        await model.DoRerankAsync(Call());

        var headers = server.Calls[0].RequestHeaders;
        Assert.Equal("deployment-1", headers["ai-o11y-deployment-id"]);
        Assert.Equal("production", headers["ai-o11y-environment"]);
        Assert.Equal("iad1", headers["ai-o11y-region"]);
    }

    [Fact]
    public async Task Should_extract_ranking_from_response()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, DummyRanking);
        using var model = CreateTestModel(server);

        var result = await model.DoRerankAsync(Call());

        Assert.Equal(
            [
                new RerankingModelV4Ranking(0, 0.89),
                new RerankingModelV4Ranking(2, 0.15),
                new RerankingModelV4Ranking(1, 0.12),
            ],
            result.Ranking
        );
    }

    [Fact]
    public async Task Should_extract_warnings()
    {
        var server = new TestServer();
        PrepareJsonResponse(
            server,
            warnings: """[{"type":"unsupported","feature":"topN","details":"This model ignores topN."}]"""
        );
        using var model = CreateTestModel(server);

        var result = await model.DoRerankAsync(Call());

        Assert.Equal([new SharedV4UnsupportedWarning("topN", "This model ignores topN.")], result.Warnings);
    }

    [Fact]
    public async Task Should_default_warnings_to_an_empty_array_when_absent()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        var result = await model.DoRerankAsync(Call());

        Assert.Empty(result.Warnings!);
    }

    [Fact]
    public async Task Should_send_documents_and_query_in_request_body()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoRerankAsync(Call(topN: 2));

        AssertJsonEqual(
            JsonNode.Parse(
                $$"""{"documents":{{TestDocumentsJson}},"query":"What is the capital of France?","topN":2}"""
            ),
            server.Calls[0].RequestBodyJson
        );
    }

    [Fact]
    public async Task Should_pass_providerOptions_into_request_body()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoRerankAsync(
            Call(
                providerOptions: GatewayDecisionModel.GatewayDecisionModelFixture.Options(
                    """{"cohere":{"maxTokensPerDoc":512}}"""
                )
            )
        );

        var body = server.Calls[0].RequestBodyJson!;
        AssertJsonEqual(JsonNode.Parse("""{"cohere":{"maxTokensPerDoc":512}}"""), body["providerOptions"]);
    }

    [Fact]
    public async Task Should_omit_topN_when_not_provided()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoRerankAsync(Call());

        AssertJsonEqual(
            JsonNode.Parse($$"""{"documents":{{TestDocumentsJson}},"query":"What is the capital of France?"}"""),
            server.Calls[0].RequestBodyJson
        );
    }

    [Fact]
    public async Task Should_return_response_headers()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, headers: new Dictionary<string, string> { ["x-request-id"] = "req-123" });
        using var model = CreateTestModel(server);

        var result = await model.DoRerankAsync(Call());

        Assert.Equal("req-123", result.Response!.Headers!["x-request-id"]);
    }

    [Fact]
    public async Task Should_return_provider_metadata()
    {
        var server = new TestServer();
        server.RespondJson(
            RerankingUrl,
            """{"ranking":""" + DummyRanking + ""","providerMetadata":{"gateway":{"cost":"0.002"}}}"""
        );
        using var model = CreateTestModel(server);

        var result = await model.DoRerankAsync(Call());

        Assert.NotNull(result.ProviderMetadata);
    }
}
