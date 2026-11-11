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

    /// <summary>Upstream's <c>ModelCall.maxRetries</c>: provider transport retries for this call. Null keeps <see cref="ModelHttp.TransportRetries"/>.</summary>
    public int? MaxRetries { get; init; }

    /// <summary>
    /// Upstream's <c>ModelCall.toolChoice</c> (the AI SDK's <c>toolChoice</c>): how the model must use the tools it is offered. Null leaves
    /// the choice to the provider, which is <see cref="ModelToolChoice.Auto"/>. The act tool loop asks for <see cref="ModelToolChoice.Required"/>
    /// and names <c>done</c> on its forced-conclusion turns. A custom <see cref="IAgentModel"/> that cannot force a tool may ignore it.
    /// </summary>
    public ModelToolChoice? ToolChoice { get; init; }

    /// <summary>Redacts the secrets of the step from a provider failure that quotes the response body.</summary>
    internal Redactor Redactor { get; init; } = Redactor.None;
}

public sealed class ModelMessage
{
    public required string Role { get; init; }

    public string? Content { get; init; }

    /// <summary>
    /// Upstream's <c>error-text</c> tool output: the call failed before its text was read (the SDK refused it, or the tool threw), so the
    /// failure streak counts it whatever the text says. Read by the loop's guards only; no provider receives it.
    /// </summary>
    internal bool ErrorText { get; init; }

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

/// <summary>
/// The AI SDK's <c>ToolChoice</c>: <c>auto</c> lets the model answer in prose or call any offered tool, <c>required</c> makes it call some
/// tool, and a named tool makes it call that one. Each provider sends its own form of it (Anthropic's <c>any</c>, Bedrock's <c>any</c>,
/// OpenAI's <c>required</c>, Gemini's <c>ANY</c>).
/// </summary>
public sealed record ModelToolChoice
{
    private ModelToolChoice(string type, string? toolName = null)
    {
        Type = type;
        ToolName = toolName;
    }

    /// <summary>The model decides whether to call a tool (<c>auto</c>).</summary>
    public static ModelToolChoice Auto { get; } = new("auto");

    /// <summary>The model must call some tool (<c>required</c>).</summary>
    public static ModelToolChoice Required { get; } = new("required");

    /// <summary>The model must call the tool named <paramref name="toolName"/> (the SDK's <c>{ type: 'tool', toolName }</c>).</summary>
    public static ModelToolChoice Tool(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        return new("tool", toolName);
    }

    /// <summary><c>auto</c>, <c>required</c>, or <c>tool</c>.</summary>
    public string Type { get; }

    /// <summary>The tool a <c>tool</c> choice names. Null for the other two.</summary>
    public string? ToolName { get; }

    /// <summary>Whether the choice makes the model call a tool (anything but <c>auto</c>).</summary>
    public bool Forced => Type != "auto";
}

/// <summary>
/// One of upstream's call warnings (<c>CallWarning</c>): the provider changed the request instead of failing it. A provider that sends
/// <c>auto</c> in place of the forced tool choice it was asked for says so with <c>Type</c> <c>unsupported</c> and <c>Feature</c> <c>toolChoice</c>.
/// </summary>
/// <param name="Type">The kind of warning, such as <c>unsupported</c>.</param>
/// <param name="Feature">The request feature it is about, such as <c>toolChoice</c>.</param>
/// <param name="Details">What the provider did about it. Provider text: redact it before it reaches a message.</param>
public sealed record ModelWarning(string Type, string? Feature = null, string? Details = null);

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

    /// <summary>
    /// What the provider attached to the call, keyed by provider (such as <c>google</c>: <c>{ "thoughtSignature": "..." }</c>), which the
    /// call carries back on the next turn (upstream's <c>providerMetadata</c> of a tool call part). null when it attached none.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement>? ProviderMetadata { get; init; }
}

public sealed class ModelCallUsage
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

    public ModelCallUsage? Usage { get; init; }

    /// <summary>The reasoning of the turn that the model can take back, to replay on the next turn. Empty when the model gave none.</summary>
    public IReadOnlyList<JsonElement> ReasoningItems { get; init; } = [];

    /// <summary>
    /// The reasoning text of the turn (upstream's <c>StepResult.reasoningText</c>), null when the model gave none. Provider text: the
    /// step's <c>model</c> event carries it only after the redactor and a 600-character bound.
    /// </summary>
    public string? ReasoningText { get; init; }

    /// <summary>
    /// What the provider attached to the whole turn, keyed by provider (such as <c>gateway</c>: <c>{ "cost": "0.0001" }</c>), as upstream's
    /// <c>StepResult.providerMetadata</c>. The step's trace carries it as <c>providerMetadata</c> of the step end, redacted. null when the
    /// provider attached none.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement>? ProviderMetadata { get; init; }

    /// <summary>Upstream's call <c>warnings</c> (<c>StepResult.warnings</c>): what the provider changed in the request without failing it. Empty when it changed nothing.</summary>
    public IReadOnlyList<ModelWarning> Warnings { get; init; } = [];

    /// <summary>The AI SDK finish reason that matters to the agent: <see cref="ContentFilter"/> when the provider stopped the turn because it refused the request, or <see cref="ErrorFinish"/> when it ended on an error. Null for any other turn.</summary>
    public string? FinishReason { get; init; }

    /// <summary>The provider's own word for the stop, such as <c>refusal</c>, when it sent one. Provider text: redact it before it reaches a message.</summary>
    public string? RawFinishReason { get; init; }

    /// <summary>The finish reason of a turn the provider refused (upstream's <c>content-filter</c>).</summary>
    public const string ContentFilter = "content-filter";

    /// <summary>The finish reason of a turn the provider ended on an error (upstream's <c>error</c>), whatever text came with it. No built-in provider reports it; a custom <see cref="IAgentModel"/> may.</summary>
    public const string ErrorFinish = "error";
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

    /// <summary>
    /// A judge's answer: one <c>agent-judgment-2</c> object as the content of the turn. <paramref name="verdict"/> is
    /// <c>holds</c>, <c>fails</c>, or <c>inconclusive</c>.
    /// </summary>
    public static ModelResponse Judgment(string verdict, string explanation)
    {
        return new ModelResponse
        {
            Content = JsonSerializer.Serialize(new { protocolVersion = "agent-judgment-2", explanation, verdict }, JsonDefaults.Options),
        };
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

/// <summary>
/// Returns whatever the callback decides for each request. Records every request. A callback that serves both the act loop
/// and the judge may answer a judgment (a request with a <see cref="ModelRequest.Schema"/>) with <see cref="ModelResponses.Done"/>:
/// <c>passed</c> reads as <c>holds</c>, a <c>failed</c> without the code <c>ASSERTION_INCONCLUSIVE</c> as <c>fails</c>, and a
/// <c>blocked</c> or inconclusive one as <c>inconclusive</c>. <see cref="ModelResponses.Judgment"/> answers it directly.
/// </summary>
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
        var response = _next(request);
        return Task.FromResult(request.Schema is null ? response : AsJudgment(response));
    }

    private static ModelResponse AsJudgment(ModelResponse response)
    {
        var done = response.ToolCalls.FirstOrDefault(call => string.Equals(call.Name, "done", StringComparison.Ordinal));
        if (done is null || response.Content is not null)
        {
            return response;
        }

        var status = Args.String(done.Arguments, "status");
        var code = Args.String(done.Arguments, "code");
        var verdict = status switch
        {
            "passed" => "holds",
            "failed" when code != "ASSERTION_INCONCLUSIVE" => "fails",
            _ => "inconclusive",
        };
        return new ModelResponse { Content = ModelResponses.Judgment(verdict, Args.String(done.Arguments, "summary") ?? "").Content, Usage = response.Usage, ReasoningText = response.ReasoningText, ProviderMetadata = response.ProviderMetadata };
    }
}

/// <summary>The input bounds of upstream's <c>agent/model/adapter.ts</c>, which the observation budget and the judgment pre-flight share.</summary>
internal static class ModelAdapter
{
    /// <summary>Side of the square patch tile-based vision encoders consume an image in.</summary>
    private const int ImageTilePx = 28;

    /// <summary>
    /// Conservative token upper bound. UTF-8 byte length bounds every byte-level tokenizer from above, so it is used when a provider
    /// reports no usage and for the pre-flight ceiling. Every budget that mixes with this one (reserves, ledger size, observation size)
    /// is therefore in the same byte-scaled units, not in real tokens.
    /// </summary>
    public static int TokenUpperBound(string text) => System.Text.Encoding.UTF8.GetByteCount(text);

    /// <summary>
    /// Conservative token bound for one image: vision providers bill images by fixed-size patches rather than bytes, so the count of
    /// patches covering the image bounds every such encoder from above.
    /// </summary>
    public static int ImageTokenUpperBound(int width, int height) =>
        (int)Math.Ceiling(Math.Max(1, width) / (double)ImageTilePx) * (int)Math.Ceiling(Math.Max(1, height) / (double)ImageTilePx);
}
