// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Internal;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>
/// .NET-only: what the <c>getFromApi</c> calls of the Gateway files share. The error handler is upstream's
/// <c>createJsonErrorResponseHandler({ errorSchema: z.any(), errorToMessage: data =&gt; getErrorMessage(data) ?? 'unknown error' })</c>.
/// </summary>
internal static class GatewayHttp
{
    /// <summary>The config's client, or one shared by the Gateway metadata calls with the model timeout.</summary>
    public static HttpClient Client(GatewayConfig config) => config.HttpClient ?? Shared;

    private static readonly HttpClient Shared = new() { Timeout = ModelHttp.Timeout };

    /// <summary><c>encodeURIComponent</c>.</summary>
    public static string EncodeUriComponent(string value) =>
        Uri.EscapeDataString(value)
            .Replace("%21", "!")
            .Replace("%27", "'")
            .Replace("%28", "(")
            .Replace("%29", ")")
            .Replace("%2A", "*");

    /// <summary><c>URLSearchParams.toString()</c>: <c>application/x-www-form-urlencoded</c>, a space as <c>+</c>.</summary>
    public static string QueryString(IEnumerable<KeyValuePair<string, string>> query) =>
        string.Join("&", query.Select(pair => FormEncode(pair.Key) + "=" + FormEncode(pair.Value)));

    // getErrorMessage(data) ?? 'unknown error' over the parsed body, else the raw text.
    public static string ErrorMessage(int status, string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var data = document.RootElement;
            if (data.ValueKind == JsonValueKind.String)
            {
                return data.GetString()!;
            }

            if (
                data.ValueKind == JsonValueKind.Object
                && data.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.String
            )
            {
                return message.GetString()!;
            }

            return data.GetRawText();
        }
        catch (JsonException)
        {
            return string.IsNullOrEmpty(text) ? "unknown error" : text;
        }
    }

    // URLSearchParams: letters, digits, and *-._ stay; a space is +; every other byte is %XX.
    private static string FormEncode(string value)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var b in System.Text.Encoding.UTF8.GetBytes(value))
        {
            var c = (char)b;
            if (b < 0x80 && (char.IsAsciiLetterOrDigit(c) || c is '*' or '-' or '.' or '_'))
            {
                builder.Append(c);
            }
            else if (c == ' ')
            {
                builder.Append('+');
            }
            else
            {
                builder.Append('%').Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }
}
