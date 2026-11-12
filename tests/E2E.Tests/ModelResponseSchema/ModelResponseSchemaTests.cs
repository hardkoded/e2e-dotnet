// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using E2E.Internal;

namespace E2E.Tests.ModelResponseSchema;

/// <summary>
/// How each provider is asked to fill <see cref="ModelRequest.Schema"/>. Upstream hands the schema to the AI SDK, which builds the
/// provider bodies, so there is no upstream <c>it</c> to port: these tests are .NET-only, like the rest of the model providers.
/// </summary>
public sealed class ModelResponseSchemaTests
{
    private static ModelRequest Request(IReadOnlyDictionary<string, JsonElement>? providerOptions = null) => new()
    {
        System = "rules",
        Messages = [new ModelMessage { Role = "user", Content = "judge" }],
        Tools = [],
        Schema = AgentProtocol.JudgmentSchema,
        SchemaName = AgentProtocol.JudgmentSchemaName,
        ProviderOptions = providerOptions,
    };

    private static IReadOnlyDictionary<string, JsonElement> Options(string provider, string json) =>
        new Dictionary<string, JsonElement> { [provider] = JsonDocument.Parse(json).RootElement.Clone() };

    private static string Schema => AgentProtocol.JudgmentSchema.GetRawText();

    [Fact]
    public void Chat_completions_asks_for_a_json_schema_response_and_offers_no_tool()
    {
        using var model = new OpenAiCompatibleModel(new OpenAiCompatibleModelOptions { Model = "m", ApiKey = "k" });
        var body = model.BuildBody(Request());

        Assert.Null(body["tools"]);
        Assert.Null(body["tool_choice"]);
        Assert.Equal("json_schema", (string?)body["response_format"]!["type"]);
        Assert.Equal("agent-judgment-2", (string?)body["response_format"]!["json_schema"]!["name"]);
        Assert.Equal(JsonNode.Parse(Schema)!.ToJsonString(), body["response_format"]!["json_schema"]!["schema"]!.ToJsonString());
    }

    [Fact]
    public void Chat_completions_leaves_out_response_format_when_the_server_rejects_it()
    {
        using var model = new OpenAiCompatibleModel(new OpenAiCompatibleModelOptions { Model = "m", ApiKey = "k", JsonSchemaResponseFormat = false });
        var body = model.BuildBody(Request());

        Assert.Null(body["response_format"]);
        Assert.Null(body["tools"]);
    }

    [Fact]
    public void Responses_asks_for_a_json_schema_text_format_beside_the_provider_text_options()
    {
        using var model = new OpenAiResponsesModel(new OpenAiResponsesModelOptions { Model = "m", ApiKey = "k" });
        var body = model.BuildBody(Request(Options("openai", """{ "text": { "verbosity": "low" } }""")));

        Assert.Null(body["tools"]);
        Assert.Equal("json_schema", (string?)body["text"]!["format"]!["type"]);
        Assert.Equal("agent-judgment-2", (string?)body["text"]!["format"]!["name"]);
        Assert.Equal("low", (string?)body["text"]!["verbosity"]);
    }

    [Fact]
    public void Google_asks_for_a_json_response_with_the_schema_beside_the_provider_generation_options()
    {
        using var model = new GoogleModel(new GoogleModelOptions { Model = "m", ApiKey = "k" });
        var body = model.BuildBody(Request(Options("google", """{ "generationConfig": { "temperature": 0 } }""")));

        Assert.Null(body["tools"]);
        Assert.Equal("application/json", (string?)body["generationConfig"]!["responseMimeType"]);
        Assert.Equal(JsonNode.Parse(Schema)!.ToJsonString(), body["generationConfig"]!["responseJsonSchema"]!.ToJsonString());
        Assert.Equal(0, (int?)body["generationConfig"]!["temperature"]);
    }

    [Fact]
    public void Anthropic_forces_a_json_tool_that_takes_the_schema()
    {
        using var model = new AnthropicModel(new AnthropicModelOptions { Model = "m", ApiKey = "k" });
        var body = model.BuildBody(Request());

        Assert.Equal("json", (string?)body["tools"]![0]!["name"]);
        Assert.Equal(JsonNode.Parse(Schema)!.ToJsonString(), body["tools"]![0]!["input_schema"]!.ToJsonString());
        Assert.Equal("""{"type":"tool","name":"json"}""", body["tool_choice"]!.ToJsonString());
    }

    [Fact]
    public void Anthropic_only_offers_the_json_tool_while_the_model_thinks()
    {
        using var model = new AnthropicModel(new AnthropicModelOptions { Model = "m", ApiKey = "k" });
        var body = model.BuildBody(Request(Options("anthropic", """{ "thinking": { "type": "enabled", "budget_tokens": 1024 } }""")));

        Assert.Equal("json", (string?)body["tools"]![0]!["name"]);
        Assert.Equal("""{"type":"auto"}""", body["tool_choice"]!.ToJsonString());
    }

    [Fact]
    public void Bedrock_forces_a_json_tool_that_takes_the_schema()
    {
        using var model = new BedrockModel(new BedrockModelOptions { Model = "m", Region = "us-east-1", ApiKey = "k" });
        var body = model.BuildBody(Request());

        Assert.Equal("json", (string?)body["toolConfig"]!["tools"]![0]!["toolSpec"]!["name"]);
        Assert.Equal("""{"tool":{"name":"json"}}""", body["toolConfig"]!["toolChoice"]!.ToJsonString());
    }

    [Fact]
    public void A_forced_json_tool_call_is_the_content_of_the_turn()
    {
        var call = new ModelToolCall { Id = "1", Name = "json", Arguments = JsonDocument.Parse("""{"verdict":"holds"}""").RootElement.Clone() };
        var answer = ModelHttp.StructuredAnswer(new ModelResponse { ToolCalls = [call], FinishReason = "stop" });

        Assert.Equal("""{"verdict":"holds"}""", answer.Content);
        Assert.Empty(answer.ToolCalls);
        Assert.Equal("stop", answer.FinishReason);
    }
}
