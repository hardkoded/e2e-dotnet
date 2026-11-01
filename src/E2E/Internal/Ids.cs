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

    /// <summary>
    /// Result ID: SHA-256/JCS of <c>{ testId, targetId, agent }</c>, with <c>repeat</c> added for a run past the first under
    /// <c>--repeat-each</c>; a test run as several agents has one per agent, and one per repeat (upstream's <c>resultId</c>).
    /// </summary>
    public static string ResultId(string test, string targetId, string agent, int repeat = 0)
    {
        var value = new JsonObject { ["testId"] = test, ["targetId"] = targetId, ["agent"] = agent };
        if (repeat != 0)
        {
            value["repeat"] = repeat;
        }

        return CanonicalDigest(value);
    }

    /// <summary>RFC 3339 UTC timestamp with exactly millisecond precision (upstream's <c>timestamp</c>, <c>toISOString</c>).</summary>
    public static string Timestamp(DateTimeOffset date) => date.UtcDateTime.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'", CultureInfo.InvariantCulture);

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
        // An integer that a double holds exactly is digits; beyond 2^53 ECMAScript rounds it to a double, so the port does too.
        if (long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer) && integer is >= -MaxSafeInteger and <= MaxSafeInteger)
        {
            builder.Append(integer.ToString(CultureInfo.InvariantCulture));
            return;
        }

        var number = double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (!double.IsFinite(number))
        {
            throw new ArgumentException("non-finite number in canonical JSON");
        }

        builder.Append(EcmaNumber(number));
    }

    private const long MaxSafeInteger = 9007199254740992;

    // ECMAScript's Number::toString (ECMA-262, 6.1.6.1.20), which RFC 8785 requires: the shortest round-trip digits, laid out as
    // plain digits for exponents from -7 to 21 and as d.ddde+x outside it.
    private static string EcmaNumber(double value)
    {
        if (value == 0)
        {
            return "0";
        }

        var sign = value < 0 ? "-" : "";

        // "R" is the shortest round-trip spelling; its digits and exponent are read back from it.
        var shortest = Math.Abs(value).ToString("R", CultureInfo.InvariantCulture);
        var digits = ShortestDigits(shortest, out var exponent);
        var k = digits.Length;
        var n = exponent + 1;
        string body;
        if (k <= n && n <= 21)
        {
            body = digits + new string('0', n - k);
        }
        else if (0 < n && n <= 21)
        {
            body = digits[..n] + "." + digits[n..];
        }
        else if (-6 < n && n <= 0)
        {
            body = "0." + new string('0', -n) + digits;
        }
        else
        {
            var e = n - 1;
            var tail = (e < 0 ? "-" : "+") + Math.Abs(e).ToString(CultureInfo.InvariantCulture);
            body = k == 1 ? digits + "e" + tail : digits[..1] + "." + digits[1..] + "e" + tail;
        }

        return sign + body;
    }

    // The significant digits of a "R" spelling with no trailing zeros, and the exponent of the first digit (1.5e3 is 3).
    private static string ShortestDigits(string spelling, out int exponent)
    {
        var mantissa = spelling;
        var shift = 0;
        var e = spelling.IndexOfAny(['E', 'e']);
        if (e >= 0)
        {
            mantissa = spelling[..e];
            shift = int.Parse(spelling[(e + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        }

        var point = mantissa.IndexOf('.', StringComparison.Ordinal);
        var whole = point < 0 ? mantissa : mantissa[..point];
        var fraction = point < 0 ? "" : mantissa[(point + 1)..];
        var all = whole + fraction;
        var leading = all.Length - all.TrimStart('0').Length;
        var digits = all.TrimStart('0').TrimEnd('0');
        exponent = whole.Length - 1 - leading + shift;
        return digits;
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
