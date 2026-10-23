// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace E2E;

/// <summary>
/// Upstream's <c>agent/message-shape.ts</c>: what a message holds, as the model reads it, for telling a rewritten message from
/// one carried forward. Anthropic binds a thinking block to the messages before it and rejects a replay after any of them
/// changed, so the loop compares each turn's history with what the last request sent: by value, since an executor may edit a
/// message through an object it was handed (the bytes of an image) or hand back a clone of a history it did not change.
/// </summary>
internal static class MessageShape
{
    /// <summary>
    /// A digest of one message's content, taken when it is sent, so an edit to the live message afterwards, image bytes
    /// included, shows against it. The reasoning a message carries is not part of it: it is the provider's own material,
    /// as upstream's <c>providerOptions</c> and <c>providerMetadata</c> are.
    /// </summary>
    public static string ShapeOf(ModelMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var shape = new JsonObject
        {
            ["role"] = message.Role,
            ["content"] = message.Content,
            ["toolCallId"] = message.ToolCallId,
            ["name"] = message.Name,
        };
        if (message.ToolCalls is { } calls)
        {
            var array = new JsonArray();
            foreach (var call in calls)
            {
                array.Add(new JsonObject
                {
                    ["id"] = call.Id,
                    ["name"] = call.Name,
                    ["arguments"] = call.Arguments.ValueKind == JsonValueKind.Undefined ? null : call.Arguments.GetRawText(),
                });
            }

            shape["toolCalls"] = array;
        }

        if (message.Images is { } images)
        {
            var array = new JsonArray();
            foreach (var image in images)
            {
                array.Add(new JsonObject
                {
                    ["mediaType"] = image.MediaType,
                    ["width"] = image.Width,
                    ["height"] = image.Height,
                    ["bytes"] = Sha256(image.Data),
                });
            }

            shape["images"] = array;
        }

        return Sha256(Encoding.UTF8.GetBytes(shape.ToJsonString()));
    }

    private static string Sha256(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
}
