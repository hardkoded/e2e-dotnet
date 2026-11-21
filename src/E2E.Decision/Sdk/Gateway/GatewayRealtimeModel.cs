// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk.Gateway;

/// <summary>What <see cref="GatewayRealtimeModelConfig.CreateClientSecret"/> is asked to mint (upstream's <c>createClientSecret</c> params).</summary>
public sealed record GatewayClientSecretRequest(
    string ModelId,
    int? ExpiresAfterSeconds = null,
    string? RouteKind = null
);

/// <summary>The minted secret of <see cref="GatewayRealtimeModelConfig.CreateClientSecret"/>.</summary>
public sealed record GatewayClientSecret(string Token, long? ExpiresAt = null);

/// <summary>The AI SDK's <c>GatewayRealtimeModelConfig</c>.</summary>
public sealed class GatewayRealtimeModelConfig
{
    public required string Provider { get; init; }

    public required string BaseUrl { get; init; }

    public string? TeamIdOrSlug { get; init; }

    public required Func<CancellationToken, Task<IReadOnlyDictionary<string, string?>>> GetHeaders { get; init; }

    /// <summary>
    /// Mints a short-lived client secret (<c>vcst_</c>) for this model via the Gateway's <c>/v1/realtime/client-secrets</c> endpoint.
    /// Implemented by the provider because minting requires the long-lived Gateway credential and must run server-side.
    /// </summary>
    public required Func<
        GatewayClientSecretRequest,
        CancellationToken,
        Task<GatewayClientSecret>
    > CreateClientSecret { get; init; }
}

/// <summary>
/// Realtime model backed by the AI Gateway (the AI SDK's <c>GatewayRealtimeModel</c>). The Gateway normalizes realtime like every other
/// modality: the client speaks the normalized AI SDK realtime protocol and the Gateway translates to and from the upstream provider
/// server-side, so this model is a thin identity codec over that protocol; only the connection and authentication are Gateway-specific.
/// </summary>
public sealed class GatewayRealtimeModel : IRealtimeModelV4
{
    public GatewayRealtimeModel(string modelId, GatewayRealtimeModelConfig config)
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
        new("turn-based", ["websocket"], ["client-secret-websocket", "server-websocket"]);

    /// <summary>Upstream's private <c>config</c>.</summary>
    internal GatewayRealtimeModelConfig Config { get; }

    /// <summary>
    /// Mints a single-use, short-lived client secret (<c>vcst_</c>) the browser uses to open the realtime WebSocket without holding the
    /// long-lived Gateway credential. <c>ExpiresAfterSeconds</c> is forwarded to the mint endpoint; <c>SessionConfig</c> is unused here, as
    /// it is applied later through the normalized <c>session-update</c> event.
    /// </summary>
    public async Task<RealtimeModelV4ClientSecretResult> DoCreateClientSecretAsync(
        RealtimeModelV4ClientSecretOptions? options,
        CancellationToken cancellationToken = default
    )
    {
        var secret = await Config
            .CreateClientSecret(
                new GatewayClientSecretRequest(ModelId, options?.ExpiresAfterSeconds),
                cancellationToken
            )
            .ConfigureAwait(false);
        return new RealtimeModelV4ClientSecretResult(
            secret.Token,
            ToGatewayRealtimeUrl(Config.BaseUrl, ModelId),
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
            Url = ToGatewayRealtimeUrl(Config.BaseUrl, ModelId),
            Headers = GatewayRealtimeHeaders.RemoveUndefinedEntries(headers),
        };
    }

    Task<RealtimeModelV4ServerConnection>? IRealtimeModelV4.GetServerWebSocketConfigAsync(
        string? intent,
        CancellationToken cancellationToken
    ) => GetServerWebSocketConfigAsync(intent, cancellationToken);

    /// <summary>
    /// The Gateway emits normalized AI SDK realtime events, so no provider-specific mapping is needed on the client: an event is returned
    /// as is, and a JSON event is read as the normalized event.
    /// </summary>
    public RealtimeModelV4ServerEvent ParseServerEvent(object? raw) => GatewayRealtimeJson.ToServerEvent(raw);

    /// <summary>The Gateway accepts normalized AI SDK realtime events directly.</summary>
    public object? SerializeClientEvent(RealtimeModelV4ClientEvent clientEvent, string? intent = null)
    {
        AssertConversationIntent(intent);
        return clientEvent;
    }

    /// <summary>The session config is already normalized; the Gateway maps it to the upstream provider's session payload server-side.</summary>
    public object? BuildSessionConfig(RealtimeModelV4SessionConfig config, string? intent = null)
    {
        AssertConversationIntent(intent);
        return config;
    }

    private static void AssertConversationIntent(string? intent)
    {
        if (intent is not null && intent != RealtimeModelV4SessionIntent.Conversation)
        {
            throw new InvalidOperationException(
                "AI Gateway realtime does not support the '"
                    + intent
                    + "' session intent on the normalized realtime route."
            );
        }
    }

    /// <summary>
    /// The Gateway realtime WebSocket URL. The HTTP(S) base URL is upgraded to WS(S) and the model id rides the <c>?ai-model-id=</c> query,
    /// the WS transport of the <c>ai-model-id</c> header the HTTP routes use. The model id is passed through verbatim.
    /// </summary>
    private static string ToGatewayRealtimeUrl(string baseUrl, string modelId) =>
        GatewayRealtimeUrls.ToWebSocketUrl(baseUrl, "/realtime-model", modelId);
}
