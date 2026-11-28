// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;

namespace E2E.Desktop;

/// <summary>
/// The contract's <c>press</c> grammar spelled as a native driver key combo. .NET-only holder name: upstream's <c>toCombo</c> and its tables in
/// <c>keys.ts</c>.
/// </summary>
public static partial class DesktopKeys
{
    /// <summary>
    /// The driver's name for each contract key. Its <c>delete</c> is the key that deletes backwards, as a Mac labels it, so the forward Delete is
    /// <c>forwarddelete</c>. Insert has no key on a Mac keyboard and no name there. Upstream's <c>KEY_NAMES</c>.
    /// </summary>
    private static readonly Dictionary<string, string?> KeyNames = new(StringComparer.Ordinal)
    {
        ["Enter"] = "return",
        ["Escape"] = "escape",
        ["Tab"] = "tab",
        ["Backspace"] = "backspace",
        ["Delete"] = "forwarddelete",
        ["Insert"] = null,
        ["Space"] = "space",
        ["ArrowUp"] = "up",
        ["ArrowDown"] = "down",
        ["ArrowLeft"] = "left",
        ["ArrowRight"] = "right",
        ["Home"] = "home",
        ["End"] = "end",
        ["PageUp"] = "pageup",
        ["PageDown"] = "pagedown",
        ["F1"] = "f1",
        ["F2"] = "f2",
        ["F3"] = "f3",
        ["F4"] = "f4",
        ["F5"] = "f5",
        ["F6"] = "f6",
        ["F7"] = "f7",
        ["F8"] = "f8",
        ["F9"] = "f9",
        ["F10"] = "f10",
        ["F11"] = "f11",
        ["F12"] = "f12",
    };

    /// <summary>
    /// The characters the driver's macOS key map presses besides letters and digits. Any other one (<c>!</c>, <c>é</c>) has no key of its own there,
    /// and is refused before a locator's press focuses its control. <c>+</c> is not one: the combo syntax uses it as the separator. Upstream's <c>PUNCTUATION</c>.
    /// </summary>
    private static readonly HashSet<string> Punctuation = [",", ".", "/", ";", "'", "[", "]", "\\", "-", "=", "`"];

    /// <summary>
    /// The shortcuts the engine refuses to press: on macOS they quit, log out, force-quit, or lock the session. It refuses any combo with one's
    /// key and at least its modifiers, so <c>cmd+q</c> stands for <c>cmd+shift+q</c> and <c>ctrl+cmd+q</c> too. Upstream's <c>REFUSED_SHORTCUTS</c>.
    /// </summary>
    private static readonly (string Key, string[] Modifiers)[] RefusedShortcuts =
    [
        ("q", ["cmd"]),
        ("escape", ["cmd", "alt"]),
        ("backspace", ["cmd", "shift"]),
    ];

    private static string ModifierName(string modifier) =>
        modifier switch
        {
            "Shift" => "shift",
            "Control" => "ctrl",
            "Alt" => "alt",
            "Meta" or "ControlOrMeta" => "cmd",
            _ => throw new ArgumentOutOfRangeException(nameof(modifier), modifier, "Unknown modifier."),
        };

    /// <summary>
    /// The combo the driver's <c>press</c> takes for one contract key: <c>Enter</c> is <c>return</c>, <c>ControlOrMeta+a</c> is <c>cmd+a</c>. A key
    /// the driver has no name for is <c>UNSUPPORTED_CAPABILITY</c>, and so is <c>+</c>, which its combo syntax uses as the separator, and a
    /// shortcut it refuses. Upstream's <c>toCombo</c>.
    /// </summary>
    public static string ToCombo(string key)
    {
        var parsed = KeyGrammar.ParseKey(key)
            ?? throw new EngineException(
                "UNSUPPORTED_CAPABILITY",
                $"\"{key}\" is not a key in the press grammar",
                retryable: false
            );
        string? name = null;
        if (parsed.Name is { } named)
        {
            name = KeyNames.GetValueOrDefault(named);
        }
        else if (parsed.Character == " ")
        {
            name = "space";
        }
        else if (
            parsed.Character is { } character
            && (AlphanumericKey().IsMatch(character) || Punctuation.Contains(character))
        )
        {
            // The driver adds Shift for a lone capital letter, so a capital needs no help.
            name = character;
        }

        if (name is null)
        {
            throw new EngineException(
                "UNSUPPORTED_CAPABILITY",
                $"the desktop engine cannot press \"{key}\" on macOS",
                retryable: false
            );
        }

        var modifiers = parsed.Modifiers.Select(ModifierName).ToList();
        // A capital letter with other modifiers is matched by its key alone, so Shift says it.
        if (
            parsed.Character is { } letter
            && CapitalLetter().IsMatch(letter)
            && modifiers.Count > 0
            && !modifiers.Contains("shift")
        )
        {
            modifiers.Add("shift");
        }

        var pressed = name.ToLowerInvariant();
        if (
            RefusedShortcuts.Any(shortcut =>
                shortcut.Key == pressed && shortcut.Modifiers.All(modifier => modifiers.Contains(modifier))
            )
        )
        {
            throw new EngineException(
                "UNSUPPORTED_CAPABILITY",
                $"the desktop engine refuses to press \"{key}\", a shortcut that quits, logs out, force-quits, or locks the session; end the app with desktop.closeApp()",
                retryable: false
            );
        }

        return string.Join('+', [.. modifiers, name]);
    }

    [GeneratedRegex(@"\A[A-Za-z0-9]\z")]
    private static partial Regex AlphanumericKey();

    [GeneratedRegex(@"\A[A-Z]\z")]
    private static partial Regex CapitalLetter();
}
