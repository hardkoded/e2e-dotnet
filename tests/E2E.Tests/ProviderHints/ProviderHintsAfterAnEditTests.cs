// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Tests.ProviderHints;

/// <summary>Upstream's nested <c>describe('after an edit')</c> of "providerHints for Anthropic".</summary>
public sealed class ProviderHintsAfterAnEditTests
{
    private static readonly global::E2E.ProviderHints Hints = global::E2E.ProviderHints.For(new AnthropicModel(new AnthropicModelOptions { Model = "claude-opus-5-5", ApiKey = "k" }, new HttpClient()));

    private static JsonElement Thinking() => JsonDocument.Parse("""{ "type": "thinking", "thinking": "", "signature": "sig" }""").RootElement.Clone();

    private static ModelToolCall Call(string id) => new() { Id = id, Name = "tap", Arguments = JsonDocument.Parse("{}").RootElement.Clone() };

    private static ModelMessage Result(string id) => new() { Role = "tool", ToolCallId = id, Name = "tap", Content = "ok" };

    private static List<ModelMessage> History() =>
    [
        new() { Role = "user", Content = "step" },
        new() { Role = "assistant", ToolCalls = [Call("c1")], ReasoningItems = [Thinking()] },
        Result("c1"),
        new() { Role = "assistant", ReasoningItems = [Thinking()] },
        new() { Role = "user", Content = "notice" },
        new() { Role = "assistant", ToolCalls = [Call("c2")], ReasoningItems = [Thinking()] },
        Result("c2"),
    ];

    [Fact]
    public void Drops_the_reasoning_from_the_edited_message_on_and_a_reply_that_was_only_reasoning()
    {
        var history = History();

        var after = Hints.AfterEdit(history, 2);

        // Untouched messages carry over as the same objects; the edited one and the reply after it keep their calls.
        Assert.Same(history[0], after[0]);
        Assert.Same(history[1], after[1]);
        Assert.Same(history[2], after[2]);
        Assert.Equal(6, after.Count);
        Assert.Same(history[4], after[3]);
        Assert.Equal("c2", Assert.Single(after[4].ToolCalls!).Id);
        Assert.Null(after[4].ReasoningItems);
        Assert.Same(history[6], after[^1]);
    }

    [Fact]
    public void Returns_the_history_itself_when_nothing_from_the_edit_on_reasoned()
    {
        var history = History();

        Assert.Same(history, Hints.AfterEdit(history, 6));
    }
}
