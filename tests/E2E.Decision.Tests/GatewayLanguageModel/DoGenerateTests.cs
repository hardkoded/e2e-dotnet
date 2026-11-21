// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayLanguageModel.GatewayLanguageModelFixture;

namespace E2E.Decision.Tests.GatewayLanguageModel;

/// <summary>Upstream's <c>describe('doGenerate')</c> of <c>gateway-language-model.test.ts</c>.</summary>
public sealed class DoGenerateTests
{
    private static LanguageModelV4CallOptions ImageOptions(params LanguageModelV4PromptPart[] parts) =>
        Options([new LanguageModelV4UserMessage(parts)]);

    private static JsonNode PromptContent(TestServerCall call, int index) =>
        call.RequestBodyJson!["prompt"]![0]!["content"]![index]!;

    [Fact]
    public async Task Should_pass_headers_correctly()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, content: """{"type":"text","text":"Hello, World!"}""");
        using var model = CreateTestModel(server);

        await model.DoGenerateAsync(
            Options(headers: new Dictionary<string, string?> { ["Custom-Header"] = "test-value" })
        );

        var headers = server.Calls[0].RequestHeaders;
        Assert.Equal("Bearer test-token", headers["authorization"]);
        Assert.Equal("test-value", headers["custom-header"]);
        Assert.Equal("4", headers["ai-language-model-specification-version"]);
        Assert.Equal("test-model", headers["ai-language-model-id"]);
        Assert.Equal("false", headers["ai-language-model-streaming"]);
    }

    [Fact]
    public async Task Should_extract_text_response()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, content: """{"type":"text","text":"Hello, World!"}""");
        using var model = CreateTestModel(server);

        var result = await model.DoGenerateAsync(Options());

        AssertJsonEqual(
            """{"type":"text","text":"Hello, World!"}""",
            JsonNode.Parse(result.Content!.Value.GetRawText())
        );
    }

    [Fact]
    public async Task Should_extract_usage_information()
    {
        var server = new TestServer();
        PrepareJsonResponse(
            server,
            content: """{"type":"text","text":"Test"}""",
            usage: """{"prompt_tokens":10,"completion_tokens":20}"""
        );
        using var model = CreateTestModel(server);

        var result = await model.DoGenerateAsync(Options());

        AssertJsonEqual(
            """{"prompt_tokens":10,"completion_tokens":20}""",
            JsonNode.Parse(result.Usage!.Value.GetRawText())
        );
    }

    [Fact]
    public async Task Should_forward_warnings_returned_by_the_gateway()
    {
        var server = new TestServer();
        IReadOnlyList<SharedV4Warning> mockWarnings =
        [
            new SharedV4Warning
            {
                Type = "compatibility",
                Feature = "maxOutputTokens",
                Details = "lowered to 38000",
            },
            new SharedV4Warning { Type = "other", Message = "from provider" },
        ];
        PrepareJsonResponse(
            server,
            content: """{"type":"text","text":"Hello, World!"}""",
            warnings: """
            [
              {"type":"compatibility","feature":"maxOutputTokens","details":"lowered to 38000"},
              {"type":"other","message":"from provider"}
            ]
            """
        );
        using var model = CreateTestModel(server);

        var result = await model.DoGenerateAsync(Options());

        Assert.Equal(mockWarnings, result.Warnings);
    }

    [Fact]
    public async Task Should_default_warnings_to_an_empty_array_when_absent()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, content: """{"type":"text","text":"Hello"}""");
        using var model = CreateTestModel(server);

        var result = await model.DoGenerateAsync(Options());

        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Should_remove_abortSignal_from_the_request_body()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, content: """{"type":"text","text":"Test response"}""");
        using var cancellation = new CancellationTokenSource();
        using var model = CreateTestModel(server);

        await model.DoGenerateAsync(Options(), cancellation.Token);

        var requestBody = server.Calls[0].RequestBodyJson!.AsObject();
        Assert.False(requestBody.ContainsKey("abortSignal"));
    }

    [Fact]
    public async Task Should_pass_abortSignal_to_fetch_when_provided()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, content: """{"type":"text","text":"Test response"}""");
        using var cancellation = new CancellationTokenSource();
        var signalPassed = false;
        using var handler = new TokenProbeHandler(server, token =>
        {
            // The request's token follows the caller's.
            cancellation.Cancel();
            signalPassed = token.IsCancellationRequested;
        });
        using var http = new HttpClient(handler);
        using var model = CreateTestModel(http);

        await model.DoGenerateAsync(Options(), cancellation.Token);

        Assert.Equal(1, handler.Calls);
        Assert.True(signalPassed);
    }

    // Upstream asserts that fetch gets no signal. HttpClient always gives its handler a token, so the test checks that no cancellation reaches it.
    [Fact]
    public async Task Partial_Should_not_pass_abortSignal_to_fetch_when_not_provided()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, content: """{"type":"text","text":"Test response"}""");
        var cancelled = true;
        using var handler = new TokenProbeHandler(server, token => cancelled = token.IsCancellationRequested);
        using var http = new HttpClient(handler);
        using var model = CreateTestModel(http);

        await model.DoGenerateAsync(Options());

        Assert.Equal(1, handler.Calls);
        Assert.False(cancelled);
    }

    [Fact]
    public async Task Should_include_o11y_headers_in_the_request()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, content: """{"type":"text","text":"Hello, World!"}""");
        var o11yHeaders = new Dictionary<string, string?>
        {
            ["ai-o11y-deployment-id"] = "test-deployment",
            ["ai-o11y-environment"] = "production",
            ["ai-o11y-region"] = "iad1",
        };
        using var model = CreateTestModel(server, o11yHeaders);

        await model.DoGenerateAsync(Options());

        var headers = server.Calls[0].RequestHeaders;
        foreach (var (name, value) in o11yHeaders)
        {
            Assert.Equal(value, headers[name]);
        }
    }

    [Fact]
    public async Task Should_convert_API_call_errors_to_Gateway_errors()
    {
        var server = new TestServer();
        server.RespondError(
            LanguageModelUrl,
            401,
            """{"error":{"message":"Invalid API key provided","type":"authentication_error"}}"""
        );
        using var model = CreateTestModel(server);

        var authError = await Assert.ThrowsAsync<GatewayAuthenticationException>(() =>
            model.DoGenerateAsync(Options())
        );

        Assert.Contains("Invalid API key", authError.Message, StringComparison.Ordinal);
        Assert.Contains("vercel.com/d?to=", authError.Message, StringComparison.Ordinal);
        Assert.Equal(401, authError.StatusCode);
        Assert.Equal("authentication_error", authError.Type);
    }

    [Fact]
    public async Task Should_handle_malformed_error_responses()
    {
        var server = new TestServer();
        server.RespondError(LanguageModelUrl, 500, "Not JSON");
        using var model = CreateTestModel(server);

        var responseError = await Assert.ThrowsAsync<GatewayResponseException>(() => model.DoGenerateAsync(Options()));

        Assert.Equal(500, responseError.StatusCode);
        Assert.Equal("response_error", responseError.Type);
    }

    [Fact]
    public async Task Should_handle_rate_limit_errors()
    {
        var server = new TestServer();
        server.RespondError(
            LanguageModelUrl,
            429,
            """{"error":{"message":"Rate limit exceeded. Try again later.","type":"rate_limit_exceeded"}}"""
        );
        using var model = CreateTestModel(server);

        var rateLimitError = await Assert.ThrowsAsync<GatewayRateLimitException>(() =>
            model.DoGenerateAsync(Options())
        );

        Assert.Equal("Rate limit exceeded. Try again later.", rateLimitError.Message);
        Assert.Equal(429, rateLimitError.StatusCode);
        Assert.Equal("rate_limit_exceeded", rateLimitError.Type);
    }

    [Fact]
    public async Task Should_handle_invalid_request_errors()
    {
        var server = new TestServer();
        server.RespondError(
            LanguageModelUrl,
            400,
            """{"error":{"message":"Invalid prompt format","type":"invalid_request_error"}}"""
        );
        using var model = CreateTestModel(server);

        var invalidError = await Assert.ThrowsAsync<GatewayInvalidRequestException>(() =>
            model.DoGenerateAsync(Options())
        );

        Assert.Equal("Invalid prompt format", invalidError.Message);
        Assert.Equal(400, invalidError.StatusCode);
        Assert.Equal("invalid_request_error", invalidError.Type);
    }

    // describe('Image part encoding')
    [Fact]
    public async Task Should_not_modify_prompt_without_image_parts()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, content: """{"type":"text","text":"response"}""");
        using var model = CreateTestModel(server);

        await model.DoGenerateAsync(Options());

        AssertJsonEqual(TestPromptJson, server.Calls[0].RequestBodyJson!["prompt"]);
    }

    [Fact]
    public async Task Should_encode_Uint8Array_image_part_to_inline_base64_data_with_default_mime_type()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, content: """{"type":"text","text":"response"}""");
        byte[] imageBytes = [1, 2, 3, 4];
        var expectedBase64 = Convert.ToBase64String(imageBytes);
        using var model = CreateTestModel(server);

        await model.DoGenerateAsync(
            ImageOptions(
                new LanguageModelV4TextPart("Describe this image:"),
                new LanguageModelV4FilePart(new SharedV4FileDataData(imageBytes), "image/jpeg")
            )
        );

        var imagePart = PromptContent(server.Calls[0], 1);
        Assert.Equal("file", (string?)imagePart["type"]);
        AssertJsonEqual($$"""{"type":"data","data":"{{expectedBase64}}"}""", imagePart["data"]);
        Assert.Equal("image/jpeg", (string?)imagePart["mediaType"]);
    }

    [Fact]
    public async Task Should_encode_Uint8Array_image_part_to_inline_base64_data_with_specified_mime_type()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, content: """{"type":"text","text":"response"}""");
        byte[] imageBytes = [5, 6, 7, 8];
        var expectedBase64 = Convert.ToBase64String(imageBytes);
        var mimeType = "image/png";
        using var model = CreateTestModel(server);

        await model.DoGenerateAsync(
            ImageOptions(new LanguageModelV4FilePart(new SharedV4FileDataData(imageBytes), mimeType))
        );

        var imagePart = PromptContent(server.Calls[0], 0);
        Assert.Equal("file", (string?)imagePart["type"]);
        AssertJsonEqual($$"""{"type":"data","data":"{{expectedBase64}}"}""", imagePart["data"]);
        Assert.Equal(mimeType, (string?)imagePart["mediaType"]);
    }

    [Fact]
    public async Task Should_not_modify_image_part_with_URL()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, content: """{"type":"text","text":"response"}""");
        var imageUrl = new Uri("https://example.com/image.jpg");
        using var model = CreateTestModel(server);

        await model.DoGenerateAsync(
            ImageOptions(
                new LanguageModelV4TextPart("Image URL:"),
                new LanguageModelV4FilePart(new SharedV4FileDataUrl(imageUrl), "image/jpeg")
            )
        );

        var imagePart = PromptContent(server.Calls[0], 1);
        Assert.Equal("file", (string?)imagePart["type"]);
        AssertJsonEqual($$"""{"type":"url","url":"{{imageUrl}}"}""", imagePart["data"]);
    }

    [Fact]
    public async Task Should_handle_mixed_content_types_correctly()
    {
        var server = new TestServer();
        PrepareJsonResponse(server, content: """{"type":"text","text":"response"}""");
        byte[] imageBytes = [1, 2, 3, 4];
        var expectedBase64 = Convert.ToBase64String(imageBytes);
        var imageUrl = new Uri("https://example.com/image2.png");
        using var model = CreateTestModel(server);

        await model.DoGenerateAsync(
            ImageOptions(
                new LanguageModelV4TextPart("First text."),
                new LanguageModelV4FilePart(new SharedV4FileDataData(imageBytes), "image/gif"),
                new LanguageModelV4TextPart("Second text."),
                new LanguageModelV4FilePart(new SharedV4FileDataUrl(imageUrl), "image/png")
            )
        );

        var call = server.Calls[0];
        AssertJsonEqual("""{"type":"text","text":"First text."}""", PromptContent(call, 0));
        AssertJsonEqual(
            $$"""{"type":"file","data":{"type":"data","data":"{{expectedBase64}}"},"mediaType":"image/gif"}""",
            PromptContent(call, 1)
        );
        AssertJsonEqual("""{"type":"text","text":"Second text."}""", PromptContent(call, 2));
        AssertJsonEqual(
            $$"""{"type":"file","data":{"type":"url","url":"{{imageUrl}}"},"mediaType":"image/png"}""",
            PromptContent(call, 3)
        );
    }

    [Fact]
    public async Task Should_handle_various_error_types_with_proper_conversion()
    {
        var server = new TestServer();
        using var model = CreateTestModel(server);

        server.RespondError(
            LanguageModelUrl,
            400,
            """{"error":{"message":"Invalid request format","type":"invalid_request_error"}}"""
        );

        var invalidError = await Assert.ThrowsAsync<GatewayInvalidRequestException>(() =>
            model.DoGenerateAsync(Options())
        );
        Assert.Equal("Invalid request format", invalidError.Message);
        Assert.Equal(400, invalidError.StatusCode);
        Assert.Equal("invalid_request_error", invalidError.Type);

        // Test model not found error
        server.RespondError(
            LanguageModelUrl,
            404,
            """{"error":{"message":"Model xyz not found","type":"model_not_found","param":{"modelId":"xyz"}}}"""
        );

        var modelError = await Assert.ThrowsAsync<GatewayModelNotFoundException>(() =>
            model.DoGenerateAsync(Options())
        );
        Assert.Equal("Model xyz not found", modelError.Message);
        Assert.Equal(404, modelError.StatusCode);
        Assert.Equal("model_not_found", modelError.Type);
        Assert.Equal("xyz", modelError.ModelId);

        // Test internal server error
        server.RespondError(
            LanguageModelUrl,
            500,
            """{"error":{"message":"Database connection failed","type":"internal_server_error"}}"""
        );

        var serverError = await Assert.ThrowsAsync<GatewayInternalServerException>(() =>
            model.DoGenerateAsync(Options())
        );
        Assert.Equal("Database connection failed", serverError.Message);
        Assert.Equal(500, serverError.StatusCode);
        Assert.Equal("internal_server_error", serverError.Type);
        var cause = Assert.IsType<ApiCallException>(serverError.InnerException);
        Assert.Contains("Database connection failed", cause.Message, StringComparison.Ordinal);
    }

    // describe('Gateway error handling for malformed responses')
    [Fact]
    public async Task Should_include_actual_response_body_when_APICallError_has_no_data()
    {
        var malformedResponse = """{"ferror":{"message":"Model not found","type":"model_not_found"}}""";
        var server = new TestServer();
        // Mock the server to return malformed response that can't be parsed by AI SDK
        server.RespondError(LanguageModelUrl, 404, malformedResponse);
        using var model = CreateTestModel(server);

        var gatewayError = await Assert.ThrowsAsync<GatewayResponseException>(() =>
            model.DoGenerateAsync(Options([new LanguageModelV4UserMessage([new LanguageModelV4TextPart("test")])]))
        );

        AssertJsonEqual(malformedResponse, JsonNode.Parse(gatewayError.Response!));
        Assert.NotNull(gatewayError.ValidationError);
    }

    [Fact]
    public async Task Should_use_raw_response_body_when_JSON_parsing_fails()
    {
        var invalidJson = "invalid json response";
        var server = new TestServer();
        // Mock the server to return invalid JSON
        server.RespondError(LanguageModelUrl, 500, invalidJson);
        using var model = CreateTestModel(server);

        var gatewayError = await Assert.ThrowsAsync<GatewayResponseException>(() =>
            model.DoGenerateAsync(Options([new LanguageModelV4UserMessage([new LanguageModelV4TextPart("test")])]))
        );

        Assert.Equal(invalidJson, gatewayError.Response);
        Assert.NotNull(gatewayError.ValidationError);
    }
}
