// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;

namespace E2E.Internal;

/// <summary>.NET-only: JavaScript's <c>JSON.stringify</c> of a string, which upstream's messages use to quote a ref, a key, or a test id.</summary>
internal static class JsonQuote
{
    /// <summary>The text in double quotes, escaping only what <c>JSON.stringify</c> escapes: the quote, the backslash, and control characters.</summary>
    public static string Of(string text)
    {
        var quoted = new StringBuilder("\"");
        foreach (var character in text)
        {
            quoted.Append(character switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\b' => "\\b",
                '\f' => "\\f",
                < ' ' => "\\u" + ((int)character).ToString("x4", CultureInfo.InvariantCulture),
                _ => character.ToString(),
            });
        }

        return quoted.Append('"').ToString();
    }
}
