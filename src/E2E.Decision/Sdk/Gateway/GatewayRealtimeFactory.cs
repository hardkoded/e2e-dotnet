// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision.Sdk.Gateway;

/// <summary>The AI SDK's <c>RealtimeFactoryV4GetTokenOptions &amp; GatewayRealtimeOptions</c>: the options of <see cref="GatewayRealtimeFactory.GetTokenAsync(GatewayRealtimeGetTokenOptions, CancellationToken)"/>.</summary>
public sealed class GatewayRealtimeGetTokenOptions : RealtimeFactoryV4GetTokenOptions
{
    /// <summary>Selects the Gateway API for the model ID: <c>live</c> or <c>realtime</c>.</summary>
    public string? Api { get; init; }
}

/// <summary>
/// The AI SDK's <c>GatewayRealtimeFactory</c>: returns a turn-based realtime model or, for Live model IDs or <c>Api = "live"</c>, a
/// continuous Live model. <c>GetTokenAsync</c> mints a client secret bound to the matching Gateway WebSocket route. Upstream's factory is
/// callable; <see cref="Create(string, GatewayRealtimeOptions?)"/> is the call.
/// </summary>
public sealed class GatewayRealtimeFactory : IRealtimeFactoryV4
{
    private readonly Func<string, GatewayRealtimeOptions?, IRealtimeModelV4> _create;

    internal GatewayRealtimeFactory(Func<string, GatewayRealtimeOptions?, IRealtimeModelV4> create) => _create = create;

    /// <summary>Creates the realtime model, or the Live model, for <paramref name="modelId"/>.</summary>
    public IRealtimeModelV4 Create(string modelId, GatewayRealtimeOptions? options = null) => _create(modelId, options);

    IRealtimeModelV4 IRealtimeFactoryV4.Create(string modelId) => Create(modelId);

    /// <summary>Mints a short-lived client secret (<c>vcst_</c>) a browser can use to open the realtime WebSocket.</summary>
    public async Task<RealtimeFactoryV4GetTokenResult> GetTokenAsync(
        GatewayRealtimeGetTokenOptions options,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        var model = _create(options.Model, new GatewayRealtimeOptions { Api = options.Api });
        var secret = await model
            .DoCreateClientSecretAsync(
                new RealtimeModelV4ClientSecretOptions
                {
                    ExpiresAfterSeconds = options.ExpiresAfterSeconds,
                    SessionConfig = options.SessionConfig,
                },
                cancellationToken
            )!
            .ConfigureAwait(false);
        return new RealtimeFactoryV4GetTokenResult(secret.Token, secret.Url, secret.ExpiresAt);
    }

    Task<RealtimeFactoryV4GetTokenResult> IRealtimeFactoryV4.GetTokenAsync(
        RealtimeFactoryV4GetTokenOptions options,
        CancellationToken cancellationToken
    ) =>
        options is GatewayRealtimeGetTokenOptions gatewayOptions
            ? GetTokenAsync(gatewayOptions, cancellationToken)
            : GetTokenAsync(
                new GatewayRealtimeGetTokenOptions
                {
                    Model = options.Model,
                    ExpiresAfterSeconds = options.ExpiresAfterSeconds,
                    SessionConfig = options.SessionConfig,
                },
                cancellationToken
            );
}
