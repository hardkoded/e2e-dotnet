// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E;

/// <summary>
/// The part of upstream's <c>providerHints</c> (<c>agent/model/provider-hints.ts</c>) that works on the history the loop keeps.
/// The port's clients add the rest of the hints to their own request bodies, so only <c>afterEdit</c> lives here.
/// <para>
/// Anthropic binds every thinking block to the prefix that produced it (the system prompt and every message before the
/// block) and rejects a replayed block whose prefix changed. The loop keeps the system prompt of a step fixed; when it
/// rewrites an earlier message (a superseded screen elided, a history shrunk), the reasoning from that message on is left
/// out of the next request.
/// </para>
/// </summary>
internal sealed partial class ProviderHints
{
    private static readonly ProviderHints Anthropic = new(true);
    private static readonly ProviderHints Plain = new(false);

    private readonly bool _anthropic;

    private ProviderHints(bool anthropic) => _anthropic = anthropic;

    public static ProviderHints For(IAgentModel? model) => SpeaksAnthropic(model) ? Anthropic : Plain;

    /// <summary>
    /// The history to send once the message at <paramref name="index"/> was rewritten since the last request. Where the
    /// provider binds reasoning to the prefix, the reasoning from that message on is left out: text and tool calls stay, and a
    /// reply that was only reasoning goes. Dropping thinking from the end of a history is allowed, dropping it from the middle
    /// is not, which is why everything after the edit goes. Returns the input list when nothing is dropped.
    /// </summary>
    public IReadOnlyList<ModelMessage> AfterEdit(IReadOnlyList<ModelMessage> messages, int index)
    {
        ArgumentNullException.ThrowIfNull(messages);
        return _anthropic ? WithoutReasoningFrom(messages, index) : messages;
    }

    /// <summary>
    /// A gateway or OpenRouter model names its upstream in the model id (<c>anthropic/claude-…</c>), a direct client is the
    /// Anthropic client (also behind Vertex AI, which only changes its base URL), and Bedrock names it in the model id behind
    /// an optional region prefix (<c>eu.anthropic.claude-…</c>). A Bedrock application inference profile ARN names no model and
    /// is not recognized.
    /// </summary>
    private static bool SpeaksAnthropic(IAgentModel? model)
    {
        if (model is null)
        {
            return false;
        }

        if (model is AnthropicModel)
        {
            return true;
        }

        var id = model.Name.ToLowerInvariant();
        return id.StartsWith("anthropic/", StringComparison.Ordinal) || BedrockAnthropicId().IsMatch(id);
    }

    [GeneratedRegex(@"(?:^|\.)anthropic\.claude", RegexOptions.CultureInvariant)]
    private static partial Regex BedrockAnthropicId();

    /// <summary>The history without the reasoning of the assistant messages at or after <paramref name="index"/>.</summary>
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

            next.Add(new ModelMessage
            {
                Role = message.Role,
                Content = message.Content,
                ToolCalls = message.ToolCalls,
                ToolCallId = message.ToolCallId,
                Name = message.Name,
                Images = message.Images,
            });
        }

        return changed ? next : messages;
    }
}
