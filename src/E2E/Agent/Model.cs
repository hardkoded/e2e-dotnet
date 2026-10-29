// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Internal;

namespace E2E;

/// <summary>One turn of the agent model. Tool calls are executed by the agent, never by the model itself.</summary>
public interface IAgentModel
{
    /// <summary>Provider and model id, printed in the run summary. Changing it does not miss the replay cache.</summary>
    string Name { get; }

    Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken);
}

public sealed class ModelRequest
{
    public required string System { get; init; }

    public required IReadOnlyList<ModelMessage> Messages { get; init; }

    public required IReadOnlyList<ModelTool> Tools { get; init; }

    /// <summary>
    /// The agent's provider options, keyed by provider, for the model to pass on. Null when none are set.
    /// <see cref="OpenAiCompatibleModel"/> adds the entry under its provider name to the request body.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }

    /// <summary>
    /// Upstream's <c>ProviderHints.markLatest</c>, which only the act tool loop calls: the next turn repeats everything up
    /// to the newest message, so <see cref="AnthropicModel"/> marks that message for caching. Judgments and extractions leave it off.
    /// </summary>
    public bool MarkLatest { get; init; }

    /// <summary>
    /// Upstream's <c>ModelCall.schema</c>: a closed response grammar the provider is asked to fill as structured output. When it is
    /// set, <see cref="Tools"/> is empty and the model answers with one JSON object that matches it, as <see cref="ModelResponse.Content"/>;
    /// the agent validates that text itself. Null for a turn that offers tools. A custom <see cref="IAgentModel"/> must handle it.
    /// </summary>
    public JsonElement? Schema { get; init; }

    /// <summary>Upstream's <c>ModelCall.schemaName</c>: the name sent with <see cref="Schema"/>, for the providers that name a structured output.</summary>
    public string? SchemaName { get; init; }

    /// <summary>Redacts the secrets of the step from a provider failure that quotes the response body.</summary>
    internal Redactor Redactor { get; init; } = Redactor.None;
}

public sealed class ModelMessage
{
    public required string Role { get; init; }

    public string? Content { get; init; }

    public IReadOnlyList<ModelToolCall>? ToolCalls { get; init; }

    public string? ToolCallId { get; init; }

    public string? Name { get; init; }

    /// <summary>The reasoning the model returned with this turn, sent back as it came: encrypted items for <see cref="OpenAiResponsesModel"/>, signed thinking blocks for <see cref="AnthropicModel"/>, thought parts for <see cref="GoogleModel"/>, the <c>reasoningContent</c> blocks for <see cref="BedrockModel"/>, the reasoning text as a JSON string for <see cref="OpenAiCompatibleModel"/>.</summary>
    public IReadOnlyList<JsonElement>? ReasoningItems { get; init; }

    /// <summary>The images that go with a <c>user</c> turn, after its text. A provider sends them as the turn's image parts; on any other role they are not sent.</summary>
    public IReadOnlyList<ModelImage>? Images { get; init; }
}

/// <summary>An image a model reads: the encoded bytes of a screenshot, already sized for the model.</summary>
/// <param name="Data">The encoded image.</param>
/// <param name="MediaType">The media type of <paramref name="Data"/>, such as <c>image/png</c>.</param>
/// <param name="Width">Width in image pixels.</param>
/// <param name="Height">Height in image pixels.</param>
public sealed record ModelImage(byte[] Data, string MediaType, int Width, int Height);

public sealed class ModelTool
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required JsonElement Parameters { get; init; }
}

public sealed class ModelToolCall
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required JsonElement Arguments { get; init; }
}

public sealed class ModelUsage
{
    public int InputTokens { get; init; }

    public int OutputTokens { get; init; }

    /// <summary>Input tokens the provider served from its prompt cache, when it reports the split. A part of <see cref="InputTokens"/>.</summary>
    public int? CacheReadTokens { get; init; }

    /// <summary>Input tokens the provider wrote to its prompt cache, when it reports the split. A part of <see cref="InputTokens"/>.</summary>
    public int? CacheWriteTokens { get; init; }

    /// <summary>The billed cost of the call in US dollars, when the provider reports one.</summary>
    public double? EstimatedCostUsd { get; init; }
}

public sealed class ModelResponse
{
    public string? Content { get; init; }

    public IReadOnlyList<ModelToolCall> ToolCalls { get; init; } = [];

    public ModelUsage? Usage { get; init; }

    /// <summary>The reasoning of the turn that the model can take back, to replay on the next turn. Empty when the model gave none.</summary>
    public IReadOnlyList<JsonElement> ReasoningItems { get; init; } = [];

    /// <summary>The AI SDK finish reason that matters to the agent: <see cref="ContentFilter"/> when the provider stopped the turn because it refused the request. Null for any other turn.</summary>
    public string? FinishReason { get; init; }

    /// <summary>The provider's own word for the stop, such as <c>refusal</c>, when it sent one. Provider text: redact it before it reaches a message.</summary>
    public string? RawFinishReason { get; init; }

    /// <summary>The finish reason of a turn the provider refused (upstream's <c>content-filter</c>).</summary>
    public const string ContentFilter = "content-filter";
}

/// <summary>Builds the tool calls a <see cref="ScriptedModel"/> (or a test) returns.</summary>
public static class ModelResponses
{
    public static ModelResponse Done(string status, string summary, string? code = null)
    {
        return Call("done", code is null
            ? new { status, summary }
            : (object)new { status, summary, code });
    }

    public static ModelResponse Tap(string role, string name)
    {
        return Call("tap", new { role, name });
    }

    public static ModelResponse Fill(string role, string name, string value)
    {
        return Call("fill", new { role, name, value });
    }

    public static ModelResponse Call(string name, object arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(arguments);
        return new ModelResponse
        {
            ToolCalls =
            [
                new ModelToolCall
                {
                    Id = "call_" + name,
                    Name = name,
                    Arguments = JsonSerializer.SerializeToElement(arguments, JsonDefaults.Options),
                },
            ],
        };
    }
}

/// <summary>Returns whatever the callback decides for each request. Records every request.</summary>
public sealed class ScriptedModel : IAgentModel
{
    private readonly Func<ModelRequest, ModelResponse> _next;

    public ScriptedModel(Func<ModelRequest, ModelResponse> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    public string Name => "scripted";

    public int CallCount { get; private set; }

    public List<ModelRequest> Requests { get; } = [];

    public Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        Requests.Add(request);
        return Task.FromResult(_next(request));
    }
}
