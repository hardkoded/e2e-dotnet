// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace E2E.Internal;

/// <summary>
/// Upstream's <c>agent/model/tool-choice.ts</c>: recognizes a provider refusing a forced tool choice. The act loop asks for a tool call
/// on every turn (<c>required</c>) and names <c>done</c> on the final ones; some models reject that request shape outright (Anthropic's
/// Claude Fable 5.1 answers HTTP 400 with <c>tool_choice: type "tool" and "any" are not supported for this model.</c>; DeepSeek's V4
/// models think by default and answer a forced choice with <c>Thinking mode does not support this tool_choice</c>; Meta's Muse Spark
/// models answer <c>only "auto" is supported for tool_choice</c>), and a gateway forwards the provider's words. The loop reacts to this
/// refusal differently from any other provider failure: the same request with <c>auto</c> and an instruction to answer with tool calls
/// can still succeed, where a 5xx needs only time.
/// </summary>
internal static class ToolChoice
{
    /// <summary>Upstream's <c>MAX_CAUSE_DEPTH</c>: the longest cause chain walked; a wrapped error rarely nests deeper.</summary>
    private const int MaxCauseDepth = 8;

    /// <summary>Upstream's <c>FORCED_CHOICE_PATTERNS</c>.</summary>
    private static readonly Regex[] ForcedChoicePatterns =
    [
        Pattern("tool_choice.*not supported"), // Anthropic
        Pattern("does not support (?:this |the )?tool_choice"), // DeepSeek V4 in thinking mode (its default)
        Pattern("tool_choice.*(?:unsupported|is not allowed|cannot be)"), // OpenAI-compatible proxies
        Pattern(@"only\W+auto\W+is supported for\W+tool_choice"), // Meta Muse Spark
        Pattern("(?:forced|required) tool (?:choice|call|use).*not supported"), // generic
        Pattern("does not support (?:forced|required) tool"), // generic
    ];

    /// <summary>
    /// Upstream's <c>isForcedToolChoiceDowngraded</c>: whether a call warning says the provider sent <c>auto</c> in place of the forced
    /// choice it was asked for. The AI SDK's Anthropic provider does this itself for models it knows reject forced tool use, so the
    /// request succeeds and no refusal ever reaches the loop.
    /// </summary>
    public static bool IsForcedToolChoiceDowngraded(ModelWarning warning)
    {
        ArgumentNullException.ThrowIfNull(warning);
        return string.Equals(warning.Type, "unsupported", StringComparison.Ordinal)
            && string.Equals(warning.Feature, "toolChoice", StringComparison.Ordinal);
    }

    /// <summary>
    /// Upstream's <c>isForcedToolCallSkipped</c>: whether a turn under a forced choice came back as a finished reply without a tool call.
    /// The AI SDK throws <c>AI_ToolChoiceViolationError</c> for that, which is how a downgrading provider's prose answer surfaces; the
    /// port reads the same fact off the response (<see cref="ToolChoiceViolationException"/>). A reply cut off by the token limit or a
    /// content filter says nothing about the provider and does not count.
    /// </summary>
    public static bool IsForcedToolCallSkipped(object? error) =>
        error is ToolChoiceViolationException { FinishReason: "stop" };

    /// <summary>
    /// Upstream's <c>isForcedToolChoiceRejected</c>: whether a provider failure says the model does not accept a forced tool choice.
    /// Reads the message of the error and of everything it wraps. Only a 4xx counts: a gateway or proxy that fails for its own reasons
    /// while quoting the request never reads as a model limitation. The port's failure message already quotes the response body
    /// (<c>HTTP 400: {body}</c>), where upstream reads <c>responseBody</c> apart.
    /// </summary>
    public static bool IsForcedToolChoiceRejected(Exception? error)
    {
        var current = error;
        for (var depth = 0; depth < MaxCauseDepth && current is not null; depth++)
        {
            if (DescribesRejection(current))
            {
                return true;
            }

            // Upstream's `cause ?? lastError`: the last attempt of a spent retry chain stands in for a missing cause.
            current = current.InnerException ?? (current as RetryError)?.LastError;
        }

        return false;
    }

    private static bool DescribesRejection(Exception error)
    {
        if (error is AgentException { ProviderStatus: { } status } && (status < 400 || status >= 500))
        {
            return false;
        }

        return ForcedChoicePatterns.Any(pattern => pattern.IsMatch(error.Message));
    }

    private static Regex Pattern(string source) =>
        new(source, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
}

/// <summary>
/// The AI SDK's <c>AI_ToolChoiceViolationError</c>, for a turn under a forced tool choice that answered without a tool call. The SDK
/// throws it; the port's loop builds it from the response so <see cref="ToolChoice.IsForcedToolCallSkipped"/> reads the same fact.
/// .NET-only: the port's models return a response where the SDK throws.
/// </summary>
internal sealed class ToolChoiceViolationException(string finishReason)
    : Exception("Model response did not contain a tool call even though tool choice was required.")
{
    /// <summary>The finish reason of the reply: <c>stop</c> for a finished one, anything else for a reply cut off or filtered.</summary>
    public string FinishReason { get; } = finishReason;
}

/// <summary>
/// The AI SDK's <c>RetryError</c> (<c>reason: 'maxRetriesExceeded'</c>): a request whose retries are spent, carrying every attempt's
/// failure and the last one. <see cref="ToolChoice.IsForcedToolChoiceRejected"/> follows <see cref="LastError"/> as upstream follows
/// <c>cause ?? lastError</c>. .NET-only: <c>ModelHttp.SendAsync</c> throws the last attempt's failure itself (callers match its
/// <c>AgentException</c> code), so nothing in the port throws this today; a caller that wraps a spent chain can.
/// </summary>
internal sealed class RetryError(string message, IReadOnlyList<Exception> errors) : Exception(message)
{
    /// <summary>Upstream's <c>errors</c>: the failure of each attempt, oldest first.</summary>
    public IReadOnlyList<Exception> Errors { get; } = [.. errors];

    /// <summary>Upstream's <c>lastError</c>: the failure of the final attempt.</summary>
    public Exception? LastError => Errors.Count > 0 ? Errors[^1] : null;
}
