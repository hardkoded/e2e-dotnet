// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Decision;

/// <summary>Upstream's <c>hash.ts</c>.</summary>
internal static class Hash
{
    private const string Digits = "0123456789abcdefghijklmnopqrstuvwxyz";

    /// <summary>Upstream's <c>fnv1a</c>: FNV-1a over text (UTF-16 code units) and bytes, as a base-36 string: the one hash the package fingerprints with.</summary>
    public static string Fnv1a(IEnumerable<object> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var hash = 2166136261u;
        foreach (var part in parts)
        {
            switch (part)
            {
                case string text:
                    foreach (var unit in text)
                    {
                        hash = unchecked((hash ^ unit) * 16777619u);
                    }

                    break;
                case byte[] bytes:
                    foreach (var value in bytes)
                    {
                        hash = unchecked((hash ^ value) * 16777619u);
                    }

                    break;
                default:
                    throw new ArgumentException("A part is text or bytes.", nameof(parts));
            }

            hash = unchecked(hash * 16777619u);
        }

        if (hash == 0)
        {
            return "0";
        }

        var digits = new char[7];
        var at = digits.Length;
        for (var rest = hash; rest > 0; rest /= 36)
        {
            digits[--at] = Digits[(int)(rest % 36)];
        }

        return new string(digits, at, digits.Length - at);
    }
}
