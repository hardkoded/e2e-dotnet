// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using static E2E.Tests.ProviderHints.HintedBodies;

namespace E2E.Tests.ProviderHints;

/// <summary>
/// "recognizes the provider through a gateway id or a direct provider name" and "keeps unrelated provider options on a
/// message that loses its breakpoint" are not ported: a gateway model is an OpenAI-compatible client with no Anthropic
/// breakpoints, and a message carries no provider options of its own.
/// </summary>
public sealed class ProviderHintsForAnthropicTests
{
    private static async Task<JsonObject> BodyAsync(ModelRequest request)
    {
        var handler = new RecordingHandler(AnthropicAnswer);
        using var http = new HttpClient(handler);
        using var model = new AnthropicModel(new AnthropicModelOptions { Model = "claude-haiku-4.5", ApiKey = "k" }, http);
        return await SendAsync(model, handler, request);
    }

    /// <summary>Partial port of "marks the system prompt as a breakpoint and adds no request options": the options of the client's own provider are checked as the body fields they become.</summary>
    [Fact]
    public async Task Partial_Marks_the_system_prompt_as_a_breakpoint_and_passes_the_provider_options_through_unchanged()
    {
        var plain = await BodyAsync(Request());
        var withOptions = await BodyAsync(Request(providerOptions: Options("anthropic", """{ "top_k": 5 }""")));

        Assert.Equal(JsonNode.Parse("""[{ "type": "text", "text": "rules", "cache_control": { "type": "ephemeral" } }]""")!.ToJsonString(), plain["system"]!.ToJsonString());
        Assert.Equal(["model", "max_tokens", "system", "messages", "tools", "tool_choice"], plain.Select(field => field.Key));
        Assert.Equal(5, withOptions["top_k"]!.GetValue<int>());
        Assert.Equal(plain["system"]!.ToJsonString(), withOptions["system"]!.ToJsonString());
    }

    /// <summary>Partial port of "moves the conversation breakpoint to the newest message each turn": the port builds its body fresh each turn, so each request is read on its own.</summary>
    [Fact]
    public async Task Partial_Marks_only_the_newest_message_of_each_turns_request()
    {
        var first = await BodyAsync(Request(markLatest: true));
        Assert.Equal("ephemeral", first["messages"]![0]!["content"]![0]!["cache_control"]!["type"]!.GetValue<string>());

        var call = new ModelToolCall { Id = "c1", Name = "tap", Arguments = JsonDocument.Parse("{}").RootElement.Clone() };
        var second = await BodyAsync(Request(
            markLatest: true,
            messages:
            [
                new ModelMessage { Role = "user", Content = "step" },
                new ModelMessage { Role = "assistant", ToolCalls = [call] },
                new ModelMessage { Role = "tool", ToolCallId = "c1", Name = "tap", Content = "ok" },
            ]));

        var messages = second["messages"]!.AsArray();
        Assert.Equal(3, messages.Count);
        Assert.Null(messages[0]!["content"]![0]!["cache_control"]);
        Assert.Null(messages[1]!["content"]![0]!["cache_control"]);
        Assert.Equal("ephemeral", messages[2]!["content"]![0]!["cache_control"]!["type"]!.GetValue<string>());
        Assert.Single(messages.SelectMany(message => message!["content"]!.AsArray()), block => block!["cache_control"] is not null);
    }

    /// <summary>
    /// Partial port of "recognizes Claude on %j, where the same check runs": the port has no provider name beside a model id,
    /// so the Vertex AI row is the Anthropic client itself, and OpenRouter and Bedrock are read by the model id.
    /// </summary>
    [Theory]
    [InlineData("eu.anthropic.claude-sonnet-4-6")]
    [InlineData("anthropic.claude-opus-5-5")]
    [InlineData("anthropic/claude-sonnet-5-5")]
    public void Partial_Recognizes_Claude_by_the_model_id_where_the_same_check_runs(string modelId)
    {
        var platform = global::E2E.ProviderHints.For(new NamedModel(modelId));
        var history = new List<ModelMessage>
        {
            new() { Role = "user", Content = "step" },
            new() { Role = "assistant", Content = "ok", ReasoningItems = [JsonDocument.Parse("\"\"").RootElement.Clone()] },
        };

        var after = platform.AfterEdit(history, 0);

        Assert.Same(history[0], after[0]);
        Assert.Null(after[1].ReasoningItems);
        Assert.Equal("ok", after[1].Content);
    }

    private sealed class NamedModel(string name) : IAgentModel
    {
        public string Name => name;

        public Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
