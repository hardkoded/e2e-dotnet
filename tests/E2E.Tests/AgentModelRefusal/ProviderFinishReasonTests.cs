// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.AgentModelRefusal;

/// <summary>.NET-only: upstream reads the finish reason from the AI SDK, which maps each provider's stop word. The port maps them in its own providers.</summary>
public sealed class ProviderFinishReasonTests
{
    [Theory]
    [InlineData("anthropic", """{ "content": [], "stop_reason": "refusal" }""", "refusal")]
    [InlineData("responses", """{ "output": [], "incomplete_details": { "reason": "content_filter" } }""", "content_filter")]
    [InlineData("compatible", """{ "choices": [{ "message": { "content": null }, "finish_reason": "content_filter" }] }""", "content_filter")]
    [InlineData("google", """{ "candidates": [{ "finishReason": "SAFETY" }] }""", "SAFETY")]
    [InlineData("bedrock", """{ "output": { "message": { "content": [] } }, "stopReason": "guardrail_intervened" }""", "guardrail_intervened")]
    public async Task Reads_the_providers_refusal_as_a_content_filter_finish(string provider, string body, string raw)
    {
        var response = await CompleteAsync(provider, body);

        Assert.Equal(ModelResponse.ContentFilter, response.FinishReason);
        Assert.Equal(raw, response.RawFinishReason);
    }

    [Theory]
    [InlineData("anthropic", """{ "content": [], "stop_reason": "end_turn" }""")]
    [InlineData("responses", """{ "output": [], "incomplete_details": { "reason": "max_output_tokens" } }""")]
    [InlineData("compatible", """{ "choices": [{ "message": { "content": "ok" }, "finish_reason": "stop" }] }""")]
    [InlineData("google", """{ "candidates": [{ "finishReason": "STOP" }] }""")]
    [InlineData("bedrock", """{ "output": { "message": { "content": [] } }, "stopReason": "end_turn" }""")]
    public async Task Reads_any_other_stop_as_no_finish_reason(string provider, string body)
    {
        var response = await CompleteAsync(provider, body);

        Assert.Null(response.FinishReason);
    }

    [Fact]
    public void Redacts_and_cuts_the_providers_finish_reason_in_the_message()
    {
        var redactor = Redactor.ForValue("s3cret-value");

        var error = ModelHttp.ModelRefusalError("m", "stopped s3cret-value " + new string('x', 500), redactor);

        Assert.Equal("MODEL_REFUSED", error.Code);
        Assert.DoesNotContain("s3cret-value", error.Message, StringComparison.Ordinal);
        Assert.True(error.Message.Length < 200);
        Assert.Equal("the model m refused the request (content-filter)", ModelHttp.ModelRefusalError("m", null, redactor).Message);
    }

    private static async Task<ModelResponse> CompleteAsync(string provider, string body)
    {
        using var http = new HttpClient(new RecordingHandler(body));
        IAgentModel model = provider switch
        {
            "anthropic" => new AnthropicModel(new AnthropicModelOptions { Model = "m", ApiKey = "k" }, http),
            "responses" => new OpenAiResponsesModel(new OpenAiResponsesModelOptions { Model = "m", ApiKey = "k" }, http),
            "compatible" => new OpenAiCompatibleModel(new OpenAiCompatibleModelOptions { Model = "m", ApiKey = "k" }, http),
            "google" => new GoogleModel(new GoogleModelOptions { Model = "m", ApiKey = "k" }, http),
            _ => new BedrockModel(new BedrockModelOptions { Model = "m", Region = "eu-west-1", ApiKey = "k" }, http),
        };
        using var disposable = (IDisposable)model;
        return await model.CompleteAsync(ModelProviderTests.Conversation(), CancellationToken.None);
    }
}
