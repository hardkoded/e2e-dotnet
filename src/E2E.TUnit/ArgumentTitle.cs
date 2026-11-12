// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace E2E.TUnit;

// The same title as the xUnit adapter writes, so a data row keeps its own replay under every framework.
internal static class ArgumentTitle
{
    // A lazy or self-containing collection must not hang the title.
    private const int MaxTitleDepth = 4;
    private const int MaxTitleItems = 1000;

    // A simple argument keeps its plain text. A null, a collection, or a string that could pass for
    // another row's argument list is written so that no two rows share a title. A comma only matters
    // when the row has several arguments, where ("a, b") and ("a", "b") would read alike.
    internal static string Format(object? argument, bool quoteComma, int depth = 0)
    {
        return argument switch
        {
            null => "null",
            string text => text.Contains('"', StringComparison.Ordinal)
                || text.Contains('\\', StringComparison.Ordinal)
                || text.StartsWith('[')
                || text == "null"
                || (quoteComma && text.Contains(',', StringComparison.Ordinal))
                ? "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\""
                : text,
            System.Collections.IEnumerable items when depth < MaxTitleDepth => "[" + string.Join(", ", items.Cast<object?>().Take(MaxTitleItems).Select(item => Format(item, true, depth + 1))) + "]",
            _ => Convert.ToString(argument, CultureInfo.InvariantCulture) ?? "null",
        };
    }
}
