// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using static E2E.Tests.ProviderHints.HintedBodies;

namespace E2E.Tests.ModelRequestToolChoice;

/// <summary>
/// How each provider is asked for <see cref="ModelRequest.ToolChoice"/>. Upstream hands <c>toolChoice</c> to the AI SDK, which builds the
/// provider bodies, so there is no upstream <c>it</c> to port: these tests are .NET-only, like the rest of the model providers.
/// </summary>
public sealed class ModelRequestToolChoiceTests
{
    private const string BedrockAnswer = """{ "output": { "message": { "role": "assistant", "content": [{ "text": "ok" }] } } }""";

    private static ModelRequest With(ModelToolChoice? choice, IReadOnlyDictionary<string, JsonElement>? providerOptions = null) => new()
    {
        System = "rules",
        Tools = ModelProviderTests.Tools(),
        Messages = [new ModelMessage { Role = "user", Content = "step" }],
        ToolChoice = choice,
        ProviderOptions = providerOptions,
    };

    private static string Wire(JsonNode? node) => node!.ToJsonString();

    [Fact]
    public async Task Chat_completions_sends_auto_required_or_the_named_function()
    {
        var options = new OpenAiCompatibleModelOptions { Model = "m", ApiKey = "k" };

        Assert.Equal("\"auto\"", Wire((await ChatAsync(options, With(null)))["tool_choice"]));
        Assert.Equal("\"auto\"", Wire((await ChatAsync(options, With(ModelToolChoice.Auto)))["tool_choice"]));
        Assert.Equal("\"required\"", Wire((await ChatAsync(options, With(ModelToolChoice.Required)))["tool_choice"]));
        Assert.Equal("""{"type":"function","function":{"name":"done"}}""", Wire((await ChatAsync(options, With(ModelToolChoice.Tool("done"))))["tool_choice"]));
    }

    [Fact]
    public async Task Responses_sends_auto_required_or_the_named_function()
    {
        var options = new OpenAiResponsesModelOptions { Model = "m", ApiKey = "k" };

        Assert.Equal("\"auto\"", Wire((await ResponsesAsync(options, With(null)))["tool_choice"]));
        Assert.Equal("\"required\"", Wire((await ResponsesAsync(options, With(ModelToolChoice.Required)))["tool_choice"]));
        Assert.Equal("""{"type":"function","name":"done"}""", Wire((await ResponsesAsync(options, With(ModelToolChoice.Tool("done"))))["tool_choice"]));
    }

    [Fact]
    public async Task Anthropic_sends_auto_any_or_the_named_tool()
    {
        Assert.Equal("""{"type":"auto"}""", Wire((await AnthropicBodyAsync(With(null)))["tool_choice"]));
        Assert.Equal("""{"type":"any"}""", Wire((await AnthropicBodyAsync(With(ModelToolChoice.Required)))["tool_choice"]));
        Assert.Equal("""{"type":"tool","name":"done"}""", Wire((await AnthropicBodyAsync(With(ModelToolChoice.Tool("done"))))["tool_choice"]));
    }

    [Fact]
    public void Google_sends_auto_any_or_any_limited_to_the_named_function()
    {
        using var model = new GoogleModel(new GoogleModelOptions { Model = "m", ApiKey = "k" });

        Assert.Equal("""{"mode":"AUTO"}""", Wire(model.BuildBody(With(null))["toolConfig"]!["functionCallingConfig"]));
        Assert.Equal("""{"mode":"ANY"}""", Wire(model.BuildBody(With(ModelToolChoice.Required))["toolConfig"]!["functionCallingConfig"]));
        Assert.Equal("""{"mode":"ANY","allowedFunctionNames":["done"]}""", Wire(model.BuildBody(With(ModelToolChoice.Tool("done")))["toolConfig"]!["functionCallingConfig"]));
    }

    [Fact]
    public void Bedrock_sends_no_choice_by_default_and_auto_any_or_the_named_tool_when_asked()
    {
        using var model = new BedrockModel(new BedrockModelOptions { Model = "m", Region = "eu-west-1", ApiKey = "k" }, new HttpClient());

        Assert.Null(model.BuildBody(With(null))["toolConfig"]!["toolChoice"]);
        Assert.Equal("""{"auto":{}}""", Wire(model.BuildBody(With(ModelToolChoice.Auto))["toolConfig"]!["toolChoice"]));
        Assert.Equal("""{"any":{}}""", Wire(model.BuildBody(With(ModelToolChoice.Required))["toolConfig"]!["toolChoice"]));
        Assert.Equal("""{"tool":{"name":"done"}}""", Wire(model.BuildBody(With(ModelToolChoice.Tool("done")))["toolConfig"]!["toolChoice"]));
    }

    [Fact]
    public async Task Anthropic_sends_auto_and_warns_when_a_forced_choice_meets_a_thinking_model()
    {
        var handler = new RecordingHandler(AnthropicAnswer);
        using var http = new HttpClient(handler);
        using var model = new AnthropicModel(new AnthropicModelOptions { Model = "m", ApiKey = "k" }, http);
        var thinking = Options("anthropic", """{ "thinking": { "type": "enabled", "budget_tokens": 1024 } }""");

        var response = await model.CompleteAsync(With(ModelToolChoice.Required, thinking), CancellationToken.None);

        Assert.Equal("""{"type":"auto"}""", Wire(handler.Requests.Single().Json()["tool_choice"]));
        var warning = Assert.Single(response.Warnings);
        Assert.Equal("unsupported", warning.Type);
        Assert.Equal("toolChoice", warning.Feature);
    }

    [Fact]
    public async Task Anthropic_does_not_warn_when_the_choice_is_sent_as_asked()
    {
        var handler = new RecordingHandler(AnthropicAnswer, AnthropicAnswer);
        using var http = new HttpClient(handler);
        using var model = new AnthropicModel(new AnthropicModelOptions { Model = "m", ApiKey = "k" }, http);
        var thinking = Options("anthropic", """{ "thinking": { "type": "enabled", "budget_tokens": 1024 } }""");

        Assert.Empty((await model.CompleteAsync(With(ModelToolChoice.Required), CancellationToken.None)).Warnings);
        Assert.Empty((await model.CompleteAsync(With(ModelToolChoice.Auto, thinking), CancellationToken.None)).Warnings);
    }

    [Fact]
    public async Task Bedrock_sends_auto_and_warns_when_a_forced_choice_meets_a_thinking_model()
    {
        var handler = new RecordingHandler(BedrockAnswer);
        using var http = new HttpClient(handler);
        using var model = new BedrockModel(new BedrockModelOptions { Model = "m", Region = "eu-west-1", ApiKey = "k" }, http);
        var thinking = Options("bedrock", """{ "additionalModelRequestFields": { "thinking": { "type": "enabled", "budget_tokens": 1024 } } }""");

        var response = await model.CompleteAsync(With(ModelToolChoice.Tool("done"), thinking), CancellationToken.None);

        Assert.Equal("""{"auto":{}}""", Wire(handler.Requests.Single().Json()["toolConfig"]!["toolChoice"]));
        Assert.Equal("toolChoice", Assert.Single(response.Warnings).Feature);
    }

    [Fact]
    public async Task Records_the_choice_in_the_ai_trace_as_the_sdk_reports_it()
    {
        var recorder = new E2E.Internal.AiTraceRecorder();
        var model = new ScriptedModel(_ => ModelResponses.Done("passed", "ok"));

        using (await AiTraceModel.BeginAsync(recorder, model, With(ModelToolChoice.Required), CancellationToken.None))
        {
        }

        using (await AiTraceModel.BeginAsync(recorder, model, With(ModelToolChoice.Tool("done")), CancellationToken.None))
        {
        }

        var choices = recorder.Drain().Steps.Select(step => JsonNode.Parse(step.Input)!["toolChoice"]!.ToJsonString()).ToList();
        Assert.Equal(["""{"type":"required"}""", """{"type":"tool","toolName":"done"}"""], choices);
    }

    private static async Task<JsonObject> AnthropicBodyAsync(ModelRequest request)
    {
        var handler = new RecordingHandler(AnthropicAnswer);
        using var http = new HttpClient(handler);
        using var model = new AnthropicModel(new AnthropicModelOptions { Model = "m", ApiKey = "k" }, http);
        return await SendAsync(model, handler, request);
    }
}
