// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;

namespace E2E.Engine;

/// <summary>Upstream's <c>ParsedKey</c> (<c>engine/contract.ts</c>): one parsed <c>press</c> key: its modifiers, and either a named key or a single character.</summary>
public sealed record ParsedKey(IReadOnlyList<string> Modifiers, string? Name, string? Character);

/// <summary>
/// .NET-only holder name (C# has no free functions): upstream's <c>KEY_MODIFIERS</c>, <c>KEY_NAMES</c> and <c>parseKey</c> in <c>engine/contract.ts</c>.
/// The key grammar of <c>press</c>, shared by every engine so a test's key names are portable: zero or more modifiers
/// and one key, joined by <c>+</c>. A modifier is <c>Shift</c>, <c>Control</c>, <c>Alt</c>, <c>Meta</c>, or
/// <c>ControlOrMeta</c>. A named key is one of <see cref="KeyNames"/>. Any single printable character is typed as itself.
/// The harness refuses a key outside it with <c>INVALID_ARGUMENT</c> (<c>Keys.RequireKey</c>); an engine without a grammar-valid key refuses it with <c>UNSUPPORTED_CAPABILITY</c>. It never reinterprets the spelling.
/// </summary>
public static class KeyGrammar
{
    public static IReadOnlyList<string> KeyModifiers => Engine.KeyModifiers.Names;

    public static IReadOnlyList<string> KeyNames { get; } =
    [
        "Enter", "Escape", "Tab", "Backspace", "Delete", "Insert", "Space", "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight",
        "Home", "End", "PageUp", "PageDown", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
    ];

    /// <summary>
    /// Parses a <c>press</c> key against the grammar. Null when it does not conform: an unknown name, a repeated
    /// modifier, a bare modifier, or several characters. A literal <c>+</c> is the character after the last separator,
    /// so <c>Shift++</c> presses <c>+</c> with Shift held.
    /// </summary>
    public static ParsedKey? ParseKey(string key)
    {
        if (key.Length == 0)
        {
            return null;
        }

        if (key == "+")
        {
            return new ParsedKey([], null, "+");
        }

        // A trailing ++ is a separator then the key +, so the last two characters are cut and + appended as the final part. A lone trailing + names no key.
        string[] parts = key.EndsWith("++") ? [.. key[..^2].Split('+'), "+"] : key.Split('+');
        var last = parts[^1];
        if (last.Length == 0)
        {
            return null;
        }

        var modifiers = new List<string>();
        foreach (var part in parts[..^1])
        {
            if (!KeyModifiers.Contains(part) || modifiers.Contains(part))
            {
                return null;
            }

            modifiers.Add(part);
        }

        if (KeyNames.Contains(last))
        {
            return new ParsedKey(modifiers, last, null);
        }

        return new StringInfo(last).LengthInTextElements == 1 && last.EnumerateRunes().Count() == 1 && IsPrintable(last)
            ? new ParsedKey(modifiers, null, last)
            : null;
    }

    /// <summary>Control, format, surrogate, private-use, unassigned, and line or paragraph separators are never a printable key.</summary>
    private static bool IsPrintable(string character) =>
        !HasLoneSurrogate(character) && !character.EnumerateRunes().Any(rune => Rune.GetUnicodeCategory(rune) is
            UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate or UnicodeCategory.PrivateUse
            or UnicodeCategory.OtherNotAssigned or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator);

    /// <summary>
    /// <c>EnumerateRunes</c> maps a lone surrogate to U+FFFD, which is printable, so the surrogate is checked on the UTF-16 units first.
    /// </summary>
    private static bool HasLoneSurrogate(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(text[i]))
            {
                return true;
            }
        }

        return false;
    }
}
