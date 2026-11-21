// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>.NET-only: the JSON shapes the realtime models read and write, and the URL and header helpers they share.</summary>
internal static class GatewayRealtimeJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        AllowOutOfOrderMetadataProperties = true,
    };

    /// <summary>
    /// The identity codec of <c>GatewayRealtimeModel.parseServerEvent</c> (<c>raw as RealtimeModelV4ServerEvent</c>). Upstream's cast never
    /// throws, so a frame whose type this port does not model passes through as a <c>custom</c> event that keeps the frame as <c>Raw</c>.
    /// </summary>
    public static RealtimeModelV4ServerEvent ToServerEvent(object? raw)
    {
        if (raw is RealtimeModelV4ServerEvent serverEvent)
        {
            return serverEvent;
        }

        var node = raw switch
        {
            JsonNode jsonNode => jsonNode,
            JsonElement element => JsonNode.Parse(element.GetRawText()),
            _ => throw new InvalidArgumentException("raw", "expected a realtime server event."),
        };
        try
        {
            return node.Deserialize<RealtimeModelV4ServerEvent>(Options)!;
        }
        catch (Exception error) when (error is JsonException or NotSupportedException)
        {
            var rawType =
                node is JsonObject frame && frame["type"] is JsonValue type && type.TryGetValue<string>(out var text)
                    ? text
                    : "";
            return new RealtimeModelV4Custom(rawType) { Raw = node };
        }
    }
}

/// <summary>.NET-only: upstream's <c>removeUndefinedEntries</c> (<c>@ai-sdk/provider-utils</c>) over resolved headers.</summary>
internal static class GatewayRealtimeHeaders
{
    public static IReadOnlyDictionary<string, string> RemoveUndefinedEntries(
        IReadOnlyDictionary<string, string?> headers
    )
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in headers)
        {
            if (value is not null)
            {
                result[name] = value;
            }
        }

        return result;
    }
}

/// <summary>.NET-only: the WebSocket URL builders of <c>toGatewayRealtimeUrl</c> and <c>toGatewayLiveUrl</c>.</summary>
internal static class GatewayRealtimeUrls
{
    // new URL(...) of the upgraded base URL, with searchParams.set('ai-model-id', modelId).
    public static string ToWebSocketUrl(string baseUrl, string path, string modelId)
    {
        var wsBase = baseUrl.StartsWith("http", StringComparison.Ordinal) ? "ws" + baseUrl[4..] : baseUrl;
        var uri = new UriBuilder(wsBase + path) { Query = GatewayHttp.QueryString([new("ai-model-id", modelId)]) };
        return uri.Uri.ToString();
    }
}
