// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayEmbeddingModel.GatewayEmbeddingModelFixture;

namespace E2E.Decision.Tests.GatewayEmbeddingModel;

/// <summary>Upstream's <c>describe('doEmbed')</c> of <c>gateway-embedding-model.test.ts</c>.</summary>
[Collection("GatewayProviderEnvironment")]
public sealed class DoEmbedTests
{
    [Fact]
    public async Task Should_forward_dimensions_alongside_provider_options_for_server_side_resolution()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoEmbedAsync(
            new EmbeddingModelV4CallOptions
            {
                Values = TestValues,
                Dimensions = 256,
                ProviderOptions = Options("""{"openai":{"dimensions":512}}"""),
            }
        );

        AssertJsonEqual(
            System.Text.Json.Nodes.JsonNode.Parse(
                """{"values":["sunny day at the beach","rainy afternoon in the city"],"dimensions":256,"providerOptions":{"openai":{"dimensions":512}}}"""
            ),
            server.Calls[0].RequestBodyJson
        );
    }

    [Fact]
    public async Task Should_forward_dimensions_without_provider_options()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoEmbedAsync(new EmbeddingModelV4CallOptions { Values = TestValues, Dimensions = 256 });

        AssertJsonEqual(
            System.Text.Json.Nodes.JsonNode.Parse(
                """{"values":["sunny day at the beach","rainy afternoon in the city"],"dimensions":256}"""
            ),
            server.Calls[0].RequestBodyJson
        );
    }

    [Fact]
    public async Task Should_pass_headers_correctly()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoEmbedAsync(
            new EmbeddingModelV4CallOptions
            {
                Values = TestValues,
                Headers = new Dictionary<string, string?> { ["Custom-Header"] = "test-value" },
            }
        );

        var headers = server.Calls[0].RequestHeaders;
        Assert.Equal("Bearer test-token", headers["authorization"]);
        Assert.Equal("test-value", headers["custom-header"]);
        Assert.Equal("4", headers["ai-embedding-model-specification-version"]);
        Assert.Equal("openai/text-embedding-3-small", headers["ai-model-id"]);
    }

    [Fact]
    public async Task Should_include_o11y_headers()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        var o11yHeaders = new Dictionary<string, string?>
        {
            ["ai-o11y-deployment-id"] = "deployment-1",
            ["ai-o11y-environment"] = "production",
            ["ai-o11y-region"] = "iad1",
        };
        using var model = CreateTestModel(server, o11yHeaders);

        await model.DoEmbedAsync(new EmbeddingModelV4CallOptions { Values = TestValues });

        var headers = server.Calls[0].RequestHeaders;
        Assert.Equal("deployment-1", headers["ai-o11y-deployment-id"]);
        Assert.Equal("production", headers["ai-o11y-environment"]);
        Assert.Equal("iad1", headers["ai-o11y-region"]);
    }

    [Fact]
    public async Task Should_extract_embeddings_and_usage()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, DummyEmbeddings, """{"tokens":42}""");
        using var model = CreateTestModel(server);

        var result = await model.DoEmbedAsync(new EmbeddingModelV4CallOptions { Values = TestValues });

        Assert.Equal(Embeddings(DummyEmbeddings), result.Embeddings);
        Assert.Equal(new EmbeddingModelV4Usage(42), result.Usage);
    }

    [Fact]
    public async Task Should_extract_warnings()
    {
        var server = new TestServer();
        PrepareJsonResponse(
            server,
            warnings: """[{"type":"deprecated","setting":"dimensions","message":"Use outputDimensions instead."}]"""
        );
        using var model = CreateTestModel(server);

        var result = await model.DoEmbedAsync(new EmbeddingModelV4CallOptions { Values = TestValues });

        Assert.Equal([new SharedV4DeprecatedWarning("dimensions", "Use outputDimensions instead.")], result.Warnings);
    }

    [Fact]
    public async Task Should_default_warnings_to_an_empty_array_when_absent()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        var result = await model.DoEmbedAsync(new EmbeddingModelV4CallOptions { Values = TestValues });

        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Should_send_value_as_array()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoEmbedAsync(new EmbeddingModelV4CallOptions { Values = TestValues });

        AssertJsonEqual(
            System.Text.Json.Nodes.JsonNode.Parse(
                """{"values":["sunny day at the beach","rainy afternoon in the city"]}"""
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

        await model.DoEmbedAsync(
            new EmbeddingModelV4CallOptions
            {
                Values = TestValues,
                ProviderOptions = Options("""{"openai":{"dimensions":64}}"""),
            }
        );

        AssertJsonEqual(
            System.Text.Json.Nodes.JsonNode.Parse(
                """{"values":["sunny day at the beach","rainy afternoon in the city"],"providerOptions":{"openai":{"dimensions":64}}}"""
            ),
            server.Calls[0].RequestBodyJson
        );
    }

    [Fact]
    public async Task Should_not_include_providerOptions_when_not_provided()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoEmbedAsync(new EmbeddingModelV4CallOptions { Values = TestValues });

        var body = server.Calls[0].RequestBodyJson!.AsObject();
        AssertJsonEqual(
            System.Text.Json.Nodes.JsonNode.Parse(
                """{"values":["sunny day at the beach","rainy afternoon in the city"]}"""
            ),
            body
        );
        Assert.False(body.ContainsKey("providerOptions"));
    }

    [Fact]
    public async Task Should_convert_gateway_error_responses()
    {
        var server = new TestServer();
        server.RespondError(
            EmbeddingUrl,
            400,
            """{"error":{"message":"Invalid input","type":"invalid_request_error"}}"""
        );
        using var model = CreateTestModel(server);

        var invalid = await Assert.ThrowsAsync<GatewayInvalidRequestException>(() =>
            model.DoEmbedAsync(new EmbeddingModelV4CallOptions { Values = TestValues })
        );
        Assert.Equal(400, invalid.StatusCode);

        server.RespondError(
            EmbeddingUrl,
            500,
            """{"error":{"message":"Server blew up","type":"internal_server_error"}}"""
        );

        var internalError = await Assert.ThrowsAsync<GatewayInternalServerException>(() =>
            model.DoEmbedAsync(new EmbeddingModelV4CallOptions { Values = TestValues })
        );
        Assert.Equal(500, internalError.StatusCode);
    }

    [Fact]
    public async Task Should_include_providerMetadata_in_response_body()
    {
        var server = new TestServer();
        server.RespondJson(
            EmbeddingUrl,
            """{"embeddings":[[0.1,0.2,0.3],[0.4,0.5,0.6]],"usage":{"tokens":5},"providerMetadata":{"gateway":{"routing":{"test":true}}}}"""
        );
        using var model = CreateTestModel(server);

        var result = await model.DoEmbedAsync(new EmbeddingModelV4CallOptions { Values = TestValues });

        Assert.True(
            result
                .Response!.Body!.Value.GetProperty("providerMetadata")
                .GetProperty("gateway")
                .GetProperty("routing")
                .GetProperty("test")
                .GetBoolean()
        );
    }

    [Fact]
    public async Task Should_extract_providerMetadata_to_top_level()
    {
        var server = new TestServer();
        server.RespondJson(
            EmbeddingUrl,
            """{"embeddings":[[0.1,0.2,0.3],[0.4,0.5,0.6]],"usage":{"tokens":5},"providerMetadata":{"gateway":{"routing":{"test":true}}}}"""
        );
        using var model = CreateTestModel(server);

        var result = await model.DoEmbedAsync(new EmbeddingModelV4CallOptions { Values = TestValues });

        AssertJsonEqual(
            System.Text.Json.Nodes.JsonNode.Parse("""{"gateway":{"routing":{"test":true}}}"""),
            System.Text.Json.JsonSerializer.SerializeToNode(result.ProviderMetadata)
        );
    }
}
