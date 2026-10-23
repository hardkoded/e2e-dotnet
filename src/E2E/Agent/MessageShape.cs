// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace E2E;

/// <summary>
/// Upstream's <c>message-shape.ts</c>: what a message holds, as the model reads it, for telling a rewritten message from one
/// carried forward. Anthropic binds a thinking block to the messages before it and rejects a replay after any of them changed,
/// so the loop compares each turn's history with what the last request sent: by value, since an executor may hand back a clone
/// of a history it did not change.
/// </summary>
internal static class MessageShape
{
    /// <summary>
    /// A digest of one message's content, taken when it is sent. Image bytes count by their hash. Upstream also leaves out the
    /// provider options of a message, where a cache breakpoint moves; the port keeps those out of its messages.
    /// </summary>
    public static string ShapeOf(ModelMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Add(hash, message.Role);
        Add(hash, message.Content);
        Add(hash, message.ToolCallId);
        Add(hash, message.Name);
        AddList(hash, message.ToolCalls, call =>
        {
            Add(hash, call.Id);
            Add(hash, call.Name);
            Add(hash, call.Arguments.ValueKind == JsonValueKind.Undefined ? null : call.Arguments.GetRawText());
        });
        AddList(hash, message.ReasoningItems, item => Add(hash, item.ValueKind == JsonValueKind.Undefined ? null : item.GetRawText()));
        AddList(hash, message.Images, image =>
        {
            Add(hash, image.MediaType);
            Add(hash, image.Width + "x" + image.Height);
            Add(hash, Convert.ToHexString(SHA256.HashData(image.Data)));
        });
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    // Every field is length-prefixed, so two messages never share a digest by moving text between fields.
    private static void Add(IncrementalHash hash, string? text)
    {
        var bytes = text is null ? [] : Encoding.UTF8.GetBytes(text);
        hash.AppendData(BitConverter.GetBytes(text is null ? -1 : bytes.Length));
        hash.AppendData(bytes);
    }

    private static void AddList<T>(IncrementalHash hash, IReadOnlyList<T>? items, Action<T> add)
    {
        hash.AppendData(BitConverter.GetBytes(items?.Count ?? -1));
        foreach (var item in items ?? [])
        {
            add(item);
        }
    }
}
