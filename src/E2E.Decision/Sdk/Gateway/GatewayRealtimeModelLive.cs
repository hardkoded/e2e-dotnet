// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>The AI SDK's <c>GatewayRealtimeModelLiveConfig</c>.</summary>
public sealed class GatewayRealtimeModelLiveConfig
{
    public required string Provider { get; init; }

    public required string BaseUrl { get; init; }

    public string? TeamIdOrSlug { get; init; }

    public required Func<CancellationToken, Task<IReadOnlyDictionary<string, string?>>> GetHeaders { get; init; }

    /// <summary>Mints a Live-bound client secret (<c>vcst_</c>) server-side.</summary>
    public required Func<
        GatewayClientSecretRequest,
        CancellationToken,
        Task<GatewayClientSecret>
    > CreateClientSecret { get; init; }
}

/// <summary>
/// Live model backed by the AI Gateway (the AI SDK's <c>GatewayRealtimeModelLive</c>). Live is a continuous conversation API: one
/// <c>session-start</c> opens the session, PCM audio streams in both directions, and <c>session-close</c> ends it with final usage. The client
/// speaks the Gateway's normalized Live protocol on <c>/v4/ai/live-model</c>, and the Gateway runs the provider mapping server-side.
/// </summary>
public sealed class GatewayRealtimeModelLive : IRealtimeModelV4
{
    private static readonly HashSet<string> s_supportedOpenAiLiveOptions = ["delegation", "input", "store", "voice"];
    private static readonly HashSet<string> s_contextChannels = ["commentary", "instructions", "thinking"];

    private static readonly Dictionary<string, string> s_openAiLiveOptionMessages = new()
    {
        ["delegation"] = "delegation must be { type: \"client\" } or null.",
        ["input"] = "input must be at most 128 messages, each { type: \"message\", role, content: [{ type, text }] }.",
        ["store"] = "store must be a boolean.",
        ["voice"] = "voice must be { id: string } with a nonempty id.",
    };

    public GatewayRealtimeModelLive(string modelId, GatewayRealtimeModelLiveConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        ModelId = modelId;
        Provider = config.Provider;
        Config = config;
    }

    public string SpecificationVersion => "v4";

    public string Provider { get; }

    public string ModelId { get; }

    public RealtimeModelV4Capabilities? Capabilities { get; } =
        new(
            "continuous",
            ["websocket"],
            ["client-secret-websocket", "server-websocket"],
            Startup: "session-start",
            Finalization: "session-close"
        );

    /// <summary>Upstream's private <c>config</c>.</summary>
    internal GatewayRealtimeModelLiveConfig Config { get; }

    /// <summary>
    /// Mints a short-lived client secret bound to this Live model. The browser opens the Live WebSocket with it through the
    /// <c>ai-gateway-auth.&lt;token&gt;</c> subprotocol; the session configuration is sent later in <c>session-start</c>.
    /// </summary>
    public async Task<RealtimeModelV4ClientSecretResult> DoCreateClientSecretAsync(
        RealtimeModelV4ClientSecretOptions? options,
        CancellationToken cancellationToken = default
    )
    {
        var secret = await Config
            .CreateClientSecret(
                new GatewayClientSecretRequest(ModelId, options?.ExpiresAfterSeconds, "live"),
                cancellationToken
            )
            .ConfigureAwait(false);
        return new RealtimeModelV4ClientSecretResult(
            secret.Token,
            ToGatewayLiveUrl(Config.BaseUrl, ModelId),
            secret.ExpiresAt
        );
    }

    Task<RealtimeModelV4ClientSecretResult>? IRealtimeModelV4.DoCreateClientSecretAsync(
        RealtimeModelV4ClientSecretOptions? options,
        CancellationToken cancellationToken
    ) => DoCreateClientSecretAsync(options, cancellationToken);

    public RealtimeModelV4WebSocketConfig GetWebSocketConfig(string token, string url) =>
        new(url, GatewayRealtimeAuth.GetGatewayRealtimeProtocols(token, Config.TeamIdOrSlug));

    RealtimeModelV4WebSocketConfig? IRealtimeModelV4.GetWebSocketConfig(string token, string url) =>
        GetWebSocketConfig(token, url);

    public async Task<RealtimeModelV4ServerConnection> GetServerWebSocketConfigAsync(
        string? intent = null,
        CancellationToken cancellationToken = default
    )
    {
        AssertConversationIntent(intent);
        var headers = await Config.GetHeaders(cancellationToken).ConfigureAwait(false);
        return new RealtimeModelV4ServerConnection
        {
            Url = ToGatewayLiveUrl(Config.BaseUrl, ModelId),
            Headers = GatewayRealtimeHeaders.RemoveUndefinedEntries(headers),
        };
    }

    Task<RealtimeModelV4ServerConnection>? IRealtimeModelV4.GetServerWebSocketConfigAsync(
        string? intent,
        CancellationToken cancellationToken
    ) => GetServerWebSocketConfigAsync(intent, cancellationToken);

    /// <summary>A normalized event passes through; anything that is not an object with a string <c>type</c> is an <c>invalid_server_event</c> error.</summary>
    public RealtimeModelV4ServerEvent ParseServerEvent(object? raw)
    {
        if (raw is RealtimeModelV4ServerEvent serverEvent)
        {
            return serverEvent;
        }

        var node =
            raw as JsonNode
            ?? (raw is System.Text.Json.JsonElement element ? JsonNode.Parse(element.GetRawText()) : null);
        if (
            node is not JsonObject frame
            || !frame.TryGetPropertyValue("type", out var type)
            || type is not JsonValue typeValue
            || !typeValue.TryGetValue<string>(out var typeName)
        )
        {
            return new RealtimeModelV4Error("Invalid AI Gateway Live server event.", "invalid_server_event")
            {
                Raw = node,
            };
        }

        // Gateway-originated errors carry no provider payload; keep the frame as raw.
        if (typeName == "error" && !frame.ContainsKey("raw"))
        {
            var error = GatewayRealtimeJson.ToServerEvent(frame.DeepClone());
            return error with { Raw = frame };
        }

        return GatewayRealtimeJson.ToServerEvent(frame);
    }

    public object? SerializeClientEvent(RealtimeModelV4ClientEvent clientEvent, string? intent = null)
    {
        AssertConversationIntent(intent);
        switch (clientEvent)
        {
            case RealtimeModelV4SessionStart start:
                return WithEventId(
                    new JsonObject
                    {
                        ["type"] = "session-start",
                        ["modelId"] = ModelId,
                        ["config"] = ToJson(BuildSessionConfig(start.Config)),
                    },
                    start.EventId
                );
            case RealtimeModelV4SessionClose close:
                return WithEventId(new JsonObject { ["type"] = "session-close" }, close.EventId);
            case RealtimeModelV4InputAudioMute mute:
                return WithEventId(new JsonObject { ["type"] = "input-audio-mute" }, mute.EventId);
            case RealtimeModelV4InputAudioUnmute unmute:
                return WithEventId(new JsonObject { ["type"] = "input-audio-unmute" }, unmute.EventId);
            case RealtimeModelV4InputAudioAppend append:
                return WithEventId(
                    new JsonObject { ["type"] = "input-audio-append", ["audio"] = append.Audio },
                    append.EventId
                );
            case RealtimeModelV4ContextAppend context:
                ValidateContextOptions(context.ProviderOptions);
                var node = new JsonObject
                {
                    ["type"] = "context-append",
                    ["content"] = context.Content,
                    ["delegationId"] = context.DelegationId,
                };
                if (context.ProviderOptions is not null)
                {
                    node["providerOptions"] = ProviderOptionsToJson(context.ProviderOptions);
                }

                return WithEventId(node, context.EventId);
            default:
                throw new UnsupportedFunctionalityException("AI Gateway Live client event: " + clientEvent.Type);
        }
    }

    public RealtimeModelV4SessionConfig BuildSessionConfig(RealtimeModelV4SessionConfig config, string? intent = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        AssertConversationIntent(intent);
        foreach (var (key, isSet) in SettingsOf(config))
        {
            if (isSet && !s_supportedSessionSettings.Contains(key))
            {
                throw new UnsupportedFunctionalityException("AI Gateway Live session setting: " + key);
            }
        }

        ValidateSessionProviderOptions(config.ProviderOptions);

        var input = config.InputAudioFormat;
        var output = config.OutputAudioFormat;
        foreach (var (argument, format) in new[] { ("inputAudioFormat", input), ("outputAudioFormat", output) })
        {
            if (format is not null && !IsSupportedAudioFormat(format))
            {
                throw new UnsupportedFunctionalityException(
                    "AI Gateway Live "
                        + argument
                        + " "
                        + format.Type
                        + " at "
                        + (format.Rate?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "an unspecified")
                        + " Hz; use audio/pcm at 16000 or 24000 Hz, or audio/pcma or audio/pcmu at 8000 Hz"
                );
            }
        }

        if (input is not null && output is not null && (input.Type != output.Type || input.Rate != output.Rate))
        {
            throw new InvalidArgumentException(
                "outputAudioFormat",
                "AI Gateway Live requires the same input and output audio format."
            );
        }

        return config;
    }

    object? IRealtimeModelV4.BuildSessionConfig(RealtimeModelV4SessionConfig config, string? intent) =>
        BuildSessionConfig(config, intent);

    private static readonly HashSet<string> s_supportedSessionSettings =
    [
        "instructions",
        "voice",
        "inputAudioFormat",
        "outputAudioFormat",
        "providerOptions",
    ];

    // Object.entries(config) with value != null: the settings the caller set.
    private static IEnumerable<(string Key, bool IsSet)> SettingsOf(RealtimeModelV4SessionConfig config)
    {
        yield return ("instructions", config.Instructions is not null);
        yield return ("voice", config.Voice is not null);
        yield return ("outputModalities", config.OutputModalities is not null);
        yield return ("inputAudioFormat", config.InputAudioFormat is not null);
        yield return ("inputAudioTranscription", config.InputAudioTranscription is not null);
        yield return ("outputAudioTranscription", config.OutputAudioTranscription is not null);
        yield return ("outputAudioFormat", config.OutputAudioFormat is not null);
        yield return ("turnDetection", config.TurnDetection is not null);
        yield return ("tools", config.Tools is not null);
        yield return ("providerOptions", config.ProviderOptions is not null);
    }

    private static void AssertConversationIntent(string? intent)
    {
        if (intent is not null && intent != RealtimeModelV4SessionIntent.Conversation)
        {
            throw new InvalidOperationException(
                "AI Gateway Live does not support the '" + intent + "' session intent."
            );
        }
    }

    private static JsonNode? ToJson(object? value) =>
        System.Text.Json.JsonSerializer.SerializeToNode(value, GatewayRealtimeJson.Options);

    private static JsonObject ProviderOptionsToJson(IReadOnlyDictionary<string, JsonNode?> providerOptions)
    {
        var result = new JsonObject();
        foreach (var (name, value) in providerOptions)
        {
            result[name] = value?.DeepClone();
        }

        return result;
    }

    private static JsonObject WithEventId(JsonObject node, string? eventId)
    {
        if (eventId is not null)
        {
            node["eventId"] = eventId;
        }

        return node;
    }

    // Gateway options stay service-owned; OpenAI Live options are WebSocket-only.
    private static void ValidateSessionProviderOptions(IReadOnlyDictionary<string, JsonNode?>? providerOptions)
    {
        if (providerOptions is null)
        {
            return;
        }

        foreach (var (ns, options) in providerOptions)
        {
            if (ns is not "gateway" and not "openai")
            {
                throw new UnsupportedFunctionalityException("AI Gateway Live provider options namespace: " + ns);
            }

            if (options is not JsonObject)
            {
                throw new InvalidArgumentException(
                    "providerOptions." + ns,
                    "providerOptions." + ns + " must be an object."
                );
            }
        }

        if (!providerOptions.TryGetValue("openai", out var openAi) || openAi is not JsonObject openAiOptions)
        {
            return;
        }

        foreach (var (key, value) in openAiOptions)
        {
            if (!s_supportedOpenAiLiveOptions.Contains(key))
            {
                throw new UnsupportedFunctionalityException("AI Gateway Live OpenAI option: " + key);
            }

            if (
                key == "delegation"
                && value is not null
                && !(
                    HasExactKeys(value, "type")
                    && value["type"] is JsonValue type
                    && type.TryGetValue<string>(out var t)
                    && t == "client"
                )
            )
            {
                throw new UnsupportedFunctionalityException(
                    "AI Gateway Live delegation other than { type: \"client\" } or null"
                );
            }

            if (!IsValidOpenAiLiveOption(key, value))
            {
                throw new InvalidArgumentException("providerOptions.openai." + key, s_openAiLiveOptionMessages[key]);
            }
        }
    }

    private static bool IsValidOpenAiLiveOption(string key, JsonNode? value) =>
        key switch
        {
            "delegation" => true,
            "store" => value is JsonValue store && store.TryGetValue<bool>(out _),
            "voice" => HasExactKeys(value, "id")
                && value!["id"] is JsonValue id
                && id.TryGetValue<string>(out var text)
                && text.Length > 0,
            "input" => value is JsonArray { Count: <= 128 } input && input.All(IsInputMessage),
            _ => false,
        };

    private static bool IsInputMessage(JsonNode? message)
    {
        if (
            !HasExactKeys(message, "type", "role", "content")
            || StringOf(message!["type"]) != "message"
            || message["content"] is not JsonArray { Count: 1 } content
        )
        {
            return false;
        }

        var part = content[0];
        if (!HasExactKeys(part, "type", "text") || StringOf(part!["text"]) is null)
        {
            return false;
        }

        var partType = StringOf(part["type"]);
        var role = StringOf(message["role"]);
        return role == "assistant"
            ? partType is "text" or "output_text"
            : role is "developer" or "user" && partType == "input_text";
    }

    private static string? StringOf(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    // Every key of keys is present, and no other: a null value is a present key, as in JavaScript.
    private static bool HasExactKeys(JsonNode? value, params string[] keys) =>
        value is JsonObject obj && obj.Count == keys.Length && keys.All(obj.ContainsKey);

    private static void ValidateContextOptions(IReadOnlyDictionary<string, JsonNode?>? providerOptions)
    {
        if (providerOptions is null)
        {
            return;
        }

        var valid = providerOptions.All(entry =>
            entry.Key == "openai"
            && entry.Value is JsonObject options
            && options.All(option =>
                option.Key == "channel" && StringOf(option.Value) is { } channel && s_contextChannels.Contains(channel)
            )
        );
        if (!valid)
        {
            throw new UnsupportedFunctionalityException(
                "AI Gateway Live context options other than openai.channel commentary, instructions, or thinking"
            );
        }
    }

    private static bool IsSupportedAudioFormat(SharedV4AudioFormat format) =>
        format.Type == "audio/pcm"
            ? format.Rate is 16000 or 24000
            : format.Type is "audio/pcma" or "audio/pcmu" && format.Rate == 8000;

    // Live WebSocket URL; the model id rides ?ai-model-id= like the realtime route.
    private static string ToGatewayLiveUrl(string baseUrl, string modelId) =>
        GatewayRealtimeUrls.ToWebSocketUrl(baseUrl, "/live-model", modelId);
}
