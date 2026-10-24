// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Mobile;

namespace E2E.WinApp;

/// <summary>What the driver sends for one <c>press</c> key: a string in the winapp key grammar, or literal text when <see cref="Verbatim"/>.</summary>
public sealed record WinAppKeys(string Text, bool Verbatim);

/// <summary>
/// The spelling of a <c>press</c> key in the winapp library's grammar. The key grammar itself is <see cref="DeviceKeys"/>, shared with
/// the mobile engine (compiled into this assembly from <c>E2E.Mobile</c>) so a test's key names are portable: zero or more modifiers
/// and one key, joined by <c>+</c>. A bare character goes out as literal text; one held with a modifier goes out as a chord.
/// </summary>
public static class WindowKeys
{
    /// <summary>The winapp spelling of each modifier. Windows has no Command key, so <c>Meta</c> is the Windows key and <c>ControlOrMeta</c> is Control.</summary>
    private static readonly Dictionary<string, string> ModifierNames = new(StringComparer.Ordinal)
    {
        ["Shift"] = "shift",
        ["Control"] = "ctrl",
        ["Alt"] = "alt",
        ["Meta"] = "win",
        ["ControlOrMeta"] = "ctrl",
    };

    /// <summary>The winapp spelling of each named key, as its <c>KeyStringParser</c> reads them (checked against 0.7.1).</summary>
    private static readonly Dictionary<string, string> KeyNames = new(StringComparer.Ordinal)
    {
        ["Enter"] = "enter",
        ["Escape"] = "esc",
        ["Tab"] = "tab",
        ["Backspace"] = "backspace",
        ["Delete"] = "delete",
        ["Insert"] = "insert",
        ["Space"] = "space",
        ["ArrowUp"] = "up",
        ["ArrowDown"] = "down",
        ["ArrowLeft"] = "left",
        ["ArrowRight"] = "right",
        ["Home"] = "home",
        ["End"] = "end",
        ["PageUp"] = "pageup",
        ["PageDown"] = "pagedown",
    };

    /// <summary>
    /// The winapp spelling of one <c>press</c> key. A bare character is literal text, so the library types it whatever the keyboard
    /// layout; a character held with a modifier is a chord of the modifiers and the character, where <c>+</c> is its virtual key and a
    /// space is <c>space</c>. <c>UNSUPPORTED_CAPABILITY</c> for a key outside the grammar.
    /// </summary>
    public static WinAppKeys ToWinApp(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var parsed = DeviceKeys.Parse(key)
            ?? throw WinAppSupport.Unsupported($"\"{key}\" is not a key: press takes one key in the form [Modifier+]...Key, a named key ({string.Join(", ", DeviceKeys.Names)}) or one character");
        if (parsed.Modifiers.Count == 0 && parsed.Character is { } typed)
        {
            return new WinAppKeys(typed, Verbatim: true);
        }

        var last = parsed switch
        {
            { Name: { } name } when KeyNames.TryGetValue(name, out var spelled) => spelled,
            { Name: { } function } => function.ToLowerInvariant(),
            { Character: "+" } => "vk=0xBB",
            { Character: " " } => "space",
            _ => parsed.Character!,
        };
        return new WinAppKeys(string.Join('+', parsed.Modifiers.Select(modifier => ModifierNames[modifier]).Append(last)), Verbatim: false);
    }
}
