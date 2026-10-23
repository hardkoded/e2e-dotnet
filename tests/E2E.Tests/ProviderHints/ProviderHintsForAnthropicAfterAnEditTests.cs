// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Tests.ProviderHints;

/// <summary>
/// Upstream's "after an edit" describe of "providerHints for Anthropic". The port asks the model it was given, so the hint is
/// <c>E2E.ProviderHints.AfterEdit</c> over an <see cref="AnthropicModel"/>. Upstream's "recognizes Claude on %j" cases (Bedrock,
/// Vertex, OpenRouter ids) are not ported: the port has no <c>providerHints</c> function to recognize a model, and those
/// platforms have no client that replays Anthropic thinking.
/// </summary>
public sealed class ProviderHintsForAnthropicAfterAnEditTests
{
    private static readonly JsonElement Reasoning = JsonDocument.Parse("""{ "type": "thinking", "thinking": "", "signature": "sig" }""").RootElement.Clone();

    private static readonly ModelToolCall C1 = Call("c1");

    private static readonly ModelToolCall C2 = Call("c2");

    private static ModelToolCall Call(string id) => new() { Id = id, Name = "tap", Arguments = JsonDocument.Parse("{}").RootElement.Clone() };

    private static ModelMessage Result(string id) => new() { Role = "tool", ToolCallId = id, Name = "tap", Content = "ok" };

    private static List<ModelMessage> History() =>
    [
        new() { Role = "user", Content = "step" },
        new() { Role = "assistant", ToolCalls = [C1], ReasoningItems = [Reasoning] },
        Result("c1"),
        new() { Role = "assistant", ReasoningItems = [Reasoning] },
        new() { Role = "user", Content = "notice" },
        new() { Role = "assistant", ToolCalls = [C2], ReasoningItems = [Reasoning] },
        Result("c2"),
    ];

    private static AnthropicModel Model() => new(new AnthropicModelOptions { Model = "claude-opus-5.5", ApiKey = "k" }, new HttpClient());

    [Fact]
    public void Drops_the_reasoning_from_the_edited_message_on_and_a_reply_that_was_only_reasoning()
    {
        var history = History();
        using var model = Model();

        var after = E2E.ProviderHints.AfterEdit(model, history, 2);

        // Untouched messages carry over as the same objects; the edited one and the reply after it keep their calls.
        Assert.Same(history[0], after[0]);
        Assert.Same(history[1], after[1]);
        Assert.Same(history[2], after[2]);
        Assert.Equal(6, after.Count);
        Assert.Same(history[4], after[3]);
        Assert.Equal([C2], after[4].ToolCalls);
        Assert.Same(history[6], after[^1]);
        Assert.All(after.Skip(2), message => Assert.Null(message.ReasoningItems));
    }

    [Fact]
    public void Returns_the_history_itself_when_nothing_from_the_edit_on_reasoned()
    {
        var history = History();
        using var model = Model();

        Assert.Same(history, E2E.ProviderHints.AfterEdit(model, history, 6));
    }

    [Fact]
    public void Does_not_touch_the_messages_of_a_model_that_binds_no_reasoning_to_the_prefix()
    {
        var history = History();
        using var model = new OpenAiCompatibleModel(new OpenAiCompatibleModelOptions { Model = "gpt-5.6", ApiKey = "k" }, new HttpClient());

        Assert.Same(history, E2E.ProviderHints.AfterEdit(model, history, 0));
    }
}
