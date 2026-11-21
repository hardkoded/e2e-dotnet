// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>
/// The AI SDK's <c>gateway-realtime-auth.ts</c>: the WebSocket subprotocol contract for AI Gateway realtime, streaming transcription, and
/// streaming speech auth. A browser <c>WebSocket</c> cannot set request headers, so the Gateway auth (bearer) token travels in the
/// <c>Sec-WebSocket-Protocol</c> handshake. The client encodes values with <c>GetGatewayRealtimeProtocols</c>,
/// <c>GetGatewayTranscriptionProtocols</c>, and <c>GetGatewaySpeechProtocols</c>; the Gateway server decodes them with
/// <c>GetGatewayRealtimeAuthToken</c> and <c>GetGatewayRealtimeTeamIdOrSlug</c>.
/// </summary>
public static class GatewayRealtimeAuth
{
    /// <summary>Marker subprotocol offered on every realtime handshake so the Gateway can echo a negotiated subprotocol.</summary>
    public const string GatewayRealtimeSubprotocol = "ai-gateway-realtime.v1";

    /// <summary>Marker subprotocol for streaming transcription handshakes.</summary>
    public const string GatewayTranscriptionSubprotocol = "ai-gateway-transcription.v1";

    /// <summary>Marker subprotocol for streaming speech handshakes.</summary>
    public const string GatewaySpeechSubprotocol = "ai-gateway-speech.v1";

    /// <summary>Subprotocol prefix that carries the Gateway auth (bearer) token.</summary>
    public const string GatewayAuthSubprotocolPrefix = "ai-gateway-auth.";

    /// <summary>Subprotocol prefix that carries optional Vercel team scoping.</summary>
    public const string GatewayTeamSubprotocolPrefix = "ai-gateway-team.";

    /// <summary>Client-side: the WebSocket subprotocols that carry <paramref name="token"/> to the Gateway.</summary>
    public static IReadOnlyList<string> GetGatewayRealtimeProtocols(string token, string? teamIdOrSlug = null) =>
        BuildGatewayProtocols(GatewayRealtimeSubprotocol, token, teamIdOrSlug);

    /// <summary><see cref="GetGatewayRealtimeProtocols"/> with the streaming transcription marker subprotocol.</summary>
    public static IReadOnlyList<string> GetGatewayTranscriptionProtocols(string token, string? teamIdOrSlug = null) =>
        BuildGatewayProtocols(GatewayTranscriptionSubprotocol, token, teamIdOrSlug);

    /// <summary><see cref="GetGatewayRealtimeProtocols"/> with the streaming speech marker subprotocol.</summary>
    public static IReadOnlyList<string> GetGatewaySpeechProtocols(string token, string? teamIdOrSlug = null) =>
        BuildGatewayProtocols(GatewaySpeechSubprotocol, token, teamIdOrSlug);

    /// <summary>
    /// Server-side: the Gateway auth (bearer) token of a <c>Sec-WebSocket-Protocol</c> header value, or null when it is absent or empty.
    /// The header's subprotocols are comma-separated and may carry surrounding whitespace.
    /// </summary>
    public static string? GetGatewayRealtimeAuthToken(string? secWebSocketProtocol)
    {
        var token = FindProtocol(secWebSocketProtocol, GatewayAuthSubprotocolPrefix)?[
            GatewayAuthSubprotocolPrefix.Length..
        ];
        return string.IsNullOrEmpty(token) ? null : token;
    }

    /// <summary>
    /// Server-side: the optional Vercel team ID or slug of a <c>Sec-WebSocket-Protocol</c> header value. Team scoping is base64url-encoded
    /// so arbitrary team slugs stay within the WebSocket subprotocol token grammar.
    /// </summary>
    public static string? GetGatewayRealtimeTeamIdOrSlug(string? secWebSocketProtocol)
    {
        var encoded = FindProtocol(secWebSocketProtocol, GatewayTeamSubprotocolPrefix)?[
            GatewayTeamSubprotocolPrefix.Length..
        ];
        if (string.IsNullOrEmpty(encoded))
        {
            return null;
        }

        try
        {
            var decoded = DecodeSubprotocolValue(encoded);
            return decoded.Length == 0 ? null : decoded;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static List<string> BuildGatewayProtocols(string marker, string token, string? teamIdOrSlug)
    {
        List<string> protocols = [marker, GatewayAuthSubprotocolPrefix + token];
        if (!string.IsNullOrEmpty(teamIdOrSlug))
        {
            protocols.Add(GatewayTeamSubprotocolPrefix + EncodeSubprotocolValue(teamIdOrSlug));
        }

        return protocols;
    }

    private static string? FindProtocol(string? secWebSocketProtocol, string prefix) =>
        secWebSocketProtocol
            ?.Split(',')
            .Select(protocol => protocol.Trim())
            .FirstOrDefault(protocol => protocol.StartsWith(prefix, StringComparison.Ordinal));

    private static string EncodeSubprotocolValue(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    // atob throws on a value that is not base64; the decoder throws FormatException likewise.
    private static string DecodeSubprotocolValue(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 += new string('=', (4 - (base64.Length % 4)) % 4);
        return new UTF8Encoding(false, false).GetString(Convert.FromBase64String(base64));
    }
}
