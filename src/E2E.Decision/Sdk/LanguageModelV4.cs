// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace E2E.Decision.Sdk;

/// <summary>
/// The AI SDK's <c>LanguageModelV4</c> (<c>language-model/v4/language-model-v4.ts</c>): a language model that implements the language model
/// interface version 4.
/// </summary>
public interface ILanguageModelV4
{
    /// <summary>The language model interface version the model implements: <c>v4</c>.</summary>
    string SpecificationVersion { get; }

    /// <summary>The provider ID.</summary>
    string Provider { get; }

    /// <summary>The provider-specific model ID.</summary>
    string ModelId { get; }

    /// <summary>
    /// Supported URL patterns by media type: the keys are media type patterns or full media types (<c>*/*</c>, <c>audio/*</c>,
    /// <c>application/pdf</c>) and the values are the patterns that match the lower-case URLs. A matched URL is supported natively by the
    /// model and is not downloaded.
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyList<Regex>> SupportedUrls { get; }

    /// <summary>Generates a language model output (non-streaming).</summary>
    Task<LanguageModelV4GenerateResult> DoGenerateAsync(
        LanguageModelV4CallOptions options,
        CancellationToken cancellationToken = default
    );

    /// <summary>Generates a language model output (streaming).</summary>
    Task<LanguageModelV4StreamResult> DoStreamAsync(
        LanguageModelV4CallOptions options,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// The AI SDK's <c>LanguageModelV4CallOptions</c> (<c>language-model-v4-call-options.ts</c>). Upstream's <c>abortSignal</c> is the
/// <see cref="CancellationToken"/> of <see cref="ILanguageModelV4.DoGenerateAsync"/> and <see cref="ILanguageModelV4.DoStreamAsync"/>.
/// </summary>
public sealed record LanguageModelV4CallOptions
{
    /// <summary>
    /// A language model prompt: the standardized prompt type. This is not the user-facing prompt; the AI SDK methods map the user-facing prompt
    /// types to this format.
    /// </summary>
    public required IReadOnlyList<LanguageModelV4Message> Prompt { get; init; }

    /// <summary>Maximum number of tokens to generate.</summary>
    public int? MaxOutputTokens { get; init; }

    /// <summary>Temperature setting. The range depends on the provider and model.</summary>
    public double? Temperature { get; init; }

    /// <summary>Stop sequences. If set, the model stops generating text when one of them is generated.</summary>
    public IReadOnlyList<string>? StopSequences { get; init; }

    /// <summary>Nucleus sampling.</summary>
    public double? TopP { get; init; }

    /// <summary>Only sample from the top K options for each subsequent token.</summary>
    public int? TopK { get; init; }

    /// <summary>Presence penalty setting: the likelihood of the model repeating information that is already in the prompt.</summary>
    public double? PresencePenalty { get; init; }

    /// <summary>Frequency penalty setting: the likelihood of the model repeatedly using the same words or phrases.</summary>
    public double? FrequencyPenalty { get; init; }

    /// <summary>Response format. The output can either be text or JSON. Default is text.</summary>
    public LanguageModelV4ResponseFormat? ResponseFormat { get; init; }

    /// <summary>The seed (integer) to use for random sampling. If set and supported by the model, calls generate deterministic results.</summary>
    public int? Seed { get; init; }

    /// <summary>The tools that are available for the model.</summary>
    public IReadOnlyList<LanguageModelV4Tool>? Tools { get; init; }

    /// <summary>Specifies how the tool should be selected. Defaults to <c>auto</c>.</summary>
    public LanguageModelV4ToolChoice? ToolChoice { get; init; }

    /// <summary>Include raw chunks in the stream. Only applicable for streaming calls.</summary>
    public bool? IncludeRawChunks { get; init; }

    /// <summary>Additional HTTP headers to be sent with the request. A null value leaves the header out.</summary>
    public IReadOnlyDictionary<string, string?>? Headers { get; init; }

    /// <summary>
    /// Reasoning effort level for the model: <c>provider-default</c>, <c>none</c>, <c>minimal</c>, <c>low</c>, <c>medium</c>, <c>high</c>,
    /// <c>xhigh</c>, or <c>max</c>. Defaults to <c>provider-default</c>.
    /// </summary>
    public string? Reasoning { get; init; }

    /// <summary>Additional provider-specific options, passed through to the provider, keyed by provider name.</summary>
    public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }
}

/// <summary>The <c>responseFormat</c> of <see cref="LanguageModelV4CallOptions"/>: text, or JSON with an optional schema.</summary>
public sealed record LanguageModelV4ResponseFormat
{
    /// <summary>Plain text output.</summary>
    public static LanguageModelV4ResponseFormat Text { get; } = new() { Type = "text" };

    /// <summary>The format type: <c>text</c> or <c>json</c>.</summary>
    public required string Type { get; init; }

    /// <summary>JSON schema that the generated output should conform to (<c>json</c> only).</summary>
    public JsonElement? Schema { get; init; }

    /// <summary>Name of the output that should be generated, used by some providers for additional guidance (<c>json</c> only).</summary>
    public string? Name { get; init; }

    /// <summary>Description of the output that should be generated, used by some providers for additional guidance (<c>json</c> only).</summary>
    public string? Description { get; init; }

    /// <summary>JSON output, optionally guided by a schema.</summary>
    public static LanguageModelV4ResponseFormat Json(
        JsonElement? schema = null,
        string? name = null,
        string? description = null
    ) =>
        new()
        {
            Type = "json",
            Schema = schema,
            Name = name,
            Description = description,
        };

    internal JsonObject ToJson()
    {
        var json = new JsonObject { ["type"] = Type };
        if (Schema is { } schema)
        {
            json["schema"] = JsonNode.Parse(schema.GetRawText());
        }

        if (Name is not null)
        {
            json["name"] = Name;
        }

        if (Description is not null)
        {
            json["description"] = Description;
        }

        return json;
    }
}

/// <summary>A tool of a call: a <see cref="LanguageModelV4FunctionTool"/> or a <see cref="LanguageModelV4ProviderTool"/>.</summary>
public abstract record LanguageModelV4Tool
{
    private protected LanguageModelV4Tool() { }

    internal abstract JsonObject ToJson();
}

/// <summary>A tool that has a name, a description, and a JSON schema for its input (<c>LanguageModelV4FunctionTool</c>).</summary>
/// <param name="Name">The name of the tool, unique within a call.</param>
/// <param name="InputSchema">The JSON schema of the tool's input.</param>
public sealed record LanguageModelV4FunctionTool(string Name, JsonElement InputSchema) : LanguageModelV4Tool
{
    /// <summary>A description of the tool, which the model uses to decide when and how to call it.</summary>
    public string? Description { get; init; }

    /// <summary>Example inputs of the tool, each a JSON object.</summary>
    public IReadOnlyList<JsonElement>? InputExamples { get; init; }

    /// <summary>The strict mode setting, for providers that support it.</summary>
    public bool? Strict { get; init; }

    /// <summary>Additional provider-specific options, keyed by provider name.</summary>
    public IReadOnlyDictionary<string, JsonElement>? ProviderOptions { get; init; }

    internal override JsonObject ToJson()
    {
        var json = new JsonObject { ["type"] = "function", ["name"] = Name };
        if (Description is not null)
        {
            json["description"] = Description;
        }

        json["inputSchema"] = JsonNode.Parse(InputSchema.GetRawText());
        if (InputExamples is not null)
        {
            json["inputExamples"] = new JsonArray([
                .. InputExamples.Select(example =>
                    (JsonNode?)new JsonObject { ["input"] = JsonNode.Parse(example.GetRawText()) }
                ),
            ]);
        }

        if (Strict is { } strict)
        {
            json["strict"] = strict;
        }

        LanguageModelV4Json.AddProviderOptions(json, ProviderOptions);
        return json;
    }
}

/// <summary>A tool that is built into a provider (<c>LanguageModelV4ProviderTool</c>).</summary>
/// <param name="Id">The ID of the tool, in the format <c>{provider}.{unique-tool-name}</c>.</param>
/// <param name="Name">The name of the tool, unique within a call.</param>
/// <param name="Args">The arguments of the tool, a JSON object.</param>
public sealed record LanguageModelV4ProviderTool(string Id, string Name, JsonElement Args) : LanguageModelV4Tool
{
    internal override JsonObject ToJson() =>
        new()
        {
            ["type"] = "provider",
            ["id"] = Id,
            ["name"] = Name,
            ["args"] = JsonNode.Parse(Args.GetRawText()),
        };
}

/// <summary>How the tool is selected (<c>LanguageModelV4ToolChoice</c>): automatic, none, required, or a specific tool.</summary>
public sealed record LanguageModelV4ToolChoice
{
    /// <summary>The tool selection is automatic; the model may call no tool.</summary>
    public static LanguageModelV4ToolChoice Auto { get; } = new() { Type = "auto" };

    /// <summary>No tool must be selected.</summary>
    public static LanguageModelV4ToolChoice None { get; } = new() { Type = "none" };

    /// <summary>One of the available tools must be selected.</summary>
    public static LanguageModelV4ToolChoice Required { get; } = new() { Type = "required" };

    /// <summary>The choice type: <c>auto</c>, <c>none</c>, <c>required</c>, or <c>tool</c>.</summary>
    public required string Type { get; init; }

    /// <summary>The name of the tool that must be selected (<c>tool</c> only).</summary>
    public string? ToolName { get; init; }

    /// <summary>A specific tool must be selected.</summary>
    public static LanguageModelV4ToolChoice Tool(string toolName) => new() { Type = "tool", ToolName = toolName };

    internal JsonObject ToJson()
    {
        var json = new JsonObject { ["type"] = Type };
        if (ToolName is not null)
        {
            json["toolName"] = ToolName;
        }

        return json;
    }
}

/// <summary>
/// The AI SDK's <c>LanguageModelV4GenerateResult</c> (<c>language-model-v4-generate-result.ts</c>) of the Gateway. The Gateway passes the
/// response through <c>z.any()</c>, so <see cref="Content"/>, <see cref="FinishReason"/>, <see cref="Usage"/>, and
/// <see cref="ProviderMetadata"/> are the JSON the Gateway sent, unvalidated, and any other member of the response is in
/// <see cref="AdditionalProperties"/>.
/// </summary>
public sealed class LanguageModelV4GenerateResult
{
    /// <summary>The ordered content that the model generated, as the Gateway sent it.</summary>
    public JsonElement? Content { get; init; }

    /// <summary>The text to parse for structured JSON output when it differs from the concatenation of the text parts of the content.</summary>
    public string? StructuredOutputText { get; init; }

    /// <summary>The finish reason, as the Gateway sent it.</summary>
    public JsonElement? FinishReason { get; init; }

    /// <summary>The usage information, as the Gateway sent it.</summary>
    public JsonElement? Usage { get; init; }

    /// <summary>Additional provider-specific metadata, as the Gateway sent it.</summary>
    public JsonElement? ProviderMetadata { get; init; }

    /// <summary>The request information, for telemetry and debugging.</summary>
    public LanguageModelV4RequestMetadata? Request { get; init; }

    /// <summary>The response information, for telemetry and debugging.</summary>
    public LanguageModelV4ResponseMetadata? Response { get; init; }

    /// <summary>The warnings for the call, for example unsupported settings: the Gateway's, then the model's own.</summary>
    public required IReadOnlyList<SharedV4Warning> Warnings { get; init; }

    /// <summary>The members of the Gateway's response that are not named above, such as <c>id</c>, <c>created</c>, and <c>model</c>.</summary>
    public IReadOnlyDictionary<string, JsonElement> AdditionalProperties { get; init; } =
        new Dictionary<string, JsonElement>();
}

/// <summary>The <c>request</c> of a language model result: the request information, for telemetry and debugging.</summary>
public sealed class LanguageModelV4RequestMetadata
{
    /// <summary>The actual HTTP endpoint used for the request, when known.</summary>
    public string? Url { get; init; }

    /// <summary>The request HTTP body that was sent to the provider API.</summary>
    public JsonNode? Body { get; init; }
}

/// <summary>
/// The AI SDK's <c>LanguageModelV4ResponseMetadata</c> (<c>language-model-v4-response-metadata.ts</c>) with the <c>headers</c> and <c>body</c>
/// of a result's <c>response</c>.
/// </summary>
public sealed class LanguageModelV4ResponseMetadata
{
    /// <summary>The ID for the generated response, if the provider sends one.</summary>
    public string? Id { get; init; }

    /// <summary>The timestamp for the start of the generated response, if the provider sends one.</summary>
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>The ID of the response model that was used to generate the response, if the provider sends one.</summary>
    public string? ModelId { get; init; }

    /// <summary>The response headers, lowercase names.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>The response HTTP body.</summary>
    public JsonElement? Body { get; init; }
}

/// <summary>
/// The AI SDK's <c>LanguageModelV4StreamResult</c> (<c>language-model-v4-stream-result.ts</c>). The stream owns the HTTP response: it is
/// released when the stream is read to its end, when its enumeration is disposed, or when it fails.
/// </summary>
public sealed class LanguageModelV4StreamResult
{
    /// <summary>The stream.</summary>
    public required IAsyncEnumerable<LanguageModelV4StreamPart> Stream { get; init; }

    /// <summary>The request information, for telemetry and debugging.</summary>
    public LanguageModelV4RequestMetadata? Request { get; init; }

    /// <summary>The response data: its headers.</summary>
    public LanguageModelV4ResponseMetadata? Response { get; init; }
}

/// <summary>
/// The AI SDK's <c>LanguageModelV4StreamPart</c> (<c>language-model-v4-stream-part.ts</c>) of the Gateway. The Gateway passes each part
/// through <c>z.any()</c>, so <see cref="Json"/> is the part as the Gateway sent it, unvalidated.
/// </summary>
public sealed class LanguageModelV4StreamPart
{
    /// <summary>The <c>type</c> of the part, such as <c>text-delta</c>, <c>finish</c>, or <c>raw</c>; empty when the part has none.</summary>
    public required string Type { get; init; }

    /// <summary>The part as the Gateway sent it. A <c>response-metadata</c> timestamp is still its ISO string here.</summary>
    public required JsonElement Json { get; init; }

    /// <summary>The <c>timestamp</c> of a <c>response-metadata</c> part whose timestamp the Gateway sent as a string (upstream's <c>Date</c>).</summary>
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>The warnings of a <c>stream-start</c> part.</summary>
    public IReadOnlyList<SharedV4Warning>? Warnings { get; init; }

    internal static LanguageModelV4StreamPart StreamStart(IReadOnlyList<SharedV4Warning> warnings) =>
        new()
        {
            Type = "stream-start",
            Json = JsonSerializer.SerializeToElement(
                new JsonObject
                {
                    ["type"] = "stream-start",
                    ["warnings"] = new JsonArray([.. warnings.Select(warning => (JsonNode?)warning.ToJson())]),
                }
            ),
            Warnings = warnings,
        };
}

/// <summary>
/// .NET-only: what <c>JSON.stringify</c> does for upstream's call options and prompt, which the Gateway language model sends as the
/// request body.
/// </summary>
internal static class LanguageModelV4Json
{
    public static void AddProviderOptions(JsonObject json, IReadOnlyDictionary<string, JsonElement>? providerOptions)
    {
        if (providerOptions is not null)
        {
            json["providerOptions"] = JsonSerializer.SerializeToNode(providerOptions);
        }
    }

    public static JsonArray Parts(IReadOnlyList<LanguageModelV4PromptPart> parts) =>
        new([.. parts.Select(part => (JsonNode?)part.ToJson())]);

    /// <summary>The call options as upstream's <c>JSON.stringify</c> writes them, without <c>abortSignal</c>.</summary>
    public static JsonObject ToJson(LanguageModelV4CallOptions options)
    {
        var json = new JsonObject
        {
            ["prompt"] = new JsonArray([.. options.Prompt.Select(message => (JsonNode?)message.ToJson())]),
        };
        if (options.MaxOutputTokens is { } maxOutputTokens)
        {
            json["maxOutputTokens"] = maxOutputTokens;
        }

        if (options.Temperature is { } temperature)
        {
            json["temperature"] = temperature;
        }

        if (options.StopSequences is not null)
        {
            json["stopSequences"] = new JsonArray([.. options.StopSequences.Select(text => (JsonNode?)text)]);
        }

        if (options.TopP is { } topP)
        {
            json["topP"] = topP;
        }

        if (options.TopK is { } topK)
        {
            json["topK"] = topK;
        }

        if (options.PresencePenalty is { } presencePenalty)
        {
            json["presencePenalty"] = presencePenalty;
        }

        if (options.FrequencyPenalty is { } frequencyPenalty)
        {
            json["frequencyPenalty"] = frequencyPenalty;
        }

        if (options.ResponseFormat is not null)
        {
            json["responseFormat"] = options.ResponseFormat.ToJson();
        }

        if (options.Seed is { } seed)
        {
            json["seed"] = seed;
        }

        if (options.Tools is not null)
        {
            json["tools"] = new JsonArray([.. options.Tools.Select(tool => (JsonNode?)tool.ToJson())]);
        }

        if (options.ToolChoice is not null)
        {
            json["toolChoice"] = options.ToolChoice.ToJson();
        }

        if (options.IncludeRawChunks is { } includeRawChunks)
        {
            json["includeRawChunks"] = includeRawChunks;
        }

        if (options.Headers is not null)
        {
            var headers = new JsonObject();
            foreach (var (name, value) in options.Headers)
            {
                // JSON.stringify leaves out an undefined value.
                if (value is not null)
                {
                    headers[name] = value;
                }
            }

            json["headers"] = headers;
        }

        if (options.Reasoning is not null)
        {
            json["reasoning"] = options.Reasoning;
        }

        AddProviderOptions(json, options.ProviderOptions);
        return json;
    }

    /// <summary>The ISO 8601 timestamp the Gateway sent, as upstream's <c>new Date(string)</c> reads it; null when it is not a date.</summary>
    public static DateTimeOffset? ParseTimestamp(string text) =>
        DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.RoundtripKind,
            out var timestamp
        )
            ? timestamp
            : null;
}
