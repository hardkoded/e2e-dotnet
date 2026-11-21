// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace E2E.Decision.Sdk;

/// <summary>The AI SDK's <c>RealtimeModelV4ToolDefinition</c>: a function the model can call in a realtime session.</summary>
public sealed class RealtimeModelV4ToolDefinition
{
    /// <summary>The type of the tool (always <c>function</c>).</summary>
    public string Type { get; init; } = "function";

    /// <summary>The name of the tool. Unique within the session.</summary>
    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>JSON Schema describing the parameters the tool expects.</summary>
    public required JsonNode Parameters { get; init; }
}

/// <summary>The <c>inputAudioTranscription</c> and <c>outputAudioTranscription</c> of a <see cref="RealtimeModelV4SessionConfig"/>.</summary>
public sealed record RealtimeModelV4AudioTranscription(
    string? Model = null,
    string? Language = null,
    string? Prompt = null
);

/// <summary>The <c>turnDetection</c> of a <see cref="RealtimeModelV4SessionConfig"/>.</summary>
public sealed record RealtimeModelV4TurnDetection(
    string Type,
    double? Threshold = null,
    double? SilenceDurationMs = null,
    double? PrefixPaddingMs = null
);

/// <summary>The AI SDK's <c>RealtimeModelV4SessionConfig</c>: the provider-neutral configuration of a realtime session.</summary>
public sealed class RealtimeModelV4SessionConfig
{
    public string? Instructions { get; init; }

    public string? Voice { get; init; }

    /// <summary>Which output modalities the model should produce (<c>text</c> or <c>audio</c>).</summary>
    public IReadOnlyList<string>? OutputModalities { get; init; }

    public SharedV4AudioFormat? InputAudioFormat { get; init; }

    public RealtimeModelV4AudioTranscription? InputAudioTranscription { get; init; }

    public RealtimeModelV4AudioTranscription? OutputAudioTranscription { get; init; }

    public SharedV4AudioFormat? OutputAudioFormat { get; init; }

    /// <summary>Voice activity detection. Null turns it off (push-to-talk).</summary>
    public RealtimeModelV4TurnDetection? TurnDetection { get; init; }

    public IReadOnlyList<RealtimeModelV4ToolDefinition>? Tools { get; init; }

    /// <summary>Provider-specific options that are passed through to the provider.</summary>
    public IReadOnlyDictionary<string, JsonNode?>? ProviderOptions { get; init; }
}

/// <summary>The AI SDK's <c>RealtimeModelV4ConversationItem</c>: an item the client creates in the conversation.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RealtimeModelV4TextMessage), "text-message")]
[JsonDerivedType(typeof(RealtimeModelV4AudioMessage), "audio-message")]
[JsonDerivedType(typeof(RealtimeModelV4FunctionCallOutput), "function-call-output")]
public abstract record RealtimeModelV4ConversationItem;

/// <summary>A text message from the user.</summary>
public sealed record RealtimeModelV4TextMessage(string Role, string Text) : RealtimeModelV4ConversationItem;

/// <summary>An audio message from the user (complete audio, not streamed), as base64.</summary>
public sealed record RealtimeModelV4AudioMessage(string Role, string Audio) : RealtimeModelV4ConversationItem;

/// <summary>The output of a function call, sent back to the model so it can continue.</summary>
public sealed record RealtimeModelV4FunctionCallOutput(string CallId, string Output, string? Name = null)
    : RealtimeModelV4ConversationItem;

/// <summary>The <c>options</c> of a <see cref="RealtimeModelV4ResponseCreate"/>.</summary>
public sealed record RealtimeModelV4ResponseCreateOptions(
    IReadOnlyList<string>? Modalities = null,
    string? Instructions = null,
    IReadOnlyDictionary<string, JsonNode?>? Metadata = null
);

/// <summary>The AI SDK's <c>RealtimeModelV4ClientEvent</c>: the normalized events the browser sends to the realtime model.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RealtimeModelV4SessionUpdate), "session-update")]
[JsonDerivedType(typeof(RealtimeModelV4SessionStart), "session-start")]
[JsonDerivedType(typeof(RealtimeModelV4SessionClose), "session-close")]
[JsonDerivedType(typeof(RealtimeModelV4InputAudioMute), "input-audio-mute")]
[JsonDerivedType(typeof(RealtimeModelV4InputAudioUnmute), "input-audio-unmute")]
[JsonDerivedType(typeof(RealtimeModelV4ContextAppend), "context-append")]
[JsonDerivedType(typeof(RealtimeModelV4InputAudioAppend), "input-audio-append")]
[JsonDerivedType(typeof(RealtimeModelV4InputAudioCommit), "input-audio-commit")]
[JsonDerivedType(typeof(RealtimeModelV4InputAudioClear), "input-audio-clear")]
[JsonDerivedType(typeof(RealtimeModelV4ConversationItemCreate), "conversation-item-create")]
[JsonDerivedType(typeof(RealtimeModelV4ConversationItemTruncate), "conversation-item-truncate")]
[JsonDerivedType(typeof(RealtimeModelV4ResponseCreate), "response-create")]
[JsonDerivedType(typeof(RealtimeModelV4ResponseCancel), "response-cancel")]
public abstract record RealtimeModelV4ClientEvent
{
    /// <summary>The upstream <c>type</c> discriminator.</summary>
    [JsonIgnore]
    public string Type =>
        this switch
        {
            RealtimeModelV4SessionUpdate => "session-update",
            RealtimeModelV4SessionStart => "session-start",
            RealtimeModelV4SessionClose => "session-close",
            RealtimeModelV4InputAudioMute => "input-audio-mute",
            RealtimeModelV4InputAudioUnmute => "input-audio-unmute",
            RealtimeModelV4ContextAppend => "context-append",
            RealtimeModelV4InputAudioAppend => "input-audio-append",
            RealtimeModelV4InputAudioCommit => "input-audio-commit",
            RealtimeModelV4InputAudioClear => "input-audio-clear",
            RealtimeModelV4ConversationItemCreate => "conversation-item-create",
            RealtimeModelV4ConversationItemTruncate => "conversation-item-truncate",
            RealtimeModelV4ResponseCreate => "response-create",
            RealtimeModelV4ResponseCancel => "response-cancel",
            _ => throw new InvalidOperationException("Unknown realtime client event."),
        };
}

public sealed record RealtimeModelV4SessionUpdate(RealtimeModelV4SessionConfig Config, string? EventId = null)
    : RealtimeModelV4ClientEvent;

public sealed record RealtimeModelV4SessionStart(RealtimeModelV4SessionConfig Config, string? EventId = null)
    : RealtimeModelV4ClientEvent;

public sealed record RealtimeModelV4SessionClose(string? EventId = null) : RealtimeModelV4ClientEvent;

public sealed record RealtimeModelV4InputAudioMute(string? EventId = null) : RealtimeModelV4ClientEvent;

public sealed record RealtimeModelV4InputAudioUnmute(string? EventId = null) : RealtimeModelV4ClientEvent;

public sealed record RealtimeModelV4ContextAppend(
    string Content,
    string? DelegationId,
    string? EventId = null,
    IReadOnlyDictionary<string, JsonNode?>? ProviderOptions = null
) : RealtimeModelV4ClientEvent;

/// <summary>Appends a base64 audio chunk to the input buffer.</summary>
public sealed record RealtimeModelV4InputAudioAppend(string Audio, string? EventId = null) : RealtimeModelV4ClientEvent;

public sealed record RealtimeModelV4InputAudioCommit : RealtimeModelV4ClientEvent;

public sealed record RealtimeModelV4InputAudioClear : RealtimeModelV4ClientEvent;

public sealed record RealtimeModelV4ConversationItemCreate(RealtimeModelV4ConversationItem Item)
    : RealtimeModelV4ClientEvent;

/// <summary>Truncates an assistant message: its id, the content part index, and the millisecond to cut the audio at.</summary>
public sealed record RealtimeModelV4ConversationItemTruncate(string ItemId, int ContentIndex, double AudioEndMs)
    : RealtimeModelV4ClientEvent;

public sealed record RealtimeModelV4ResponseCreate(RealtimeModelV4ResponseCreateOptions? Options = null)
    : RealtimeModelV4ClientEvent;

public sealed record RealtimeModelV4ResponseCancel : RealtimeModelV4ClientEvent;

/// <summary>The AI SDK's <c>RealtimeModelV4Usage</c>: the provider's raw counts; every field is optional.</summary>
public sealed class RealtimeModelV4Usage
{
    /// <summary>Gross input tokens across all reported modalities; includes cached tokens.</summary>
    public double? InputTokens { get; init; }

    public double? OutputTokens { get; init; }

    public double? CachedInputTokens { get; init; }

    public double? InputTextTokens { get; init; }

    public double? InputAudioTokens { get; init; }

    public double? OutputTextTokens { get; init; }

    public double? OutputAudioTokens { get; init; }

    public double? CachedInputTextTokens { get; init; }

    public double? CachedInputAudioTokens { get; init; }

    /// <summary>Audio duration in seconds. Populated for duration-billed transcription.</summary>
    public double? Seconds { get; init; }
}

/// <summary>The <c>usage</c> of a session event: the seconds the session ran.</summary>
public sealed record RealtimeModelV4SessionSeconds(double Seconds);

/// <summary>
/// The AI SDK's <c>RealtimeModelV4ServerEvent</c>: the normalized events the realtime model emits. Every event carries the provider's
/// original event as <see cref="Raw"/>.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RealtimeModelV4SessionStarted), "session-started")]
[JsonDerivedType(typeof(RealtimeModelV4SessionClosed), "session-closed")]
[JsonDerivedType(typeof(RealtimeModelV4SessionUsage), "session-usage")]
[JsonDerivedType(typeof(RealtimeModelV4AudioChunk), "audio-chunk")]
[JsonDerivedType(typeof(RealtimeModelV4TranscriptFragment), "transcript-fragment")]
[JsonDerivedType(typeof(RealtimeModelV4DelegationCreated), "delegation-created")]
[JsonDerivedType(typeof(RealtimeModelV4CommandAcknowledged), "command-acknowledged")]
[JsonDerivedType(typeof(RealtimeModelV4SessionCreated), "session-created")]
[JsonDerivedType(typeof(RealtimeModelV4SessionUpdated), "session-updated")]
[JsonDerivedType(typeof(RealtimeModelV4SpeechStarted), "speech-started")]
[JsonDerivedType(typeof(RealtimeModelV4SpeechStopped), "speech-stopped")]
[JsonDerivedType(typeof(RealtimeModelV4AudioCommitted), "audio-committed")]
[JsonDerivedType(typeof(RealtimeModelV4ConversationItemAdded), "conversation-item-added")]
[JsonDerivedType(typeof(RealtimeModelV4InputTranscriptionCompleted), "input-transcription-completed")]
[JsonDerivedType(typeof(RealtimeModelV4ResponseCreated), "response-created")]
[JsonDerivedType(typeof(RealtimeModelV4ResponseDone), "response-done")]
[JsonDerivedType(typeof(RealtimeModelV4OutputItemAdded), "output-item-added")]
[JsonDerivedType(typeof(RealtimeModelV4OutputItemDone), "output-item-done")]
[JsonDerivedType(typeof(RealtimeModelV4ContentPartAdded), "content-part-added")]
[JsonDerivedType(typeof(RealtimeModelV4ContentPartDone), "content-part-done")]
[JsonDerivedType(typeof(RealtimeModelV4AudioDelta), "audio-delta")]
[JsonDerivedType(typeof(RealtimeModelV4AudioDone), "audio-done")]
[JsonDerivedType(typeof(RealtimeModelV4AudioTranscriptDelta), "audio-transcript-delta")]
[JsonDerivedType(typeof(RealtimeModelV4AudioTranscriptDone), "audio-transcript-done")]
[JsonDerivedType(typeof(RealtimeModelV4TextDelta), "text-delta")]
[JsonDerivedType(typeof(RealtimeModelV4TextDone), "text-done")]
[JsonDerivedType(typeof(RealtimeModelV4FunctionCallArgumentsDelta), "function-call-arguments-delta")]
[JsonDerivedType(typeof(RealtimeModelV4FunctionCallArgumentsDone), "function-call-arguments-done")]
[JsonDerivedType(typeof(RealtimeModelV4Error), "error")]
[JsonDerivedType(typeof(RealtimeModelV4Custom), "custom")]
public abstract record RealtimeModelV4ServerEvent
{
    /// <summary>The original provider event.</summary>
    public JsonNode? Raw { get; init; }
}

public sealed record RealtimeModelV4SessionStarted(string SessionId, string? DelegationMode = null)
    : RealtimeModelV4ServerEvent;

public sealed record RealtimeModelV4SessionClosed(string? SessionId, RealtimeModelV4SessionSeconds Usage, string Reason)
    : RealtimeModelV4ServerEvent;

/// <summary>A cumulative duration snapshot, not an increment.</summary>
public sealed record RealtimeModelV4SessionUsage(
    RealtimeModelV4SessionSeconds Usage,
    double? ContextWindowUsageRatio = null
) : RealtimeModelV4ServerEvent;

public sealed record RealtimeModelV4AudioChunk(string Delta) : RealtimeModelV4ServerEvent;

public sealed record RealtimeModelV4TranscriptFragment(string Speaker, string Delta, double StartMs, double EndMs)
    : RealtimeModelV4ServerEvent;

public sealed record RealtimeModelV4DelegationCreated(
    string DelegationId,
    string? Target = null,
    double? OffsetMs = null,
    string? ResponseId = null
) : RealtimeModelV4ServerEvent;

/// <summary>Acknowledges a command: the provider-native command name.</summary>
public sealed record RealtimeModelV4CommandAcknowledged(string Command, string? ClientEventId = null)
    : RealtimeModelV4ServerEvent;

public sealed record RealtimeModelV4SessionCreated(string? SessionId = null) : RealtimeModelV4ServerEvent;

public sealed record RealtimeModelV4SessionUpdated : RealtimeModelV4ServerEvent;

public sealed record RealtimeModelV4SpeechStarted(string? ItemId = null) : RealtimeModelV4ServerEvent;

public sealed record RealtimeModelV4SpeechStopped(string? ItemId = null) : RealtimeModelV4ServerEvent;

public sealed record RealtimeModelV4AudioCommitted(string? ItemId = null, string? PreviousItemId = null)
    : RealtimeModelV4ServerEvent;

public sealed record RealtimeModelV4ConversationItemAdded(string ItemId, JsonNode? Item) : RealtimeModelV4ServerEvent;

public sealed record RealtimeModelV4InputTranscriptionCompleted(
    string ItemId,
    string Transcript,
    RealtimeModelV4Usage? Usage = null
) : RealtimeModelV4ServerEvent;

public sealed record RealtimeModelV4ResponseCreated(string ResponseId) : RealtimeModelV4ServerEvent;

public sealed record RealtimeModelV4ResponseDone(string ResponseId, string Status, RealtimeModelV4Usage? Usage = null)
    : RealtimeModelV4ServerEvent;

public sealed record RealtimeModelV4OutputItemAdded(string ResponseId, string ItemId) : RealtimeModelV4ServerEvent;

public sealed record RealtimeModelV4OutputItemDone(string ResponseId, string ItemId) : RealtimeModelV4ServerEvent;

public sealed record RealtimeModelV4ContentPartAdded(string ResponseId, string ItemId) : RealtimeModelV4ServerEvent;

public sealed record RealtimeModelV4ContentPartDone(string ResponseId, string ItemId) : RealtimeModelV4ServerEvent;

/// <summary>A base64 audio chunk.</summary>
public sealed record RealtimeModelV4AudioDelta(string ResponseId, string ItemId, string Delta)
    : RealtimeModelV4ServerEvent;

public sealed record RealtimeModelV4AudioDone(string ResponseId, string ItemId) : RealtimeModelV4ServerEvent;

public sealed record RealtimeModelV4AudioTranscriptDelta(string ResponseId, string ItemId, string Delta)
    : RealtimeModelV4ServerEvent;

public sealed record RealtimeModelV4AudioTranscriptDone(string ResponseId, string ItemId, string? Transcript = null)
    : RealtimeModelV4ServerEvent;

public sealed record RealtimeModelV4TextDelta(string ResponseId, string ItemId, string Delta)
    : RealtimeModelV4ServerEvent;

public sealed record RealtimeModelV4TextDone(string ResponseId, string ItemId, string? Text = null)
    : RealtimeModelV4ServerEvent;

/// <summary>A partial JSON string of function call arguments.</summary>
public sealed record RealtimeModelV4FunctionCallArgumentsDelta(
    string ResponseId,
    string ItemId,
    string CallId,
    string Delta
) : RealtimeModelV4ServerEvent;

/// <summary>The function to call and its complete JSON arguments.</summary>
public sealed record RealtimeModelV4FunctionCallArgumentsDone(
    string ResponseId,
    string ItemId,
    string CallId,
    string Name,
    string Arguments
) : RealtimeModelV4ServerEvent;

public sealed record RealtimeModelV4Error(string Message, string? Code = null, string? ClientEventId = null)
    : RealtimeModelV4ServerEvent;

/// <summary>A provider-specific event: its original type string, and a usage snapshot without an inferred response association.</summary>
public sealed record RealtimeModelV4Custom(string RawType, RealtimeModelV4Usage? Usage = null)
    : RealtimeModelV4ServerEvent;

/// <summary>
/// The AI SDK's <c>RealtimeModelV4SessionIntent</c>: the endpoint variant to open (<c>conversation</c> or <c>transcription</c>).
/// </summary>
public static class RealtimeModelV4SessionIntent
{
    public const string Conversation = "conversation";

    public const string Transcription = "transcription";
}

/// <summary>The AI SDK's <c>RealtimeModelV4ClientSecretOptions</c>.</summary>
public class RealtimeModelV4ClientSecretOptions
{
    /// <summary>Number of seconds until the client secret expires.</summary>
    public int? ExpiresAfterSeconds { get; init; }

    /// <summary>Session configuration to embed in the token request, for providers that need it at token creation time.</summary>
    public RealtimeModelV4SessionConfig? SessionConfig { get; init; }
}

/// <summary>The AI SDK's <c>RealtimeModelV4ClientSecretResult</c>.</summary>
public sealed record RealtimeModelV4ClientSecretResult(string Token, string Url, long? ExpiresAt = null);

/// <summary>The AI SDK's <c>RealtimeModelV4ServerConnection</c>: the URL to dial and the headers (and subprotocols) of the handshake.</summary>
public sealed class RealtimeModelV4ServerConnection
{
    public required string Url { get; init; }

    public required IReadOnlyDictionary<string, string> Headers { get; init; }

    public IReadOnlyList<string>? Protocols { get; init; }
}

/// <summary>The browser-side WebSocket config <see cref="IRealtimeModelV4.GetWebSocketConfig"/> returns.</summary>
public sealed record RealtimeModelV4WebSocketConfig(string Url, IReadOnlyList<string>? Protocols = null);

/// <summary>The <c>capabilities</c> of a <see cref="IRealtimeModelV4"/>.</summary>
public sealed record RealtimeModelV4Capabilities(
    string Conversation,
    IReadOnlyList<string> Transports,
    IReadOnlyList<string>? Connections = null,
    string? Startup = null,
    string? Finalization = null
);

/// <summary>The AI SDK's <c>RealtimeModelV4</c>: a model that supports bidirectional audio/text communication over WebSocket.</summary>
public interface IRealtimeModelV4
{
    string SpecificationVersion { get; }

    string Provider { get; }

    string ModelId { get; }

    RealtimeModelV4Capabilities? Capabilities { get; }

    /// <summary>Server-side: creates an ephemeral client secret for authenticating browser-side WebSocket connections.</summary>
    Task<RealtimeModelV4ClientSecretResult>? DoCreateClientSecretAsync(
        RealtimeModelV4ClientSecretOptions? options,
        CancellationToken cancellationToken = default
    ) => null;

    /// <summary>Browser-side: the WebSocket URL and subprotocols to connect with.</summary>
    RealtimeModelV4WebSocketConfig? GetWebSocketConfig(string token, string url) => null;

    /// <summary>Browser-side: parses a raw event (a <see cref="RealtimeModelV4ServerEvent"/> or JSON) into a normalized event.</summary>
    RealtimeModelV4ServerEvent ParseServerEvent(object? raw);

    /// <summary>Browser-side: serializes a normalized client event into the provider's native WebSocket format.</summary>
    object? SerializeClientEvent(RealtimeModelV4ClientEvent clientEvent, string? intent = null);

    /// <summary>Browser-side: builds the provider-specific session configuration payload.</summary>
    object? BuildSessionConfig(RealtimeModelV4SessionConfig config, string? intent = null);

    /// <summary>Server-side: the upstream WebSocket URL and auth headers for a server-initiated connection.</summary>
    Task<RealtimeModelV4ServerConnection>? GetServerWebSocketConfigAsync(
        string? intent = null,
        CancellationToken cancellationToken = default
    ) => null;
}

/// <summary>The AI SDK's <c>RealtimeFactoryV4GetTokenOptions</c>.</summary>
public class RealtimeFactoryV4GetTokenOptions : RealtimeModelV4ClientSecretOptions
{
    public required string Model { get; init; }
}

/// <summary>The AI SDK's <c>RealtimeFactoryV4GetTokenResult</c>.</summary>
public sealed record RealtimeFactoryV4GetTokenResult(string Token, string Url, long? ExpiresAt = null);

/// <summary>The AI SDK's <c>RealtimeFactoryV4</c>: creates realtime models and mints client secrets for them.</summary>
public interface IRealtimeFactoryV4
{
    IRealtimeModelV4 Create(string modelId);

    Task<RealtimeFactoryV4GetTokenResult> GetTokenAsync(
        RealtimeFactoryV4GetTokenOptions options,
        CancellationToken cancellationToken = default
    );
}
