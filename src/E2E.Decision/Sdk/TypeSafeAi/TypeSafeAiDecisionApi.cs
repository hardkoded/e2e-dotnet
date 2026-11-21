// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace E2E.Decision.Sdk.TypeSafeAi;

/// <summary>The AI SDK's <c>typesafe-ai-decision-api.ts</c>: the message of TypeSafe's failures.</summary>
internal static class TypeSafeAiDecisionApi
{
    /// <summary>
    /// Upstream's <c>typesafeFailedResponseHandler</c> message: the first of <c>message</c>, <c>error</c>, <c>detail</c>, and <c>error_type</c>
    /// the body carries, else <c>TypeSafe request failed</c>.
    /// </summary>
    public static string ErrorMessage(string body)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return string.IsNullOrEmpty(body) ? "TypeSafe request failed" : body;
        }

        using var _ = document;
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            return "TypeSafe request failed";
        }

        if (String(root, "message") is { } message)
        {
            return message;
        }

        if (root.TryGetProperty("error", out var error))
        {
            var text = error.ValueKind == JsonValueKind.String ? error.GetString() : String(error, "message");
            if (text is not null)
            {
                return text;
            }
        }

        if (root.TryGetProperty("detail", out var detail) && detail.ValueKind != JsonValueKind.Undefined)
        {
            return detail.ValueKind == JsonValueKind.String ? detail.GetString()! : Stringify(detail);
        }

        return String(root, "error_type") ?? "TypeSafe request failed";
    }

    private static string? String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string Stringify(JsonElement value) => JsonSerializer.Serialize(value);
}
