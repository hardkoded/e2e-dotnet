// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace E2E.Internal;

/// <summary>Upstream's <c>internal/ids.ts</c>: the hashes and the canonical JSON that cache keys are built on.</summary>
internal static class Ids
{
    /// <summary>SHA-256 of a UTF-8 string, lowercase hex.</summary>
    public static string Sha256Hex(string input) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)));

    /// <summary>SHA-256 over the RFC 8785 canonical form, lowercase hex.</summary>
    public static string CanonicalDigest(JsonNode? value) => Sha256Hex(CanonicalJson(value));

    /// <summary>
    /// RFC 8785 (JCS) canonical JSON serialization for JSON-safe values. Numbers must be finite. An integer that fits 64 bits is
    /// written as digits and any other number by its shortest round-trip spelling, which matches ECMAScript's for the magnitudes
    /// a param holds.
    /// </summary>
    public static string CanonicalJson(JsonNode? value)
    {
        var builder = new StringBuilder();
        Write(builder, value);
        return builder.ToString();
    }

    private static void Write(StringBuilder builder, JsonNode? value)
    {
        switch (value)
        {
            case null:
                builder.Append("null");
                break;
            case JsonObject obj:
                builder.Append('{');
                var first = true;

                // RFC 8785 orders keys by UTF-16 code unit, never by locale.
                foreach (var pair in obj.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    if (!first)
                    {
                        builder.Append(',');
                    }

                    first = false;
                    WriteString(builder, pair.Key);
                    builder.Append(':');
                    Write(builder, pair.Value);
                }

                builder.Append('}');
                break;
            case JsonArray array:
                builder.Append('[');
                for (var i = 0; i < array.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    Write(builder, array[i]);
                }

                builder.Append(']');
                break;
            case JsonValue scalar:
                WriteScalar(builder, scalar);
                break;
        }
    }

    private static void WriteScalar(StringBuilder builder, JsonValue scalar)
    {
        switch (scalar.GetValueKind())
        {
            case System.Text.Json.JsonValueKind.String:
                WriteString(builder, scalar.GetValue<string>());
                break;
            case System.Text.Json.JsonValueKind.True:
                builder.Append("true");
                break;
            case System.Text.Json.JsonValueKind.False:
                builder.Append("false");
                break;
            case System.Text.Json.JsonValueKind.Number:
                WriteNumber(builder, scalar.ToJsonString());
                break;
            default:
                throw new ArgumentException($"value of type {scalar.GetValueKind()} is not JSON-safe");
        }
    }

    private static void WriteNumber(StringBuilder builder, string raw)
    {
        if (long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer))
        {
            builder.Append(integer.ToString(CultureInfo.InvariantCulture));
            return;
        }

        var number = double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (!double.IsFinite(number))
        {
            throw new ArgumentException("non-finite number in canonical JSON");
        }

        builder.Append(number.ToString("R", CultureInfo.InvariantCulture).Replace("E+", "e+", StringComparison.Ordinal).Replace("E-", "e-", StringComparison.Ordinal));
    }

    // JSON.stringify's spelling of a string: only the quote, the backslash, and control characters are escaped, and a lone surrogate.
    private static void WriteString(StringBuilder builder, string text)
    {
        builder.Append('"');
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    var paired = char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]);
                    if (c < 0x20 || (char.IsSurrogate(c) && !paired))
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                        if (paired)
                        {
                            builder.Append(text[++i]);
                        }
                    }

                    break;
            }
        }

        builder.Append('"');
    }
}
