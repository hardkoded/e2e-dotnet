// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.OAuth;

namespace E2E;

/// <summary>
/// The part of upstream's <c>provider-hints.ts</c> that the tool loop asks for: <c>afterEdit</c>. The port has no
/// <c>providerHints</c> function, so the loop asks the model it was given. Anthropic binds every thinking block to the prefix
/// that produced it (the system prompt and every message before the block) and rejects a replayed block whose prefix changed.
/// The loop keeps the system prompt of a step fixed; when it rewrites an earlier message (a history shrunk, or one an executor's
/// <see cref="ToolLoopExecutorOptions.PrepareMessages"/> changed), the reasoning from that message on is left out of the next request.
/// </summary>
internal static class ProviderHints
{
    /// <summary>
    /// The history to send once the message at <paramref name="index"/> was rewritten since the last request. Where the provider binds
    /// reasoning to the prefix (<see cref="AnthropicModel"/>), the reasoning from that message on is left out: text and tool calls stay,
    /// and a reply that was only reasoning goes. Dropping thinking from the end of a history is allowed, dropping it from the middle
    /// is not, which is why everything after the edit goes. Returns the input list when nothing is dropped.
    /// </summary>
    public static IReadOnlyList<ModelMessage> AfterEdit(IAgentModel? model, IReadOnlyList<ModelMessage> messages, int index)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return BindsReasoningToPrefix(model) ? WithoutReasoningFrom(messages, index) : messages;
    }

    /// <summary>
    /// Whether the model's provider rejects a replayed reasoning block whose prefix changed. A <see cref="DelegatingModel"/> counts as the
    /// model it settled on: a wrapper that reaches Claude over the Anthropic route binds reasoning too. (.NET-only: upstream sees the provider id.)
    /// </summary>
    public static bool BindsReasoningToPrefix(IAgentModel? model)
    {
        while (model is DelegatingModel wrapper)
        {
            model = wrapper.Resolved;
        }

        return model is AnthropicModel;
    }

    private static IReadOnlyList<ModelMessage> WithoutReasoningFrom(IReadOnlyList<ModelMessage> messages, int index)
    {
        var changed = false;
        var next = new List<ModelMessage>(messages.Count);
        for (var position = 0; position < messages.Count; position++)
        {
            var message = messages[position];
            if (position < index || message.Role != "assistant" || message.ReasoningItems is not { Count: > 0 })
            {
                next.Add(message);
                continue;
            }

            changed = true;
            if (string.IsNullOrEmpty(message.Content) && message.ToolCalls is not { Count: > 0 })
            {
                continue;
            }

            next.Add(new ModelMessage { Role = message.Role, Content = message.Content, ToolCalls = message.ToolCalls, ToolCallId = message.ToolCallId, Name = message.Name, Images = message.Images });
        }

        return changed ? next : messages;
    }
}
