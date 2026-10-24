// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace E2E;

/// <summary>
/// Upstream's <c>message-shape.ts</c>: what a message holds, as the model reads it, for telling a rewritten message from one
/// carried forward. Anthropic binds a thinking block to the messages before it and rejects a replay after any of them changed,
/// so the loop compares each turn's history with what the last request sent: by value, since an executor may hand back a clone
/// of a history it did not change.
/// </summary>
internal static class MessageShape
{
    // Upstream's replacer of `JSON.stringify`: System.Text.Json takes a converter where it takes a function.
    private static readonly JsonSerializerOptions Json = new() { Converters = { new BytesDigest(), new UndefinedAsNull() } };

    /// <summary>
    /// A digest of one message's content, taken when it is sent, so an edit to the live message afterwards, bytes included, shows
    /// against it. Upstream also leaves out the provider options of a message, where a cache breakpoint moves; the port keeps those
    /// out of its messages.
    /// </summary>
    public static string ShapeOf(ModelMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return Sha256(JsonSerializer.SerializeToUtf8Bytes(message, Json));
    }

    private static string Sha256(ReadOnlySpan<byte> data) => Convert.ToHexString(SHA256.HashData(data));

    /// <summary>A default <see cref="JsonElement"/> (a tool call without arguments) has no JSON to write: it reads as null, as a missing value does in upstream.</summary>
    private sealed class UndefinedAsNull : JsonConverter<JsonElement>
    {
        public override JsonElement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException("a shape is only written");

        public override void Write(Utf8JsonWriter writer, JsonElement value, JsonSerializerOptions options)
        {
            if (value.ValueKind == JsonValueKind.Undefined)
            {
                writer.WriteNullValue();
            }
            else
            {
                value.WriteTo(writer);
            }
        }
    }

    /// <summary>Writes a payload as <c>&lt;bytes sha256&gt;</c>, as upstream's replacer does, so the digest reads the bytes without copying them into the JSON.</summary>
    private sealed class BytesDigest : JsonConverter<byte[]>
    {
        public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotSupportedException("a shape is only written");

        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options) => writer.WriteStringValue("<bytes " + Sha256(value) + ">");
    }
}
