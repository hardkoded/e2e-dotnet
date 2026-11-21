// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayImageModel.GatewayImageModelFixture;

namespace E2E.Decision.Tests.GatewayImageModel;

/// <summary>Upstream's <c>describe('doGenerate')</c> of <c>gateway-image-model.test.ts</c>.</summary>
[Collection("GatewayProviderEnvironment")]
public sealed class DoGenerateTests
{
    [Fact]
    public async Task Should_send_correct_request_headers()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoGenerateAsync(Call("A beautiful sunset over mountains"));

        var headers = server.Calls[0].RequestHeaders;
        Assert.Equal("Bearer test-token", headers["authorization"]);
        Assert.Equal("4", headers["ai-image-model-specification-version"]);
        Assert.Equal(TestModelId, headers["ai-model-id"]);
    }

    [Fact]
    public async Task Should_send_correct_request_body_with_all_parameters()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, """["base64-1","base64-2"]""");
        using var model = CreateTestModel(server);

        await model.DoGenerateAsync(
            Call(
                "A cat playing piano",
                n: 2,
                size: "1024x1024",
                aspectRatio: "16:9",
                seed: 42,
                providerOptions: """{"vertex":{"safetySettings":"block_none"}}"""
            )
        );

        AssertJsonEqual(
            Json(
                """{"prompt":"A cat playing piano","n":2,"size":"1024x1024","aspectRatio":"16:9","seed":42,"providerOptions":{"vertex":{"safetySettings":"block_none"}}}"""
            ),
            server.Calls[0].RequestBodyJson
        );
    }

    [Fact]
    public async Task Should_omit_optional_parameters_when_not_provided()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoGenerateAsync(Call("A simple prompt"));

        var requestBody = server.Calls[0].RequestBodyJson!.AsObject();
        AssertJsonEqual(Json("""{"prompt":"A simple prompt","n":1,"providerOptions":{}}"""), requestBody);
        Assert.False(requestBody.ContainsKey("size"));
        Assert.False(requestBody.ContainsKey("aspectRatio"));
        Assert.False(requestBody.ContainsKey("seed"));
    }

    [Fact]
    public async Task Should_pass_seed_when_it_is_0()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoGenerateAsync(Call("A simple prompt", seed: 0));

        Assert.Equal(0, server.Calls[0].RequestBodyJson!["seed"]!.GetValue<int>());
    }

    [Fact]
    public async Task Should_return_images_array_correctly()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, """["base64-image-1","base64-image-2"]""");
        using var model = CreateTestModel(server);

        var result = await model.DoGenerateAsync(Call(n: 2));

        Assert.Equal(["base64-image-1", "base64-image-2"], result.Images);
    }

    [Fact]
    public async Task Should_preserve_response_retryability()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, "[]", isRetryable: false);
        using var model = CreateTestModel(server);

        var result = await model.DoGenerateAsync(Call());

        Assert.False(result.IsRetryable);
    }

    [Fact]
    public async Task Should_return_provider_metadata_correctly()
    {
        var server = new TestServer();
        const string metadata =
            """{"vertex":{"images":[{"revisedPrompt":"Revised prompt 1"},{"revisedPrompt":"Revised prompt 2"}]},"gateway":{"routing":{"provider":"vertex"},"cost":"0.08"}}""";
        PrepareJsonResponse(server, """["base64-1","base64-2"]""", providerMetadata: metadata);
        using var model = CreateTestModel(server);

        var result = await model.DoGenerateAsync(Call(n: 2));

        AssertJsonEqual(Json(metadata), JsonSerializer.SerializeToNode(result.ProviderMetadata));
    }

    [Fact]
    public async Task Should_handle_provider_metadata_without_images_field()
    {
        var server = new TestServer();
        const string metadata = """{"gateway":{"routing":{"provider":"vertex"},"cost":"0.04"}}""";
        PrepareJsonResponse(server, """["base64-1"]""", providerMetadata: metadata);
        using var model = CreateTestModel(server);

        var result = await model.DoGenerateAsync(Call());

        AssertJsonEqual(Json(metadata), JsonSerializer.SerializeToNode(result.ProviderMetadata));
    }

    [Fact]
    public async Task Should_handle_empty_provider_metadata()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, """["base64-1"]""", providerMetadata: "{}");
        using var model = CreateTestModel(server);

        var result = await model.DoGenerateAsync(Call());

        Assert.Empty(result.ProviderMetadata!);
    }

    [Fact]
    public async Task Should_handle_undefined_provider_metadata()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, """["base64-1"]""");
        using var model = CreateTestModel(server);

        var result = await model.DoGenerateAsync(Call());

        Assert.Null(result.ProviderMetadata);
    }

    [Fact]
    public async Task Should_return_warnings_when_provided()
    {
        var server = new TestServer();
        PrepareJsonResponse(
            server,
            """["base64-1"]""",
            warnings: """[{"type":"other","message":"Setting not supported"}]"""
        );
        using var model = CreateTestModel(server);

        var result = await model.DoGenerateAsync(Call());

        Assert.Equal([new SharedV4OtherWarning("Setting not supported")], result.Warnings);
    }

    [Fact]
    public async Task Should_return_deprecated_warnings_correctly()
    {
        var server = new TestServer();
        PrepareJsonResponse(
            server,
            """["base64-1"]""",
            warnings: """[{"type":"deprecated","setting":"size","message":"Use `aspectRatio` instead."}]"""
        );
        using var model = CreateTestModel(server);

        var result = await model.DoGenerateAsync(Call());

        Assert.Equal([new SharedV4DeprecatedWarning("size", "Use `aspectRatio` instead.")], result.Warnings);
    }

    [Fact]
    public async Task Should_return_unsupported_warnings_correctly()
    {
        var server = new TestServer();
        PrepareJsonResponse(
            server,
            """["base64-1"]""",
            warnings: """[{"type":"unsupported","feature":"size","details":"This model does not support the `size` option. Use `aspectRatio` instead."}]"""
        );
        using var model = CreateTestModel(server);

        var result = await model.DoGenerateAsync(Call(size: "1024x1024"));

        Assert.Equal(
            [
                new SharedV4UnsupportedWarning(
                    "size",
                    "This model does not support the `size` option. Use `aspectRatio` instead."
                ),
            ],
            result.Warnings
        );
    }

    [Fact]
    public async Task Should_return_compatibility_warnings_correctly()
    {
        var server = new TestServer();
        PrepareJsonResponse(
            server,
            """["base64-1"]""",
            warnings: """[{"type":"compatibility","feature":"seed","details":"Seed support is approximate for this model."}]"""
        );
        using var model = CreateTestModel(server);

        var result = await model.DoGenerateAsync(Call(seed: 42));

        Assert.Equal(
            [new SharedV4CompatibilityWarning("seed", "Seed support is approximate for this model.")],
            result.Warnings
        );
    }

    [Fact]
    public async Task Should_handle_mixed_warning_types()
    {
        var server = new TestServer();
        PrepareJsonResponse(
            server,
            """["base64-1"]""",
            warnings: """[{"type":"unsupported","feature":"size"},{"type":"compatibility","feature":"seed","details":"Approximate seed support."},{"type":"other","message":"Rate limit approaching."}]"""
        );
        using var model = CreateTestModel(server);

        var result = await model.DoGenerateAsync(Call(size: "1024x1024", seed: 42));

        Assert.Equal(
            [
                new SharedV4UnsupportedWarning("size"),
                new SharedV4CompatibilityWarning("seed", "Approximate seed support."),
                new SharedV4OtherWarning("Rate limit approaching."),
            ],
            result.Warnings
        );
    }

    [Fact]
    public async Task Should_return_empty_warnings_array_when_not_provided()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, """["base64-1"]""");
        using var model = CreateTestModel(server);

        var result = await model.DoGenerateAsync(Call());

        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Should_include_response_metadata()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, """["base64-1"]""");
        using var model = CreateTestModel(server);

        var result = await model.DoGenerateAsync(Call());

        Assert.Equal(TestModelId, result.Response.ModelId);
        Assert.NotEqual(default, result.Response.Timestamp);
        Assert.NotNull(result.Response.Headers);
    }

    [Fact]
    public async Task Should_return_usage_when_provided()
    {
        var server = new TestServer();
        server.RespondJson(
            ImageUrl,
            """{"images":["base64-1"],"usage":{"inputTokens":27,"outputTokens":6240,"totalTokens":6267}}"""
        );
        using var model = CreateTestModel(server);

        var result = await model.DoGenerateAsync(Call());

        Assert.Equal(new ImageModelV4Usage(27, 6240, 6267), result.Usage);
    }

    [Fact]
    public async Task Should_return_usage_with_partial_token_counts()
    {
        var server = new TestServer();
        server.RespondJson(ImageUrl, """{"images":["base64-1"],"usage":{"inputTokens":10}}""");
        using var model = CreateTestModel(server);

        var result = await model.DoGenerateAsync(Call());

        Assert.Equal(new ImageModelV4Usage(10, null, null), result.Usage);
    }

    [Fact]
    public async Task Should_not_include_usage_when_not_provided()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, """["base64-1"]""");
        using var model = CreateTestModel(server);

        var result = await model.DoGenerateAsync(Call());

        Assert.Null(result.Usage);
    }

    [Fact]
    public async Task Should_merge_custom_headers_with_config_headers()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoGenerateAsync(
            Call(headers: new Dictionary<string, string?> { ["X-Custom-Header"] = "custom-value" })
        );

        var headers = server.Calls[0].RequestHeaders;
        Assert.Equal("Bearer test-token", headers["authorization"]);
        Assert.Equal("custom-value", headers["x-custom-header"]);
        Assert.Equal("4", headers["ai-image-model-specification-version"]);
        Assert.Equal(TestModelId, headers["ai-model-id"]);
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
                ["ai-o11y-deployment-id"] = "dpl_123",
                ["ai-o11y-environment"] = "production",
            }
        );

        await model.DoGenerateAsync(Call());

        var headers = server.Calls[0].RequestHeaders;
        Assert.Equal("dpl_123", headers["ai-o11y-deployment-id"]);
        Assert.Equal("production", headers["ai-o11y-environment"]);
    }

    [Fact]
    public async Task Should_pass_abort_signal_to_fetch()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);
        using var abortController = new CancellationTokenSource();

        await model.DoGenerateAsync(Call(), abortController.Token);

        Assert.Single(server.Calls);
    }

    [Fact]
    public async Task Should_handle_API_errors_correctly()
    {
        var server = new TestServer();
        server.RespondError(ImageUrl, 400, """{"error":{"message":"Invalid request","code":"invalid_request"}}""");
        using var model = CreateTestModel(server);

        await Assert.ThrowsAnyAsync<Exception>(() => model.DoGenerateAsync(Call()));
    }

    [Fact]
    public async Task Should_handle_authentication_errors()
    {
        var server = new TestServer();
        server.RespondError(ImageUrl, 401, """{"error":{"message":"Unauthorized","code":"unauthorized"}}""");
        using var model = CreateTestModel(server);

        await Assert.ThrowsAnyAsync<Exception>(() => model.DoGenerateAsync(Call()));
    }

    [Fact]
    public async Task Should_include_providerOptions_object_in_request_body()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoGenerateAsync(
            Call(providerOptions: """{"vertex":{"safetySettings":"block_none"},"openai":{"style":"vivid"}}""")
        );

        AssertJsonEqual(
            Json(
                """{"prompt":"Test prompt","n":1,"providerOptions":{"vertex":{"safetySettings":"block_none"},"openai":{"style":"vivid"}}}"""
            ),
            server.Calls[0].RequestBodyJson
        );
    }

    [Fact]
    public async Task Should_handle_empty_provider_options()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        using var model = CreateTestModel(server);

        await model.DoGenerateAsync(Call());

        AssertJsonEqual(
            Json("""{"prompt":"Test prompt","n":1,"providerOptions":{}}"""),
            server.Calls[0].RequestBodyJson
        );
    }

    [Fact]
    public async Task Should_handle_different_model_IDs()
    {
        var server = new TestServer();
        PrepareJsonResponse(server);
        const string customModelId = "openai/dall-e-3";
        using var model = new E2E.Decision.Sdk.Gateway.GatewayImageModel(
            customModelId,
            Config(server, withAuthorization: false)
        );

        await model.DoGenerateAsync(Call());

        Assert.Equal(customModelId, server.Calls[0].RequestHeaders["ai-model-id"]);
    }

    [Fact]
    public async Task Should_handle_complex_provider_metadata_with_multiple_providers()
    {
        var server = new TestServer();
        const string metadata = """
            {
              "vertex": { "images": [{ "revisedPrompt": "Revised 1" }, { "revisedPrompt": "Revised 2" }], "usage": { "tokens": 150 } },
              "gateway": {
                "routing": {
                  "provider": "vertex",
                  "attempts": [{ "provider": "openai", "success": false }, { "provider": "vertex", "success": true }]
                },
                "cost": "0.08",
                "marketCost": "0.12",
                "generationId": "gen-xyz-789"
              }
            }
            """;
        PrepareJsonResponse(server, """["base64-1","base64-2"]""", providerMetadata: metadata);
        using var model = CreateTestModel(server);

        var result = await model.DoGenerateAsync(Call(n: 2));

        AssertJsonEqual(Json(metadata), JsonSerializer.SerializeToNode(result.ProviderMetadata));
    }
}
