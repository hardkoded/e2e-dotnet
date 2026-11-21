// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Decision.Sdk;
using E2E.Decision.Sdk.Gateway;
using static E2E.Decision.Tests.GatewayLanguageModel.GatewayLanguageModelFixture;

namespace E2E.Decision.Tests.GatewayLanguageModel;

/// <summary>Upstream's <c>describe('doStream')</c> of <c>gateway-language-model.test.ts</c>.</summary>
public sealed class DoStreamTests
{
    private static LanguageModelV4CallOptions ImageOptions(params LanguageModelV4PromptPart[] parts) =>
        Options([new LanguageModelV4UserMessage(parts)], includeRawChunks: false);

    private static JsonNode PromptContent(TestServerCall call, int index) =>
        call.RequestBodyJson!["prompt"]![0]!["content"]![index]!;

    [Fact]
    public async Task Should_stream_text_deltas()
    {
        var server = new TestServer();
        PrepareStreamResponse(server, ["Hello", ", ", "World!"]);
        using var model = CreateTestModel(server);

        var result = await model.DoStreamAsync(Options(includeRawChunks: false));

        AssertJsonEqual(
            """
            [
              { "textDelta": "Hello", "type": "text-delta" },
              { "textDelta": ", ", "type": "text-delta" },
              { "textDelta": "World!", "type": "text-delta" },
              {
                "finishReason": "stop",
                "type": "finish",
                "usage": { "completion_tokens": 20, "prompt_tokens": 10 }
              }
            ]
            """,
            PartsJson(await ConvertReadableStreamToArrayAsync(result.Stream))
        );
    }

    [Fact]
    public async Task Should_forward_server_stream_start_warnings_exactly_once()
    {
        var server = new TestServer();
        server.RespondStreamChunks(
            LanguageModelUrl,
            [
                """data: {"type":"stream-start","warnings":[{"type":"other","message":"from provider"}]}""" + "\n\n",
                """data: {"type":"text-delta","textDelta":"Hello"}""" + "\n\n",
                """data: {"type":"finish","finishReason":"stop","usage":{"prompt_tokens":10,"completion_tokens":20}}"""
                    + "\n\n",
            ]
        );
        using var model = CreateTestModel(server);

        var result = await model.DoStreamAsync(Options(includeRawChunks: false));

        var parts = await ConvertReadableStreamToArrayAsync(result.Stream);

        var streamStarts = parts.Where(part => part.Type == "stream-start").ToList();
        var streamStart = Assert.Single(streamStarts);
        AssertJsonEqual(
            """{"type":"stream-start","warnings":[{"type":"other","message":"from provider"}]}""",
            JsonNode.Parse(streamStart.Json.GetRawText())
        );
        Assert.Equal([new SharedV4Warning { Type = "other", Message = "from provider" }], streamStart.Warnings);
    }

    [Fact]
    public async Task Should_preserve_explicit_mid_stream_provider_error_metadata()
    {
        var error = """
            {"message":"Upstream provider overloaded","type":"provider_overloaded","statusCode":503,"isRetryable":true}
            """;
        var server = new TestServer();
        server.RespondStreamChunks(
            LanguageModelUrl,
            [
                """data: {"type":"text-delta","textDelta":"Partial output"}""" + "\n\n",
                $$"""data: {"type":"error","error":{{error}}}""" + "\n\n",
            ]
        );
        using var model = CreateTestModel(server);

        var result = await model.DoStreamAsync(Options(includeRawChunks: false));

        AssertJsonEqual(
            $$"""[{"type":"text-delta","textDelta":"Partial output"},{"type":"error","error":{{error}}}]""",
            PartsJson(await ConvertReadableStreamToArrayAsync(result.Stream))
        );
    }

    [Fact]
    public async Task Should_pass_streaming_headers()
    {
        var server = new TestServer();
        PrepareStreamResponse(server, ["Test"]);
        using var model = CreateTestModel(server);

        await model.DoStreamAsync(Options(includeRawChunks: false));

        var headers = server.Calls[0].RequestHeaders;
        Assert.Equal("4", headers["ai-language-model-specification-version"]);
        Assert.Equal("test-model", headers["ai-language-model-id"]);
        Assert.Equal("true", headers["ai-language-model-streaming"]);
    }

    [Fact]
    public async Task Should_remove_abortSignal_from_the_streaming_request_body()
    {
        var server = new TestServer();
        PrepareStreamResponse(server, ["Test content"]);
        using var cancellation = new CancellationTokenSource();
        using var model = CreateTestModel(server);

        await model.DoStreamAsync(Options(includeRawChunks: false), cancellation.Token);

        var requestBody = server.Calls[0].RequestBodyJson!.AsObject();
        Assert.False(requestBody.ContainsKey("abortSignal"));
    }

    [Fact]
    public async Task Should_pass_abortSignal_to_fetch_when_provided_for_streaming()
    {
        var server = new TestServer();
        PrepareStreamResponse(server, ["Test content"]);
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

        await model.DoStreamAsync(Options(includeRawChunks: false), cancellation.Token);

        Assert.Equal(1, handler.Calls);
        Assert.True(signalPassed);
    }

    // Upstream asserts that fetch gets no signal. HttpClient always gives its handler a token, so the test checks that no cancellation reaches it.
    [Fact]
    public async Task Partial_Should_not_pass_abortSignal_to_fetch_when_not_provided_for_streaming()
    {
        var server = new TestServer();
        PrepareStreamResponse(server, ["Test content"]);
        var cancelled = true;
        using var handler = new TokenProbeHandler(server, token => cancelled = token.IsCancellationRequested);
        using var http = new HttpClient(handler);
        using var model = CreateTestModel(http);

        await model.DoStreamAsync(Options(includeRawChunks: false));

        Assert.Equal(1, handler.Calls);
        Assert.False(cancelled);
    }

    [Fact]
    public async Task Should_include_o11y_headers_in_the_streaming_request()
    {
        var server = new TestServer();
        PrepareStreamResponse(server, ["Test content"]);
        var o11yHeaders = new Dictionary<string, string?>
        {
            ["ai-o11y-deployment-id"] = "test-deployment",
            ["ai-o11y-environment"] = "production",
            ["ai-o11y-region"] = "iad1",
        };
        using var model = CreateTestModel(server, o11yHeaders);

        await model.DoStreamAsync(Options(includeRawChunks: false));

        var headers = server.Calls[0].RequestHeaders;
        foreach (var (name, value) in o11yHeaders)
        {
            Assert.Equal(value, headers[name]);
        }
    }

    [Fact]
    public async Task Should_convert_API_call_errors_to_Gateway_errors_in_streaming()
    {
        var server = new TestServer();
        server.RespondError(
            LanguageModelUrl,
            429,
            """{"error":{"message":"Rate limit exceeded","type":"rate_limit_exceeded"}}"""
        );
        using var model = CreateTestModel(server);

        var rateLimitError = await Assert.ThrowsAsync<GatewayRateLimitException>(() =>
            model.DoStreamAsync(Options(includeRawChunks: false))
        );

        Assert.Equal("Rate limit exceeded", rateLimitError.Message);
        Assert.Equal(429, rateLimitError.StatusCode);
        Assert.Equal("rate_limit_exceeded", rateLimitError.Type);
    }

    [Fact]
    public async Task Should_handle_authentication_errors_in_streaming()
    {
        var server = new TestServer();
        server.RespondError(
            LanguageModelUrl,
            401,
            """{"error":{"message":"Authentication failed for streaming","type":"authentication_error"}}"""
        );
        using var model = CreateTestModel(server);

        var authError = await Assert.ThrowsAsync<GatewayAuthenticationException>(() =>
            model.DoStreamAsync(Options(includeRawChunks: false))
        );

        Assert.Contains("Invalid API key", authError.Message, StringComparison.Ordinal);
        Assert.Contains("vercel.com/d?to=", authError.Message, StringComparison.Ordinal);
        Assert.Equal(401, authError.StatusCode);
        Assert.Equal("authentication_error", authError.Type);
    }

    [Fact]
    public async Task Should_handle_invalid_request_errors_in_streaming()
    {
        var server = new TestServer();
        server.RespondError(
            LanguageModelUrl,
            400,
            """{"error":{"message":"Invalid streaming request","type":"invalid_request_error"}}"""
        );
        using var model = CreateTestModel(server);

        var invalidError = await Assert.ThrowsAsync<GatewayInvalidRequestException>(() =>
            model.DoStreamAsync(Options(includeRawChunks: false))
        );

        Assert.Equal("Invalid streaming request", invalidError.Message);
        Assert.Equal(400, invalidError.StatusCode);
        Assert.Equal("invalid_request_error", invalidError.Type);
    }

    [Fact]
    public async Task Should_handle_malformed_error_responses_in_streaming()
    {
        var server = new TestServer();
        server.RespondError(LanguageModelUrl, 500, "Invalid JSON for streaming");
        using var model = CreateTestModel(server);

        var responseError = await Assert.ThrowsAsync<GatewayResponseException>(() =>
            model.DoStreamAsync(Options(includeRawChunks: false))
        );

        Assert.Equal(500, responseError.StatusCode);
        Assert.Equal("response_error", responseError.Type);
    }

    // describe('Image part encoding')
    [Fact]
    public async Task Should_not_modify_prompt_without_image_parts()
    {
        var server = new TestServer();
        PrepareStreamResponse(server, ["response"]);
        using var model = CreateTestModel(server);

        await model.DoStreamAsync(Options(includeRawChunks: false));

        AssertJsonEqual(TestPromptJson, server.Calls[0].RequestBodyJson!["prompt"]);
    }

    [Fact]
    public async Task Should_encode_Uint8Array_image_part_to_inline_base64_data_with_default_mime_type()
    {
        var server = new TestServer();
        PrepareStreamResponse(server, ["response"]);
        byte[] imageBytes = [1, 2, 3, 4];
        var expectedBase64 = Convert.ToBase64String(imageBytes);
        using var model = CreateTestModel(server);

        await model.DoStreamAsync(
            ImageOptions(
                new LanguageModelV4TextPart("Describe:"),
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
        PrepareStreamResponse(server, ["response"]);
        byte[] imageBytes = [5, 6, 7, 8];
        var expectedBase64 = Convert.ToBase64String(imageBytes);
        var mimeType = "image/png";
        using var model = CreateTestModel(server);

        await model.DoStreamAsync(
            ImageOptions(
                new LanguageModelV4TextPart("Describe:"),
                new LanguageModelV4FilePart(new SharedV4FileDataData(imageBytes), mimeType)
            )
        );

        var imagePart = PromptContent(server.Calls[0], 1);
        Assert.Equal("file", (string?)imagePart["type"]);
        AssertJsonEqual($$"""{"type":"data","data":"{{expectedBase64}}"}""", imagePart["data"]);
        Assert.Equal(mimeType, (string?)imagePart["mediaType"]);
    }

    [Fact]
    public async Task Should_not_modify_image_part_with_URL()
    {
        var server = new TestServer();
        PrepareStreamResponse(server, ["response"]);
        var imageUrl = new Uri("https://example.com/image.jpg");
        using var model = CreateTestModel(server);

        await model.DoStreamAsync(
            ImageOptions(
                new LanguageModelV4TextPart("URL:"),
                new LanguageModelV4FilePart(new SharedV4FileDataUrl(imageUrl), "image/jpeg")
            )
        );

        var imagePart = PromptContent(server.Calls[0], 1);
        Assert.Equal("file", (string?)imagePart["type"]);
        AssertJsonEqual($$"""{"type":"url","url":"{{imageUrl}}"}""", imagePart["data"]);
        Assert.Equal("image/jpeg", (string?)imagePart["mediaType"]);
    }

    [Fact]
    public async Task Should_handle_mixed_content_types_correctly_for_streaming()
    {
        var server = new TestServer();
        PrepareStreamResponse(server, ["response"]);
        byte[] imageBytes = [1, 2, 3, 4];
        var expectedBase64 = Convert.ToBase64String(imageBytes);
        var imageUrl = new Uri("https://example.com/image2.png");
        using var model = CreateTestModel(server);

        await model.DoStreamAsync(
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

    // describe('Error handling')
    [Fact]
    public async Task Should_not_double_wrap_existing_Gateway_errors()
    {
        // Mock fetch to throw a Gateway error directly
        var existingGatewayError = new GatewayAuthenticationException("Already a Gateway error", 401);
        using var handler = new ThrowingHandler(existingGatewayError);
        using var http = new HttpClient(handler);
        using var model = CreateTestModel(http);

        var error = await Assert.ThrowsAsync<GatewayAuthenticationException>(() => model.DoGenerateAsync(Options()));

        // Should be the same instance, not wrapped
        Assert.Same(existingGatewayError, error);
        Assert.Equal("Already a Gateway error", error.Message);
    }

    [Fact]
    public async Task Should_handle_network_errors_gracefully()
    {
        // Mock fetch to throw a network error
        var networkError = new InvalidOperationException("Network connection failed");
        using var handler = new ThrowingHandler(networkError);
        using var http = new HttpClient(handler);
        using var model = CreateTestModel(http);

        var responseError = await Assert.ThrowsAsync<GatewayResponseException>(() => model.DoGenerateAsync(Options()));

        Assert.Equal(
            "Invalid error response format: Gateway request failed: Network connection failed",
            responseError.Message
        );
        Assert.Same(networkError, responseError.InnerException);
    }

    [Fact]
    public async Task Should_handle_network_errors_gracefully_in_streaming()
    {
        // Mock fetch to throw a network error during streaming
        var networkError = new InvalidOperationException("Network connection failed");
        using var handler = new ThrowingHandler(networkError);
        using var http = new HttpClient(handler);
        using var model = CreateTestModel(http);

        var responseError = await Assert.ThrowsAsync<GatewayResponseException>(() =>
            model.DoStreamAsync(Options(includeRawChunks: false))
        );

        Assert.Equal(
            "Invalid error response format: Gateway request failed: Network connection failed",
            responseError.Message
        );
        Assert.Same(networkError, responseError.InnerException);
    }

    [Fact]
    public async Task Should_preserve_error_cause_chain()
    {
        var server = new TestServer();
        server.RespondError(
            LanguageModelUrl,
            401,
            """{"error":{"message":"Token expired","type":"authentication_error"}}"""
        );
        using var model = CreateTestModel(server);

        var authError = await Assert.ThrowsAsync<GatewayAuthenticationException>(() =>
            model.DoGenerateAsync(Options())
        );

        Assert.NotNull(authError.InnerException);
    }
}
